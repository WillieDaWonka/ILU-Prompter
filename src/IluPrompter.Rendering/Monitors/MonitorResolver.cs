using IluPrompter.Core;

namespace IluPrompter.Rendering;

/// <summary>
/// Pure rules for choosing the output display (ARCHITECTURE §2.7, §4.C). No Windows calls, so it is unit tested.
/// A display is a <i>candidate</i> only when it is neither the primary (operator) display nor part of a mirrored pair.
/// </summary>
internal static class MonitorResolver
{
    /// <summary>
    /// The saved display if it is connected and usable; else the lowest-numbered candidate (list order breaks ties);
    /// else null. Never returns the primary display.
    /// </summary>
    public static MonitorInfo? Resolve(IReadOnlyList<MonitorInfo> monitors, string? savedId)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        MonitorInfo? best = null;
        for (var i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            if (!IsCandidate(m)) continue;
            if (savedId is not null && string.Equals(m.StableId, savedId, StringComparison.Ordinal)) return m;
            if (best is null || m.DisplayNumber < best.DisplayNumber) best = m;
        }

        return best;
    }

    /// <summary>The display with exactly this StableId if it is usable as an output; else null (no fallback).
    /// Used while the output is open, so that it never jumps to another display.</summary>
    public static MonitorInfo? Find(IReadOnlyList<MonitorInfo> monitors, string stableId)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        for (var i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            if (IsCandidate(m) && string.Equals(m.StableId, stableId, StringComparison.Ordinal)) return m;
        }

        return null;
    }

    /// <summary>Why there is (or is not) an output display: Ready, Duplicated (Windows is mirroring), or NoSecondDisplay.</summary>
    public static OutputDisplayStatus Classify(IReadOnlyList<MonitorInfo> monitors, string? savedId)
    {
        if (Resolve(monitors, savedId) is not null) return OutputDisplayStatus.Ready;
        return AnyDuplicated(monitors) ? OutputDisplayStatus.Duplicated : OutputDisplayStatus.NoSecondDisplay;
    }

    /// <summary>The status for an output that is open on <paramref name="stableId"/> and cannot be found:
    /// Duplicated while Windows mirrors, otherwise Missing. Ready when the display is back.</summary>
    public static OutputDisplayStatus ClassifyOpen(IReadOnlyList<MonitorInfo> monitors, string stableId)
    {
        if (Find(monitors, stableId) is not null) return OutputDisplayStatus.Ready;
        return AnyDuplicated(monitors) ? OutputDisplayStatus.Duplicated : OutputDisplayStatus.Missing;
    }

    public static OutputGeometry ToGeometry(MonitorInfo monitor) =>
        new(Math.Max(1, monitor.BoundsPx.Width), Math.Max(1, monitor.BoundsPx.Height),
            monitor.DpiScale > 0 ? monitor.DpiScale : 1.0,
            monitor.RefreshHz > 0 ? monitor.RefreshHz : 60.0);

    private static bool IsCandidate(MonitorInfo m) => !m.IsPrimary && !m.IsDuplicated;

    private static bool AnyDuplicated(IReadOnlyList<MonitorInfo> monitors)
    {
        for (var i = 0; i < monitors.Count; i++)
        {
            if (monitors[i].IsDuplicated) return true;
        }

        return false;
    }
}
