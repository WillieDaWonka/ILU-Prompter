using System.Globalization;
using IluPrompter.Core;

namespace IluPrompter.Rendering;

/// <summary>One GDI monitor (a "source" in display-config terms) from EnumDisplayMonitors + GetMonitorInfo.</summary>
internal sealed record GdiMonitor(string DeviceName, PixelRect BoundsPx, PixelRect WorkAreaPx, double DpiScale, bool IsPrimary);

/// <summary>One active display-config path from QueryDisplayConfig: a source (GDI name) driving a target (a physical panel).</summary>
internal sealed record DisplayPath(string SourceGdiName, string TargetDevicePath, string FriendlyName, double RefreshHz, bool IsInternal);

/// <summary>Pure merge of GDI monitors and display-config paths into <see cref="MonitorInfo"/>s (unit tested).</summary>
internal static class DisplayComposer
{
    public const string BuiltInName = "Built-in display";
    public const double FallbackRefreshHz = 60.0;

    public static IReadOnlyList<MonitorInfo> Compose(IReadOnlyList<GdiMonitor> monitors, IReadOnlyList<DisplayPath> paths)
    {
        var result = new List<MonitorInfo>(monitors.Count);
        foreach (var gdi in monitors)
        {
            var number = ParseDisplayNumber(gdi.DeviceName);
            var own = new List<DisplayPath>(2);
            foreach (var p in paths)
            {
                if (string.Equals(p.SourceGdiName, gdi.DeviceName, StringComparison.OrdinalIgnoreCase)) own.Add(p);
            }

            if (own.Count == 0)
            {
                // No display-config data (remote session, driver quirk): the GDI name is the best stable id we have.
                result.Add(new MonitorInfo(gdi.DeviceName, number, "", gdi.BoundsPx, gdi.WorkAreaPx, gdi.DpiScale,
                    FallbackRefreshHz, gdi.IsPrimary, false));
                continue;
            }

            // Two or more targets on one source = Windows is duplicating (mirroring) this display.
            var duplicated = own.Count > 1;
            foreach (var p in own)
            {
                var name = p.FriendlyName.Length == 0 && p.IsInternal ? BuiltInName : p.FriendlyName;
                result.Add(new MonitorInfo(p.TargetDevicePath, number, name, gdi.BoundsPx, gdi.WorkAreaPx, gdi.DpiScale,
                    p.RefreshHz > 1 ? p.RefreshHz : FallbackRefreshHz, gdi.IsPrimary, duplicated));
            }
        }

        return result;
    }

    /// <summary>"\\.\DISPLAY3" gives 3; anything else gives 0.</summary>
    public static int ParseDisplayNumber(string gdiDeviceName)
    {
        const string marker = "DISPLAY";
        var at = gdiDeviceName.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return 0;
        var tail = gdiDeviceName.AsSpan(at + marker.Length);
        var len = 0;
        while (len < tail.Length && char.IsAsciiDigit(tail[len])) len++;
        return len > 0 && int.TryParse(tail[..len], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
