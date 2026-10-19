namespace IluPrompter.Core.Tests;

public class EngineMotionTests
{
    private const double Lh = TestGeometry.LineHeight;

    [Fact]
    public void StartsAtPminPaused()
    {
        var h = new EngineHarness();
        Assert.Equal(h.Pmin, h.Pos, 9);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        Assert.True(h.Status.IsAtStart);
        Assert.Equal(4.5, h.Status.SetSpeed);
        Assert.Equal(0, h.Status.LineIndex);
        Assert.Equal(100, h.Status.LineCount);
    }

    [Fact]
    public void BeforeAnyLayout_UsesEmptyGeometryAndDefaultViewport()
    {
        var engine = new ScrollEngine(new FakeSettings());
        Assert.Null(engine.Geometry);
        Assert.Equal(new ViewportMetrics(1600, 952, 314), engine.Viewport);
        Assert.Equal(-314, engine.ReadPosition(), 9);
        Assert.Equal(-1, engine.Status.LineIndex);
    }

    [Fact]
    public void PlayRamp_ReachesSetSpeedAt0Point4Seconds()
    {
        var h = new EngineHarness();
        var start = h.Pos;
        var v = h.Speed(4.5);
        h.Play();
        Assert.Equal(TransportState.Playing, h.Status.Transport);

        h.Run(0.2);
        Assert.Equal(start + (v * 0.2 * 0.2 / (2 * 0.4)), h.Pos, 3); // linear ramp: v t^2 / (2 T)

        h.Run(0.2);
        Assert.Equal(start + (0.5 * v * 0.4), h.Pos, 3);
        Assert.Equal(4.5, h.Status.EffectiveSpeed, 1);

        h.Run(1.0);
        Assert.Equal(start + (0.5 * v * 0.4) + v, h.Pos, 3);
    }

    [Fact]
    public void DistanceOverTenSeconds_IsTheIntegralOfVelocity()
    {
        var h = new EngineHarness();
        var start = h.Pos;
        var v = h.Speed(4.5);
        h.Play();
        h.Run(10.0);
        Assert.Equal(start + (v * (10.0 - 0.2)), h.Pos, 2);
    }

    [Fact]
    public void PauseRamp_StopsOver0Point2Seconds_AndRemembersSpeed()
    {
        var h = new EngineHarness();
        var v = h.Speed(4.5);
        h.Play();
        h.Run(5);
        var p0 = h.Pos;

        h.Pause();
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        h.Run(0.1);
        Assert.Equal(p0 + (v * 0.1) - (v * 0.1 * 0.1 / (2 * 0.2)), h.Pos, 3);
        h.Run(0.1);
        Assert.Equal(p0 + (0.5 * v * 0.2), h.Pos, 3);

        var stopped = h.Pos;
        h.Run(2);
        Assert.Equal(stopped, h.Pos, 9);
        Assert.Equal(4.5, h.Status.SetSpeed);

        h.Play(); // resumes at the remembered speed
        h.Run(2);
        Assert.True(h.Pos > stopped + v);
    }

    [Theory]
    [InlineData(Acceleration.Gentle, 0.6)]
    [InlineData(Acceleration.Normal, 0.3)]
    [InlineData(Acceleration.Quick, 0.15)]
    public void SpeedChange_RampsOverTheAccelerationTime(Acceleration acceleration, double duration)
    {
        var settings = PrompterSettings.Default with { Scroll = PrompterSettings.Default.Scroll with { Acceleration = acceleration } };
        var h = new EngineHarness(settings: settings);
        h.Play();
        h.Run(2.0);
        var lpmA = SpeedCurve.ToLinesPerMinute(4.5, settings.Scroll.Curve);
        var lpmB = SpeedCurve.ToLinesPerMinute(7, settings.Scroll.Curve);

        h.Apply(EngineCommand.SetSpeed(7));
        var p0 = h.Pos;
        h.Run(duration / 2);
        Assert.Equal((lpmA + lpmB) / 2, h.Status.LinesPerMinute, 0.06); // linear in velocity

        h.Run(duration / 2);
        Assert.Equal(7.0, h.Status.EffectiveSpeed, 1);
        var expected = (lpmA + lpmB) / 2 / 60.0 * Lh * duration;
        Assert.Equal(p0 + expected, h.Pos, 2);

        var before = h.Pos;
        h.Run(1);
        Assert.Equal(before + h.Speed(7), h.Pos, 2);
    }

    [Fact]
    public void StopRamp_UsesRampStop_NotRampChange_WhenSpeedGoesToZero()
    {
        var settings = PrompterSettings.Default with { Scroll = PrompterSettings.Default.Scroll with { Acceleration = Acceleration.Gentle } };
        var h = new EngineHarness(settings: settings);
        h.Play();
        h.Run(2);
        var p0 = h.Pos;
        h.Pause();
        h.Run(0.2);
        Assert.Equal(p0 + (0.5 * h.Speed(4.5) * 0.2), h.Pos, 3);
    }

    [Fact]
    public void StallLongerThanTheClamp_AdvancesOnlyOneTenthOfASecond()
    {
        var h = new EngineHarness();
        h.Play();
        h.Run(2);
        var p0 = h.Pos;
        h.Step(1.0);
        Assert.Equal(p0 + (h.Speed(4.5) * 0.1), h.Pos, 3);
    }

    [Fact]
    public void DuplicateAndOlderTimestamps_AreIgnored()
    {
        var h = new EngineHarness();
        h.Play();
        h.Run(1);
        var p = h.Pos;

        h.Engine.Post(EngineCommand.Pause());
        h.TickDuplicate();
        Assert.Equal(TransportState.Playing, h.Status.Transport); // not applied yet
        Assert.Equal(p, h.Pos, 12);

        h.Engine.Tick(h.Timestamp(h.Now - 0.5)); // older than the last tick
        Assert.Equal(TransportState.Playing, h.Status.Transport);

        h.Step(0.01);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
    }

    [Fact]
    public void SetSpeed_IsClampedToTheConfiguredRange()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetSpeed(50));
        Assert.Equal(10.0, h.Status.SetSpeed);
        h.Apply(EngineCommand.SetSpeed(-50));
        Assert.Equal(-10.0, h.Status.SetSpeed);
        h.Apply(EngineCommand.SetSpeed(double.NaN));
        Assert.Equal(-10.0, h.Status.SetSpeed);
    }

    [Fact]
    public void NudgeAndReset()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.NudgeSpeed(0.5));
        Assert.Equal(5.0, h.Status.SetSpeed);
        h.Apply(EngineCommand.NudgeSpeed(0.1), EngineCommand.NudgeSpeed(0.1), EngineCommand.NudgeSpeed(0.1));
        Assert.Equal(5.3, h.Status.SetSpeed, 9);
        h.Apply(EngineCommand.NudgeSpeed(100));
        Assert.Equal(10.0, h.Status.SetSpeed);
        h.Apply(EngineCommand.ResetSpeed());
        Assert.Equal(4.5, h.Status.SetSpeed);
    }

    [Fact]
    public void Nudge_CanCrossZero_AndReverseTheDirection()
    {
        var h = new EngineHarness();
        h.SeekToLine(50);
        h.Apply(EngineCommand.SetSpeed(0.3));
        h.Apply(EngineCommand.NudgeSpeed(-0.5));
        Assert.Equal(-0.2, h.Status.SetSpeed, 9);
        h.Play();
        var p0 = h.Pos;
        h.Run(5);
        Assert.True(h.Pos < p0, "negative speed scrolls backwards");
    }

    [Fact]
    public void Reverse_ReachingPmin_ClampsAndPauses()
    {
        var h = new EngineHarness();
        h.SeekToLine(1);
        h.Apply(EngineCommand.SetSpeed(-4.5));
        h.Play();
        h.Run(10);
        Assert.Equal(h.Pmin, h.Pos, 9);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        Assert.Equal(-4.5, h.Status.SetSpeed);
        Assert.True(h.Status.IsAtStart);
    }

    [Fact]
    public void Forward_ReachingPmax_ClampsAndSetsAtEnd_KeepingTheSpeed()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetSpeed(10));
        h.Apply(EngineCommand.SeekToOffset(h.Pmax - 200));
        h.Play();
        h.Run(5);
        Assert.Equal(h.Pmax, h.Pos, 9);
        Assert.Equal(TransportState.AtEnd, h.Status.Transport);
        Assert.True(h.Status.IsAtEnd);
        Assert.Equal(10.0, h.Status.SetSpeed);
        Assert.Equal(0.0, h.Status.EffectiveSpeed);

        h.Run(1);
        Assert.Equal(h.Pmax, h.Pos, 9);
    }

    [Fact]
    public void AtEnd_RefusesPlayAndToggle_UntilThePositionMovesBack()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.JumpToEnd());
        Assert.Equal(TransportState.AtEnd, h.Status.Transport);
        Assert.Equal(h.Pmax, h.Pos, 9);

        h.Apply(EngineCommand.Play());
        Assert.Equal(TransportState.AtEnd, h.Status.Transport);
        h.Apply(EngineCommand.TogglePlayPause());
        Assert.Equal(TransportState.AtEnd, h.Status.Transport);
        h.Run(1);
        Assert.Equal(h.Pmax, h.Pos, 9);

        h.Apply(EngineCommand.StepLines(-1));
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        h.Apply(EngineCommand.Play());
        Assert.Equal(TransportState.Playing, h.Status.Transport);
    }

    [Theory]
    [InlineData("step")]
    [InlineData("paragraph")]
    [InlineData("jump")]
    [InlineData("pixels")]
    [InlineData("seek")]
    [InlineData("axis")]
    public void AtEnd_AnyBackwardMove_Pauses(string how)
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.JumpToEnd());
        switch (how)
        {
            case "step": h.Apply(EngineCommand.StepLines(-1)); break;
            case "paragraph": h.Apply(EngineCommand.StepParagraphs(-1)); break;
            case "jump": h.Apply(EngineCommand.JumpToStart()); break;
            case "pixels": h.Apply(EngineCommand.ScrollPixels(-5)); break;
            case "seek": h.Apply(EngineCommand.SeekToOffset(h.Pmax - 10)); break;
            case "axis": h.Apply(EngineCommand.SetAxisSpeed(0, -3)); h.Run(0.1); break;
        }

        Assert.Equal(TransportState.Paused, h.Status.Transport);
    }

    [Fact]
    public void JumpToStartAndEnd()
    {
        var h = new EngineHarness();
        h.Play();
        h.Run(3);
        h.Apply(EngineCommand.JumpToStart());
        Assert.Equal(h.Pmin, h.Pos, 9);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        h.Run(1);
        Assert.Equal(h.Pmin, h.Pos, 9); // velocity was cancelled, no drift

        h.Apply(EngineCommand.JumpToEnd());
        Assert.Equal(h.Pmax, h.Pos, 9);
        Assert.Equal(TransportState.AtEnd, h.Status.Transport);
    }

    [Fact]
    public void SeekToAnchor_CentresTheAnchorLine_AndPauses()
    {
        var h = new EngineHarness();
        h.Play();
        h.Run(1);
        h.Apply(EngineCommand.SeekToAnchor(new DocumentAnchor(3, 80))); // paragraph 3, third line => line 17
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 17), h.Pos, 9);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        Assert.Equal(17, h.Status.LineIndex);
        Assert.Equal(new DocumentAnchor(3, 80), h.Status.ReadingAnchor);
    }

    [Fact]
    public void SeekToOffset_AndScrollPixels_KeepThePlayState()
    {
        var h = new EngineHarness();
        h.Play();
        h.Apply(EngineCommand.SeekToOffset(1000));
        Assert.Equal(1000, h.Pos, 6);
        Assert.Equal(TransportState.Playing, h.Status.Transport);

        h.Apply(EngineCommand.ScrollPixels(50));
        Assert.Equal(1050, h.Pos, 6);
        h.Apply(EngineCommand.ScrollPixels(-70));
        Assert.Equal(980, h.Pos, 6);
        Assert.Equal(TransportState.Playing, h.Status.Transport);

        h.Apply(EngineCommand.Pause());
        h.Apply(EngineCommand.ScrollPixels(25));
        Assert.Equal(TransportState.Paused, h.Status.Transport);
    }

    [Fact]
    public void SeekAndScroll_AreClamped_AndHitTheEnds()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SeekToOffset(-99999));
        Assert.Equal(h.Pmin, h.Pos, 9);
        h.Apply(EngineCommand.ScrollPixels(-5));
        Assert.Equal(h.Pmin, h.Pos, 9);

        h.Apply(EngineCommand.SeekToOffset(1e9));
        Assert.Equal(h.Pmax, h.Pos, 9);
        Assert.Equal(TransportState.AtEnd, h.Status.Transport);
    }

    [Fact]
    public void ScrollPixels_BackwardToStart_PausesAPlayingPrompt()
    {
        var h = new EngineHarness();
        h.SeekToLine(2);
        h.Play();
        h.Apply(EngineCommand.ScrollPixels(-99999));
        Assert.Equal(h.Pmin, h.Pos, 9);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
    }

    [Fact]
    public void PauseAtSections_StopsExactlyOnTheSectionLine_ThenContinues()
    {
        var geometry = TestGeometry.Create(lines: 100, sectionParagraphs: [2, 6]);
        var settings = PrompterSettings.Default with { Scroll = PrompterSettings.Default.Scroll with { PauseAtSections = true } };
        var h = new EngineHarness(geometry, settings: settings);
        h.Apply(EngineCommand.SetSpeed(10));
        h.SeekToLine(5);
        h.Play();

        RunUntilPaused(h);
        Assert.Equal(TestGeometry.PosOfLine(geometry, 10), h.Pos, 9);
        Assert.Equal(TransportState.Paused, h.Status.Transport);
        Assert.Equal(0, h.Status.SectionIndex);

        h.Play(); // starts exactly on the section line: must not re-trigger
        h.Run(1);
        Assert.True(h.Pos > TestGeometry.PosOfLine(geometry, 10) + 50);
        Assert.Equal(TransportState.Playing, h.Status.Transport);

        RunUntilPaused(h);
        Assert.Equal(TestGeometry.PosOfLine(geometry, 30), h.Pos, 9);
    }

    [Fact]
    public void PauseAtSections_Off_PlaysStraightThrough()
    {
        var geometry = TestGeometry.Create(lines: 100, sectionParagraphs: [2]);
        var h = new EngineHarness(geometry);
        h.Apply(EngineCommand.SetSpeed(10));
        h.SeekToLine(8);
        h.Play();
        h.Run(4);
        Assert.Equal(TransportState.Playing, h.Status.Transport);
        Assert.True(h.Pos > TestGeometry.PosOfLine(geometry, 10));
    }

    [Fact]
    public void ChangingTheCurveInSettings_WhilePlaying_RampsInsteadOfJumping()
    {
        var h = new EngineHarness();
        h.Play();
        h.Run(3);
        var before = h.Status.LinesPerMinute;

        h.Settings.Update(s => s with { Scroll = s.Scroll with { Curve = new SpeedCurveSettings { LinesPerMinuteAtMax = 40 } } });
        h.Run(0.1); // the next tick notices the new target and starts a RampChange
        Assert.True(h.Status.LinesPerMinute < before && h.Status.LinesPerMinute > before / 2, "mid-ramp");
        h.Run(0.3);
        Assert.Equal(before / 2, h.Status.LinesPerMinute, 0.1);
        Assert.Equal(4.5, h.Status.EffectiveSpeed, 1); // still the same UI speed, now half the lines per minute
    }

    [Fact]
    public void SeekToAnchor_OutsideTheScript_ClampsToTheNearestLine()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SeekToAnchor(new DocumentAnchor(9999, 5)));
        Assert.Equal(TestGeometry.PosOfLine(h.Geometry, 99), h.Pos, 9);
        h.Apply(EngineCommand.SeekToAnchor(new DocumentAnchor(-4, -9)));
        Assert.True(double.IsFinite(h.Pos));
        Assert.InRange(h.Pos, h.Pmin, h.Pmax);
    }

    [Fact]
    public void ConfiguredSpeedRange_IsRespected()
    {
        var settings = PrompterSettings.Default with { Scroll = PrompterSettings.Default.Scroll with { MinSpeed = -6, MaxSpeed = 8, StartSpeed = 3 } };
        var h = new EngineHarness(settings: settings);
        Assert.Equal(3.0, h.Status.SetSpeed);
        h.Apply(EngineCommand.SetSpeed(20));
        Assert.Equal(8.0, h.Status.SetSpeed);
        h.Apply(EngineCommand.SetSpeed(-20));
        Assert.Equal(-6.0, h.Status.SetSpeed);
        h.Apply(EngineCommand.ResetSpeed());
        Assert.Equal(3.0, h.Status.SetSpeed);
    }

    private static void RunUntilPaused(EngineHarness h)
    {
        for (var i = 0; i < 3000 && h.Status.Transport == TransportState.Playing; i++)
        {
            h.Run(1.0 / 60);
        }
    }
}
