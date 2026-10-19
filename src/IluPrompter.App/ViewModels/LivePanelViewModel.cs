using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IluPrompter.App.Logic;
using IluPrompter.App.Services;
using IluPrompter.Core;
using IluPrompter.Theme;

namespace IluPrompter.App.ViewModels;

/// <summary>
/// The foot of the live column (DESIGN.md D7): one panel with two tabs, Controller and Output layout, and a hide control.
/// Holds which tab is open and whether the panel is hidden (both remembered in <c>ControlWindow</c>), where the body goes
/// (inline large, inline small, or a flyout over the editor, decided from the window's height only), and the flyout's
/// transient open state. Nothing here moves anything: hiding leaves the space empty so the preview never rescales mid-show.
/// </summary>
public sealed partial class LivePanelViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IUiThread _ui;
    private bool _flyoutOpen;
    private bool _typing;
    private double _editorWidth = 800;
    private LivePanelFitResult _fit = new(LivePanelBodyForm.InlineLarge, false, false);

    public LivePanelViewModel(
        ISettingsService settings,
        IKeyModeState keyMode,
        IUiThread ui,
        OutputLayoutViewModel layout,
        ControllerPanelViewModel controller)
    {
        _settings = settings;
        _ui = ui;
        Layout = layout;
        Controller = controller;
        _typing = keyMode.Mode == KeyMode.Typing;
        keyMode.Changed += (_, mode) => _ui.Post(() =>
        {
            _typing = mode == KeyMode.Typing;
            RaiseState();
        });
        settings.Changed += (_, e) =>
        {
            if (!ReferenceEquals(e.Old.ControlWindow, e.New.ControlWindow)) _ui.Post(RaiseState);
        };
        controller.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControllerPanelViewModel.BarText)) OnPropertyChanged(nameof(BarText));
        };
        SyncController();
    }

    public OutputLayoutViewModel Layout { get; }

    public ControllerPanelViewModel Controller { get; }

    private WindowSettings Window => _settings.Current.ControlWindow;

    // ===== what is remembered =====

    /// <summary>The tab the panel is on (or will open on).</summary>
    public LivePanelTab Tab => Window.LivePanelTab;

    public bool IsControllerTab => Tab == LivePanelTab.Controller;

    public bool IsOutputLayoutTab => Tab == LivePanelTab.OutputLayout;

    // ===== where the body goes =====

    public LivePanelBodyForm Form => _fit.Form;

    public bool IsFlyoutForm => _fit.Form == LivePanelBodyForm.Flyout;

    /// <summary>Hide the transport captions so the bar fits (step 1 of "The bar always fits").</summary>
    public bool HideTransportCaptions => _fit.HideTransportCaptions;

    /// <summary>Move the preview's output caption into its tooltip (step 2).</summary>
    public bool MovePreviewCaption => _fit.MovePreviewCaption;

    /// <summary>The small body (120 tall, 240 x 120 drawing): inline small, or a flyout narrower than 600.</summary>
    public bool IsSmall => _fit.Form switch
    {
        LivePanelBodyForm.InlineLarge => false,
        LivePanelBodyForm.InlineSmall => true,
        _ => !LivePanelFit.FlyoutIsLarge(_editorWidth),
    };

    public double FlyoutWidth => LivePanelFit.FlyoutWidth(_editorWidth);

    /// <summary>
    /// Fit the panel to the free height below the readouts row, measured with no give-way applied and with the LIVE caption
    /// counted as showing. Called by the live column on layout.
    /// </summary>
    public void UpdateFit(double freeHeight, bool transportCaptionsShown)
    {
        var fit = LivePanelFit.ForHeight(freeHeight, transportCaptionsShown);
        if (fit == _fit) return;
        var wasFlyout = IsFlyoutForm;
        _fit = fit;
        if (IsFlyoutForm != wasFlyout) _flyoutOpen = false;   // it never opens by itself
        RaiseState();
    }

    /// <summary>The editor's width, which sets the flyout's width and so its drawing size.</summary>
    public void UpdateEditorWidth(double width)
    {
        if (Math.Abs(width - _editorWidth) < 0.5) return;
        _editorWidth = width;
        RaiseState();
    }

    /// <summary>The window changed size: a flyout closes (it never opens by itself). The remembered setting is untouched.</summary>
    public void OnWindowResized()
    {
        if (!_flyoutOpen) return;
        _flyoutOpen = false;
        RaiseState();
    }

    // ===== open or hidden =====

    /// <summary>What the operator last asked for: the remembered setting for an inline body, the flyout's own flag for a flyout.</summary>
    public bool IsOpen => IsFlyoutForm ? _flyoutOpen : Window.LivePanelOpen;

    /// <summary>The body is on screen. A flyout hides while the caret is in the script, and comes back with the transport keys.</summary>
    public bool BodyVisible => IsFlyoutForm ? _flyoutOpen && !_typing : Window.LivePanelOpen;

    public bool InlineBodyVisible => !IsFlyoutForm && Window.LivePanelOpen;

    public bool FlyoutVisible => IsFlyoutForm && _flyoutOpen && !_typing;

    /// <summary>The controller drawing is on screen: the bar's last-input line steps aside for it.</summary>
    public bool DrawingOnScreen => BodyVisible && IsControllerTab;

    public bool ShowBarText => !DrawingOnScreen;

    public string BarText => Controller.BarText;

    /// <summary>The theme's Tag for the Controller tab: "open" draws the 2 px bar. While hidden neither tab has it.</summary>
    public string ControllerTabTag => IsOpen && IsControllerTab ? "open" : "";

    public string OutputLayoutTabTag => IsOpen && IsOutputLayoutTab ? "open" : "";

    /// <summary>The open tab's name: the flyout's title row.</summary>
    public string TabTitle => IsControllerTab ? "Controller" : "Output layout";

    public string ToggleGlyph => IsOpen ? IluIcons.ChevronDown : IluIcons.Up;

    public string ToggleTooltip => IsOpen ? "Hide this panel" : "Show this panel";

    /// <summary>The toggle's screen-reader name: its tooltip without the key.</summary>
    public string ToggleName => IsOpen ? "Hide this panel" : "Show this panel";

    public string TabNameControllerTooltip => "Controller: shows what you press and what it did";

    public string TabNameOutputLayoutTooltip => "Output layout: lines, spacing and margins";

    /// <summary>Show or hide the panel (the chevron on the tab bar, a mouse click). Writes the remembered setting.</summary>
    [RelayCommand]
    public void ToggleOpen() => SetOpen(!IsOpen);

    /// <summary>Show the Controller tab: a hidden panel opens on it, the open tab does nothing (it never hides the panel by accident).</summary>
    [RelayCommand]
    public void ShowControllerTab() => SelectTab(LivePanelTab.Controller);

    [RelayCommand]
    public void ShowOutputLayoutTab() => SelectTab(LivePanelTab.OutputLayout);

    public void SelectTab(LivePanelTab tab)
    {
        if (IsOpen && Tab == tab) return;
        var wasHidden = !IsOpen;
        WriteTab(tab);
        if (wasHidden) SetOpen(true);
        else RaiseState();
    }

    private void WriteTab(LivePanelTab tab) =>
        _settings.Update(s => s.ControlWindow.LivePanelTab == tab ? s : s with { ControlWindow = s.ControlWindow with { LivePanelTab = tab } });

    private void SetOpen(bool open)
    {
        if (IsFlyoutForm) _flyoutOpen = open;
        // The flyout ignores the setting at launch, but opening or hiding it writes it.
        _settings.Update(s => s.ControlWindow.LivePanelOpen == open ? s : s with { ControlWindow = s.ControlWindow with { LivePanelOpen = open } });
        RaiseState();
    }

    // ===== 10 Hz =====

    /// <summary>Called from the live column's poll (10 Hz). The drawing is skipped while it is not on screen.</summary>
    public void Poll()
    {
        SyncController();
        Controller.Poll();
    }

    private void SyncController()
    {
        Controller.Active = DrawingOnScreen;
        Controller.Small = IsSmall;
    }

    private void RaiseState()
    {
        SyncController();
        OnPropertyChanged(string.Empty);
    }
}
