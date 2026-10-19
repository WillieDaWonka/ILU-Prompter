using System.Diagnostics;

namespace IluPrompter.Rendering;

/// <summary>
/// Turns <c>RenderingEventArgs.RenderingTime</c> into engine timestamps (Stopwatch ticks).
/// WPF can raise <c>CompositionTarget.Rendering</c> more than once per frame, so a repeated RenderingTime is rejected.
/// Using the compositor's frame time rather than "now" at the moment the handler runs removes dispatcher latency
/// jitter from <c>pos += v * dt</c> (RESEARCH §1.2). The mapping is anchored to the Stopwatch and re-anchored if it
/// drifts more than <see cref="MaxDriftSeconds"/>, so it also stays comparable with the preview's
/// <c>TickIfStale(Stopwatch.GetTimestamp())</c>.
/// </summary>
internal sealed class FrameClock
{
    public const double MaxDriftSeconds = 0.25;

    private readonly long _frequency;
    private readonly double _ratio;          // Stopwatch ticks per 100 ns TimeSpan tick
    private long _lastRenderingTicks = long.MinValue;
    private long _anchorRenderingTicks;
    private long _anchorStopwatch;
    private long _lastMapped = long.MinValue;

    public FrameClock() : this(Stopwatch.Frequency)
    {
    }

    public FrameClock(long stopwatchFrequency)
    {
        _frequency = stopwatchFrequency;
        _ratio = stopwatchFrequency / (double)TimeSpan.TicksPerSecond;
    }

    /// <summary>False when this RenderingTime was already handled. Otherwise <paramref name="timestamp"/> is strictly
    /// greater than every timestamp returned before.</summary>
    public bool TryMap(TimeSpan renderingTime, long stopwatchNow, out long timestamp)
    {
        var ticks = renderingTime.Ticks;
        if (ticks == _lastRenderingTicks)
        {
            timestamp = 0;
            return false;
        }

        var backwards = ticks < _lastRenderingTicks;
        var first = _lastRenderingTicks == long.MinValue;
        _lastRenderingTicks = ticks;

        if (first || backwards)
        {
            Anchor(ticks, stopwatchNow);
        }

        var mapped = Map(ticks);
        if (Math.Abs(mapped - stopwatchNow) > MaxDriftSeconds * _frequency)
        {
            Anchor(ticks, stopwatchNow);
            mapped = Map(ticks);
        }

        if (mapped <= _lastMapped) mapped = _lastMapped + 1;   // monotonic, whatever the compositor reports
        _lastMapped = mapped;
        timestamp = mapped;
        return true;
    }

    private void Anchor(long renderingTicks, long stopwatchNow)
    {
        _anchorRenderingTicks = renderingTicks;
        _anchorStopwatch = _lastMapped == long.MinValue ? stopwatchNow : Math.Max(stopwatchNow, _lastMapped + 1);
    }

    private long Map(long renderingTicks) =>
        _anchorStopwatch + (long)Math.Round((renderingTicks - _anchorRenderingTicks) * _ratio);
}
