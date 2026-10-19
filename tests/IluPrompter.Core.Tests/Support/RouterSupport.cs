using System.Diagnostics;

namespace IluPrompter.Core.Tests;

/// <summary>Records everything posted; Status is whatever the test sets.</summary>
internal sealed class FakeEngine : IScrollEngine
{
    private readonly object _gate = new();
    private readonly List<EngineCommand> _posted = [];
    private EngineStatus _status = new();

    public IReadOnlyList<EngineCommand> Posted
    {
        get
        {
            lock (_gate)
            {
                return _posted.ToArray();
            }
        }
    }

    public EngineStatus Status
    {
        get => Volatile.Read(ref _status);
        set => Volatile.Write(ref _status, value);
    }

    public LayoutGeometry? Geometry => null;

    public ViewportMetrics Viewport => default;

    public event EventHandler<EngineStatus>? StatusChanged
    {
        add { }
        remove { }
    }

    public void Post(EngineCommand command)
    {
        lock (_gate)
        {
            _posted.Add(command);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _posted.Clear();
        }
    }

    public void Tick(long timestamp)
    {
    }

    public bool TickIfStale(long timestamp, double staleAfterSeconds = 0.05) => false;

    public void SetLayout(LayoutGeometry geometry, ViewportMetrics viewport)
    {
    }

    public double ReadPosition() => 0;
}

internal sealed class FakeOutput : IOutputSurface
{
    private OutputState _state = OutputState.Initial;

    public OutputState State => _state;

    public int SetBlankCalls { get; private set; }

    public event EventHandler<OutputState>? StateChanged
    {
        add { }
        remove { }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task ShowAsync() => Task.CompletedTask;

    public Task HideAsync() => Task.CompletedTask;

    public void SetBlank(bool blank)
    {
        SetBlankCalls++;
        _state = _state with { IsBlank = blank };
    }

    public Task ShutdownAsync() => Task.CompletedTask;
}

internal sealed class FakeProvider : IScrollInputProvider
{
    public FakeProvider(ControllerKind kind) => Kind = kind;

    public ControllerKind Kind { get; }

    public bool IsRunning { get; private set; }

    public List<(IInputSink Sink, string? DeviceId)> StartCalls { get; } = [];

    public int StopCalls { get; private set; }

    public Exception? ThrowOnStart { get; set; }

    public IReadOnlyList<InputDeviceInfo> EnumerateDevices() => [];

    public void Start(IInputSink sink, string? deviceId)
    {
        StartCalls.Add((sink, deviceId));
        if (ThrowOnStart is not null)
        {
            throw ThrowOnStart;
        }

        IsRunning = true;
    }

    public void Stop()
    {
        StopCalls++;
        IsRunning = false;
    }

    public void Dispose() => Stop();
}

internal sealed class RouterHarness
{
    public RouterHarness(PrompterSettings? initial = null, bool start = true)
    {
        Settings = new FakeSettings(initial);
        Engine = new FakeEngine();
        KeyMode = new KeyModeState();
        Output = new FakeOutput();
        Log = new TestLog();
        Wheel = new FakeProvider(ControllerKind.MouseWheel);
        Xbox = new FakeProvider(ControllerKind.Xbox);
        Shuttle = new FakeProvider(ControllerKind.JogShuttle);
        Router = new InputRouter(Engine, Settings, KeyMode, Output, [Wheel, Xbox, Shuttle], Log);
        if (start)
        {
            Router.Start();
        }
    }

    public FakeSettings Settings { get; }

    public FakeEngine Engine { get; }

    public KeyModeState KeyMode { get; }

    public FakeOutput Output { get; }

    public TestLog Log { get; }

    public FakeProvider Wheel { get; }

    public FakeProvider Xbox { get; }

    public FakeProvider Shuttle { get; }

    public InputRouter Router { get; }

    public IReadOnlyList<EngineCommand> Posted => Engine.Posted;

    public void Configure(Func<InputSettings, InputSettings> change) =>
        Settings.Update(s => s with { Input = change(s.Input) });

    public void Select(ControllerKind kind) => Configure(i => i with { SelectedController = kind });

    public void SetMode(ScrollMode mode) => Engine.Status = new EngineStatus { Mode = mode };

    public static InputSourceId App => new(ControllerKind.Keyboard, AppSources.App);

    public bool Key(string chord, bool pressed = true, bool repeat = false, string device = AppSources.App) =>
        Router.Submit(new ButtonInput(new InputSourceId(ControllerKind.Keyboard, device), chord, Stopwatch.GetTimestamp(), pressed, repeat));

    public bool Notch(double amount, string device = AppSources.App) =>
        Router.Submit(new RelativeInput(new InputSourceId(ControllerKind.MouseWheel, device), ControlIds.Wheel, Stopwatch.GetTimestamp(), amount));

    public bool Button(ControllerKind kind, string control, string device, bool pressed = true, bool repeat = false) =>
        Router.Submit(new ButtonInput(new InputSourceId(kind, device), control, Stopwatch.GetTimestamp(), pressed, repeat));

    public bool Axis(ControllerKind kind, string control, string device, double value) =>
        Router.Submit(new AxisInput(new InputSourceId(kind, device), control, Stopwatch.GetTimestamp(), value));

    public bool Jog(double amount, string device = "hid:shuttle") =>
        Router.Submit(new RelativeInput(new InputSourceId(ControllerKind.JogShuttle, device), ControlIds.JogWheel, Stopwatch.GetTimestamp(), amount));

    public static InputSourceId Pad => new(ControllerKind.Xbox, "xinput:0");
}
