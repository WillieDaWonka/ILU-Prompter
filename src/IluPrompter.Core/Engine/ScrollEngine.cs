using System.Collections.Concurrent;
using System.Diagnostics;

namespace IluPrompter.Core;

/// <summary>
/// STUB (stream A replaces the body; keep the type name and constructor signature).
/// Naive: Play/Pause/TogglePlayPause/SetSpeed/NudgeSpeed/JumpToStart/SeekToOffset/ScrollPixels only,
/// no ramps, no steps, no re-anchoring. Enough for streams C and D to see motion.
/// </summary>
public sealed class ScrollEngine : IScrollEngine
{
    private readonly ISettingsService _settings;
    private readonly ConcurrentQueue<EngineCommand> _queue = new();
    private readonly object _gate = new();
    private long _positionBits;
    private long _lastTick;
    private double _setSpeed;
    private bool _playing;
    private LayoutGeometry? _geometry;
    private ViewportMetrics _viewport = new(1600, 952, 314);
    private EngineStatus _status;

    public ScrollEngine(ISettingsService settings)
    {
        _settings = settings;
        _setSpeed = settings.Current.Scroll.StartSpeed;
        _status = new EngineStatus { SetSpeed = _setSpeed };
    }

    public EngineStatus Status => Volatile.Read(ref _status);
    public LayoutGeometry? Geometry => Volatile.Read(ref _geometry);
    public ViewportMetrics Viewport { get { lock (_gate) { return _viewport; } } }
    public event EventHandler<EngineStatus>? StatusChanged;

    public void Post(EngineCommand command) => _queue.Enqueue(command);

    public double ReadPosition() => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _positionBits));

    public void SetLayout(LayoutGeometry geometry, ViewportMetrics viewport)
    {
        lock (_gate) { Volatile.Write(ref _geometry, geometry); _viewport = viewport; }
    }

    public bool TickIfStale(long timestamp, double staleAfterSeconds = 0.05)
    {
        if (timestamp - Interlocked.Read(ref _lastTick) < staleAfterSeconds * Stopwatch.Frequency) return false;
        Tick(timestamp);
        return true;
    }

    public void Tick(long timestamp)
    {
        EngineStatus? changed = null;
        lock (_gate)
        {
            var last = Interlocked.Read(ref _lastTick);
            if (timestamp <= last) return;
            Interlocked.Exchange(ref _lastTick, timestamp);
            var dt = last == 0 ? 0 : Math.Min(0.1, (timestamp - last) / (double)Stopwatch.Frequency);
            var pos = ReadPosition();
            while (_queue.TryDequeue(out var c))
            {
                switch (c.Kind)
                {
                    case EngineCommandKind.Play: _playing = true; break;
                    case EngineCommandKind.Pause: _playing = false; break;
                    case EngineCommandKind.TogglePlayPause: _playing = !_playing; break;
                    case EngineCommandKind.SetSpeed: _setSpeed = c.Value; break;
                    case EngineCommandKind.NudgeSpeed: _setSpeed += c.Value; break;
                    case EngineCommandKind.JumpToStart: pos = -_viewport.ReadingLineY; _playing = false; break;
                    case EngineCommandKind.SeekToOffset: pos = c.Value; break;
                    case EngineCommandKind.ScrollPixels: pos += c.Value; break;
                }
            }

            var lineHeight = _geometry?.BaseLineHeight ?? 119.0;
            if (_playing) pos += SpeedCurve.ToDipsPerSecond(_setSpeed, lineHeight, _settings.Current.Scroll.Curve) * dt;
            Interlocked.Exchange(ref _positionBits, BitConverter.DoubleToInt64Bits(pos));
            var transport = _playing ? TransportState.Playing : TransportState.Paused;
            if (transport != _status.Transport || _setSpeed != _status.SetSpeed)
            {
                changed = _status with { Transport = transport, SetSpeed = _setSpeed, EffectiveSpeed = _playing ? _setSpeed : 0 };
                Volatile.Write(ref _status, changed);
            }
        }

        if (changed is not null) StatusChanged?.Invoke(this, changed);
    }
}
