using IluPrompter.App.Services;
using IluPrompter.Core;

namespace IluPrompter.App.Tests;

/// <summary>Runs UI-thread work immediately (view models are tested without a Dispatcher).</summary>
public sealed class ImmediateUi : IUiThread
{
    public bool CheckAccess() => true;

    public void Post(Action action) => action();
}

/// <summary>A millisecond clock the tests move by hand (starts well above zero: the input table treats 0 as "never").</summary>
public sealed class TestClock
{
    public long Ms { get; set; } = 10_000;

    public long Now() => Ms;

    public void Advance(long ms) => Ms += ms;
}

public sealed class FakeSettings : ISettingsService
{
    private PrompterSettings _current = PrompterSettings.Default;

    public PrompterSettings Current => _current;

    public string SettingsPath => "fake";

    public int UpdateCount { get; private set; }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public void Load()
    {
    }

    public Task SaveAsync() => Task.CompletedTask;

    public void Update(Func<PrompterSettings, PrompterSettings> change)
    {
        var old = _current;
        var updated = change(old);
        if (ReferenceEquals(old, updated)) return;
        _current = updated;
        UpdateCount++;
        Changed?.Invoke(this, new SettingsChangedEventArgs(old, updated));
    }
}

public sealed class FakeEngine : IScrollEngine
{
    public List<EngineCommand> Posted { get; } = [];

    public EngineStatus Status { get; set; } = new();

    public LayoutGeometry? Geometry { get; set; }

    public ViewportMetrics Viewport => new(1600, 952, 314);

    public event EventHandler<EngineStatus>? StatusChanged;

    public void Post(EngineCommand command) => Posted.Add(command);

    public void Tick(long timestamp)
    {
    }

    public bool TickIfStale(long timestamp, double staleAfterSeconds = 0.05) => false;

    public void SetLayout(LayoutGeometry geometry, ViewportMetrics viewport)
    {
    }

    public double ReadPosition() => 0;

    public void RaiseStatus() => StatusChanged?.Invoke(this, Status);
}

public sealed class FakeOutput : IOutputSurface
{
    public OutputState State { get; private set; } = OutputState.Initial;

    public int BlankCalls { get; private set; }

    public event EventHandler<OutputState>? StateChanged;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task ShowAsync()
    {
        Set(State with { IsOpen = true, Display = OutputDisplayStatus.Ready });
        return Task.CompletedTask;
    }

    public Task HideAsync()
    {
        Set(State with { IsOpen = false });
        return Task.CompletedTask;
    }

    public void SetBlank(bool blank)
    {
        BlankCalls++;
        Set(State with { IsBlank = blank });
    }

    public Task ShutdownAsync() => Task.CompletedTask;

    public void Set(OutputState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }
}

public sealed class FakeRouter : IInputRouter
{
    public List<PrompterCommand> Executed { get; } = [];

    public IReadOnlyList<DeviceStatus> DeviceStatuses { get; set; } = Array.Empty<DeviceStatus>();

    public event EventHandler<DeviceStatus>? DeviceStatusChanged;

    public event EventHandler<InputEvent>? InputObserved;

    public event EventHandler<PrompterCommand>? AppCommandRequested;

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public bool Execute(PrompterCommand command)
    {
        Executed.Add(command);
        return true;
    }

    public void RaiseStatus(DeviceStatus status) => DeviceStatusChanged?.Invoke(this, status);

    public void RaiseInput(InputEvent input) => InputObserved?.Invoke(this, input);

    public void RaiseAppCommand(PrompterCommand command) => AppCommandRequested?.Invoke(this, command);
}

public sealed class FakeMonitors : IMonitorService
{
    public List<MonitorInfo> Monitors { get; } = [];

    public event EventHandler? MonitorsChanged;

    public IReadOnlyList<MonitorInfo> GetMonitors() => Monitors;

    public MonitorInfo? ResolveOutputMonitor(string? savedStableId) =>
        Monitors.FirstOrDefault(m => !m.IsPrimary && !m.IsDuplicated);

    public void RaiseChanged() => MonitorsChanged?.Invoke(this, EventArgs.Empty);

    public static MonitorInfo Display2(bool duplicated = false) =>
        new("dev2", 2, "LG 55UH5", new PixelRect(1920, 0, 1920, 1080), new PixelRect(1920, 0, 1920, 1080), 1.0, 60, false, duplicated);
}

public sealed class FakeDialogs : IDialogService
{
    public DialogChoice Answer { get; set; } = DialogChoice.Primary;

    public List<DialogRequest> Asked { get; } = [];

    public List<string> Informed { get; } = [];

    public DialogChoice Ask(DialogRequest request)
    {
        Asked.Add(request);
        return Answer;
    }

    public void Inform(string title, string message, string buttonLabel = "Close") => Informed.Add(title + ": " + message);

    public string? Prompt(string title, string message, string initialText, string primaryLabel) => initialText;
}

public sealed class NullLog : IAppLog
{
    public bool DiagnosticsEnabled => false;

    public string LogDirectory => "";

    public void Info(string area, string message)
    {
    }

    public void Warn(string area, string message)
    {
    }

    public void Error(string area, string message, Exception? exception = null)
    {
    }

    public void Trace(string area, string message)
    {
    }
}
