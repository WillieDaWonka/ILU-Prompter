namespace IluPrompter.Core.Tests;

public class EngineModeTests
{
    private const double Lh = TestGeometry.LineHeight;
    private static readonly SpeedCurveSettings Curve = new();

    private static double Interval(double speed) => 60.0 / SpeedCurve.ToLinesPerMinute(speed, Curve);

    [Fact]
    public void LinePlay_StepsOneLineEveryInterval()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Play();
        var interval = Interval(4.5); // 60 / 28.3 = 2.12 s
        Assert.Equal(interval, h.Status.StepIntervalSeconds, 0.06);
        Assert.Equal(ScrollMode.Line, h.Status.Mode);

        h.Run(interval - 0.1);
        Assert.Equal(h.Pmin, h.Pos, 9); // nothing before the first interval has elapsed

        h.Run(0.15); // the step starts at 2.12 s
        Assert.True(h.Pos > h.Pmin);
        Assert.True(h.Pos < TestGeometry.PosOfLine(h.Geometry, 1));

        h.Run(0.3);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 1), h.Pos, 9);
    }

    [Fact]
    public void LinePlay_OverTenSeconds_StepsFourTimes()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Play();
        h.Run(10);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 4), h.Pos, 9); // steps at 2.12, 4.24, 6.36, 8.48 s
        Assert.Equal(4, h.Status.LineIndex);
    }

    [Fact]
    public void LinePlay_StepsBackwardsForANegativeSpeed()
    {
        var h = new EngineHarness();
        h.SeekToLine(20);
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Apply(EngineCommand.SetSpeed(-10));
        h.Play();
        h.Run(2.9); // interval 0.75 s: three steps
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 17), h.Pos, 9);
    }

    [Fact]
    public void LineMode_Paused_DoesNotStep()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Run(10);
        Assert.Equal(h.Pmin, h.Pos, 9);
        Assert.Equal(0.0, h.Status.StepIntervalSeconds);
        Assert.Equal(0.0, h.Status.EffectiveSpeed);
    }

    [Fact]
    public void LineMode_SpeedChangeChangesTheRateWithoutARamp()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Apply(EngineCommand.SetSpeed(10));
        h.Play();
        Assert.Equal(0.75, h.Status.StepIntervalSeconds, 0.06);
        h.Run(2.9);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 3), h.Pos, 9);
    }

    [Fact]
    public void LineMode_Axis_SetsTheAutoStepRate_EvenWhilePaused()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Apply(EngineCommand.SetAxisSpeed(0, 10));
        Assert.True(h.Status.IsAxisActive);
        h.Run(2.9);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 3), h.Pos, 9); // 80 lpm: a step every 0.75 s
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        Assert.Equal(0.75, h.Status.StepIntervalSeconds, 0.06);

        h.Apply(EngineCommand.SetAxisSpeed(0, 0));
        h.Run(3);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 3), h.Pos, 9); // released: no more steps
        Assert.False(h.Status.IsAxisActive);
    }

    [Fact]
    public void LineMode_Axis_SlowerRate_GivesALongerInterval()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Apply(EngineCommand.SetAxisSpeed(0, 5));
        var interval = Interval(5);
        h.Run((2 * interval) + 0.5);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 2), h.Pos, 9);
    }

    [Fact]
    public void LineMode_NegativeAxis_StepsBackwards()
    {
        var h = new EngineHarness();
        h.SeekToLine(10);
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Apply(EngineCommand.SetAxisSpeed(0, -10));
        h.Run(2.9);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 7), h.Pos, 9);
    }

    [Fact]
    public void ModeSwitch_SnapsToTheNearestLineCentre_WithoutLeavingThePosition()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SeekToOffset(TestGeometry.PosOfLine(h.Geometry, 3) + (0.3 * Lh)));
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Run(0.3);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 3), h.Pos, 9);

        h.Apply(EngineCommand.ToggleMode());
        Assert.Equal(ScrollMode.Display, h.Status.Mode);
        h.Apply(EngineCommand.SeekToOffset(TestGeometry.PosOfLine(h.Geometry, 3) + (0.7 * Lh)));
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Run(0.3);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 4), h.Pos, 9);
    }

    [Fact]
    public void ModeSwitch_Snap_IsAnimatedAndContinuous()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SeekToOffset(TestGeometry.PosOfLine(h.Geometry, 3) + (0.4 * Lh)));
        var before = h.Pos;
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        Assert.Equal(before, h.Pos, 3);
        h.Run(0.05);
        Assert.NotEqual(before, h.Pos);
        Assert.True(Math.Abs(h.Pos - before) < 0.4 * Lh);
    }

    [Fact]
    public void ModeSwitch_WhilePlaying_StopsTheVelocity_AndBackToDisplayRampsUp()
    {
        var h = new EngineHarness();
        h.Play();
        h.Run(3);
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Run(0.5);
        var p = h.Pos;
        Assert.Equal(TransportState.Playing, h.Status.Transport);
        h.Run(0.5);
        Assert.Equal(p, h.Pos, 9); // no continuous motion in Line scrolling

        h.Apply(EngineCommand.SetMode(ScrollMode.Display));
        h.Run(0.2);
        var rising = h.Pos - p;
        Assert.True(rising > 0 && rising < h.Speed(4.5) * 0.2, "ramps up from rest over RampStart");
        h.Run(1);
        Assert.True(h.Pos > p + (h.Speed(4.5) * 0.8));
    }

    [Fact]
    public void SetMode_ToTheCurrentMode_IsANoOp()
    {
        var h = new EngineHarness();
        h.Play();
        h.Run(2);
        var rate = h.Status.EffectiveSpeed;
        h.Apply(EngineCommand.SetMode(ScrollMode.Display));
        h.Run(0.1);
        Assert.Equal(rate, h.Status.EffectiveSpeed, 1);
    }

    [Fact]
    public void LineMode_PauseAtSections_StopsOnTheSectionLine()
    {
        var geometry = TestGeometry.Create(lines: 100, sectionParagraphs: [2]);
        var settings = PrompterSettings.Default with { Scroll = PrompterSettings.Default.Scroll with { PauseAtSections = true } };
        var h = new EngineHarness(geometry, settings: settings);
        h.SeekToLine(8);
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Apply(EngineCommand.SetSpeed(10));
        h.Play();
        h.Run(4);
        Assert.Equal(TestGeometry.PosOfLine(geometry, 10), h.Pos, 9);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
    }

    [Fact]
    public void LineMode_PlayingToTheEnd_ReachesAtEnd()
    {
        var h = new EngineHarness();
        h.SeekToLine(97);
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Apply(EngineCommand.SetSpeed(10));
        h.Play();
        h.Run(6);
        Assert.Equal(h.Pmax, h.Pos, 9);
        Assert.Equal(TransportState.AtEnd, h.Status.Transport);
    }

    [Fact]
    public void LineMode_ManualSteps_StillWork()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Apply(EngineCommand.StepLines(3));
        h.Run(0.3);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 3), h.Pos, 9);
        h.Apply(EngineCommand.StepParagraphs(1));
        h.Run(0.4);
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 5), h.Pos, 9);
    }

    [Fact]
    public void DefaultMode_ComesFromSettings()
    {
        var settings = PrompterSettings.Default with { Scroll = PrompterSettings.Default.Scroll with { DefaultMode = ScrollMode.Line, StartSpeed = 6 } };
        var h = new EngineHarness(settings: settings);
        Assert.Equal(ScrollMode.Line, h.Status.Mode);
        Assert.Equal(6.0, h.Status.SetSpeed);
    }
}

public class EngineAxisTests
{
    [Fact]
    public void Axis_OverridesTheSetSpeed_ImmediatelyAndWhilePaused()
    {
        var h = new EngineHarness();
        h.SeekToLine(10);
        var p0 = h.Pos;
        h.Apply(EngineCommand.SetAxisSpeed(0, 5));
        Assert.True(h.Status.IsAxisActive);
        h.Run(1);
        Assert.Equal(p0 + h.Speed(5), h.Pos, 3); // no ramp: full speed from the first tick
        Assert.Equal(5.0, h.Status.EffectiveSpeed, 1);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
    }

    [Fact]
    public void Axis_WhilePlaying_ReplacesTheSetSpeed()
    {
        var h = new EngineHarness();
        h.SeekToLine(5);
        h.Play();
        h.Run(3);
        var p0 = h.Pos;
        h.Apply(EngineCommand.SetAxisSpeed(0, 8));
        h.Run(1);
        Assert.Equal(p0 + h.Speed(8), h.Pos, 2);
        Assert.Equal(8.0, h.Status.EffectiveSpeed, 1);
        Assert.Equal(4.5, h.Status.SetSpeed);
    }

    [Fact]
    public void Axis_Release_RampsDownOverRampStop_WhenPaused()
    {
        var h = new EngineHarness();
        h.SeekToLine(10);
        h.Apply(EngineCommand.SetAxisSpeed(0, 6));
        h.Run(1);
        var p0 = h.Pos;
        h.Apply(EngineCommand.SetAxisSpeed(0, 0));
        Assert.False(h.Status.IsAxisActive);
        h.Run(0.2);
        Assert.Equal(p0 + (0.5 * h.Speed(6) * 0.2), h.Pos, 3);
        var stopped = h.Pos;
        h.Run(1);
        Assert.Equal(stopped, h.Pos, 9);
    }

    [Fact]
    public void Axis_Release_RampsBackToTheTransportTarget_WhenPlaying()
    {
        var h = new EngineHarness();
        h.SeekToLine(5);
        h.Play();
        h.Run(3);
        h.Apply(EngineCommand.SetAxisSpeed(0, 9));
        h.Run(1);
        var p0 = h.Pos;
        h.Apply(EngineCommand.SetAxisSpeed(0, 0));
        h.Run(0.2);
        Assert.Equal(p0 + (0.2 * (h.Speed(9) + h.Speed(4.5)) / 2), h.Pos, 3);
        var before = h.Pos;
        h.Run(1);
        Assert.Equal(before + h.Speed(4.5), h.Pos, 2);
        Assert.Equal(TransportState.Playing, h.Status.Transport);
    }

    [Fact]
    public void Axis_Slots_AreSummed_AndClamped()
    {
        var h = new EngineHarness();
        h.SeekToLine(20);
        var p0 = h.Pos;
        h.Apply(EngineCommand.SetAxisSpeed(0, 6), EngineCommand.SetAxisSpeed(1, -2));
        h.Run(1);
        Assert.Equal(p0 + h.Speed(4), h.Pos, 3);
        Assert.Equal(4.0, h.Status.EffectiveSpeed, 1);

        h.Apply(EngineCommand.SetAxisSpeed(0, 8), EngineCommand.SetAxisSpeed(1, 8));
        h.Run(0.1);
        Assert.Equal(10.0, h.Status.EffectiveSpeed, 1);
    }

    [Fact]
    public void Axis_OutOfRangeSlots_AreIgnored()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetAxisSpeed(8, 5), EngineCommand.SetAxisSpeed(-1, 5), EngineCommand.SetAxisSpeed(99, 5));
        h.Run(1);
        Assert.False(h.Status.IsAxisActive);
        Assert.Equal(h.Pmin, h.Pos, 9);
    }

    [Fact]
    public void Axis_Negative_FromTheMiddle_ScrollsBackAndStopsAtPmin()
    {
        var h = new EngineHarness();
        h.SeekToLine(1);
        h.Apply(EngineCommand.SetAxisSpeed(0, -10));
        h.Run(2);
        Assert.Equal(h.Pmin, h.Pos, 9);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
    }

    [Fact]
    public void Axis_Forward_ReachesTheEnd()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SeekToOffset(h.Pmax - 100));
        h.Apply(EngineCommand.SetAxisSpeed(0, 10));
        h.Run(2);
        Assert.Equal(h.Pmax, h.Pos, 9);
        Assert.Equal(TransportState.AtEnd, h.Status.Transport);
    }

    [Fact]
    public void FailSafeStop_RampsToZeroOver0Point3Seconds_ThenPauses()
    {
        var h = new EngineHarness();
        h.SeekToLine(5);
        h.Play();
        h.Run(3);
        var p0 = h.Pos;
        var v = h.Speed(4.5);

        h.Apply(EngineCommand.FailSafeStop());
        h.Run(0.15);
        Assert.Equal(TransportState.Playing, h.Status.Transport); // still ramping
        h.Run(0.16);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        Assert.Equal(p0 + (0.5 * v * 0.3), h.Pos, 2);
        Assert.Equal(0.0, h.Status.EffectiveSpeed);

        var stopped = h.Pos;
        h.Run(1);
        Assert.Equal(stopped, h.Pos, 9);

        h.Play(); // the operator can start again
        h.Run(1);
        Assert.True(h.Pos > stopped);
    }

    [Fact]
    public void FailSafeStop_ClearsAxisSlots_AndRampsFromTheAxisSpeed()
    {
        var h = new EngineHarness();
        h.SeekToLine(10);
        h.Apply(EngineCommand.SetAxisSpeed(0, 10));
        h.Run(1);
        var p0 = h.Pos;
        h.Apply(EngineCommand.FailSafeStop());
        Assert.False(h.Status.IsAxisActive);
        h.Run(0.3);
        Assert.Equal(p0 + (0.5 * h.Speed(10) * 0.3), h.Pos, 2);
        var stopped = h.Pos;
        h.Run(1);
        Assert.Equal(stopped, h.Pos, 9);
    }

    [Fact]
    public void FailSafeStop_InLineMode_StopsTheAutoStep()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetMode(ScrollMode.Line));
        h.Apply(EngineCommand.SetSpeed(10));
        h.Play();
        h.Run(1.0);
        h.Apply(EngineCommand.FailSafeStop());
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        h.Run(0.5);
        var p = h.Pos;
        h.Run(5);
        Assert.Equal(p, h.Pos, 9);
    }

    [Fact]
    public void FailSafeStop_WhilePaused_IsHarmless()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.FailSafeStop());
        h.Run(1);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        Assert.Equal(h.Pmin, h.Pos, 9);
    }
}
