namespace IluPrompter.Core;

public readonly record struct PixelRect(int X, int Y, int Width, int Height);

public readonly record struct PixelSize(int Width, int Height);

/// <summary>A connected display. StableId = the QueryDisplayConfig target device path
/// (survives reordering and reboots); DisplayNumber = the number Windows shows in Settings › Display.</summary>
public sealed record MonitorInfo(
    string StableId,
    int DisplayNumber,
    string FriendlyName,
    PixelRect BoundsPx,
    PixelRect WorkAreaPx,
    double DpiScale,
    double RefreshHz,
    bool IsPrimary,
    bool IsDuplicated)
{
    /// <summary>"Display 2 · LG 55UH5" (DESIGN.md wording).</summary>
    public string Label => FriendlyName.Length == 0 ? $"Display {DisplayNumber}" : $"Display {DisplayNumber} · {FriendlyName}";
}

public interface IMonitorService
{
    /// <summary>Snapshot of connected displays (any thread).</summary>
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>The saved display if connected; else the first non-primary, non-duplicated display;
    /// else null. Never returns the primary (operator) display.</summary>
    MonitorInfo? ResolveOutputMonitor(string? savedStableId);

    /// <summary>Raised (debounced 500 ms, any thread) when displays are added, removed or reconfigured.</summary>
    event EventHandler? MonitorsChanged;
}

public enum OutputDisplayStatus
{
    Ready = 0,          // a usable output display exists
    NoSecondDisplay,    // only the operator display is connected
    Duplicated,         // Windows is mirroring; no extended display
    Missing,            // the chosen display vanished while the output was open (window hidden, waiting)
}

/// <summary>Size of the output display. Layout always uses DIPs = px / DpiScale of the output display.</summary>
public readonly record struct OutputGeometry(int WidthPx, int HeightPx, double DpiScale, double RefreshHz)
{
    public double WidthDip => WidthPx / DpiScale;
    public double HeightDip => HeightPx / DpiScale;
    public PixelSize SizePx => new(WidthPx, HeightPx);

    /// <summary>Used for layout when no output display has ever been seen.</summary>
    public static OutputGeometry Default1080 => new(1920, 1080, 1.0, 60.0);
}

/// <summary>Output window state. Geometry is the current display's, or the last known one when no display.</summary>
public sealed record OutputState(
    bool IsOpen,
    bool IsBlank,
    OutputDisplayStatus Display,
    MonitorInfo? Monitor,
    OutputGeometry Geometry)
{
    /// <summary>The presenter can see the script now (drives the LIVE chip and the green preview frame).</summary>
    public bool IsLive => IsOpen && !IsBlank && Display == OutputDisplayStatus.Ready;

    public static OutputState Initial { get; } =
        new(false, false, OutputDisplayStatus.NoSecondDisplay, null, OutputGeometry.Default1080);
}

/// <summary>
/// The output window, which lives on its own STA/Dispatcher thread. All methods are callable from any
/// thread; they marshal internally. Never shows the window on the primary display.
/// </summary>
public interface IOutputSurface
{
    OutputState State { get; }

    /// <summary>Raised on the output thread. UI must marshal.</summary>
    event EventHandler<OutputState>? StateChanged;

    /// <summary>Start the output thread and its Dispatcher (window created but not shown). Call once at startup.</summary>
    Task InitializeAsync();

    /// <summary>Show fullscreen on the resolved display. If none, stays closed and State.Display says why.</summary>
    Task ShowAsync();

    Task HideAsync();

    /// <summary>Blank = the presenter sees black; scrolling and the preview continue.</summary>
    void SetBlank(bool blank);

    /// <summary>Close the window, stop the Dispatcher and join the thread.</summary>
    Task ShutdownAsync();
}
