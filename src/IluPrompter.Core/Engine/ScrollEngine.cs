using System.Diagnostics;

namespace IluPrompter.Core;

/// <summary>
/// The scroll engine (ARCHITECTURE §2.4). Single source of truth for the position P, the document Y shown at the
/// top of the text area, in output DIPs.
///
/// Model: a logical position <c>_pos</c> (where the prompt is heading: it already includes any running step
/// animation's destination) and a decaying animation offset <c>_remaining</c>. The published position is
/// <c>_pos - _remaining</c>, so a step animation is superposed on continuous motion and a retarget never jumps.
/// Velocity is kept in lines per minute (<c>_appliedLpm</c>) and converted with the geometry's base line height.
///
/// Per Tick the order is: advance time over the OLD state (velocity ramp, auto-step, animation), then apply the
/// queued commands, then publish. A command posted before Tick(t) therefore takes effect at t, which keeps
/// the behaviour easy to reason about with synthetic time.
/// </summary>
public sealed class ScrollEngine : IScrollEngine
{
    private const double Eps = 1e-6;
    private const double MaxDt = 0.1;
    private const int QueueCapacity = 16384;
    private const int MaxAutoStepsPerTick = 4;

    private readonly ISettingsService _settings;
    private readonly EngineCommandQueue _queue = new(QueueCapacity);
    private readonly object _gate = new();
    private readonly double[] _axisSlots = new double[EngineCommand.MaxAxisSlots];

    // Published (lock-free reads).
    private long _positionBits;
    private long _lastTick;
    private int _hasTicked;
    private EngineStatus _status;
    private LayoutGeometry? _geometry;

    // State below is guarded by _gate.
    private LayoutGeometry _geo = LayoutGeometry.Empty();
    private ViewportMetrics _viewport = new(1600, 952, 314);
    private bool _hasLayout;
    private double _readingY = 314;
    private double _pMin;
    private double _pMax;
    private double[] _sectionTargets = [];

    private double _pos;                 // logical position (destination of any running animation)
    private double _remaining;           // animation offset still to play: published = _pos - _remaining
    private double _animStart;
    private double _animElapsed;
    private double _animDuration;

    private double _setSpeed;
    private ScrollMode _mode;
    private TransportState _transport = TransportState.Paused;
    private bool _failSafe;

    private double _appliedLpm;
    private double _rampTargetLpm;
    private double _rampSlope;           // lpm per second
    private double _rampRemaining;       // seconds
    private bool _axisActive;
    private double _autoStepAccum;

    public ScrollEngine(ISettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        var scroll = settings.Current.Scroll;
        _setSpeed = ClampSpeed(scroll.StartSpeed, scroll);
        _mode = scroll.DefaultMode;
        _readingY = _viewport.ReadingLineY;
        ComputeBounds();
        BuildSectionTargets();
        _pos = _pMin;
        _status = new EngineStatus { SetSpeed = _setSpeed, Mode = _mode };
        _status = BuildStatus(_status, _pos, axisNow: false, axisSum: 0, scroll) ?? _status;
        Interlocked.Exchange(ref _positionBits, BitConverter.DoubleToInt64Bits(_pos));
    }

    public EngineStatus Status => Volatile.Read(ref _status);

    public LayoutGeometry? Geometry => Volatile.Read(ref _geometry);

    public ViewportMetrics Viewport
    {
        get
        {
            lock (_gate)
            {
                return _viewport;
            }
        }
    }

    public event EventHandler<EngineStatus>? StatusChanged;

    /// <summary>Commands dropped because the ring stayed full. Zero in any healthy run.</summary>
    internal long DroppedCommands => _queue.Dropped;

    public void Post(EngineCommand command) => _queue.Enqueue(command);

    public double ReadPosition() => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _positionBits));

    public void Tick(long timestamp) => TickCore(timestamp);

    public bool TickIfStale(long timestamp, double staleAfterSeconds = 0.05)
    {
        if (Volatile.Read(ref _hasTicked) == 1)
        {
            var last = Interlocked.Read(ref _lastTick);
            if (timestamp - last < (long)(staleAfterSeconds * Stopwatch.Frequency))
            {
                return false;
            }
        }

        return TickCore(timestamp);
    }

    public void SetLayout(LayoutGeometry geometry, ViewportMetrics viewport)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        // The edit diff is O(text): work it out before taking the lock, so a tick on another thread never waits for it.
        var current = Volatile.Read(ref _geometry);
        var map = current is not null && !string.Equals(geometry.LayoutKey, current.LayoutKey, StringComparison.Ordinal)
            ? AnchorMap.Between(current, geometry)
            : null;

        EngineStatus? changed;
        lock (_gate)
        {
            if (_hasLayout && string.Equals(geometry.LayoutKey, _geo.LayoutKey, StringComparison.Ordinal))
            {
                ApplyViewportOnly(viewport);
            }
            else
            {
                ApplyNewGeometry(geometry, viewport, ReferenceEquals(current, _geo) ? map : null);
            }

            var visible = Publish();
            changed = RefreshStatus(visible);
        }

        RaiseStatusChanged(changed);
    }

    // ---- Tick ----------------------------------------------------------------------------------------------

    private bool TickCore(long timestamp)
    {
        EngineStatus? changed;
        lock (_gate)
        {
            double dt;
            if (Volatile.Read(ref _hasTicked) == 1)
            {
                var last = Interlocked.Read(ref _lastTick);
                if (timestamp <= last)
                {
                    return false;
                }

                dt = Math.Min(MaxDt, (timestamp - last) / (double)Stopwatch.Frequency);
            }
            else
            {
                dt = 0;
                Volatile.Write(ref _hasTicked, 1);
            }

            Interlocked.Exchange(ref _lastTick, timestamp);

            var scroll = _settings.Current.Scroll;
            Advance(dt, scroll);
            while (_queue.TryDequeue(out var command))
            {
                Apply(command, scroll);
            }

            var visible = Publish();
            changed = RefreshStatus(visible);
        }

        RaiseStatusChanged(changed);
        return true;
    }

    private void RaiseStatusChanged(EngineStatus? changed)
    {
        if (changed is null)
        {
            return;
        }

        try
        {
            StatusChanged?.Invoke(this, changed);
        }
        catch (Exception)
        {
            // A faulty UI subscriber must never stop the render-thread tick.
        }
    }

    // ---- Time advance ---------------------------------------------------------------------------------------

    private void Advance(double dt, ScrollSettings scroll)
    {
        var axisNow = ReadAxis(scroll, out var axisSum);
        if (_mode == ScrollMode.Display)
        {
            IntegrateDisplay(dt, scroll, axisNow, axisSum);
        }
        else
        {
            AutoStepLine(dt, scroll, axisNow, axisSum);
        }

        if (_remaining != 0)
        {
            _animElapsed += dt;
            if (_animDuration <= 0 || _animElapsed >= _animDuration)
            {
                _remaining = 0;
            }
            else
            {
                var v = 1.0 - (_animElapsed / _animDuration);
                _remaining = _animStart * v * v * v; // cubic ease-out in position
            }
        }
    }

    private bool ReadAxis(ScrollSettings scroll, out double sum)
    {
        var any = false;
        var total = 0.0;
        for (var i = 0; i < _axisSlots.Length; i++)
        {
            var v = _axisSlots[i];
            if (v != 0.0)
            {
                any = true;
                total += v;
            }
        }

        sum = any ? ClampSpeed(total, scroll) : 0.0;
        return any;
    }

    private void IntegrateDisplay(double dt, ScrollSettings scroll, bool axisNow, double axisSum)
    {
        var curve = scroll.Curve;
        var maxSpeed = CurveMax(scroll);
        double distLines; // in lines (lpm * seconds / 60), converted below

        if (axisNow)
        {
            // Axis input is applied directly, with no ramp, whether playing or paused.
            _axisActive = true;
            _appliedLpm = SpeedCurve.ToLinesPerMinute(axisSum, curve, maxSpeed);
            _rampTargetLpm = _appliedLpm;
            _rampRemaining = 0;
            distLines = _appliedLpm / 60.0 * dt;
        }
        else
        {
            var target = (_transport == TransportState.Playing && !_failSafe)
                ? SpeedCurve.ToLinesPerMinute(_setSpeed, curve, maxSpeed)
                : 0.0;

            if (_axisActive)
            {
                _axisActive = false;
                StartRamp(target, MotionTimings.RampStop);
            }
            else if (Math.Abs(target - _rampTargetLpm) > 1e-9)
            {
                StartRamp(target, ClassifyRampDuration(target, scroll));
            }

            distLines = RampDistance(dt);
            if (_failSafe && _rampRemaining <= 0 && Math.Abs(_appliedLpm) < 1e-9)
            {
                _failSafe = false;
                if (_transport == TransportState.Playing)
                {
                    _transport = TransportState.Paused;
                }
            }
        }

        var dist = distLines * SafeLineHeight();
        if (dist == 0.0 || !double.IsFinite(dist))
        {
            return;
        }

        var newPos = _pos + dist;
        if (dist > 0)
        {
            var sectionStop = double.NaN;
            if (scroll.PauseAtSections && _transport == TransportState.Playing && !_failSafe && !axisNow)
            {
                var idx = FirstSectionTargetAbove(_pos);
                if (idx >= 0 && _sectionTargets[idx] <= newPos)
                {
                    sectionStop = _sectionTargets[idx];
                }
            }

            if (newPos >= _pMax - Eps && (double.IsNaN(sectionStop) || sectionStop >= _pMax - Eps))
            {
                _pos = _pMax;
                EnterAtEnd();
            }
            else if (!double.IsNaN(sectionStop))
            {
                _pos = sectionStop;
                _transport = TransportState.Paused;
                CancelVelocity();
            }
            else
            {
                _pos = newPos;
            }
        }
        else
        {
            if (_transport == TransportState.AtEnd)
            {
                _transport = TransportState.Paused;
            }

            if (newPos <= _pMin + Eps)
            {
                _pos = _pMin;
                CancelVelocity();
                if (_transport == TransportState.Playing)
                {
                    _transport = TransportState.Paused;
                }
            }
            else
            {
                _pos = newPos;
            }
        }
    }

    /// <summary>Distance in lines covered by the velocity ramp over dt (exact for a linear ramp).</summary>
    private double RampDistance(double dt)
    {
        var v0 = _appliedLpm;
        if (_rampRemaining <= 0)
        {
            _appliedLpm = _rampTargetLpm;
            return _appliedLpm / 60.0 * dt;
        }

        if (dt >= _rampRemaining)
        {
            var t = _rampRemaining;
            var vEnd = _rampTargetLpm;
            _rampRemaining = 0;
            _appliedLpm = vEnd;
            return ((t * (v0 + vEnd) / 2.0) + ((dt - t) * vEnd)) / 60.0;
        }

        var v1 = v0 + (_rampSlope * dt);
        _rampRemaining -= dt;
        _appliedLpm = v1;
        return dt * (v0 + v1) / 2.0 / 60.0;
    }

    private void StartRamp(double target, double duration)
    {
        _rampTargetLpm = target;
        if (Math.Abs(target - _appliedLpm) < 1e-9 || duration <= 0)
        {
            _appliedLpm = target;
            _rampRemaining = 0;
            _rampSlope = 0;
            return;
        }

        _rampRemaining = duration;
        _rampSlope = (target - _appliedLpm) / duration;
    }

    private double ClassifyRampDuration(double target, ScrollSettings scroll)
    {
        if (Math.Abs(_appliedLpm) < 1e-9)
        {
            return MotionTimings.RampStart;
        }

        return Math.Abs(target) < 1e-9 ? MotionTimings.RampStop : MotionTimings.RampChange(scroll.Acceleration);
    }

    private void AutoStepLine(double dt, ScrollSettings scroll, bool axisNow, double axisSum)
    {
        // Line scrolling has no continuous velocity.
        _appliedLpm = 0;
        _rampRemaining = 0;
        _rampTargetLpm = 0;
        _axisActive = axisNow;

        double speed;
        if (axisNow)
        {
            speed = axisSum;
        }
        else if (_transport == TransportState.Playing && !_failSafe)
        {
            speed = _setSpeed;
        }
        else
        {
            _autoStepAccum = 0;
            return;
        }

        var lpm = Math.Abs(SpeedCurve.ToLinesPerMinute(speed, scroll.Curve, CurveMax(scroll)));
        if (lpm < 1e-6)
        {
            _autoStepAccum = 0;
            return;
        }

        var interval = 60.0 / lpm;
        var direction = speed < 0 ? -1 : 1;
        _autoStepAccum += dt;
        var steps = 0;
        while (_autoStepAccum >= interval && steps < MaxAutoStepsPerTick)
        {
            _autoStepAccum -= interval;
            steps++;
            AutoStepOnce(direction, scroll);
            if (_transport != TransportState.Playing && !axisNow)
            {
                _autoStepAccum = 0;
                break;
            }
        }

        if (steps == MaxAutoStepsPerTick)
        {
            _autoStepAccum = Math.Min(_autoStepAccum, interval);
        }
    }

    private void AutoStepOnce(int direction, ScrollSettings scroll)
    {
        var geo = _geo;
        var last = geo.LineCount - 1;
        var k = geo.FindLineAt(_pos + _readingY);
        double newPos;
        if (direction > 0)
        {
            newPos = (k < 0 || k >= last) ? _pMax : Math.Min(geo.LineCentre(k + 1) - _readingY, _pMax);
        }
        else
        {
            newPos = (k <= 0) ? _pMin : geo.LineCentre(k - 1) - _readingY;
        }

        var stopAtSection = false;
        if (direction > 0 && scroll.PauseAtSections && _transport == TransportState.Playing && !_failSafe)
        {
            var idx = FirstSectionTargetAbove(_pos);
            if (idx >= 0 && _sectionTargets[idx] <= newPos + Eps && _sectionTargets[idx] < _pMax - Eps)
            {
                newPos = _sectionTargets[idx];
                stopAtSection = true;
            }
        }

        MoveAnimated(newPos, MotionTimings.LineStep, direction);
        if (stopAtSection && _transport == TransportState.Playing)
        {
            _transport = TransportState.Paused;
            _autoStepAccum = 0;
        }
    }

    // ---- Commands -------------------------------------------------------------------------------------------

    private void Apply(in EngineCommand c, ScrollSettings scroll)
    {
        switch (c.Kind)
        {
            case EngineCommandKind.Play:
                DoPlay();
                break;
            case EngineCommandKind.Pause:
                DoPause();
                break;
            case EngineCommandKind.TogglePlayPause:
                if (_transport == TransportState.Playing) { DoPause(); } else { DoPlay(); }
                break;
            case EngineCommandKind.SetSpeed:
                if (!double.IsNaN(c.Value)) { _setSpeed = ClampSpeed(c.Value, scroll); }
                break;
            case EngineCommandKind.NudgeSpeed:
                if (double.IsFinite(c.Value)) { _setSpeed = ClampSpeed(Math.Round(_setSpeed + c.Value, 6), scroll); }
                break;
            case EngineCommandKind.ResetSpeed:
                _setSpeed = ClampSpeed(scroll.StartSpeed, scroll);
                break;
            case EngineCommandKind.SetAxisSpeed:
                if (c.Index >= 0 && c.Index < _axisSlots.Length)
                {
                    var v = double.IsFinite(c.Value) ? c.Value : 0.0;
                    _axisSlots[c.Index] = v;
                    if (v != 0.0)
                    {
                        _failSafe = false;
                    }
                }

                break;
            case EngineCommandKind.FailSafeStop:
                DoFailSafe();
                break;
            case EngineCommandKind.StepLines:
                StepLines(c.Index);
                break;
            case EngineCommandKind.StepParagraphs:
                StepParagraphs(c.Index);
                break;
            case EngineCommandKind.StepSections:
                StepSections(c.Index);
                break;
            case EngineCommandKind.JumpToStart:
                Cut(_pMin);
                break;
            case EngineCommandKind.JumpToEnd:
                Cut(_pMax);
                EnterAtEnd();
                break;
            case EngineCommandKind.GoToSection:
                if (c.Index >= 0 && c.Index < _sectionTargets.Length) { Cut(_sectionTargets[c.Index]); }
                break;
            case EngineCommandKind.SeekToAnchor:
                SeekToAnchor(new DocumentAnchor(c.Index, double.IsFinite(c.Value) ? (int)Math.Clamp(c.Value, 0, int.MaxValue) : 0));
                break;
            case EngineCommandKind.SeekToOffset:
                if (double.IsFinite(c.Value)) { MoveDirect(c.Value, cutAnimation: true); }
                break;
            case EngineCommandKind.ScrollPixels:
                if (double.IsFinite(c.Value) && c.Value != 0.0) { MoveDirect(_pos + c.Value, cutAnimation: false); }
                break;
            case EngineCommandKind.SetMode:
                SetMode(c.Index == (int)ScrollMode.Line ? ScrollMode.Line : ScrollMode.Display);
                break;
            case EngineCommandKind.ToggleMode:
                SetMode(_mode == ScrollMode.Display ? ScrollMode.Line : ScrollMode.Display);
                break;
        }
    }

    private void DoPlay()
    {
        if (_transport == TransportState.AtEnd)
        {
            return; // refused until the position moves back
        }

        if (_transport != TransportState.Playing)
        {
            _transport = TransportState.Playing;
            _autoStepAccum = 0;
        }

        _failSafe = false;
    }

    private void DoPause()
    {
        _failSafe = false;
        if (_transport == TransportState.Playing)
        {
            _transport = TransportState.Paused;
        }
    }

    private void DoFailSafe()
    {
        Array.Clear(_axisSlots);
        _axisActive = false;
        _autoStepAccum = 0;
        if (_mode == ScrollMode.Line)
        {
            if (_transport == TransportState.Playing)
            {
                _transport = TransportState.Paused;
            }

            return;
        }

        _failSafe = true;
        StartRamp(0.0, MotionTimings.FailSafeStop);
        if (_rampRemaining <= 0 && _transport == TransportState.Playing)
        {
            _failSafe = false;
            _transport = TransportState.Paused;
        }
    }

    private void SetMode(ScrollMode mode)
    {
        if (mode == _mode)
        {
            return;
        }

        _mode = mode;
        CancelVelocity();
        if (mode == ScrollMode.Line)
        {
            SnapToNearestLine();
        }
    }

    private void SnapToNearestLine()
    {
        var geo = _geo;
        if (geo.LineCount == 0)
        {
            return;
        }

        var y = _pos + _readingY;
        var k = geo.FindLineAt(y);
        if (y < geo.LineCentre(k) && k > 0 && (geo.LineCentre(k) - y) > (y - geo.LineCentre(k - 1)))
        {
            k--;
        }
        else if (y > geo.LineCentre(k) && k < geo.LineCount - 1 && (y - geo.LineCentre(k)) > (geo.LineCentre(k + 1) - y))
        {
            k++;
        }

        MoveAnimated(geo.LineCentre(k) - _readingY, MotionTimings.LineStep, 0);
    }

    private void StepLines(int n)
    {
        var geo = _geo;
        if (n == 0 || geo.LineCount == 0)
        {
            return;
        }

        var k = geo.FindLineAt(_pos + _readingY);
        var t = (int)Math.Clamp((long)k + n, 0, geo.LineCount - 1);
        MoveAnimated(geo.LineCentre(t) - _readingY, MotionTimings.LineStep, Math.Sign(n));
    }

    private void StepParagraphs(int n)
    {
        var geo = _geo;
        if (n == 0 || geo.LineCount == 0 || geo.ParagraphCount == 0)
        {
            return;
        }

        // Paragraphs are counted by their lines: a paragraph with no line (an empty line, or a section marker whose name is
        // hidden) is space, not a stop, so it is stepped over in both directions (integration fix: .txt scripts have an
        // empty paragraph between every two paragraphs).
        var y = _pos + _readingY;
        var k = geo.FindLineAt(y);
        var p = geo.LineParagraphs[k];
        var target = FirstLineOfParagraph(p);
        if (n > 0)
        {
            var moved = false;
            for (var j = 0; j < n; j++)
            {
                var next = FirstLineOfParagraph(p + 1);   // first line of the next paragraph that has lines
                if (next < 0 || geo.LineParagraphs[next] <= p)
                {
                    break; // already in the last paragraph
                }

                target = next;
                p = geo.LineParagraphs[next];
                moved = true;
            }

            if (!moved)
            {
                return;
            }
        }
        else
        {
            for (var j = 0; j < -(long)n; j++)
            {
                if (j == 0 && target >= 0 && (y - geo.LineCentre(target)) > 0.5 * geo.LineHeights[target])
                {
                    continue; // below the paragraph's first line by more than half a line: go to its start
                }

                if (target <= 0)
                {
                    break;
                }

                target = FirstLineOfParagraph(geo.LineParagraphs[target - 1]); // the previous paragraph that has lines
            }
        }

        if (target < 0)
        {
            return;
        }

        MoveAnimated(geo.LineCentre(target) - _readingY, MotionTimings.ParagraphStep, Math.Sign(n));
    }

    private void StepSections(int n)
    {
        var geo = _geo;
        var count = _sectionTargets.Length;
        if (n == 0 || count == 0)
        {
            if (n < 0)
            {
                Cut(_pMin); // no sections at all: "previous" means the start
            }

            return;
        }

        var y = _pos + _readingY;
        var s = geo.FindSectionAt(y);
        if (n > 0)
        {
            var t = (int)Math.Min((long)s + n, count - 1);
            if (t > s)
            {
                Cut(_sectionTargets[t]);
            }

            return;
        }

        var target = s;
        for (var j = 0; j < -(long)n; j++)
        {
            if (target < 0)
            {
                break;
            }

            if (j == 0 && BelowSectionStart(target, y))
            {
                continue;
            }

            target--;
        }

        Cut(target < 0 ? _pMin : _sectionTargets[target]);
    }

    private bool BelowSectionStart(int section, double y)
    {
        var geo = _geo;
        if (geo.LineCount == 0)
        {
            return false;
        }

        var first = Math.Min(LowerBound(geo.LineTops, geo.Sections[section].Top), geo.LineCount - 1);
        return (y - geo.LineCentre(first)) > 0.5 * geo.LineHeights[first];
    }

    private void SeekToAnchor(DocumentAnchor anchor)
    {
        var geo = _geo;
        if (geo.LineCount == 0)
        {
            Cut(_pMin);
            return;
        }

        var k = geo.FindLineForAnchor(anchor);
        Cut(geo.LineCentre(Math.Max(0, k)) - _readingY);
    }

    // ---- Movement helpers -----------------------------------------------------------------------------------

    /// <summary>Animated move of the logical position; the visible position eases there. direction = requested sign.</summary>
    private void MoveAnimated(double newPos, double duration, int direction)
    {
        newPos = Math.Clamp(newPos, _pMin, _pMax);
        var delta = newPos - _pos;
        if (Math.Abs(delta) > 1e-9)
        {
            _remaining += delta; // keeps pos - remaining (the visible position) continuous
            _animStart = _remaining;
            _animElapsed = 0;
            _animDuration = duration;
            _pos = newPos;
        }

        AfterMove(direction);
    }

    /// <summary>Immediate move (SeekToOffset / ScrollPixels): keeps the play state.</summary>
    private void MoveDirect(double newPos, bool cutAnimation)
    {
        var requested = newPos - _pos;
        _pos = Math.Clamp(newPos, _pMin, _pMax);
        if (cutAnimation)
        {
            _remaining = 0;
            _animStart = 0;
        }

        AfterMove(Math.Sign(requested));
    }

    /// <summary>A cut: no animation, velocity cancelled, transport Paused (AtEnd if it lands on the end).</summary>
    private void Cut(double newPos)
    {
        _pos = Math.Clamp(newPos, _pMin, _pMax);
        _remaining = 0;
        _animStart = 0;
        _failSafe = false;
        CancelVelocity();
        _transport = _pos >= _pMax - Eps ? TransportState.AtEnd : TransportState.Paused;
    }

    private void AfterMove(int direction)
    {
        if (direction > 0 && _pos >= _pMax - Eps)
        {
            _pos = _pMax;
            EnterAtEnd();
        }
        else if (direction < 0)
        {
            if (_transport == TransportState.AtEnd && _pos < _pMax - Eps)
            {
                _transport = TransportState.Paused;
            }

            if (_pos <= _pMin + Eps)
            {
                _pos = _pMin;
                if (_transport == TransportState.Playing)
                {
                    _transport = TransportState.Paused;
                }
            }
        }
    }

    private void EnterAtEnd()
    {
        _transport = TransportState.AtEnd;
        _failSafe = false;
        CancelVelocity();
    }

    private void CancelVelocity()
    {
        _appliedLpm = 0;
        _rampTargetLpm = 0;
        _rampSlope = 0;
        _rampRemaining = 0;
        _autoStepAccum = 0;
    }

    // ---- Layout ---------------------------------------------------------------------------------------------

    private void ApplyViewportOnly(ViewportMetrics viewport)
    {
        if (viewport == _viewport)
        {
            return;
        }

        var oldReading = _readingY;
        _viewport = viewport;
        _readingY = SafeReading(viewport);
        ComputeBounds();
        BuildSectionTargets();
        _pos = Math.Clamp(_pos + oldReading - _readingY, _pMin, _pMax);
        Resettle();
    }

    private void ApplyNewGeometry(LayoutGeometry geometry, ViewportMetrics viewport, AnchorMap? precomputed)
    {
        var old = _geo;
        var hadLines = _hasLayout && old.LineCount > 0;
        var wasAtEnd = _transport == TransportState.AtEnd;
        var oldReading = _readingY;
        var target = _pos;                 // destination of any running step animation
        var shown = _pos - _remaining;     // what is on screen now

        _geo = geometry;
        _hasLayout = true;
        Volatile.Write(ref _geometry, geometry);
        _viewport = viewport;
        _readingY = SafeReading(viewport);
        ComputeBounds();
        BuildSectionTargets();

        if (wasAtEnd)
        {
            _pos = _pMax;
            CutAnimation();
        }
        else if (!hadLines || geometry.LineCount == 0)
        {
            _pos = _pMin;
            CutAnimation();
        }
        else
        {
            // Live-edit invariant (ARCHITECTURE §2.4 "Layout change"): the text on the reading line stays there. The reading
            // point is mapped through the edit (AnchorMap: paragraphs inserted/deleted/edited anywhere) and through reflow,
            // for both the animation's destination and the picture on screen, so a running step keeps running from where
            // it visibly is. The line-scrolling step timer is not reset.
            var map = precomputed ?? AnchorMap.Between(old, geometry);
            var newTarget = LayoutMapping.MapPosition(old, geometry, map, target + oldReading - _readingY, _readingY);
            var newShown = _remaining == 0 ? newTarget : LayoutMapping.MapPosition(old, geometry, map, shown + oldReading - _readingY, _readingY);
            _pos = Math.Clamp(newTarget, _pMin, _pMax);
            if (_remaining != 0)
            {
                var remaining = _pos - Math.Clamp(newShown, _pMin, _pMax);
                _animStart = double.IsFinite(remaining / _remaining) ? _animStart * (remaining / _remaining) : 0;
                _remaining = remaining;
                if (_remaining == 0) _animStart = 0;
            }
        }

        Resettle();
    }

    private void CutAnimation()
    {
        _remaining = 0;
        _animStart = 0;
        _animElapsed = 0;
    }

    /// <summary>Re-establish transport invariants after the bounds moved.</summary>
    private void Resettle()
    {
        if (_pos >= _pMax - Eps)
        {
            _pos = _pMax;
            if (_transport != TransportState.AtEnd)
            {
                EnterAtEnd();
            }
        }
        else if (_transport == TransportState.AtEnd)
        {
            _transport = TransportState.Paused;
        }

        if (_pos <= _pMin + Eps)
        {
            _pos = _pMin;
        }
    }

    private void ComputeBounds()
    {
        var g = _geo;
        var r = _readingY;
        _pMin = g.LineCount > 0 ? g.LineCentre(0) - r : -r;
        var pMax = g.EndTop + (g.BaseLineHeight / 2.0) - r;
        _pMax = double.IsFinite(pMax) ? Math.Max(_pMin, pMax) : _pMin;
        if (!double.IsFinite(_pMin))
        {
            _pMin = 0;
            _pMax = 0;
        }
    }

    private void BuildSectionTargets()
    {
        var g = _geo;
        var sections = g.Sections;
        if (_sectionTargets.Length != sections.Count)
        {
            _sectionTargets = sections.Count == 0 ? [] : new double[sections.Count];
        }

        for (var i = 0; i < sections.Count; i++)
        {
            var target = _pMax;
            if (g.LineCount > 0)
            {
                var line = Math.Min(LowerBound(g.LineTops, sections[i].Top), g.LineCount - 1);
                target = g.LineCentre(line) - _readingY;
            }

            _sectionTargets[i] = Math.Clamp(target, _pMin, _pMax);
        }

        // Keep the table sorted so the binary searches stay valid even for odd inputs.
        for (var i = 1; i < _sectionTargets.Length; i++)
        {
            if (_sectionTargets[i] < _sectionTargets[i - 1])
            {
                _sectionTargets[i] = _sectionTargets[i - 1];
            }
        }
    }

    private int FirstSectionTargetAbove(double position)
    {
        int lo = 0, hi = _sectionTargets.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (_sectionTargets[mid] <= position + 1e-9) { lo = mid + 1; } else { hi = mid; }
        }

        return lo < _sectionTargets.Length ? lo : -1;
    }

    private int FirstLineOfParagraph(int paragraph)
    {
        var geo = _geo;
        var lines = geo.LineParagraphs;
        int lo = 0, hi = lines.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (lines[mid] < paragraph) { lo = mid + 1; } else { hi = mid; }
        }

        return lo < lines.Count ? lo : lines.Count - 1;
    }

    private static int LowerBound(IReadOnlyList<double> sorted, double value)
    {
        int lo = 0, hi = sorted.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (sorted[mid] < value) { lo = mid + 1; } else { hi = mid; }
        }

        return lo;
    }

    // ---- Publish and status ---------------------------------------------------------------------------------

    private double Publish()
    {
        var visible = _pos - _remaining;
        if (!double.IsFinite(visible) || !double.IsFinite(_pos))
        {
            // Self-heal; never publish NaN.
            _pos = _pMin;
            _remaining = 0;
            CancelVelocity();
            visible = _pMin;
        }

        visible = Math.Clamp(visible, _pMin, _pMax);
        Interlocked.Exchange(ref _positionBits, BitConverter.DoubleToInt64Bits(visible));
        return visible;
    }

    private EngineStatus? RefreshStatus(double visible)
    {
        var scroll = _settings.Current.Scroll;
        var axisNow = false;
        var axisSum = 0.0;
        for (var i = 0; i < _axisSlots.Length; i++)
        {
            if (_axisSlots[i] != 0.0)
            {
                axisNow = true;
                axisSum += _axisSlots[i];
            }
        }

        axisSum = axisNow ? ClampSpeed(axisSum, scroll) : 0.0;
        var next = BuildStatus(_status, visible, axisNow, axisSum, scroll);
        if (next is null)
        {
            return null;
        }

        Volatile.Write(ref _status, next);
        return next;
    }

    /// <summary>Returns a new status, or null when no (rounded) field differs from <paramref name="current"/>.
    /// Allocation-free when nothing changed.</summary>
    private EngineStatus? BuildStatus(EngineStatus current, double visible, bool axisNow, double axisSum, ScrollSettings scroll)
    {
        var geo = _geo;
        var y = visible + _readingY;
        var lineCount = geo.LineCount;
        var lineIndex = lineCount == 0 ? -1 : geo.FindLineAt(y);
        var paragraphCount = geo.ParagraphCount;
        var sectionIndex = geo.FindSectionAt(y);
        var curve = scroll.Curve;
        var maxSpeed = CurveMax(scroll);

        double effectiveSpeed;
        double effectiveLpm;
        if (_mode == ScrollMode.Display)
        {
            effectiveLpm = _appliedLpm;
            effectiveSpeed = SpeedCurve.FromLinesPerMinute(_appliedLpm, curve, maxSpeed);
        }
        else
        {
            effectiveSpeed = axisNow ? axisSum : (_transport == TransportState.Playing && !_failSafe ? _setSpeed : 0.0);
            effectiveLpm = SpeedCurve.ToLinesPerMinute(effectiveSpeed, curve, maxSpeed);
        }

        var stepInterval = 0.0;
        if (_mode == ScrollMode.Line && Math.Abs(effectiveLpm) > 1e-6)
        {
            stepInterval = 60.0 / Math.Abs(effectiveLpm);
        }

        // Time left at the set speed. Display scrolling covers distance at lpm x BaseLineHeight, so it counts the space
        // from the current line to END (paragraph gaps, empty lines and hidden section markers included) in base lines;
        // Line scrolling makes one step per text line. Measured from the current line's centre, so the value (and the
        // status) changes only when the line does. (Integration fix: counting text lines alone under-read a real script
        // by a quarter or more.)
        var remainingLines = lineCount == 0 ? 0 : lineCount - 1 - lineIndex;
        var setLpm = Math.Abs(SpeedCurve.ToLinesPerMinute(_setSpeed, curve, maxSpeed));
        var remainingUnits = _transport == TransportState.AtEnd ? 0.0
            : _mode == ScrollMode.Display && geo.BaseLineHeight > 0 && lineIndex >= 0
                ? Math.Max(0.0, _pMax + _readingY - geo.LineCentre(lineIndex)) / geo.BaseLineHeight
                : remainingLines;
        var seconds = lineCount == 0 || remainingUnits <= 1e-9 ? 0.0
            : setLpm < 1e-6 ? double.PositiveInfinity
            : remainingUnits * 60.0 / setLpm;

        var effRounded = Round1(effectiveSpeed);
        var lpmRounded = Round1(effectiveLpm);
        var wpm = _mode == ScrollMode.Display || lineCount == 0
            ? TimeEstimator.WordsPerMinute(geo, effectiveLpm)
            : Math.Abs(effectiveLpm) * geo.WordCount / lineCount; // Line scrolling: one text line per step
        var wpmRounded = Round1(double.IsFinite(wpm) ? wpm : 0.0);
        var stepRounded = Round1(stepInterval);
        var secRounded = double.IsFinite(seconds) ? Math.Round(seconds) : seconds;
        var isAtStart = visible <= _pMin + Eps;
        var paragraphIndex = lineIndex >= 0 ? geo.LineParagraphs[lineIndex] : 0;
        var anchor = lineIndex >= 0 ? new DocumentAnchor(geo.LineParagraphs[lineIndex], geo.LineCharOffsets[lineIndex]) : DocumentAnchor.Start;
        var isAtEnd = _transport == TransportState.AtEnd;

        if (current.Transport == _transport
            && current.Mode == _mode
            && current.SetSpeed == _setSpeed
            && current.EffectiveSpeed == effRounded
            && current.LinesPerMinute == lpmRounded
            && current.WordsPerMinute == wpmRounded
            && current.StepIntervalSeconds == stepRounded
            && current.IsAxisActive == axisNow
            && current.LineIndex == lineIndex
            && current.LineCount == lineCount
            && current.ParagraphIndex == paragraphIndex
            && current.ParagraphCount == paragraphCount
            && current.SectionIndex == sectionIndex
            && current.SectionCount == geo.Sections.Count
            && current.ReadingAnchor == anchor
            && current.IsAtStart == isAtStart
            && current.IsAtEnd == isAtEnd
            && current.SecondsRemaining.Equals(secRounded)
            && ReferenceEquals(current.LayoutKey, geo.LayoutKey))
        {
            return null;
        }

        return new EngineStatus
        {
            Transport = _transport,
            Mode = _mode,
            SetSpeed = _setSpeed,
            EffectiveSpeed = effRounded,
            LinesPerMinute = lpmRounded,
            WordsPerMinute = wpmRounded,
            StepIntervalSeconds = stepRounded,
            IsAxisActive = axisNow,
            LineIndex = lineIndex,
            LineCount = lineCount,
            ParagraphIndex = paragraphIndex,
            ParagraphCount = paragraphCount,
            SectionIndex = sectionIndex,
            SectionCount = geo.Sections.Count,
            ReadingAnchor = anchor,
            IsAtStart = isAtStart,
            IsAtEnd = isAtEnd,
            SecondsRemaining = secRounded,
            LayoutKey = geo.LayoutKey,
        };
    }

    // ---- Small helpers --------------------------------------------------------------------------------------

    private static double Round1(double v) => Math.Round(v, 1) + 0.0; // + 0.0 turns -0.0 into 0.0

    private double SafeLineHeight()
    {
        var h = _geo.BaseLineHeight;
        return double.IsFinite(h) && h > 0 ? h : 0.0;
    }

    private static double SafeReading(ViewportMetrics viewport) =>
        double.IsFinite(viewport.ReadingLineY) ? viewport.ReadingLineY : 0.0;

    private static double CurveMax(ScrollSettings scroll)
    {
        var m = Math.Max(Math.Abs(scroll.MaxSpeed), Math.Abs(scroll.MinSpeed));
        return double.IsFinite(m) && m > 0 ? m : 10.0;
    }

    private static double ClampSpeed(double speed, ScrollSettings scroll)
    {
        var lo = Math.Min(scroll.MinSpeed, scroll.MaxSpeed);
        var hi = Math.Max(scroll.MinSpeed, scroll.MaxSpeed);
        return Math.Clamp(speed, lo, hi);
    }
}
