using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IluPrompter.App.Editing;
using IluPrompter.App.Logic;
using IluPrompter.App.Services;
using IluPrompter.App.ViewModels;
using IluPrompter.Core;
using IluPrompter.Input;
using IluPrompter.Rendering;

namespace IluPrompter.App.Views;

/// <summary>
/// The control window shell. Layout and tiers live in XAML and <see cref="LayoutTiers"/>; everything stateful is in the
/// view models. This class wires the pieces that need the window itself: the input bridge, the preview, key focus,
/// bounds, the 10 Hz poll and the Settings window.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _model;
    private readonly ScriptSession _session;
    private readonly IInputRouter _router;
    private readonly ISettingsService _settings;
    private readonly IKeyModeState _keyMode;
    private readonly IDialogService _dialogs;
    private readonly IAppLog _log;
    private readonly Func<SettingsWindow> _settingsFactory;
    private readonly EditorController _editor;
    private readonly DispatcherTimer _poll;
    private SettingsWindow? _settingsWindow;
    private SettingsPageId _lastSettingsPage = SettingsPageId.Controller;
    private bool _devSized;
    private bool _flyoutTracking;

    public MainWindow(
        MainViewModel model,
        IAppInputBridge inputBridge,
        PreviewServices previewServices,
        EditorServices editorServices,
        EditorToolbarViewModel toolbar,
        ScriptSession session,
        IInputRouter router,
        ISettingsService settings,
        IKeyModeState keyMode,
        IDialogService dialogs,
        IAppLog log,
        Func<SettingsWindow> settingsFactory)
    {
        _model = model;
        _session = session;
        _router = router;
        _settings = settings;
        _keyMode = keyMode;
        _dialogs = dialogs;
        _log = log;
        _settingsFactory = settingsFactory;
        DataContext = model;

        InitializeComponent();
        RestoreWindowBounds();

        // Startup order (ARCHITECTURE 2.15): attach the input bridge to the window and the preview, then Attach the preview.
        _editor = EditorArea.Attach(editorServices, toolbar, model.Live);
        _editor.SetTransportFocus(FocusTransport);
        session.Attach(_editor);
        model.Sections.Attach(_editor);
        inputBridge.AttachWindow(this);
        inputBridge.AttachWheelArea(LiveArea.PreviewControl);
        LiveArea.PreviewControl.Attach(previewServices);

        model.SettingsRequested += (_, page) => ShowSettings(page);
        model.Live.SettingsPageRequested += (_, page) => ShowSettings(page);
        model.ShortcutsRequested += (_, _) => ShowSettings(SettingsPageId.Shortcuts);
        model.ExitRequested += (_, _) => Close();
        model.Sections.PropertyChanged += OnSectionsChanged;
        router.AppCommandRequested += OnAppCommandRequested;
        model.Live.Panel.PropertyChanged += OnLivePanelChanged;
        EditorArea.SizeChanged += (_, e) => model.Live.Panel.UpdateEditorWidth(e.NewSize.Width);

        _poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        _poll.Tick += OnPoll;
        _poll.Start();
        Closed += (_, _) =>
        {
            _poll.Stop();
            router.AppCommandRequested -= OnAppCommandRequested;
        };
        ApplyTier(LayoutTiers.ForWidth(Width));
    }

    /// <summary>Dev only (<c>--window-size WxH</c>): open at an exact size instead of maximised, and do not remember it.</summary>
    public void UseDevWindowSize(double width, double height)
    {
        _devSized = true;
        WindowState = WindowState.Normal;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0;
        Top = 0;
        Width = width;
        Height = height;
        ApplyTier(LayoutTiers.ForWidth(width));
    }

    // ===== tiers =====

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyTier(LayoutTiers.ForWidth(e.NewSize.Width));
        _model.Live.Panel.OnWindowResized();   // a live panel flyout closes when the window changes size
    }

    private void ApplyTier(LayoutTier tier)
    {
        if (_model.Tier != tier) _model.Tier = tier;
        var wide = tier == LayoutTier.Wide;
        SectionsColumn.Width = new GridLength(wide ? 240 : 44);
        SectionsWide.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        SectionsRail.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
        LiveColumnDefinition.Width = new GridLength(LayoutTiers.LiveColumnWidth(tier));
        EditorArea.ApplyTier(wide);
        if (wide) _model.Sections.IsFlyoutOpen = false;
    }

    /// <summary>The tier the window is showing now (for screenshots and tests).</summary>
    public LayoutTier Tier => _model.Tier;

    // ===== bounds =====

    private void RestoreWindowBounds()
    {
        var w = _settings.Current.ControlWindow;
        if (!double.IsNaN(w.Left) && !double.IsNaN(w.Top) && IsOnScreen(w.Left, w.Top, w.Width, w.Height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = w.Left;
            Top = w.Top;
            Width = Math.Max(MinWidth, w.Width);
            Height = Math.Max(MinHeight, w.Height);
        }

        WindowState = w.IsMaximized ? WindowState.Maximized : WindowState.Normal;
    }

    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var window = new Rect(left, top, Math.Max(100, width), Math.Max(100, height));
        window.Intersect(screen);
        return !window.IsEmpty && window.Width >= 100 && window.Height >= 100;
    }

    private void SaveBounds()
    {
        if (_devSized) return;
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        var maximised = WindowState == WindowState.Maximized;
        // A window that opened maximised and was never restored has an empty RestoreBounds (Left/Top = +Infinity,
        // Width/Height = -Infinity): keep the remembered normal bounds and store only the maximised flag.
        var valid = !bounds.IsEmpty && double.IsFinite(bounds.Left) && double.IsFinite(bounds.Top) && bounds.Width > 0 && bounds.Height > 0;
        // Copied with "with", never rebuilt: the live panel's tab and hidden flag live in the same record.
        _settings.Update(s => s with
        {
            ControlWindow = WindowBounds.Apply(s.ControlWindow, bounds.Left, bounds.Top, bounds.Width, bounds.Height, maximised, valid),
        });
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_session.ConfirmCanDiscard())
        {
            e.Cancel = true;
            return;
        }

        SaveBounds();
    }

    // ===== key focus: who has the keys =====

    protected override void OnPreviewGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnPreviewGotKeyboardFocus(e);
        if (e.NewFocus is not DependencyObject target) return;
        if (IsWithin(EditorArea.Editor, target)) _keyMode.Mode = KeyMode.Typing;
        else if (IsWithin(LiveArea, target) || IsWithin(PanelFlyout, target) || IsWithin(SectionsWide, target) || IsWithin(SectionsRail, target) || IsWithin(SectionsFlyout, target))
            _keyMode.Mode = KeyMode.Transport;
    }

    private static bool IsWithin(DependencyObject ancestor, DependencyObject element)
    {
        for (var current = element; current is not null; current = current is Visual or System.Windows.Media.Media3D.Visual3D
                 ? VisualTreeHelper.GetParent(current)
                 : LogicalTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, ancestor)) return true;
        }

        return false;
    }

    private void FocusTransport()
    {
        if (!LiveArea.PreviewControl.Focus()) LiveArea.FocusPlay();
    }

    // ===== 10 Hz poll =====

    private void OnPoll(object? sender, EventArgs e)
    {
        try
        {
            _model.Live.Poll();
            _model.Sections.Poll();
            _model.Poll();
            _editor.UpdateReadingMarker();
            _settingsWindow?.ViewModel.Poll();
        }
        catch (Exception ex)
        {
            _log.Error("ui", "Status poll failed", ex);
        }
    }

    private void OnAppCommandRequested(object? sender, PrompterCommand command)
    {
        if (command != PrompterCommand.ToggleOutput) return;
        Dispatcher.BeginInvoke(new Action(() => _model.ToggleOutputCommand.Execute(null)));
    }

    // ===== live panel (D7) =====

    /// <summary>The flyout's bottom edge is level with the tab bar's bottom edge. It is placed only while it is showing.</summary>
    private void OnLivePanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        var visible = _model.Live.Panel.FlyoutVisible;
        if (visible == _flyoutTracking) return;
        _flyoutTracking = visible;
        if (visible)
        {
            LayoutUpdated += OnFlyoutLayoutUpdated;
            PositionFlyout();
        }
        else
        {
            LayoutUpdated -= OnFlyoutLayoutUpdated;
        }
    }

    private void OnFlyoutLayoutUpdated(object? sender, EventArgs e) => PositionFlyout();

    private void PositionFlyout()
    {
        var bar = LiveArea.PanelView.TabBar;
        if (!bar.IsVisible || BodyGrid.ActualHeight <= 0) return;
        var barBottom = bar.TranslatePoint(new Point(0, bar.ActualHeight), BodyGrid).Y;
        var offset = Math.Max(0, Math.Round(BodyGrid.ActualHeight - barBottom));
        if (Math.Abs(PanelFlyout.Margin.Bottom - offset) > 0.5) PanelFlyout.Margin = new Thickness(0, 0, 0, offset);
    }

    // ===== sections flyout =====

    private void OnSectionsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SectionsViewModel.IsFlyoutOpen)) return;
        var open = _model.Sections.IsFlyoutOpen;
        Flyout.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open) Dispatcher.BeginInvoke(new Action(SectionsFlyout.FocusList), DispatcherPriority.Input);
        else if (_keyMode.Mode != KeyMode.Typing) FocusTransport();
    }

    private void OnToggleFlyout(object sender, RoutedEventArgs e) => _model.Sections.IsFlyoutOpen = !_model.Sections.IsFlyoutOpen;

    private void OnFlyoutScrimDown(object sender, MouseButtonEventArgs e)
    {
        _model.Sections.IsFlyoutOpen = false;
        e.Handled = true;
    }

    private void OnRenameSection(object? sender, SectionRowViewModel row)
    {
        var name = _dialogs.Prompt("Rename section", "Give this section a new name.", row.Name, "Rename section");
        if (name is not null) _model.Sections.Rename(row, name);
    }

    private void OnDeleteSection(object? sender, SectionRowViewModel row) => _model.Sections.DeleteMarker(row);

    // ===== settings =====

    private void ShowSettings(SettingsPageId page)
    {
        _lastSettingsPage = page;
        if (_settingsWindow is null)
        {
            var window = _settingsFactory();
            window.Owner = this;
            window.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow = window;
            window.Show();
        }

        _settingsWindow.ViewModel.Select(page);
        _settingsWindow.Activate();
    }

    // ===== commands and menus =====

    private void OnNew(object sender, ExecutedRoutedEventArgs e) => _model.NewScriptCommand.Execute(null);

    private void OnOpen(object sender, ExecutedRoutedEventArgs e) => _model.OpenScriptCommand.Execute(null);

    private void OnSave(object sender, ExecutedRoutedEventArgs e) => _model.SaveScriptCommand.Execute(null);

    private void OnSaveAs(object sender, ExecutedRoutedEventArgs e) => _model.SaveScriptAsCommand.Execute(null);

    private void OnShowSettings(object sender, ExecutedRoutedEventArgs e) => ShowSettings(_settingsWindow is null ? SettingsPageId.Controller : _lastSettingsPage);

    private void OnShowShortcuts(object sender, ExecutedRoutedEventArgs e) => ShowSettings(SettingsPageId.Shortcuts);

    private void OnGoToScript(object sender, ExecutedRoutedEventArgs e) => _editor.FocusEditor();

    private void OnOutputSettings(object sender, RoutedEventArgs e) => ShowSettings(SettingsPageId.Output);

    private void OnAbout(object sender, RoutedEventArgs e) => ShowSettings(SettingsPageId.About);

    private void OnUndo(object sender, RoutedEventArgs e) => _editor.Undo();

    private void OnRedo(object sender, RoutedEventArgs e) => _editor.Redo();

    private void OnCut(object sender, RoutedEventArgs e) => ApplicationCommands.Cut.Execute(null, EditorArea.Editor);

    private void OnCopy(object sender, RoutedEventArgs e) => ApplicationCommands.Copy.Execute(null, EditorArea.Editor);

    private void OnPaste(object sender, RoutedEventArgs e) => ApplicationCommands.Paste.Execute(null, EditorArea.Editor);

    private void OnSelectAll(object sender, RoutedEventArgs e) => ApplicationCommands.SelectAll.Execute(null, EditorArea.Editor);

    private void OnFind(object sender, RoutedEventArgs e) => _editor.OpenFind();

    private void OnInsertSection(object? sender, EventArgs e) => _editor.InsertSectionMarker();

    private void OnClearFormatting(object sender, RoutedEventArgs e) => _editor.ClearFormatting();

    private void OnLarger(object sender, RoutedEventArgs e) => _editor.StepSize(+1);

    private void OnSmaller(object sender, RoutedEventArgs e) => _editor.StepSize(-1);

    private void OnJumpToStart(object sender, RoutedEventArgs e) => _router.Execute(PrompterCommand.JumpToStart);
}
