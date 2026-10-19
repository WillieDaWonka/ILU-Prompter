using IluPrompter.Core;
using Microsoft.Win32;

namespace IluPrompter.Rendering;

/// <summary>
/// Enumerates connected displays (EnumDisplayMonitors + GetDpiForMonitor + QueryDisplayConfig) and raises
/// <see cref="MonitorsChanged"/> 500 ms after the last display change. Thread-safe.
/// </summary>
public sealed class MonitorService : IMonitorService, IDisposable
{
    private readonly IAppLog _log;
    private readonly Func<IReadOnlyList<MonitorInfo>> _enumerate;
    private readonly TimeSpan _debounce;
    private readonly object _gate = new();
    private readonly Timer _timer;
    private IReadOnlyList<MonitorInfo>? _cache;
    private bool _disposed;

    public MonitorService(IAppLog log)
        : this(log, null, TimeSpan.FromMilliseconds(500), listenToSystemEvents: true)
    {
    }

    /// <summary>Test seam: a fake enumerator, a short debounce, and no process-wide SystemEvents subscription.</summary>
    internal MonitorService(IAppLog log, Func<IReadOnlyList<MonitorInfo>>? enumerate, TimeSpan debounce, bool listenToSystemEvents)
    {
        _log = log;
        _enumerate = enumerate ?? (() => DisplayEnumerator.Enumerate(log));
        _debounce = debounce;
        _timer = new Timer(OnDebounceElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        if (listenToSystemEvents) SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public event EventHandler? MonitorsChanged;

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        lock (_gate)
        {
            if (_cache is not null) return _cache;
        }

        IReadOnlyList<MonitorInfo> fresh;
        try
        {
            fresh = _enumerate();
        }
        catch (Exception ex)
        {
            _log.Error("monitors", "Display enumeration failed", ex);
            return Array.Empty<MonitorInfo>();
        }

        lock (_gate)
        {
            _cache ??= fresh;
            return _cache;
        }
    }

    public MonitorInfo? ResolveOutputMonitor(string? savedStableId) =>
        MonitorResolver.Resolve(GetMonitors(), savedStableId);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _timer.Dispose();
    }

    /// <summary>Called for every display-settings event (many arrive per real change); restarts the debounce.</summary>
    internal void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _cache = null;
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _cache = null;
        }

        try
        {
            var monitors = GetMonitors();
            _log.Info("monitors", "Displays changed: " + string.Join("; ", monitors.Select(m =>
                $"{m.Label} {m.BoundsPx.Width}x{m.BoundsPx.Height}@{m.BoundsPx.X},{m.BoundsPx.Y} {m.DpiScale:0.##}x {m.RefreshHz:0.#}Hz" +
                (m.IsPrimary ? " primary" : "") + (m.IsDuplicated ? " duplicated" : ""))));
            MonitorsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log.Error("monitors", "MonitorsChanged handler failed", ex);
        }
    }
}
