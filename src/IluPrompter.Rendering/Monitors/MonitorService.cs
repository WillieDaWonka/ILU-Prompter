using IluPrompter.Core;

namespace IluPrompter.Rendering;

/// <summary>STUB (stream C replaces the body; keep the type name and constructor signature). Reports no displays.</summary>
public sealed class MonitorService : IMonitorService
{
    public MonitorService(IAppLog log)
    {
    }

    public event EventHandler? MonitorsChanged;

    public IReadOnlyList<MonitorInfo> GetMonitors() => Array.Empty<MonitorInfo>();

    public MonitorInfo? ResolveOutputMonitor(string? savedStableId) => null;

    internal void RaiseChanged() => MonitorsChanged?.Invoke(this, EventArgs.Empty);
}
