using System.Runtime.InteropServices;
using IluPrompter.Core;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace IluPrompter.Rendering;

/// <summary>Reads the connected displays from Windows (EnumDisplayMonitors, GetDpiForMonitor, QueryDisplayConfig).
/// Read-only and local: it asks the graphics driver stack what is plugged into this machine.</summary>
internal static unsafe class DisplayEnumerator
{
    public static IReadOnlyList<MonitorInfo> Enumerate(IAppLog? log = null)
    {
        var gdi = EnumerateGdi();
        IReadOnlyList<DisplayPath> paths;
        try
        {
            paths = EnumeratePaths();
        }
        catch (Exception ex)
        {
            log?.Warn("monitors", "QueryDisplayConfig failed, falling back to GDI names only: " + ex.Message);
            paths = Array.Empty<DisplayPath>();
        }

        return DisplayComposer.Compose(gdi, paths);
    }

    private static List<GdiMonitor> EnumerateGdi()
    {
        var found = new List<GdiMonitor>();
        MONITORENUMPROC callback = (HMONITOR monitor, HDC _, RECT* _, LPARAM _) =>
        {
            var info = new MONITORINFOEXW();
            info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
            if (PInvoke.GetMonitorInfo(monitor, (MONITORINFO*)&info))
            {
                uint dpiX = 96;
                if (!OperatingSystem.IsWindowsVersionAtLeast(8, 1)
                    || PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out dpiX, out _).Failed
                    || dpiX == 0)
                {
                    dpiX = 96;
                }

                var rc = info.monitorInfo.rcMonitor;
                var wk = info.monitorInfo.rcWork;
                found.Add(new GdiMonitor(
                    info.szDevice.ToString(),
                    new PixelRect(rc.left, rc.top, rc.right - rc.left, rc.bottom - rc.top),
                    new PixelRect(wk.left, wk.top, wk.right - wk.left, wk.bottom - wk.top),
                    dpiX / 96.0,
                    (info.monitorInfo.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0));
            }

            return true;
        };

        PInvoke.EnumDisplayMonitors(default(HDC), (RECT?)null, callback, 0);
        GC.KeepAlive(callback);
        return found;
    }

    private static List<DisplayPath> EnumeratePaths()
    {
        const QUERY_DISPLAY_CONFIG_FLAGS flags = QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS;
        var result = new List<DisplayPath>();

        // The path set can change between the size query and the data query; retry a few times.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            if (PInvoke.GetDisplayConfigBufferSizes(flags, out var pathCount, out var modeCount) != WIN32_ERROR.ERROR_SUCCESS) return result;
            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            WIN32_ERROR rc;
            fixed (DISPLAYCONFIG_PATH_INFO* pp = paths)
            fixed (DISPLAYCONFIG_MODE_INFO* pm = modes)
            {
                rc = PInvoke.QueryDisplayConfig(flags, ref pathCount, pp, ref modeCount, pm, null);
            }

            if (rc == WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER) continue;
            if (rc != WIN32_ERROR.ERROR_SUCCESS) return result;

            for (var i = 0; i < pathCount; i++)
            {
                var path = paths[i];
                var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                source.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                source.header.size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
                source.header.adapterId = path.sourceInfo.adapterId;
                source.header.id = path.sourceInfo.id;
                if (PInvoke.DisplayConfigGetDeviceInfo(&source.header) != 0) continue;

                var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                target.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                target.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
                target.header.adapterId = path.targetInfo.adapterId;
                target.header.id = path.targetInfo.id;
                if (PInvoke.DisplayConfigGetDeviceInfo(&target.header) != 0) continue;

                var rate = path.targetInfo.refreshRate;
                var hz = rate.Denominator == 0 ? 0 : rate.Numerator / (double)rate.Denominator;
                var tech = path.targetInfo.outputTechnology;
                var internalPanel = tech == DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL
                    || tech == DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED
                    || tech == DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED;

                result.Add(new DisplayPath(
                    source.viewGdiDeviceName.ToString(),
                    target.monitorDevicePath.ToString(),
                    target.monitorFriendlyDeviceName.ToString().Trim(),
                    hz,
                    internalPanel));
            }

            return result;
        }

        return result;
    }
}
