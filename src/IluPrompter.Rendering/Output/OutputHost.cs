using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering;

/// <summary>Switches that exist for tests and development; production uses the defaults.</summary>
/// <param name="DevOutputOnPrimary">Open as a 960 x 540 ordinary window on the primary display
/// (<c>--dev-output-on-primary</c>), for single-monitor development. Never the default.</param>
/// <param name="Headless">Run the whole state machine but never create or show a native window (unit tests).</param>
internal sealed record OutputHostOptions(bool DevOutputOnPrimary = false, bool Headless = false)
{
    public const string DevFlag = "--dev-output-on-primary";

    public static OutputHostOptions FromCommandLine() =>
        new(Environment.GetCommandLineArgs().Contains(DevFlag, StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// IOutputSurface implementation: owns the output STA thread, its Dispatcher and the OutputWindow (ARCHITECTURE §3, §4.C).
/// All members are callable from any thread and marshal internally. The window is only ever shown on the resolved
/// output display, never on the primary one (except with the dev flag), and it stays hidden, not moved, when that
/// display disappears.
/// </summary>
public sealed class OutputHost : IOutputSurface, IFramePulse
{
    private static readonly long HeartbeatTicks = Stopwatch.Frequency / 4;
    private const double DevWidthDip = 960;
    private const double DevHeightDip = 540;

    private readonly IScrollEngine _engine;
    private readonly IPromptDocumentService _documents;
    private readonly ISettingsService _settings;
    private readonly IMonitorService _monitors;
    private readonly IOutputDocumentFactory _documentFactory;
    private readonly ILayoutGeometryBuilder _geometryBuilder;
    private readonly IAppLog _log;
    private readonly OutputHostOptions _options;
    private readonly object _stateGate = new();
    private readonly object _threadGate = new();

    private OutputState _state;
    private Thread? _thread;
    private Dispatcher? _dispatcher;
    private Task? _initTask;

    // Output-thread state.
    private OutputWindow? _window;
    private PrompterSurface? _surface;
    private FrameClock _clock = new();
    private FrameStats? _stats;
    private DisplaySleepGuard? _guard;
    private string? _chosenId;
    private bool _visible;
    private bool _rendering;
    private double _lastPulsePosition = double.NaN;
    private long _lastPulseTimestamp;
    private readonly object _pulseGate = new();
    private EventHandler? _framePulse;

    public OutputHost(
        IScrollEngine engine,
        IPromptDocumentService documents,
        ISettingsService settings,
        IMonitorService monitors,
        IOutputDocumentFactory documentFactory,
        ILayoutGeometryBuilder geometryBuilder,
        IAppLog log)
        : this(engine, documents, settings, monitors, documentFactory, geometryBuilder, log, OutputHostOptions.FromCommandLine())
    {
    }

    internal OutputHost(
        IScrollEngine engine,
        IPromptDocumentService documents,
        ISettingsService settings,
        IMonitorService monitors,
        IOutputDocumentFactory documentFactory,
        ILayoutGeometryBuilder geometryBuilder,
        IAppLog log,
        OutputHostOptions options)
    {
        _engine = engine;
        _documents = documents;
        _settings = settings;
        _monitors = monitors;
        _documentFactory = documentFactory;
        _geometryBuilder = geometryBuilder;
        _log = log;
        _options = options;
        _state = OutputState.Initial with { Geometry = settings.Current.Output.LastGeometry };
    }

    public OutputState State => Volatile.Read(ref _state);

    public event EventHandler<OutputState>? StateChanged;

    /// <summary>Raised on the output thread after a frame in which the position changed, and every 250 ms while rendering.</summary>
    event EventHandler? IFramePulse.FramePulse
    {
        add { lock (_pulseGate) { _framePulse += value; } }
        remove { lock (_pulseGate) { _framePulse -= value; } }
    }

    /// <summary>The surface on the output thread (tests only; touch it from that thread).</summary>
    internal PrompterSurface? Surface => _surface;

    /// <summary>The window on the output thread (tests only).</summary>
    internal OutputWindow? Window => _window;

    internal Dispatcher? OutputDispatcher => _dispatcher;

    // ------------------------------------------------------------------ lifecycle

    public Task InitializeAsync()
    {
        lock (_threadGate)
        {
            if (_initTask is not null) return _initTask;
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() => ThreadMain(ready)) { Name = "ILU Output", IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            _thread = thread;
            _initTask = ready.Task;
            thread.Start();
            return _initTask;
        }
    }

    public async Task ShowAsync()
    {
        await InitializeAsync().ConfigureAwait(false);
        await InvokeOnOutputThreadAsync(ShowCore).ConfigureAwait(false);
    }

    public async Task HideAsync()
    {
        if (_dispatcher is null) return;
        await InvokeOnOutputThreadAsync(HideCore).ConfigureAwait(false);
    }

    public void SetBlank(bool blank)
    {
        UpdateState(s => s with { IsBlank = blank });
        _dispatcher?.BeginInvoke(DispatcherPriority.Send, () => _window?.SetBlank(State.IsBlank));
    }

    public async Task ShutdownAsync()
    {
        Dispatcher? dispatcher;
        Thread? thread;
        lock (_threadGate)
        {
            dispatcher = _dispatcher;
            thread = _thread;
            _dispatcher = null;
            _thread = null;
            _initTask = null;
        }

        if (dispatcher is null || thread is null) return;

        try
        {
            await dispatcher.InvokeAsync(() => ShutdownCore(dispatcher)).Task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error("output", "Output shutdown failed", ex);
        }

        dispatcher.InvokeShutdown();
        var joined = await Task.Run(() => thread.Join(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        if (!joined) _log.Warn("output", "The output thread did not stop within 2 s; continuing.");
    }

    private void ThreadMain(TaskCompletionSource ready)
    {
        try
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.UnhandledException += (_, e) =>
            {
                _log.Error("output", "Unhandled exception on the output thread (frame skipped)", e.Exception);
                e.Handled = true;
            };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            Volatile.Write(ref _dispatcher, dispatcher);

            CreateWindow();
            _monitors.MonitorsChanged += OnMonitorsChanged;
            _settings.Changed += OnSettingsChanged;
            RefreshIdleState();
            _log.Info("output", "Output thread ready. Displays: " + DescribeMonitors(_monitors.GetMonitors()));
            ready.TrySetResult();
            Dispatcher.Run();
        }
        catch (Exception ex)
        {
            _log.Error("output", "Output thread failed to start", ex);
            ready.TrySetException(ex);
        }
    }

    private void CreateWindow()
    {
        var services = new SurfaceServices(_engine, _documents, _settings, _documentFactory, _geometryBuilder, _log, () => State.Geometry);
        _surface = new PrompterSurface("output", services, snapToPixels: true);
        _guard = new DisplaySleepGuard();
        if (_options.Headless) return;
        _window = new OutputWindow(_surface, _log, kiosk: !_options.DevOutputOnPrimary);
        _window.Replaced += (_, _) => OnWindowReplaced();
    }

    private void ShutdownCore(Dispatcher dispatcher)
    {
        _monitors.MonitorsChanged -= OnMonitorsChanged;
        _settings.Changed -= OnSettingsChanged;
        StopRendering();
        _guard?.Dispose();
        _surface?.Stop();
        _visible = false;
        _chosenId = null;
        try
        {
            _window?.Close();
        }
        catch (Exception ex)
        {
            _log.Warn("output", "Closing the output window failed: " + ex.Message);
        }

        UpdateState(s => s with { IsOpen = false });
    }

    // ------------------------------------------------------------------ show / hide (output thread)

    private void ShowCore()
    {
        OutputGeometry geometry;
        MonitorInfo? monitor;
        if (_options.DevOutputOnPrimary)
        {
            monitor = _monitors.GetMonitors().FirstOrDefault(m => m.IsPrimary);
            geometry = DevGeometry(monitor);
            _chosenId = null;
            _log.Warn("output", "--dev-output-on-primary: opening the output as a 960 x 540 window on the primary display (development only).");
        }
        else
        {
            var monitors = _monitors.GetMonitors();
            monitor = MonitorResolver.Resolve(monitors, _settings.Current.Output.MonitorStableId);
            if (monitor is null)
            {
                var why = MonitorResolver.Classify(monitors, _settings.Current.Output.MonitorStableId);
                UpdateState(s => s with { IsOpen = false, Display = why, Monitor = null });
                _log.Info("output", "Show refused: " + why);
                return;
            }

            geometry = MonitorResolver.ToGeometry(monitor);
            _chosenId = monitor.StableId;
            PersistOutputSettings(monitor.StableId, geometry);
        }

        UpdateState(s => s with { IsOpen = true, Display = OutputDisplayStatus.Ready, Monitor = monitor, Geometry = geometry });
        PlaceAndShow(monitor, geometry);
    }

    private void PlaceAndShow(MonitorInfo? monitor, OutputGeometry geometry)
    {
        _surface!.Start();
        _surface.Rebuild();                      // laid out and positioned before the first visible frame

        if (_window is not null)
        {
            _window.SetOutputGeometry(geometry);
            _window.SetBlank(State.IsBlank);
            if (_options.DevOutputOnPrimary)
            {
                if (!_window.IsVisible) _window.Show();
            }
            else if (monitor is not null)
            {
                var bounds = monitor.BoundsPx;
                // Place while hidden. Moving to a display with another DPI makes WPF apply Windows' suggested rectangle,
                // so look and place again before the window becomes visible (it never shows at the wrong place).
                _window.PlaceOn(bounds);
                if (_window.GetActualBounds() != bounds) _window.PlaceOn(bounds);
                if (!_window.IsVisible) _window.Show();
                var actual = _window.GetActualBounds();
                if (actual != bounds)
                {
                    _log.Warn("output", $"Window landed on {Describe(actual)} instead of {Describe(bounds)}; placing it again.");
                    _window.PlaceOn(bounds);
                    actual = _window.GetActualBounds();
                }

                _log.Info("output", $"Shown on {monitor.Label} at {Describe(actual)} (display bounds {Describe(bounds)}), {geometry.WidthDip:0}x{geometry.HeightDip:0} DIP at {geometry.DpiScale:0.##}x, {geometry.RefreshHz:0.#} Hz.");
            }
        }

        _visible = true;
        StartRendering(geometry);
    }

    private void HideCore()
    {
        StopRendering();
        _guard?.Update(false);
        _surface?.Stop();
        if (_window is { IsVisible: true }) _window.Hide();
        _visible = false;
        _chosenId = null;
        UpdateState(s => s with { IsOpen = false });
        RefreshIdleState();
    }

    /// <summary>Output closed: report whether an output display is available, and keep the layout size in step with it.</summary>
    private void RefreshIdleState()
    {
        if (State.IsOpen) return;
        if (_options.DevOutputOnPrimary)
        {
            var primary = _monitors.GetMonitors().FirstOrDefault(m => m.IsPrimary);
            var dev = DevGeometry(primary);
            UpdateState(s => s with { Display = OutputDisplayStatus.Ready, Monitor = primary, Geometry = dev });
            return;
        }

        var monitors = _monitors.GetMonitors();
        var saved = _settings.Current.Output.MonitorStableId;
        var monitor = MonitorResolver.Resolve(monitors, saved);
        var status = monitor is not null ? OutputDisplayStatus.Ready : MonitorResolver.Classify(monitors, saved);
        UpdateState(s => s with
        {
            Display = status,
            Monitor = monitor,
            Geometry = monitor is null ? s.Geometry : MonitorResolver.ToGeometry(monitor),
        });
    }

    // ------------------------------------------------------------------ display changes (output thread)

    private void OnMonitorsChanged(object? sender, EventArgs e) =>
        _dispatcher?.BeginInvoke(DispatcherPriority.Normal, HandleMonitorsChanged);

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (!string.Equals(e.Old.Output.MonitorStableId, e.New.Output.MonitorStableId, StringComparison.Ordinal))
        {
            _dispatcher?.BeginInvoke(DispatcherPriority.Normal, HandleOutputChoiceChanged);
        }
    }

    private void HandleMonitorsChanged()
    {
        try
        {
            var monitors = _monitors.GetMonitors();
            _log.Info("output", "Displays now: " + DescribeMonitors(monitors));
            if (!State.IsOpen || _options.DevOutputOnPrimary)
            {
                RefreshIdleState();
                return;
            }

            var chosen = _chosenId;
            if (chosen is null) return;

            var monitor = MonitorResolver.Find(monitors, chosen);
            if (monitor is null)
            {
                // The display vanished (or Windows started mirroring). Hide, stay "open", never move to another display.
                var why = MonitorResolver.ClassifyOpen(monitors, chosen);
                StopRendering();
                _guard?.Update(false);
                if (_window is { IsVisible: true }) _window.Hide();
                _visible = false;
                UpdateState(s => s with { IsOpen = true, Display = why, Monitor = null });
                _log.Warn("output", "Output display is gone (" + why + "); window hidden, waiting for it to return.");
                return;
            }

            var geometry = MonitorResolver.ToGeometry(monitor);
            var changed = State.Geometry != geometry || !_visible;
            UpdateState(s => s with { IsOpen = true, Display = OutputDisplayStatus.Ready, Monitor = monitor, Geometry = geometry });
            if (changed)
            {
                PersistOutputSettings(monitor.StableId, geometry);
                PlaceAndShow(monitor, geometry);
            }
        }
        catch (Exception ex)
        {
            _log.Error("output", "Handling a display change failed", ex);
        }
    }

    private void HandleOutputChoiceChanged()
    {
        try
        {
            if (!State.IsOpen || _options.DevOutputOnPrimary)
            {
                RefreshIdleState();
                return;
            }

            var saved = _settings.Current.Output.MonitorStableId;
            if (saved is null || string.Equals(saved, _chosenId, StringComparison.Ordinal)) return;
            var monitor = MonitorResolver.Find(_monitors.GetMonitors(), saved);
            if (monitor is null) return;
            _chosenId = saved;
            var geometry = MonitorResolver.ToGeometry(monitor);
            UpdateState(s => s with { IsOpen = true, Display = OutputDisplayStatus.Ready, Monitor = monitor, Geometry = geometry });
            PlaceAndShow(monitor, geometry);
        }
        catch (Exception ex)
        {
            _log.Error("output", "Switching the output display failed", ex);
        }
    }

    private void OnWindowReplaced() => _surface?.RequestRebuild();

    // ------------------------------------------------------------------ per frame (output thread)

    private void StartRendering(OutputGeometry geometry)
    {
        _clock = new FrameClock();
        _stats = _log.DiagnosticsEnabled ? new FrameStats("output", _log, geometry.RefreshHz, TimeSpan.FromSeconds(10)) : null;
        if (_rendering) return;
        _rendering = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopRendering()
    {
        if (!_rendering) return;
        _rendering = false;
        CompositionTarget.Rendering -= OnRendering;
        _stats?.Reset();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        // Allocation-free: a cast, a clock lookup, three lock-free reads and one transform update.
        if (!_clock.TryMap(((RenderingEventArgs)e).RenderingTime, Stopwatch.GetTimestamp(), out var timestamp)) return;
        _engine.Tick(timestamp);
        var position = _engine.ReadPosition();
        _surface!.SetPosition(position);
        _stats?.Record(timestamp);
        if (position != _lastPulsePosition || timestamp - _lastPulseTimestamp > HeartbeatTicks)
        {
            _lastPulsePosition = position;
            _lastPulseTimestamp = timestamp;
            _framePulse?.Invoke(this, EventArgs.Empty);
        }
        _guard!.Update(_engine.Status.Transport == TransportState.Playing);
    }

    // ------------------------------------------------------------------ state

    private void UpdateState(Func<OutputState, OutputState> change)
    {
        OutputState next;
        OutputState previous;
        lock (_stateGate)
        {
            previous = _state;
            next = change(previous);
            if (next == previous) return;
            _state = next;
        }

        if (next.Geometry != previous.Geometry) _surface?.RequestRebuild();

        var dispatcher = _dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            StateChanged?.Invoke(this, next);
        }
        else
        {
            dispatcher.BeginInvoke(DispatcherPriority.Normal, () => StateChanged?.Invoke(this, next));
        }
    }

    private Task InvokeOnOutputThreadAsync(Action action)
    {
        var dispatcher = _dispatcher ?? throw new InvalidOperationException("The output thread is not running.");
        return dispatcher.InvokeAsync(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _log.Error("output", "Output operation failed", ex);
                throw;
            }
        }).Task;
    }

    private void PersistOutputSettings(string stableId, OutputGeometry g) =>
        _settings.Update(s =>
        {
            var o = s.Output;
            var n = o with
            {
                MonitorStableId = o.MonitorStableId ?? stableId,
                LastWidthPx = g.WidthPx,
                LastHeightPx = g.HeightPx,
                LastDpiScale = g.DpiScale,
                LastRefreshHz = g.RefreshHz,
            };
            return n == o ? s : s with { Output = n };
        });

    private static OutputGeometry DevGeometry(MonitorInfo? primary)
    {
        var dpi = primary is { DpiScale: > 0 } ? primary.DpiScale : 1.0;
        return new OutputGeometry((int)Math.Round(DevWidthDip * dpi), (int)Math.Round(DevHeightDip * dpi), dpi, primary?.RefreshHz ?? 60.0);
    }

    private static string Describe(PixelRect? r) => r is { } v ? $"{v.X},{v.Y} {v.Width}x{v.Height}" : "unknown";

    private static string DescribeMonitors(IReadOnlyList<MonitorInfo> monitors) =>
        monitors.Count == 0
            ? "none"
            : string.Join("; ", monitors.Select(m =>
                $"{m.Label} {m.BoundsPx.Width}x{m.BoundsPx.Height} at {m.BoundsPx.X},{m.BoundsPx.Y}, {m.DpiScale:0.##}x, {m.RefreshHz:0.#} Hz" +
                (m.IsPrimary ? ", primary" : "") + (m.IsDuplicated ? ", duplicated" : "")));
}
