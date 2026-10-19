using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IluPrompter.App.Logic;
using IluPrompter.App.Services;
using IluPrompter.Core;

namespace IluPrompter.App.ViewModels;

/// <summary>
/// The control window shell: app bar (script name, saved state, Show output), status bar (key focus, position,
/// words, output display, controller), file commands and recent files, and the responsive tier.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    public const long NoteMilliseconds = 8000;

    private readonly IPromptDocumentService _documents;
    private readonly ScriptSession _session;
    private readonly OutputController _outputController;
    private readonly IKeyModeState _keyMode;
    private readonly IScrollEngine _engine;
    private readonly IOutputSurface _output;
    private readonly ISettingsService _settings;
    private readonly IUiThread _ui;
    private long _noteUntil;

    [ObservableProperty]
    private LayoutTier _tier = LayoutTier.Wide;

    [ObservableProperty]
    private string _scriptName = ScriptSession.DefaultTitle;

    [ObservableProperty]
    private string _savedText = "";

    [ObservableProperty]
    private string _windowTitle = "ILU Prompter";

    [ObservableProperty]
    private string _showOutputLabel = "Show output";

    [ObservableProperty]
    private string _keyFocusText = "";

    [ObservableProperty]
    private string _positionText = "";

    [ObservableProperty]
    private string _wordsText = "";

    [ObservableProperty]
    private string _outputDisplayText = "";

    [ObservableProperty]
    private string _controllerText = "";

    [ObservableProperty]
    private string _noteText = "";

    public MainViewModel(
        IPromptDocumentService documents,
        ScriptSession session,
        OutputController outputController,
        IKeyModeState keyMode,
        IScrollEngine engine,
        IOutputSurface output,
        ISettingsService settings,
        IUiThread ui,
        LiveViewModel live,
        SectionsViewModel sections,
        OutputLayoutViewModel layout)
    {
        _documents = documents;
        _session = session;
        _outputController = outputController;
        _keyMode = keyMode;
        _engine = engine;
        _output = output;
        _settings = settings;
        _ui = ui;
        Live = live;
        Sections = sections;
        Layout = layout;

        documents.FileInfoChanged += (_, _) => _ui.Post(RefreshFileInfo);
        documents.SnapshotPublished += (_, _) => _ui.Post(RefreshWords);
        keyMode.Changed += (_, _) => _ui.Post(RefreshKeyFocus);
        output.StateChanged += (_, _) => _ui.Post(RefreshOutput);
        settings.Changed += (_, e) =>
        {
            if (!ReferenceEquals(e.Old.RecentFiles, e.New.RecentFiles) || !ReferenceEquals(e.Old.Input, e.New.Input))
                _ui.Post(() =>
                {
                    RefreshRecent();
                    RefreshOutput();
                });
        };
        session.Note += (_, text) => _ui.Post(() => ShowNote(text));

        RefreshFileInfo();
        RefreshKeyFocus();
        RefreshWords();
        RefreshRecent();
        RefreshOutput();
        Poll();
    }

    public LiveViewModel Live { get; }

    public SectionsViewModel Sections { get; }

    public OutputLayoutViewModel Layout { get; }

    public ObservableCollection<RecentFileItem> Recent { get; } = [];

    public bool HasRecent => Recent.Count > 0;

    /// <summary>Raised when something should open Settings (menu, Ctrl+, or a chip).</summary>
    public event EventHandler<SettingsPageId>? SettingsRequested;

    public event EventHandler? ShortcutsRequested;

    public event EventHandler? ExitRequested;

    partial void OnTierChanged(LayoutTier value) => Live.Tier = value;

    // ===== commands =====

    [RelayCommand]
    private void NewScript() => _session.New();

    [RelayCommand]
    private void OpenScript() => _session.Open();

    [RelayCommand]
    private void OpenRecent(string? path)
    {
        if (!string.IsNullOrEmpty(path)) _session.OpenPath(path);
    }

    [RelayCommand]
    private void SaveScript() => _session.Save();

    [RelayCommand]
    private void SaveScriptAs() => _session.SaveAs();

    [RelayCommand]
    private void ExportScript() => _session.Export();

    [RelayCommand]
    private async Task ToggleOutput() => await _outputController.ToggleAsync();

    [RelayCommand]
    private void ToggleBlank() => _outputController.ToggleBlank();

    [RelayCommand]
    private void OpenSettings(SettingsPageId? page) => SettingsRequested?.Invoke(this, page ?? SettingsPageId.Controller);

    [RelayCommand]
    private void ShowShortcuts() => ShortcutsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    public void ShowNote(string text)
    {
        NoteText = text;
        _noteUntil = Environment.TickCount64 + NoteMilliseconds;
    }

    // ===== refresh =====

    /// <summary>10 Hz: position, words, transient note expiry.</summary>
    public void Poll()
    {
        var status = _engine.Status;
        string? sectionName = null;
        var sections = _documents.Current.Sections;
        if (status.SectionIndex >= 0 && status.SectionIndex < sections.Count) sectionName = sections[status.SectionIndex].Name;
        PositionText = ReadoutFormat.Position(status, sectionName);

        if (NoteText.Length > 0 && Environment.TickCount64 >= _noteUntil) NoteText = "";
    }

    private void RefreshFileInfo()
    {
        ScriptName = _documents.Title;
        SavedText = ReadoutFormat.SavedText(_documents.IsDirty, _documents.LastSaved);
        WindowTitle = _documents.Title.Length == 0
            ? "ILU Prompter"
            : $"ILU Prompter - {(_documents.FilePath is { } p ? Path.GetFileName(p) : _documents.Title)}{(_documents.IsDirty ? " *" : "")}";
    }

    private void RefreshWords() => WordsText = ReadoutFormat.Words(_documents.Current.WordCount);

    private void RefreshKeyFocus() =>
        KeyFocusText = _keyMode.Mode == KeyMode.Typing ? "Typing" : "Transport keys";

    private void RefreshOutput()
    {
        var state = _output.State;
        ShowOutputLabel = state.IsOpen ? "Hide output" : "Show output";
        OutputDisplayText = state.Monitor is { } m ? m.Label : "No output display";
        ControllerText = _settings.Current.Input.SelectedController switch
        {
            ControllerKind.Xbox => "Xbox",
            ControllerKind.JogShuttle => "Jog-shuttle",
            _ => "Wheel",
        };
    }

    private void RefreshRecent()
    {
        Recent.Clear();
        foreach (var path in _settings.Current.RecentFiles) Recent.Add(new RecentFileItem(path));
        OnPropertyChanged(nameof(HasRecent));
    }
}

/// <summary>A recent-files menu entry.</summary>
public sealed record RecentFileItem(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);
}
