namespace IluPrompter.Core;

/// <summary>
/// STUB (stream A replaces the body; keep the type name and constructor signature).
/// Maps keyboard chords and Execute() for the basic transport only; ignores controllers.
/// </summary>
public sealed class InputRouter : IInputRouter, IInputSink
{
    private readonly IScrollEngine _engine;
    private readonly ISettingsService _settings;
    private readonly IKeyModeState _keyMode;
    private readonly IOutputSurface _output;
    private readonly IEnumerable<IScrollInputProvider> _providers;
    private readonly IAppLog _log;

    public InputRouter(
        IScrollEngine engine,
        ISettingsService settings,
        IKeyModeState keyMode,
        IOutputSurface output,
        IEnumerable<IScrollInputProvider> providers,
        IAppLog log)
    {
        _engine = engine;
        _settings = settings;
        _keyMode = keyMode;
        _output = output;
        _providers = providers;
        _log = log;
    }

    public IReadOnlyList<DeviceStatus> DeviceStatuses => Array.Empty<DeviceStatus>();

    public event EventHandler<DeviceStatus>? DeviceStatusChanged;
    public event EventHandler<InputEvent>? InputObserved;
    public event EventHandler<PrompterCommand>? AppCommandRequested;

    public void Start() => _log.Info("input", $"Router stub started; {_providers.Count()} providers registered.");

    public void Stop()
    {
    }

    public bool Execute(PrompterCommand command)
    {
        switch (command)
        {
            case PrompterCommand.PlayPause: _engine.Post(EngineCommand.TogglePlayPause()); return true;
            case PrompterCommand.Play: _engine.Post(EngineCommand.Play()); return true;
            case PrompterCommand.Pause: _engine.Post(EngineCommand.Pause()); return true;
            case PrompterCommand.SpeedUp: _engine.Post(EngineCommand.NudgeSpeed(_settings.Current.Scroll.SpeedStep)); return true;
            case PrompterCommand.SpeedDown: _engine.Post(EngineCommand.NudgeSpeed(-_settings.Current.Scroll.SpeedStep)); return true;
            case PrompterCommand.JumpToStart: _engine.Post(EngineCommand.JumpToStart()); return true;
            case PrompterCommand.ToggleBlank: _output.SetBlank(!_output.State.IsBlank); return true;
            case PrompterCommand.ToggleOutput: AppCommandRequested?.Invoke(this, command); return true;
            default: return false;
        }
    }

    public bool Submit(InputEvent input)
    {
        InputObserved?.Invoke(this, input);
        if (input is not ButtonInput { IsPressed: true } b || input.Source.Kind != ControllerKind.Keyboard) return false;
        var keys = _settings.Current.Input.Keyboard;
        var mode = _engine.Status.Mode;
        if (keys.Global.TryGetValue(b.ControlId, out var g)) return Execute(g.For(mode));
        if (_keyMode.Mode == KeyMode.Transport && keys.Transport.TryGetValue(b.ControlId, out var t)) return Execute(t.For(mode));
        return false;
    }

    public void ReportStatus(DeviceStatus status) => DeviceStatusChanged?.Invoke(this, status);
}
