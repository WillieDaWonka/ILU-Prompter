using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IluPrompter.App.Logic;
using IluPrompter.App.Services;
using IluPrompter.Core;

namespace IluPrompter.App.ViewModels;

/// <summary>
/// The Controller tab of the live panel (DESIGN.md "Controller drawing"): a live readout of the chosen controller.
/// <para>
/// Threading (ARCHITECTURE §3 rule 10): <c>InputObserved</c> arrives on provider threads and is only recorded in a small
/// <see cref="ControllerInputTable"/> (no WPF, no marshalling per event). <see cref="Poll"/> runs on the existing 10 Hz
/// status poll, reads the table once, builds the model and replaces <see cref="Model"/> only when it changed. Device
/// status changes are marshalled to the UI thread with <see cref="IUiThread"/>. There is no timer, animation or
/// <c>CompositionTarget.Rendering</c> here.
/// </para>
/// </summary>
public sealed partial class ControllerPanelViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IScrollEngine _engine;
    private readonly IOutputSurface _output;
    private readonly IUiThread _ui;
    private readonly Func<long> _clockMs;
    private readonly Dictionary<ControllerKind, DeviceStatus> _status = new();

    [ObservableProperty]
    private ControllerPanelModel _model = ControllerPanelModel.Empty;

    [ObservableProperty]
    private string _barText = "";

    public ControllerPanelViewModel(
        IInputRouter router,
        ISettingsService settings,
        IScrollEngine engine,
        IOutputSurface output,
        IUiThread ui,
        Func<long>? clockMs = null)
    {
        _settings = settings;
        _engine = engine;
        _output = output;
        _ui = ui;
        _clockMs = clockMs ?? (() => Environment.TickCount64);

        foreach (var s in router.DeviceStatuses) _status[s.Source.Kind] = s;
        router.InputObserved += OnInputObserved;
        router.DeviceStatusChanged += (_, status) => _ui.Post(() => _status[status.Source.Kind] = status);
        settings.Changed += OnSettingsChanged;
        Poll();
    }

    /// <summary>Raised by the "Choose a controller" link and the Missing chip: Settings › Controller should open.</summary>
    public event EventHandler<SettingsPageId>? SettingsPageRequested;

    [RelayCommand]
    private void ChooseController() => SettingsPageRequested?.Invoke(this, SettingsPageId.Controller);

    /// <summary>The table the input thread writes (exposed for tests).</summary>
    public ControllerInputTable Table { get; } = new();

    /// <summary>True when the small body (240 x 120, up to 3 rows) is showing. Set by the live panel from the fit.</summary>
    public bool Small { get; set; }

    /// <summary>
    /// True while the drawing is on screen (panel open on the Controller tab, flyout shown). When false the poll skips
    /// the drawing and updates only <see cref="BarText"/>.
    /// </summary>
    public bool Active { get; set; } = true;

    // ===== input thread =====

    private void OnInputObserved(object? sender, InputEvent e)
    {
        var input = _settings.Current.Input;
        var kind = e.Source.Kind;
        // Only inputs the router accepts for the chosen controller: never the keyboard, never another controller kind,
        // and with your own mouse only what reached the preview (the "app" source); a second mouse only its own events.
        if (kind == ControllerKind.Keyboard || kind != input.SelectedController) return;
        if (kind == ControllerKind.MouseWheel)
        {
            var fromPreview = string.Equals(e.Source.DeviceId, AppSources.App, StringComparison.Ordinal);
            if (fromPreview == (input.Wheel.DedicatedDeviceId is not null)) return;
        }

        Table.Record(e, _clockMs(), e is AxisInput ? AxisThreshold(input, kind, e.ControlId) : ControllerInputTable.MinSignificantAxis);
    }

    /// <summary>An axis counts as input only outside its deadzone, so a resting stick's jitter is not "use".</summary>
    private static double AxisThreshold(InputSettings input, ControllerKind kind, string controlId)
    {
        var axes = kind == ControllerKind.Xbox ? input.Xbox.Axes : kind == ControllerKind.JogShuttle ? input.Shuttle.Axes : null;
        var configured = axes is not null && axes.TryGetValue(controlId, out var b) && b.Role != AxisRole.None ? b.Deadzone : DefaultDeadzone(controlId);
        return Math.Max(ControllerInputTable.MinSignificantAxis, configured);
    }

    private static double DefaultDeadzone(string controlId) => controlId switch
    {
        ControlIds.LeftThumbX or ControlIds.LeftThumbY => ControllerModelBuilder.XboxLeftStickDeadzone,
        ControlIds.RightThumbX or ControlIds.RightThumbY => ControllerModelBuilder.XboxRightStickDeadzone,
        ControlIds.LeftTrigger or ControlIds.RightTrigger => ControllerModelBuilder.XboxTriggerDeadzone,
        _ => 0,
    };

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        var a = e.Old.Input;
        var b = e.New.Input;
        if (a.SelectedController != b.SelectedController
            || a.Wheel.DedicatedDeviceId != b.Wheel.DedicatedDeviceId
            || a.Xbox.UserIndex != b.Xbox.UserIndex
            || a.Shuttle.DeviceId != b.Shuttle.DeviceId)
        {
            Table.Clear();   // a different controller: nothing seen so far belongs to it
        }
    }

    // ===== 10 Hz poll =====

    /// <summary>Called at 10 Hz on the UI thread. Sets properties only when their value changed.</summary>
    public void Poll()
    {
        var input = _settings.Current.Input;
        _status.TryGetValue(input.SelectedController, out var status);
        var build = new ControllerBuildInput(input, _engine.Status.Mode, status, _output.State.IsLive, Table.Snapshot(), _clockMs(), Small);
        if (!Active)
        {
            var bar = ControllerModelBuilder.BarText(build);
            if (bar != BarText) BarText = bar;
            return;
        }

        var model = ControllerModelBuilder.Build(build);
        if (model.Equals(Model)) return;
        Model = model;
        if (model.BarText != BarText) BarText = model.BarText;
    }
}
