namespace IluPrompter.Core;

/// <summary>
/// Core's input hub (ARCHITECTURE §2.3). Owns the providers' lifecycle from <c>Settings.Input.SelectedController</c>,
/// applies bindings, deadzones, curves and the key context, and turns input into <see cref="EngineCommand"/>s.
/// Thread-safe: providers and the in-window bridge call <see cref="Submit"/> and <see cref="ReportStatus"/> from
/// their own threads. It never touches WPF objects.
/// </summary>
public sealed class InputRouter : IInputRouter, IInputSink
{
    private const int MaxSlots = EngineCommand.MaxAxisSlots;
    private const int MaxLinesPerEvent = 1000;

    private readonly IScrollEngine _engine;
    private readonly ISettingsService _settings;
    private readonly IKeyModeState _keyMode;
    private readonly IOutputSurface _output;
    private readonly IScrollInputProvider[] _providers;
    private readonly IAppLog _log;

    // _gate guards the slot table, accumulators and statuses. It is never held while calling out.
    private readonly object _gate = new();
    private readonly Entry[] _slots = new Entry[MaxSlots];
    private readonly Entry[] _accumulators = new Entry[MaxSlots];
    private readonly List<DeviceStatus> _statuses = [];

    // _applyGate serialises provider start/stop (providers may report status from their own threads meanwhile).
    private readonly object _applyGate = new();
    private InputSettings? _lastApplied;
    private bool _started;
    private bool _slotsFullLogged;

    public InputRouter(
        IScrollEngine engine,
        ISettingsService settings,
        IKeyModeState keyMode,
        IOutputSurface output,
        IEnumerable<IScrollInputProvider> providers,
        IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(keyMode);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(log);
        _engine = engine;
        _settings = settings;
        _keyMode = keyMode;
        _output = output;
        _providers = providers.ToArray();
        _log = log;
    }

    public IReadOnlyList<DeviceStatus> DeviceStatuses
    {
        get
        {
            lock (_gate)
            {
                return _statuses.ToArray();
            }
        }
    }

    public event EventHandler<DeviceStatus>? DeviceStatusChanged;

    public event EventHandler<InputEvent>? InputObserved;

    public event EventHandler<PrompterCommand>? AppCommandRequested;

    // ---- Lifecycle ------------------------------------------------------------------------------------------

    public void Start()
    {
        lock (_gate)
        {
            _statuses.Clear();
        }

        lock (_applyGate)
        {
            if (_started)
            {
                _settings.Changed -= OnSettingsChanged;
            }

            _started = true;
            _lastApplied = null;
            _settings.Changed += OnSettingsChanged;
        }

        ApplyInputSettings();
    }

    public void Stop()
    {
        lock (_applyGate)
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _settings.Changed -= OnSettingsChanged;
            _lastApplied = null;
            foreach (var provider in _providers)
            {
                if (provider.Kind == ControllerKind.Keyboard)
                {
                    continue;
                }

                try
                {
                    provider.Stop();
                }
                catch (Exception ex)
                {
                    _log.Error("input", $"Stopping the {provider.Kind} provider failed.", ex);
                }
            }
        }

        ReleaseAllControllerState();
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Old.Input, e.New.Input))
        {
            ApplyInputSettings();
        }
    }

    private void ApplyInputSettings()
    {
        InputKindChange? change = null;
        lock (_applyGate)
        {
            if (!_started)
            {
                return;
            }

            var input = _settings.Current.Input;
            if (ReferenceEquals(input, _lastApplied))
            {
                return;
            }

            var previous = _lastApplied;
            _lastApplied = input;
            var selected = input.SelectedController;
            TryGetDeviceId(input, selected, out var deviceId);
            var wanted = WantsProvider(input, selected);
            var sameDevice = previous is not null
                && previous.SelectedController == selected
                && TryGetDeviceId(previous, selected, out var previousId)
                && string.Equals(previousId, deviceId, StringComparison.Ordinal);

            foreach (var provider in _providers)
            {
                if (provider.Kind == ControllerKind.Keyboard)
                {
                    continue;
                }

                try
                {
                    if (wanted && provider.Kind == selected)
                    {
                        if (!provider.IsRunning || !sameDevice)
                        {
                            provider.Start(this, deviceId);
                        }
                    }
                    else if (provider.IsRunning)
                    {
                        provider.Stop();
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("input", $"The {provider.Kind} provider failed to start or stop.", ex);
                }
            }

            if (previous is not null && previous.SelectedController != selected)
            {
                change = new InputKindChange(previous.SelectedController);
            }
        }

        if (change is { } c)
        {
            ReleaseControllerState(c.Previous, postZeros: true);
        }
    }

    /// <summary>MouseWheel needs a dedicated device to run a provider; the other kinds always run.</summary>
    private static bool WantsProvider(InputSettings input, ControllerKind kind) => kind switch
    {
        ControllerKind.MouseWheel => input.Wheel.DedicatedDeviceId is not null,
        ControllerKind.Xbox or ControllerKind.JogShuttle => true,
        _ => false,
    };

    private static bool TryGetDeviceId(InputSettings input, ControllerKind kind, out string? deviceId)
    {
        switch (kind)
        {
            case ControllerKind.MouseWheel:
                deviceId = input.Wheel.DedicatedDeviceId;
                return deviceId is not null;
            case ControllerKind.Xbox:
                deviceId = input.Xbox.UserIndex >= 0
                    ? "xinput:" + Math.Min(input.Xbox.UserIndex, 3).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : null;
                return true;
            case ControllerKind.JogShuttle:
                deviceId = input.Shuttle.DeviceId;
                return true;
            default:
                deviceId = null;
                return false;
        }
    }

    // ---- Execute --------------------------------------------------------------------------------------------

    public bool Execute(PrompterCommand command)
    {
        var scroll = _settings.Current.Scroll;
        switch (command)
        {
            case PrompterCommand.PlayPause:
                if (_engine.Status.Transport == TransportState.AtEnd) { return false; }
                _engine.Post(EngineCommand.TogglePlayPause());
                return true;
            case PrompterCommand.Play:
                if (_engine.Status.Transport == TransportState.AtEnd) { return false; }
                _engine.Post(EngineCommand.Play());
                return true;
            case PrompterCommand.Pause:
                _engine.Post(EngineCommand.Pause());
                return true;
            case PrompterCommand.SpeedUp:
                _engine.Post(EngineCommand.NudgeSpeed(scroll.SpeedStep));
                return true;
            case PrompterCommand.SpeedDown:
                _engine.Post(EngineCommand.NudgeSpeed(-scroll.SpeedStep));
                return true;
            case PrompterCommand.SpeedUpFine:
                _engine.Post(EngineCommand.NudgeSpeed(scroll.FineSpeedStep));
                return true;
            case PrompterCommand.SpeedDownFine:
                _engine.Post(EngineCommand.NudgeSpeed(-scroll.FineSpeedStep));
                return true;
            case PrompterCommand.SpeedReset:
                _engine.Post(EngineCommand.ResetSpeed());
                return true;
            case PrompterCommand.LineNext:
                _engine.Post(EngineCommand.StepLines(1));
                return true;
            case PrompterCommand.LinePrevious:
                _engine.Post(EngineCommand.StepLines(-1));
                return true;
            case PrompterCommand.ParagraphNext:
                _engine.Post(EngineCommand.StepParagraphs(1));
                return true;
            case PrompterCommand.ParagraphPrevious:
                _engine.Post(EngineCommand.StepParagraphs(-1));
                return true;
            case PrompterCommand.SectionNext:
                _engine.Post(EngineCommand.StepSections(1));
                return true;
            case PrompterCommand.SectionPrevious:
                _engine.Post(EngineCommand.StepSections(-1));
                return true;
            case PrompterCommand.JumpToStart:
                _engine.Post(EngineCommand.JumpToStart());
                return true;
            case PrompterCommand.JumpToEnd:
                _engine.Post(EngineCommand.JumpToEnd());
                return true;
            case PrompterCommand.ToggleScrollMode:
                _engine.Post(EngineCommand.ToggleMode());
                return true;
            case PrompterCommand.SetDisplayScrolling:
                _engine.Post(EngineCommand.SetMode(ScrollMode.Display));
                return true;
            case PrompterCommand.SetLineScrolling:
                _engine.Post(EngineCommand.SetMode(ScrollMode.Line));
                return true;
            case PrompterCommand.ToggleBlank:
                _output.SetBlank(!_output.State.IsBlank);
                return true;
            case PrompterCommand.ToggleOutput:
                RaiseAppCommand(command);
                return true;
            default:
                return false;
        }
    }

    // ---- Submit ---------------------------------------------------------------------------------------------

    public bool Submit(InputEvent input)
    {
        ArgumentNullException.ThrowIfNull(input);
        RaiseObserved(input);

        var settings = _settings.Current.Input;
        var kind = input.Source.Kind;
        if (kind == ControllerKind.Keyboard)
        {
            return input is ButtonInput key && HandleKey(key, settings);
        }

        if (kind != settings.SelectedController)
        {
            return false;
        }

        if (kind == ControllerKind.MouseWheel)
        {
            // The WPF wheel over the preview ("app") counts only without a dedicated mouse; a dedicated mouse's
            // Raw Input events count only with one. This avoids double counting.
            var isApp = string.Equals(input.Source.DeviceId, AppSources.App, StringComparison.Ordinal);
            if (isApp == (settings.Wheel.DedicatedDeviceId is not null))
            {
                return false;
            }
        }

        return input switch
        {
            ButtonInput button => HandleButton(button, settings),
            AxisInput axis => HandleAxis(axis, settings),
            RelativeInput relative => HandleRelative(relative, settings),
            _ => false,
        };
    }

    private bool HandleKey(ButtonInput key, InputSettings settings)
    {
        var keys = settings.Keyboard;
        var typing = _keyMode.Mode == KeyMode.Typing
            || string.Equals(key.Source.DeviceId, AppSources.TextInput, StringComparison.Ordinal);

        BindingAction? action = null;
        if (keys.Global is { } global && global.TryGetValue(key.ControlId, out var g))
        {
            action = g;
        }
        else if (!typing && keys.Transport is { } transport && transport.TryGetValue(key.ControlId, out var t))
        {
            action = t;
        }

        if (action is null)
        {
            return false;
        }

        if (key.IsPressed)
        {
            RunBound(action, key.IsRepeat);
        }

        return true; // bound: handled, even on release and even when the command is a no-op right now
    }

    private bool HandleButton(ButtonInput button, InputSettings settings)
    {
        var map = button.Source.Kind switch
        {
            ControllerKind.MouseWheel => settings.Wheel.Buttons,
            ControllerKind.Xbox => settings.Xbox.Buttons,
            ControllerKind.JogShuttle => settings.Shuttle.Buttons,
            _ => null,
        };

        if (map is null || !map.TryGetValue(button.ControlId, out var action))
        {
            return false;
        }

        if (button.IsPressed)
        {
            RunBound(action, button.IsRepeat);
        }

        return true;
    }

    private void RunBound(BindingAction action, bool isRepeat)
    {
        var command = action.For(_engine.Status.Mode);
        if (isRepeat && !ActsOnRepeat(command))
        {
            return;
        }

        Execute(command);
    }

    private static bool ActsOnRepeat(PrompterCommand command) => command is
        PrompterCommand.SpeedUp or PrompterCommand.SpeedDown
        or PrompterCommand.SpeedUpFine or PrompterCommand.SpeedDownFine
        or PrompterCommand.LineNext or PrompterCommand.LinePrevious
        or PrompterCommand.ParagraphNext or PrompterCommand.ParagraphPrevious;

    private bool HandleAxis(AxisInput axis, InputSettings settings)
    {
        var map = axis.Source.Kind switch
        {
            ControllerKind.Xbox => settings.Xbox.Axes,
            ControllerKind.JogShuttle => settings.Shuttle.Axes,
            _ => null,
        };

        if (map is null || !map.TryGetValue(axis.ControlId, out var binding) || binding.Role == AxisRole.None)
        {
            return false;
        }

        var slot = AssignSlot(axis.Source, axis.ControlId);
        if (slot < 0)
        {
            return false;
        }

        _engine.Post(EngineCommand.SetAxisSpeed(slot, AxisCurve.Apply(axis.Value, binding)));
        return true;
    }

    private bool HandleRelative(RelativeInput relative, InputSettings settings)
    {
        RelativeBinding? binding = null;
        var kind = relative.Source.Kind;
        if (kind == ControllerKind.MouseWheel && relative.ControlId == ControlIds.Wheel)
        {
            binding = settings.Wheel.Wheel;
        }
        else if (kind == ControllerKind.JogShuttle && relative.ControlId == ControlIds.JogWheel)
        {
            binding = settings.Shuttle.Jog;
        }

        if (binding is null)
        {
            return false;
        }

        var role = binding.For(_engine.Status.Mode);
        if (role == RelativeRole.None)
        {
            return false;
        }

        // Forward is "towards the user" for the wheel and clockwise for the jog.
        var units = kind == ControllerKind.MouseWheel ? -relative.Amount : relative.Amount;
        if (binding.Invert)
        {
            units = -units;
        }

        if (!double.IsFinite(units) || units == 0.0)
        {
            return true;
        }

        switch (role)
        {
            case RelativeRole.Speed:
                _engine.Post(EngineCommand.NudgeSpeed(units * binding.SpeedPerUnit));
                break;
            case RelativeRole.Lines:
                var lines = Accumulate(relative.Source, relative.ControlId, units * binding.LinesPerUnit);
                if (lines != 0)
                {
                    _engine.Post(EngineCommand.StepLines(lines));
                }

                break;
            case RelativeRole.Pixels:
                _engine.Post(EngineCommand.ScrollPixels(units * binding.PixelsPerUnit));
                break;
        }

        return true;
    }

    // ---- Slots and accumulators -----------------------------------------------------------------------------

    private int AssignSlot(InputSourceId source, string controlId)
    {
        var logFull = false;
        int result;
        lock (_gate)
        {
            result = FindOrAdd(_slots, source, controlId);
            if (result < 0 && !_slotsFullLogged)
            {
                _slotsFullLogged = true;
                logFull = true;
            }
        }

        if (logFull)
        {
            _log.Warn("input", "All axis slots are in use; further axes are ignored.");
        }

        return result;
    }

    private int Accumulate(InputSourceId source, string controlId, double amount)
    {
        lock (_gate)
        {
            var index = FindOrAdd(_accumulators, source, controlId);
            if (index < 0)
            {
                // No room to keep a remainder: step by the whole part only.
                return (int)Math.Clamp(Math.Truncate(amount + (Math.Sign(amount) * 1e-9)), -MaxLinesPerEvent, MaxLinesPerEvent);
            }

            ref var entry = ref _accumulators[index];
            entry.Value += amount;
            var whole = (int)Math.Clamp(Math.Truncate(entry.Value + (Math.Sign(entry.Value) * 1e-9)), -MaxLinesPerEvent, MaxLinesPerEvent);
            entry.Value -= whole;
            if (Math.Abs(entry.Value) < 1e-9)
            {
                entry.Value = 0;
            }

            return whole;
        }
    }

    private static int FindOrAdd(Entry[] table, InputSourceId source, string controlId)
    {
        var free = -1;
        for (var i = 0; i < table.Length; i++)
        {
            ref var e = ref table[i];
            if (!e.Used)
            {
                if (free < 0) { free = i; }
                continue;
            }

            if (e.Source == source && string.Equals(e.Control, controlId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        if (free < 0)
        {
            return -1;
        }

        table[free] = new Entry { Used = true, Source = source, Control = controlId, Value = 0 };
        return free;
    }

    private void ReleaseControllerState(ControllerKind kind, bool postZeros)
    {
        Span<int> freed = stackalloc int[MaxSlots];
        var count = 0;
        lock (_gate)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Used && _slots[i].Source.Kind == kind)
                {
                    freed[count++] = i;
                    _slots[i] = default;
                }

                if (_accumulators[i].Used && _accumulators[i].Source.Kind == kind)
                {
                    _accumulators[i] = default;
                }
            }
        }

        if (postZeros)
        {
            for (var i = 0; i < count; i++)
            {
                _engine.Post(EngineCommand.SetAxisSpeed(freed[i], 0));
            }
        }
    }

    private void ReleaseAllControllerState()
    {
        Span<int> freed = stackalloc int[MaxSlots];
        var count = 0;
        lock (_gate)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Used)
                {
                    freed[count++] = i;
                }

                _slots[i] = default;
                _accumulators[i] = default;
            }
        }

        for (var i = 0; i < count; i++)
        {
            _engine.Post(EngineCommand.SetAxisSpeed(freed[i], 0));
        }
    }

    // ---- Status ---------------------------------------------------------------------------------------------

    public void ReportStatus(DeviceStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (status.Source.Kind == ControllerKind.Keyboard)
        {
            return;
        }

        lock (_gate)
        {
            var index = _statuses.FindIndex(s => s.Source == status.Source);
            if (index >= 0) { _statuses[index] = status; } else { _statuses.Add(status); }
        }

        if (status.Connection == DeviceConnection.Disconnected
            && status.Source.Kind == _settings.Current.Input.SelectedController)
        {
            // Safety first: stop the prompt, then tell the UI.
            _engine.Post(EngineCommand.FailSafeStop());
            ReleaseControllerState(status.Source.Kind, postZeros: false);
        }

        try
        {
            DeviceStatusChanged?.Invoke(this, status);
        }
        catch (Exception ex)
        {
            _log.Error("input", "A DeviceStatusChanged handler threw.", ex);
        }
    }

    private void RaiseObserved(InputEvent input)
    {
        try
        {
            InputObserved?.Invoke(this, input);
        }
        catch (Exception ex)
        {
            _log.Error("input", "An InputObserved handler threw.", ex);
        }
    }

    private void RaiseAppCommand(PrompterCommand command)
    {
        try
        {
            AppCommandRequested?.Invoke(this, command);
        }
        catch (Exception ex)
        {
            _log.Error("input", "An AppCommandRequested handler threw.", ex);
        }
    }

    private struct Entry
    {
        public bool Used;
        public InputSourceId Source;
        public string? Control;
        public double Value;
    }

    private readonly record struct InputKindChange(ControllerKind Previous);
}
