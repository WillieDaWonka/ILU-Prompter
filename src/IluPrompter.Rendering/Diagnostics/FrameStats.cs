using System.Diagnostics;
using System.Globalization;
using IluPrompter.Core;

namespace IluPrompter.Rendering;

/// <summary>A summary of frame intervals over one reporting window (all times in milliseconds).</summary>
internal readonly record struct FrameStatsReport(int Frames, double Mean, double P95, double P99, double Max, int OverThreshold, double NominalMs);

/// <summary>
/// Frame-interval statistics for one surface, active with <c>--diag</c> (ARCHITECTURE §4.C, Phase-0 S2/S3).
/// A pre-allocated ring buffer holds the intervals; every <c>reportEvery</c> a line with mean/p95/p99/max and the count of
/// intervals above 1.5 x nominal is written to the log, with the GC collection counts for allocation checks.
/// <see cref="Record"/> does not allocate.
/// </summary>
internal sealed class FrameStats
{
    public const int Capacity = 4096;
    public const double ThresholdFactor = 1.5;
    private const double GapMs = 1000.0;   // longer than this is a pause, not a frame interval

    private readonly string _name;
    private readonly IAppLog? _log;
    private readonly double _nominalMs;
    private readonly long _frequency;
    private readonly long _reportEveryTicks;
    private readonly float[] _ring = new float[Capacity];
    private readonly float[] _scratch = new float[Capacity];
    private int _count;
    private int _next;
    private int _over;
    private long _lastTimestamp;
    private long _windowStart;
    private int _gc0, _gc1, _gc2;
    private FrameStatsReport _lastReport;

    public FrameStats(string name, IAppLog? log, double refreshHz, TimeSpan reportEvery)
        : this(name, log, refreshHz, reportEvery, Stopwatch.Frequency)
    {
    }

    public FrameStats(string name, IAppLog? log, double refreshHz, TimeSpan reportEvery, long stopwatchFrequency)
    {
        _name = name;
        _log = log;
        _frequency = stopwatchFrequency;
        _nominalMs = 1000.0 / (refreshHz > 1 ? refreshHz : 60.0);
        _reportEveryTicks = (long)(reportEvery.TotalSeconds * stopwatchFrequency);
    }

    public FrameStatsReport LastReport => _lastReport;

    /// <summary>Record that a frame happened at <paramref name="timestamp"/> (Stopwatch ticks).</summary>
    public void Record(long timestamp)
    {
        if (_lastTimestamp != 0)
        {
            var ms = (timestamp - _lastTimestamp) * 1000.0 / _frequency;
            if (ms > 0 && ms < GapMs)
            {
                _ring[_next] = (float)ms;
                _next = (_next + 1) & (Capacity - 1);
                if (_count < Capacity) _count++;
                if (ms > _nominalMs * ThresholdFactor) _over++;
            }
        }

        _lastTimestamp = timestamp;
        if (_windowStart == 0)
        {
            _windowStart = timestamp;
            _gc0 = GC.CollectionCount(0);
            _gc1 = GC.CollectionCount(1);
            _gc2 = GC.CollectionCount(2);
        }
        else if (timestamp - _windowStart >= _reportEveryTicks)
        {
            Report(timestamp);
        }
    }

    /// <summary>Forget the current window (the surface was hidden or paused for a while).</summary>
    public void Reset()
    {
        _count = 0;
        _next = 0;
        _over = 0;
        _lastTimestamp = 0;
        _windowStart = 0;
    }

    public FrameStatsReport Summarise()
    {
        if (_count == 0) return new FrameStatsReport(0, 0, 0, 0, 0, _over, _nominalMs);
        Array.Copy(_ring, _scratch, _count);
        var span = _scratch.AsSpan(0, _count);
        span.Sort();
        double sum = 0;
        for (var i = 0; i < span.Length; i++) sum += span[i];
        return new FrameStatsReport(_count, sum / _count, Percentile(span, 0.95), Percentile(span, 0.99), span[^1], _over, _nominalMs);
    }

    private void Report(long now)
    {
        var report = Summarise();
        _lastReport = report;
        if (_log is { DiagnosticsEnabled: true })
        {
            var c = CultureInfo.InvariantCulture;
            _log.Trace("frames:" + _name, string.Create(c,
                $"{report.Frames} frames, nominal {report.NominalMs:0.00} ms, mean {report.Mean:0.00}, p95 {report.P95:0.00}, p99 {report.P99:0.00}, max {report.Max:0.00}, over {ThresholdFactor}x nominal {report.OverThreshold} ({(report.Frames == 0 ? 0 : 100.0 * report.OverThreshold / report.Frames):0.00}%), GC +{GC.CollectionCount(0) - _gc0}/{GC.CollectionCount(1) - _gc1}/{GC.CollectionCount(2) - _gc2}"));
        }

        _count = 0;
        _next = 0;
        _over = 0;
        _windowStart = now;
        _gc0 = GC.CollectionCount(0);
        _gc1 = GC.CollectionCount(1);
        _gc2 = GC.CollectionCount(2);
    }

    private static double Percentile(ReadOnlySpan<float> sorted, double p)
    {
        var index = (int)Math.Ceiling(p * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }
}
