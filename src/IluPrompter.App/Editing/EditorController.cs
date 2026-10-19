using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IluPrompter.App.Services;
using IluPrompter.App.ViewModels;
using IluPrompter.Core;
using IluPrompter.Documents;
using IluPrompter.Theme;

namespace IluPrompter.App.Editing;

/// <summary>Services the editor needs; registered once in DI and handed to each EditorController.</summary>
public sealed record EditorServices(
    ISnapshotBuilder Snapshots,
    IPromptDocumentService Documents,
    ISettingsService Settings,
    IScriptFormatter Formatter,
    IDocumentNavigator Navigator,
    IScriptFileService Files,
    IKeyModeState KeyMode,
    IScrollEngine Engine,
    IAppLog Log);

/// <summary>
/// The RichTextBox editor (DESIGN.md C, E and F). WPF's RichTextBox does not bind well, so this class is the
/// editor's code-behind: it publishes debounced snapshots, owns the script keys, reflects the caret's formatting
/// in the toolbar, applies zoom as a layout transform (never as font size), marks the reading line, and keeps a
/// recovery copy of unsaved work. Everything here runs on the control UI thread.
/// </summary>
public sealed class EditorController : IScriptEditor, ISectionEditing
{
    public const int MinZoom = 70;
    public const int MaxZoom = 200;

    private readonly RichTextBox _editor;
    private readonly EditorServices _s;
    private readonly DispatcherTimer _debounce;
    private readonly DispatcherTimer _recovery;
    private readonly Brush _sectionLabelBrush;
    private readonly Brush _sectionRuleBrush;
    private int _suppress;
    private bool _dirtySinceRecovery;
    private string _sectionSignature = "";
    private string _lastHighlight = "highlight-yellow";
    private DocumentAnchor _lastMarkerAnchor = new(-1, -1);
    private bool _markerNeedsUpdate = true;
    private Action? _focusTransport;

    public EditorController(RichTextBox editor, EditorToolbarViewModel toolbar, EditorServices services)
    {
        _editor = editor;
        Toolbar = toolbar;
        _s = services;
        _sectionLabelBrush = ThemeResources.GetBrush("text-muted");
        _sectionRuleBrush = ThemeResources.GetBrush("line-strong");
        BaseFontSize = services.Settings.Current.Editor.BaseFontSize;

        _editor.Document.PagePadding = new Thickness(0);
        _editor.SelectionBrush = ThemeResources.GetBrush("selection");
        _editor.SelectionTextBrush = ThemeResources.GetBrush("script-ink");
        _editor.SelectionOpacity = 0.4; // WPF paints the selection over the text; SelectionTextBrush does not repaint it
        ApplyTextSettings();
        ApplyZoomText();

        _debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            PublishNow();
        };

        var seconds = services.Settings.Current.Editor.RecoverySeconds;
        _recovery = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(seconds > 0 ? seconds : 60) };
        _recovery.Tick += (_, _) => WriteRecoveryCopy();
        if (seconds > 0) _recovery.Start();

        _editor.TextChanged += OnTextChanged;
        _editor.SelectionChanged += (_, _) => UpdateToolbar();
        _editor.GotKeyboardFocus += (_, _) => _s.KeyMode.Mode = KeyMode.Typing;
        _editor.PreviewKeyDown += OnPreviewKeyDown;
        _editor.LayoutUpdated += (_, _) => _markerNeedsUpdate = true;
        _editor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => _markerNeedsUpdate = true));
        _s.Settings.Changed += OnSettingsChanged;
        _s.Documents.FileInfoChanged += (_, _) =>
        {
            if (!_s.Documents.IsDirty) DeleteRecoveryCopy();
        };
        UpdateToolbar();
    }

    public EditorToolbarViewModel Toolbar { get; }

    public RichTextBox Editor => _editor;

    public FlowDocument Document => _editor.Document;

    public double BaseFontSize { get; private set; }

    public event EventHandler? ContentChanged;

    /// <summary>Raised when the zoom changes so the view can rescale the paper (LayoutTransform).</summary>
    public event EventHandler<double>? ZoomChanged;

    /// <summary>Raised when the reading-line marker should move: (gutter ▶ top, line height) in editor coordinates, or null.</summary>
    public event EventHandler<Rect?>? ReadingLineMoved;

    /// <summary>Esc in the script hands the keys to the transport; the shell supplies where focus should go.</summary>
    public void SetTransportFocus(Action focusTransport) => _focusTransport = focusTransport;

    public void FocusEditor()
    {
        _editor.Focus();
        _s.KeyMode.Mode = KeyMode.Typing;
    }

    // ===== loading and publishing =====

    public void LoadScript(ScriptFile file)
    {
        _suppress++;
        try
        {
            BaseFontSize = file.EditorBaseFontSize > 0 ? file.EditorBaseFontSize : _s.Settings.Current.Editor.BaseFontSize;
            _editor.FontSize = BaseFontSize;
            _editor.IsUndoEnabled = false;
            _s.Snapshots.LoadInto(_editor.Document, file.DocumentXaml);
            _editor.IsUndoEnabled = true;
            _sectionSignature = "";
            StyleSections(force: true);
            _editor.CaretPosition = _editor.Document.ContentStart;
            _editor.ScrollToHome();
        }
        finally
        {
            _suppress--;
        }

        _dirtySinceRecovery = false;
        _lastMarkerAnchor = new DocumentAnchor(-1, -1);
        PublishNow();
        UpdateToolbar();
    }

    /// <summary>Build and publish a snapshot now (also what the 250 ms debounce calls).</summary>
    public void PublishNow()
    {
        _debounce.Stop();
        try
        {
            var snapshot = _s.Snapshots.Build(
                _editor.Document, _s.Documents.NextVersion(), BaseFontSize, _s.Settings.Current.Editor.SectionMarker);
            _s.Documents.Publish(snapshot);
            StyleSections(force: false, snapshot.Sections);
        }
        catch (Exception ex)
        {
            // An editing glitch must never take the operator's show down: keep the last good snapshot.
            _s.Log.Error("editor", "Snapshot failed", ex);
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress > 0) return;
        _dirtySinceRecovery = true;
        _debounce.Stop();
        _debounce.Start();
        ContentChanged?.Invoke(this, EventArgs.Empty);
        UpdateToolbar();
        _markerNeedsUpdate = true;
    }

    private void StyleSections(bool force, IReadOnlyList<SectionInfo>? sections = null)
    {
        sections ??= _s.Documents.Current.Sections;
        var signature = string.Join("|", sections.Select(x => x.ParagraphIndex.ToString(CultureInfo.InvariantCulture)));
        if (!force && signature == _sectionSignature) return;
        _sectionSignature = signature;
        _suppress++;
        try
        {
            _s.Formatter.StyleSectionParagraphs(_editor.Document, _s.Settings.Current.Editor.SectionMarker, _sectionLabelBrush, _sectionRuleBrush);
        }
        catch (Exception ex)
        {
            _s.Log.Error("editor", "Styling sections failed", ex);
        }
        finally
        {
            _suppress--;
        }
    }

    // ===== settings: the editor shows the script in its output face and default ink =====

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Old.Text, e.New.Text)) _editor.Dispatcher.BeginInvoke(new Action(ApplyTextSettings));
        if (e.Old.Editor.ZoomPercent != e.New.Editor.ZoomPercent) _editor.Dispatcher.BeginInvoke(new Action(ApplyZoomText));
    }

    private void ApplyTextSettings()
    {
        var text = _s.Settings.Current.Text;
        _editor.FontFamily = ThemeResources.ScriptFont(text.FontFace);
        _editor.FontWeight = FontWeight.FromOpenTypeWeight(text.FontWeight == 400 ? 400 : 500);
        _editor.FontSize = BaseFontSize;
        if (ThemeResources.TryGetColor(text.DefaultInk, out var ink)) _editor.Foreground = ThemeResources.GetBrush(text.DefaultInk);
        else _editor.Foreground = ThemeResources.GetBrush("script-ink");
    }

    // ===== zoom (layout transform, never font size) =====

    public int Zoom => Math.Clamp(_s.Settings.Current.Editor.ZoomPercent, MinZoom, MaxZoom);

    public void SetZoom(int percent)
    {
        var v = Math.Clamp(percent, MinZoom, MaxZoom);
        if (v == _s.Settings.Current.Editor.ZoomPercent) return;
        _s.Settings.Update(s => s with { Editor = s.Editor with { ZoomPercent = v } });
    }

    public void ZoomBy(int delta) => SetZoom(NextStep(Zoom, delta));

    private static int NextStep(int current, int direction)
    {
        var steps = EditorToolbarViewModel.ZoomSteps;
        if (direction > 0) return steps.FirstOrDefault(s => s > current, steps[^1]);
        return steps.LastOrDefault(s => s < current, steps[0]);
    }

    private void ApplyZoomText()
    {
        Toolbar.ZoomText = Zoom.ToString(CultureInfo.InvariantCulture) + " %";
        ZoomChanged?.Invoke(this, Zoom / 100.0);
    }

    // ===== keyboard =====

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        if (alt) return;

        if (key == Key.Escape && !ctrl && !shift)
        {
            // Esc in the script: hand the keys to the transport. Never blanks, never closes.
            _s.KeyMode.Mode = KeyMode.Transport;
            _focusTransport?.Invoke();
            e.Handled = true;
            return;
        }

        if (ctrl && !shift)
        {
            switch (key)
            {
                case Key.OemCloseBrackets: StepSize(+1); e.Handled = true; return;
                case Key.OemOpenBrackets: StepSize(-1); e.Handled = true; return;
                case Key.Space: ClearFormatting(); e.Handled = true; return;
                case Key.OemPlus or Key.Add: ZoomBy(+1); e.Handled = true; return;
                case Key.OemMinus or Key.Subtract: ZoomBy(-1); e.Handled = true; return;
                case Key.F: OpenFind(); e.Handled = true; return;
            }
        }
        else if (ctrl && shift)
        {
            switch (key)
            {
                case Key.X: ToggleStrikethrough(); e.Handled = true; return;
                case Key.H: ApplyHighlight(_lastHighlight); e.Handled = true; return;
                case Key.Enter: InsertSectionMarker(); e.Handled = true; return;
            }
        }
        else if (key == Key.F3)
        {
            if (shift) FindPrevious();
            else FindNext();
            e.Handled = true;
        }
    }

    // ===== toolbar actions =====

    public void Undo() => _editor.Undo();

    public void Redo() => _editor.Redo();

    public void ToggleBold() => Run(EditingCommands.ToggleBold);

    public void ToggleItalic() => Run(EditingCommands.ToggleItalic);

    public void ToggleUnderline() => Run(EditingCommands.ToggleUnderline);

    private void Run(RoutedCommand command)
    {
        _editor.Focus();
        command.Execute(null, _editor);
        UpdateToolbar();
    }

    public void ToggleStrikethrough()
    {
        _editor.Focus();
        _s.Formatter.ToggleStrikethrough(_editor.Selection);
        UpdateToolbar();
    }

    public void ApplySize(ScriptSize size)
    {
        _editor.Focus();
        _s.Formatter.ApplySize(_editor.Selection, size, BaseFontSize);
        UpdateToolbar();
    }

    public void StepSize(int direction)
    {
        var current = _s.Formatter.GetSize(_editor.Selection, BaseFontSize) ?? ScriptSize.Normal;
        var next = (ScriptSize)Math.Clamp((int)current + direction, 0, 3);
        if (next != current) ApplySize(next);
    }

    /// <summary>token "" or null removes the explicit colour (back to the default ink).</summary>
    public void ApplyInk(string? token)
    {
        _editor.Focus();
        Color? ink = string.IsNullOrEmpty(token) || !ThemeResources.TryGetColor(token, out var c) ? null : c;
        _s.Formatter.ApplyInk(_editor.Selection, ink);
        UpdateToolbar();
    }

    /// <summary>token "" removes the highlight. A highlight carries its own ink (DESIGN.md).</summary>
    public void ApplyHighlight(string? token)
    {
        _editor.Focus();
        if (string.IsNullOrEmpty(token))
        {
            _s.Formatter.ApplyHighlight(_editor.Selection, null, null);
        }
        else
        {
            var option = ThemeResources.Highlights.FirstOrDefault(h => h.Token == token);
            if (option is null) return;
            _lastHighlight = token;
            Color? inkColour = option.InkToken is { } inkToken && ThemeResources.TryGetColor(inkToken, out var ink) ? ink : null;
            _s.Formatter.ApplyHighlight(_editor.Selection, ThemeResources.GetColor(token), inkColour);
        }

        UpdateToolbar();
    }

    public void ClearFormatting()
    {
        _editor.Focus();
        _s.Formatter.ClearFormatting(_editor.Selection);
        UpdateToolbar();
    }

    public void InsertSectionMarker()
    {
        _editor.Focus();
        _s.Formatter.InsertSectionMarker(_editor, _s.Settings.Current.Editor.SectionMarker, "");
        UpdateToolbar();
    }

    /// <summary>Reflect the formatting at the caret in the toolbar.</summary>
    public void UpdateToolbar()
    {
        if (_suppress > 0) return;
        try
        {
            var sel = _editor.Selection;
            Toolbar.IsBold = sel.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight w && w.ToOpenTypeWeight() >= 600;
            Toolbar.IsItalic = Equals(sel.GetPropertyValue(TextElement.FontStyleProperty), FontStyles.Italic);
            Toolbar.IsUnderline = sel.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection d
                                  && d.Any(x => x.Location == TextDecorationLocation.Underline);
            Toolbar.IsStrikethrough = _s.Formatter.GetStrikethrough(sel) == true;
            Toolbar.SizeText = _s.Formatter.GetSize(sel, BaseFontSize) is { } size ? EditorToolbarViewModel.SizeName(size) : "Mixed";
            Toolbar.InkBar = sel.GetPropertyValue(TextElement.ForegroundProperty) is SolidColorBrush ink ? ink : _editor.Foreground;
            Toolbar.HighlightBar = sel.GetPropertyValue(TextElement.BackgroundProperty) is SolidColorBrush bg && bg.Color.A > 0 ? bg : null;
            Toolbar.CanUndo = _editor.CanUndo;
            Toolbar.CanRedo = _editor.CanRedo;
        }
        catch (Exception ex)
        {
            _s.Log.Warn("editor", "Toolbar state failed: " + ex.Message);
        }
    }

    // ===== find =====

    public void OpenFind()
    {
        Toolbar.IsFindOpen = true;
        var selected = _editor.Selection.Text;
        if (!string.IsNullOrEmpty(selected) && !selected.Contains('\n') && !selected.Contains('\r')) Toolbar.FindText = selected;
    }

    public void CloseFind()
    {
        Toolbar.IsFindOpen = false;
        Toolbar.FindStatus = "";
        FocusEditor();
    }

    public void FindNext() => Find(forward: true);

    public void FindPrevious() => Find(forward: false);

    private void Find(bool forward)
    {
        var text = Toolbar.FindText;
        if (string.IsNullOrEmpty(text)) return;
        var range = forward
            ? DocumentFinder.FindNext(_editor.Document, _editor.Selection.End, text)
            : DocumentFinder.FindPrevious(_editor.Document, _editor.Selection.Start, text);
        if (range is null)
        {
            Toolbar.FindStatus = "Not found";
            return;
        }

        Toolbar.FindStatus = "";
        _editor.Selection.Select(range.Start, range.End);
        range.Start.Paragraph?.BringIntoView();
    }

    // ===== reading line: gutter marker and band =====

    /// <summary>
    /// Called at 10 Hz. Moves the gutter ▶ and the `raised` band to the line under the reading line
    /// (Status.ReadingAnchor, via IDocumentNavigator.GetPointer). Cheap when nothing moved.
    /// </summary>
    public void UpdateReadingMarker()
    {
        var anchor = _s.Engine.Status.ReadingAnchor;
        if (!_markerNeedsUpdate && anchor == _lastMarkerAnchor) return;
        _markerNeedsUpdate = false;
        _lastMarkerAnchor = anchor;
        try
        {
            if (!_editor.IsLoaded || _s.Engine.Status.LineCount <= 0)
            {
                ReadingLineMoved?.Invoke(this, null);
                return;
            }

            var pointer = _s.Navigator.GetPointer(_editor.Document, anchor).GetLineStartPosition(0) ?? _s.Navigator.GetPointer(_editor.Document, anchor);
            var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
            if (rect.IsEmpty || rect.Bottom < 0 || rect.Top > _editor.ActualHeight)
            {
                ReadingLineMoved?.Invoke(this, null);
                return;
            }

            ReadingLineMoved?.Invoke(this, rect);
        }
        catch (Exception ex)
        {
            _s.Log.Warn("editor", "Reading marker failed: " + ex.Message);
            ReadingLineMoved?.Invoke(this, null);
        }
    }

    /// <summary>Double-click in the gutter: "Jump the prompt here" (a cut, then paused).</summary>
    public void JumpPromptToEditorPoint(Point pointInEditor)
    {
        var pointer = _editor.GetPositionFromPoint(pointInEditor, true);
        if (pointer is null) return;
        var anchor = _s.Navigator.GetAnchor(_editor.Document, pointer);
        _s.Engine.Post(EngineCommand.SeekToAnchor(anchor));
    }

    // ===== sections =====

    private Paragraph? SectionParagraph(int sectionIndex)
    {
        var sections = _s.Documents.Current.Sections;
        if (sectionIndex < 0 || sectionIndex >= sections.Count) return null;
        var paragraphs = _s.Navigator.GetParagraphs(_editor.Document);
        var p = sections[sectionIndex].ParagraphIndex;
        return p >= 0 && p < paragraphs.Count ? paragraphs[p] : null;
    }

    public void ShowSection(int sectionIndex)
    {
        if (SectionParagraph(sectionIndex) is not { } paragraph) return;
        paragraph.BringIntoView();
        _editor.CaretPosition = paragraph.ContentStart;
    }

    public void RenameSection(int sectionIndex, string newName)
    {
        if (SectionParagraph(sectionIndex) is not { } paragraph) return;
        new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text = _s.Settings.Current.Editor.SectionMarker + " " + newName;
    }

    public void DeleteSectionMarker(int sectionIndex)
    {
        if (SectionParagraph(sectionIndex) is not { } paragraph) return;
        paragraph.SiblingBlocks?.Remove(paragraph);
    }

    // ===== recovery copy =====

    private static string RecoveryDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ILU Prompter", "recovery");

    private string RecoveryPath()
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string(_s.Documents.Title.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return Path.Combine(RecoveryDirectory, (name.Length == 0 ? "Untitled" : name) + ".ilup");
    }

    private void WriteRecoveryCopy()
    {
        if (!_dirtySinceRecovery || !_s.Documents.IsDirty) return;
        try
        {
            Directory.CreateDirectory(RecoveryDirectory);
            var file = _s.Snapshots.ToScriptFile(_editor.Document, _s.Documents.Title, BaseFontSize, null);
            _s.Files.Save(RecoveryPath(), file);
            _dirtySinceRecovery = false;
        }
        catch (Exception ex)
        {
            _s.Log.Warn("editor", "Recovery copy failed: " + ex.Message);
        }
    }

    private void DeleteRecoveryCopy()
    {
        try
        {
            var path = RecoveryPath();
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
        catch (IOException)
        {
            // A stale recovery copy is harmless.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
