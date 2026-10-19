using System.Runtime;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace IluPrompter.Rendering;

/// <summary>
/// While the script is playing on an open output: keeps the display and system awake
/// (<c>SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED)</c>) and raises the GC latency
/// mode to <see cref="GCLatencyMode.SustainedLowLatency"/> (ARCHITECTURE §3 rule 8). Both are released otherwise.
/// Execution state is a per-thread request, so every call must come from the same thread: the output thread.
/// </summary>
internal sealed class DisplaySleepGuard : IDisposable
{
    private readonly Action<bool> _setExecutionState;
    private readonly Func<GCLatencyMode> _getGcMode;
    private readonly Action<GCLatencyMode> _setGcMode;
    private GCLatencyMode _savedGcMode;
    private bool _held;

    public DisplaySleepGuard()
        : this(SetNativeExecutionState, () => GCSettings.LatencyMode, mode => GCSettings.LatencyMode = mode)
    {
    }

    public DisplaySleepGuard(Action<bool> setExecutionState, Func<GCLatencyMode> getGcMode, Action<GCLatencyMode> setGcMode)
    {
        _setExecutionState = setExecutionState;
        _getGcMode = getGcMode;
        _setGcMode = setGcMode;
    }

    public bool IsHeld => _held;

    /// <summary>Cheap when nothing changes (a bool compare), so it can be called every frame.</summary>
    public void Update(bool shouldHold)
    {
        if (shouldHold == _held) return;
        _held = shouldHold;
        if (shouldHold)
        {
            _savedGcMode = _getGcMode();
            _setGcMode(GCLatencyMode.SustainedLowLatency);
            _setExecutionState(true);
        }
        else
        {
            _setExecutionState(false);
            _setGcMode(_savedGcMode);
        }
    }

    public void Dispose() => Update(false);

    private static void SetNativeExecutionState(bool hold) =>
        PInvoke.SetThreadExecutionState(hold
            ? EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_SYSTEM_REQUIRED
            : EXECUTION_STATE.ES_CONTINUOUS);
}
