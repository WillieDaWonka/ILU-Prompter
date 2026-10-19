using System.Collections.ObjectModel;
using System.Globalization;
using IluPrompter.App.Logic;
using IluPrompter.App.Services;
using IluPrompter.Core;

namespace IluPrompter.App.ViewModels;

/// <summary>A choice in a device picker.</summary>
public sealed record DeviceChoice(string Id, string Label);

/// <summary>A row of the read-only mapping table.</summary>
public sealed record MappingRow(string Control, string Action);

/// <summary>
/// Settings › Controller (DESIGN.md): three selectable cards with live chips, a device picker for the chosen
/// controller, its read-only mapping table, a speed-sensitivity value and the "Test: move the controller" readout.
/// </summary>
public sealed class ControllerPageViewModel : SettingsBackedViewModel, ISettingsPage
{
    public const string AnyDevice = "";

    private readonly IInputRouter _router;
    private readonly IOutputSurface _output;
    private readonly IReadOnlyList<IScrollInputProvider> _providers;
    private readonly Dictionary<ControllerKind, DeviceStatus> _status = new();
    private InputEvent? _lastInput;
    private bool _rebuilding;
    private string _lastRendered = "";

    public ControllerPageViewModel(
        ISettingsService settings,
        IUiThread ui,
        IInputRouter router,
        IOutputSurface output,
        IEnumerable<IScrollInputProvider> providers)
        : base(settings, ui)
    {
        _router = router;
        _output = output;
        _providers = providers.ToList();
        foreach (var s in router.DeviceStatuses) _status[s.Source.Kind] = s;
        router.DeviceStatusChanged += (_, s) =>
            Ui.Post(() =>
            {
                _status[s.Source.Kind] = s;
                RaiseChips();
                RefreshDevices();
            });
        output.StateChanged += (_, _) => Ui.Post(RaiseChips);
        router.InputObserved += (_, e) =>
        {
            if (e.Source.Kind == ControllerKind.Keyboard) return;
            _lastInput = e;
        };
        RefreshDevices();
    }

    public string Title => "Controller";

    public bool CanReset => true;

    public string ResetDescription => "Every controller setting goes back to its factory value. The mouse wheel is chosen.";

    // ===== cards =====

    public bool WheelSelected { get => Selected == ControllerKind.MouseWheel; set => Select(ControllerKind.MouseWheel, value); }

    public bool XboxSelected { get => Selected == ControllerKind.Xbox; set => Select(ControllerKind.Xbox, value); }

    public bool ShuttleSelected { get => Selected == ControllerKind.JogShuttle; set => Select(ControllerKind.JogShuttle, value); }

    public ControllerKind Selected => Current.Input.SelectedController;

    public ChipModel WheelChip => ChipFor(ControllerKind.MouseWheel);

    public ChipModel XboxChip => ChipFor(ControllerKind.Xbox);

    public ChipModel ShuttleChip => ChipFor(ControllerKind.JogShuttle);

    private void Select(ControllerKind kind, bool isOn)
    {
        if (!isOn || Selected == kind) return;
        Update(s => s with { Input = s.Input with { SelectedController = kind } });
        RefreshDevices();
    }

    private ChipModel ChipFor(ControllerKind kind)
    {
        if (kind != Selected) return ChipMapper.NotInUse(kind);
        _status.TryGetValue(kind, out var status);
        return ChipMapper.Controller(kind, status, _output.State.IsLive);
    }

    private void RaiseChips()
    {
        OnPropertyChanged(nameof(WheelChip));
        OnPropertyChanged(nameof(XboxChip));
        OnPropertyChanged(nameof(ShuttleChip));
    }

    // ===== device picker =====

    public ObservableCollection<DeviceChoice> Devices { get; } = [];

    public string DeviceLabel => Selected switch
    {
        ControllerKind.Xbox => "Controller",
        ControllerKind.JogShuttle => "Device",
        _ => "Mouse",
    };

    public string DeviceHint => Selected switch
    {
        ControllerKind.MouseWheel => "Choose a second mouse to drive the prompt even when this window is not in front. Leave it on the preview to use the wheel over the preview only.",
        ControllerKind.Xbox => "The prompt follows this controller even when this window is not in front.",
        _ => "The shuttle ring sets the speed and the jog wheel steps lines, even when this window is not in front.",
    };

    public string SelectedDeviceId
    {
        get => Selected switch
        {
            ControllerKind.MouseWheel => Current.Input.Wheel.DedicatedDeviceId ?? AnyDevice,
            ControllerKind.Xbox => Current.Input.Xbox.UserIndex < 0 ? AnyDevice : "xinput:" + Current.Input.Xbox.UserIndex.ToString(CultureInfo.InvariantCulture),
            _ => Current.Input.Shuttle.DeviceId ?? AnyDevice,
        };
        set
        {
            if (value is null || _rebuilding) return; // the picker clears itself while it is rebuilt
            var id = value;
            if (id == SelectedDeviceId) return;
            switch (Selected)
            {
                case ControllerKind.MouseWheel:
                    Update(s => s with { Input = s.Input with { Wheel = s.Input.Wheel with { DedicatedDeviceId = id.Length == 0 ? null : id } } });
                    break;
                case ControllerKind.Xbox:
                    var index = id.Length == 0 ? -1 : int.Parse(id["xinput:".Length..], CultureInfo.InvariantCulture);
                    Update(s => s with { Input = s.Input with { Xbox = s.Input.Xbox with { UserIndex = index } } });
                    break;
                default:
                    Update(s => s with { Input = s.Input with { Shuttle = s.Input.Shuttle with { DeviceId = id.Length == 0 ? null : id } } });
                    break;
            }
        }
    }

    /// <summary>Rebuild the picker from the providers. Cheap; called on open, on Refresh and on device changes.</summary>
    public void RefreshDevices()
    {
        _rebuilding = true;
        Devices.Clear();
        var found = _providers.FirstOrDefault(p => p.Kind == Selected)?.EnumerateDevices() ?? Array.Empty<InputDeviceInfo>();
        switch (Selected)
        {
            case ControllerKind.MouseWheel:
                Devices.Add(new DeviceChoice(AnyDevice, "Wheel over the preview only"));
                foreach (var d in found) Devices.Add(new DeviceChoice(d.DeviceId, d.DisplayName));
                break;
            case ControllerKind.Xbox:
                Devices.Add(new DeviceChoice(AnyDevice, "First connected controller"));
                for (var i = 0; i < 4; i++)
                {
                    var id = "xinput:" + i.ToString(CultureInfo.InvariantCulture);
                    var live = found.Any(d => d.DeviceId == id && d.IsConnected);
                    Devices.Add(new DeviceChoice(id, $"Controller {i + 1}" + (live ? " · connected" : "")));
                }

                break;
            default:
                Devices.Add(new DeviceChoice(AnyDevice, "First device found"));
                foreach (var d in found) Devices.Add(new DeviceChoice(d.DeviceId, d.DisplayName));
                break;
        }

        _rebuilding = false;
        OnPropertyChanged(nameof(SelectedDeviceId));
        OnPropertyChanged(nameof(DeviceLabel));
        OnPropertyChanged(nameof(DeviceHint));
        OnPropertyChanged(nameof(Mapping));
        OnPropertyChanged(nameof(SensitivityLabel));
        OnPropertyChanged(nameof(SensitivityMin));
        OnPropertyChanged(nameof(SensitivityMax));
        OnPropertyChanged(nameof(SensitivityStep));
        OnPropertyChanged(nameof(SensitivityValue));
        RaiseChips();
    }

    // ===== sensitivity =====

    public string SensitivityLabel => Selected switch
    {
        ControllerKind.MouseWheel => "Speed change per notch",
        ControllerKind.Xbox => "Top speed at a full trigger",
        _ => "Top speed at the end of the ring",
    };

    public double SensitivityMin => Selected == ControllerKind.MouseWheel ? 0.1 : 2;

    public double SensitivityMax => Selected == ControllerKind.MouseWheel ? 2 : 10;

    public double SensitivityStep => Selected == ControllerKind.MouseWheel ? 0.1 : 0.5;

    public double SensitivityValue
    {
        get => Selected switch
        {
            ControllerKind.MouseWheel => Current.Input.Wheel.Wheel.SpeedPerUnit,
            ControllerKind.Xbox => Current.Input.Xbox.Axes.TryGetValue(ControlIds.RightTrigger, out var a) ? a.MaxSpeed : 10,
            _ => Current.Input.Shuttle.Axes.TryGetValue(ControlIds.ShuttleRing, out var r) ? r.MaxSpeed : 10,
        };
        set
        {
            var v = Math.Round(Clamp(value, SensitivityMin, SensitivityMax), 1);
            if (Math.Abs(v - SensitivityValue) < 0.0001) return;
            switch (Selected)
            {
                case ControllerKind.MouseWheel:
                    Update(s => s with { Input = s.Input with { Wheel = s.Input.Wheel with { Wheel = s.Input.Wheel.Wheel with { SpeedPerUnit = v } } } });
                    break;
                case ControllerKind.Xbox:
                    Update(s => s with { Input = s.Input with { Xbox = s.Input.Xbox with { Axes = WithMaxSpeed(s.Input.Xbox.Axes, v, ControlIds.RightTrigger, ControlIds.LeftTrigger) } } });
                    break;
                default:
                    Update(s => s with { Input = s.Input with { Shuttle = s.Input.Shuttle with { Axes = WithMaxSpeed(s.Input.Shuttle.Axes, v, ControlIds.ShuttleRing) } } });
                    break;
            }
        }
    }

    private static IReadOnlyDictionary<string, AxisBinding> WithMaxSpeed(IReadOnlyDictionary<string, AxisBinding> axes, double max, params string[] ids)
    {
        var copy = new Dictionary<string, AxisBinding>(axes);
        foreach (var id in ids)
            if (copy.TryGetValue(id, out var binding)) copy[id] = binding with { MaxSpeed = max };
        return copy;
    }

    // ===== mapping table (read-only in v1) =====

    public IReadOnlyList<MappingRow> Mapping
    {
        get
        {
            var rows = new List<MappingRow>();
            var input = Current.Input;
            switch (Selected)
            {
                case ControllerKind.MouseWheel:
                    rows.Add(new MappingRow(Labels.Control(ControlIds.Wheel), Labels.Relative(input.Wheel.Wheel)));
                    AddButtons(rows, input.Wheel.Buttons);
                    break;
                case ControllerKind.Xbox:
                    AddAxes(rows, input.Xbox.Axes);
                    AddButtons(rows, input.Xbox.Buttons);
                    break;
                default:
                    AddAxes(rows, input.Shuttle.Axes);
                    rows.Add(new MappingRow(Labels.Control(ControlIds.JogWheel), Labels.Relative(input.Shuttle.Jog)));
                    AddButtons(rows, input.Shuttle.Buttons);
                    break;
            }

            return rows;
        }
    }

    private static void AddAxes(List<MappingRow> rows, IReadOnlyDictionary<string, AxisBinding> axes)
    {
        foreach (var (id, binding) in axes.Where(a => a.Value.Role != AxisRole.None))
            rows.Add(new MappingRow(Labels.Control(id), Labels.AxisRoleText(binding.Role)));
    }

    private static void AddButtons(List<MappingRow> rows, IReadOnlyDictionary<string, BindingAction> buttons)
    {
        foreach (var (id, action) in buttons.OrderBy(b => b.Key, StringComparer.Ordinal))
            rows.Add(new MappingRow(Labels.Control(id), Labels.Action(action)));
    }

    // ===== test readout =====

    /// <summary>The last controller input, or the prompt to move one. Updated by <see cref="Poll"/>.</summary>
    public string TestReadout { get; private set; } = "Move the controller to see its input here.";

    public bool HasTestInput { get; private set; }

    /// <summary>10 Hz while the window is open. Keeps the input thread free of UI work.</summary>
    public void Poll()
    {
        var input = _lastInput;
        if (input is null) return;
        var text = Describe(input);
        if (text == _lastRendered) return;
        _lastRendered = text;
        TestReadout = text;
        HasTestInput = true;
        OnPropertyChanged(nameof(TestReadout));
        OnPropertyChanged(nameof(HasTestInput));
    }

    public static string Describe(InputEvent input)
    {
        var name = Labels.Control(input.ControlId);
        return input switch
        {
            AxisInput a => $"{name} · {a.Value.ToString("0.00", CultureInfo.InvariantCulture)}",
            RelativeInput r => $"{name} · {(r.Amount >= 0 ? "+" : ReadoutFormat.Minus)}{Math.Abs(r.Amount).ToString("0.##", CultureInfo.InvariantCulture)}",
            ButtonInput b => $"{name} · {(b.IsPressed ? "pressed" : "released")}",
            _ => name,
        };
    }

    public void ResetToDefaults()
    {
        Update(s => s with
        {
            Input = s.Input with
            {
                SelectedController = ControllerKind.MouseWheel,
                Wheel = new WheelSettings(),
                Xbox = new XboxSettings(),
                Shuttle = new ShuttleSettings(),
            },
        });
        RefreshDevices();
    }

    protected override void OnSettingsReloaded() => RefreshDevices();
}
