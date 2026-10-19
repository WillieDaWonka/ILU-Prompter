namespace IluPrompter.Core.Tests;

public class InputRouterKeyboardTests
{
    [Fact]
    public void Transport_Space_TogglesPlayPause()
    {
        var h = new RouterHarness();
        Assert.True(h.Key("Space"));
        Assert.Equal([EngineCommand.TogglePlayPause()], h.Posted);
    }

    [Fact]
    public void Typing_OnlyGlobalBindingsApply()
    {
        var h = new RouterHarness();
        h.KeyMode.Mode = KeyMode.Typing;

        Assert.False(h.Key("Space"));
        Assert.False(h.Key("Escape"));
        Assert.False(h.Key("Down"));
        Assert.Empty(h.Posted);

        Assert.True(h.Key("F5")); // Global
        Assert.Equal([EngineCommand.TogglePlayPause()], h.Posted);
        Assert.True(h.Key("F8"));
        Assert.Equal(1, h.Output.SetBlankCalls);
    }

    [Fact]
    public void TextInputSource_ForcesTypingContext_EvenInTransportMode()
    {
        var h = new RouterHarness();
        Assert.Equal(KeyMode.Transport, h.KeyMode.Mode);

        Assert.False(h.Key("Space", device: AppSources.TextInput));
        Assert.False(h.Key("B", device: AppSources.TextInput));
        Assert.Empty(h.Posted);
        Assert.True(h.Key("F5", device: AppSources.TextInput));
        Assert.Single(h.Posted);
    }

    [Fact]
    public void KeyModeChange_TakesEffectImmediately()
    {
        var h = new RouterHarness();
        h.KeyMode.Mode = KeyMode.Typing;
        Assert.False(h.Key("Home"));
        h.KeyMode.Mode = KeyMode.Transport;
        Assert.True(h.Key("Home"));
        Assert.Equal([EngineCommand.JumpToStart()], h.Posted);
    }

    [Fact]
    public void Down_IsSpeedInDisplayScrolling_AndLineStepInLineScrolling()
    {
        var h = new RouterHarness();
        Assert.True(h.Key("Down"));
        Assert.True(h.Key("Up"));
        Assert.True(h.Key("Shift+Down"));
        Assert.True(h.Key("Shift+Up"));
        Assert.Equal(
            [
                EngineCommand.NudgeSpeed(0.5),
                EngineCommand.NudgeSpeed(-0.5),
                EngineCommand.NudgeSpeed(0.1),
                EngineCommand.NudgeSpeed(-0.1),
            ],
            h.Posted);

        h.Engine.Clear();
        h.SetMode(ScrollMode.Line);
        Assert.True(h.Key("Down"));
        Assert.True(h.Key("Up"));
        Assert.True(h.Key("Shift+Down"));
        Assert.True(h.Key("Shift+Up"));
        Assert.Equal(
            [
                EngineCommand.StepLines(1),
                EngineCommand.StepLines(-1),
                EngineCommand.StepLines(1),
                EngineCommand.StepLines(-1),
            ],
            h.Posted);
    }

    [Fact]
    public void SpeedReset_IsDisabledInLineScrolling_ButStillHandled()
    {
        var h = new RouterHarness();
        Assert.True(h.Key("D0"));
        Assert.Equal([EngineCommand.ResetSpeed()], h.Posted);

        h.Engine.Clear();
        h.SetMode(ScrollMode.Line);
        Assert.True(h.Key("D0")); // bound (to None): handled, nothing to do
        Assert.True(h.Key("NumPad0"));
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void SpeedStepsComeFromSettings()
    {
        var settings = PrompterSettings.Default with { Scroll = PrompterSettings.Default.Scroll with { SpeedStep = 0.25, FineSpeedStep = 0.05 } };
        var h = new RouterHarness(settings);
        h.Key("Down");
        h.Key("Shift+Up");
        Assert.Equal([EngineCommand.NudgeSpeed(0.25), EngineCommand.NudgeSpeed(-0.05)], h.Posted);
    }

    [Fact]
    public void Repeats_ActOnlyForSpeedLineAndParagraphCommands()
    {
        var h = new RouterHarness();
        Assert.True(h.Key("Down", repeat: true));
        Assert.True(h.Key("Shift+Up", repeat: true));
        Assert.True(h.Key("Right", repeat: true));
        Assert.True(h.Key("PageDown", repeat: true));
        Assert.Equal(
            [
                EngineCommand.NudgeSpeed(0.5),
                EngineCommand.NudgeSpeed(-0.1),
                EngineCommand.StepLines(1),
                EngineCommand.StepParagraphs(1),
            ],
            h.Posted);

        h.Engine.Clear();
        Assert.True(h.Key("Space", repeat: true));      // handled, but a held key must not flip play/pause
        Assert.True(h.Key("Home", repeat: true));
        Assert.True(h.Key("B", repeat: true));
        Assert.True(h.Key("Ctrl+Down", repeat: true));
        Assert.True(h.Key("Escape", repeat: true));
        Assert.True(h.Key("F5", repeat: true));
        Assert.Empty(h.Posted);
        Assert.Equal(0, h.Output.SetBlankCalls);
    }

    [Fact]
    public void KeyRelease_DoesNothing_ButIsHandledWhenBound()
    {
        var h = new RouterHarness();
        Assert.True(h.Key("Space", pressed: false)); // so the focused control does not "click" on the release
        Assert.False(h.Key("Q", pressed: false));
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void UnboundKey_IsNotHandled()
    {
        var h = new RouterHarness();
        Assert.False(h.Key("Q"));
        Assert.False(h.Key("Ctrl+Q"));
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void EveryDefaultTransportKey_HasAWorkingBinding()
    {
        var h = new RouterHarness();
        foreach (var chord in DefaultBindings.KeyboardTransport.Keys)
        {
            Assert.True(h.Key(chord), chord);
        }

        foreach (var chord in DefaultBindings.KeyboardGlobal.Keys)
        {
            Assert.True(h.Key(chord), chord);
        }
    }

    [Fact]
    public void ChangedBindings_TakeEffectWithoutRestart()
    {
        var h = new RouterHarness();
        h.Configure(i => i with
        {
            Keyboard = i.Keyboard with
            {
                Transport = new Dictionary<string, BindingAction> { ["X"] = new(PrompterCommand.JumpToEnd, PrompterCommand.JumpToStart) },
            },
        });

        Assert.False(h.Key("Space")); // no longer bound
        Assert.True(h.Key("X"));
        h.SetMode(ScrollMode.Line);
        Assert.True(h.Key("X"));
        Assert.Equal([EngineCommand.JumpToEnd(), EngineCommand.JumpToStart()], h.Posted);
    }

    [Fact]
    public void Global_WinsOverTransport_ForTheSameChord()
    {
        var h = new RouterHarness();
        h.Configure(i => i with
        {
            Keyboard = i.Keyboard with
            {
                Global = new Dictionary<string, BindingAction> { ["Space"] = new(PrompterCommand.JumpToEnd) },
            },
        });

        h.Key("Space");
        Assert.Equal([EngineCommand.JumpToEnd()], h.Posted);
        h.KeyMode.Mode = KeyMode.Typing;
        h.Key("Space");
        Assert.Equal(2, h.Posted.Count); // Global also applies while typing
    }

    [Fact]
    public void KeyboardIsAccepted_WhateverControllerIsSelected()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        Assert.True(h.Key("Space"));
        h.Select(ControllerKind.JogShuttle);
        Assert.True(h.Key("Space"));
        Assert.Equal(2, h.Posted.Count);
    }
}

public class InputRouterExecuteTests
{
    [Theory]
    [InlineData(PrompterCommand.PlayPause, EngineCommandKind.TogglePlayPause)]
    [InlineData(PrompterCommand.Play, EngineCommandKind.Play)]
    [InlineData(PrompterCommand.Pause, EngineCommandKind.Pause)]
    [InlineData(PrompterCommand.SpeedReset, EngineCommandKind.ResetSpeed)]
    [InlineData(PrompterCommand.JumpToStart, EngineCommandKind.JumpToStart)]
    [InlineData(PrompterCommand.JumpToEnd, EngineCommandKind.JumpToEnd)]
    [InlineData(PrompterCommand.ToggleScrollMode, EngineCommandKind.ToggleMode)]
    public void SimpleCommands_PostTheMatchingEngineCommand(PrompterCommand command, EngineCommandKind expected)
    {
        var h = new RouterHarness();
        Assert.True(h.Router.Execute(command));
        Assert.Equal([new EngineCommand(expected)], h.Posted);
    }

    [Fact]
    public void SteppingCommands()
    {
        var h = new RouterHarness();
        foreach (var c in new[]
        {
            PrompterCommand.LineNext, PrompterCommand.LinePrevious, PrompterCommand.ParagraphNext, PrompterCommand.ParagraphPrevious,
            PrompterCommand.SectionNext, PrompterCommand.SectionPrevious,
        })
        {
            Assert.True(h.Router.Execute(c));
        }

        Assert.Equal(
            [
                EngineCommand.StepLines(1), EngineCommand.StepLines(-1),
                EngineCommand.StepParagraphs(1), EngineCommand.StepParagraphs(-1),
                EngineCommand.StepSections(1), EngineCommand.StepSections(-1),
            ],
            h.Posted);
    }

    [Fact]
    public void SpeedCommands_UseTheConfiguredSteps()
    {
        var h = new RouterHarness();
        h.Router.Execute(PrompterCommand.SpeedUp);
        h.Router.Execute(PrompterCommand.SpeedDown);
        h.Router.Execute(PrompterCommand.SpeedUpFine);
        h.Router.Execute(PrompterCommand.SpeedDownFine);
        Assert.Equal(
            [EngineCommand.NudgeSpeed(0.5), EngineCommand.NudgeSpeed(-0.5), EngineCommand.NudgeSpeed(0.1), EngineCommand.NudgeSpeed(-0.1)],
            h.Posted);
    }

    [Fact]
    public void ModeCommands()
    {
        var h = new RouterHarness();
        h.Router.Execute(PrompterCommand.SetDisplayScrolling);
        h.Router.Execute(PrompterCommand.SetLineScrolling);
        Assert.Equal([EngineCommand.SetMode(ScrollMode.Display), EngineCommand.SetMode(ScrollMode.Line)], h.Posted);
    }

    [Fact]
    public void PlayAtTheEnd_ReturnsFalse_AndPostsNothing()
    {
        var h = new RouterHarness();
        h.Engine.Status = new EngineStatus { Transport = TransportState.AtEnd };
        Assert.False(h.Router.Execute(PrompterCommand.PlayPause));
        Assert.False(h.Router.Execute(PrompterCommand.Play));
        Assert.Empty(h.Posted);
        Assert.True(h.Router.Execute(PrompterCommand.Pause));
        Assert.True(h.Router.Execute(PrompterCommand.JumpToStart));
    }

    [Fact]
    public void None_DoesNotApply()
    {
        var h = new RouterHarness();
        Assert.False(h.Router.Execute(PrompterCommand.None));
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void ToggleBlank_CallsSetBlankWithTheOpposite()
    {
        var h = new RouterHarness();
        Assert.True(h.Router.Execute(PrompterCommand.ToggleBlank));
        Assert.True(h.Output.State.IsBlank);
        Assert.True(h.Router.Execute(PrompterCommand.ToggleBlank));
        Assert.False(h.Output.State.IsBlank);
        Assert.Equal(2, h.Output.SetBlankCalls);
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void ToggleOutput_RaisesTheAppCommand()
    {
        var h = new RouterHarness();
        var raised = new List<PrompterCommand>();
        h.Router.AppCommandRequested += (_, c) => raised.Add(c);
        Assert.True(h.Router.Execute(PrompterCommand.ToggleOutput));
        Assert.Equal([PrompterCommand.ToggleOutput], raised);
        Assert.Empty(h.Posted);

        Assert.True(h.Key("F11"));
        Assert.Equal(2, raised.Count);
    }

    [Fact]
    public void ThrowingEventHandlers_DoNotBreakTheRouter()
    {
        var h = new RouterHarness();
        h.Router.AppCommandRequested += (_, _) => throw new InvalidOperationException("boom");
        h.Router.InputObserved += (_, _) => throw new InvalidOperationException("boom");
        Assert.True(h.Router.Execute(PrompterCommand.ToggleOutput));
        Assert.True(h.Key("Space"));
        Assert.Single(h.Posted);
    }
}

public class InputRouterWheelTests
{
    [Fact]
    public void Wheel_TowardsTheUser_SpeedsUp_AwayFromTheUser_SlowsDown()
    {
        var h = new RouterHarness();
        Assert.True(h.Notch(-1));
        Assert.True(h.Notch(1));
        Assert.True(h.Notch(-0.25)); // high-resolution wheel
        Assert.Equal([EngineCommand.NudgeSpeed(0.5), EngineCommand.NudgeSpeed(-0.5), EngineCommand.NudgeSpeed(0.125)], h.Posted);
    }

    [Fact]
    public void Wheel_Invert_FlipsTheDirection()
    {
        var h = new RouterHarness();
        h.Configure(i => i with { Wheel = i.Wheel with { Wheel = i.Wheel.Wheel with { Invert = true } } });
        h.Notch(-1);
        Assert.Equal([EngineCommand.NudgeSpeed(-0.5)], h.Posted);
    }

    [Fact]
    public void Wheel_SpeedPerUnit_Scales()
    {
        var h = new RouterHarness();
        h.Configure(i => i with { Wheel = i.Wheel with { Wheel = i.Wheel.Wheel with { SpeedPerUnit = 0.2 } } });
        h.Notch(-3);
        Assert.Equal([EngineCommand.NudgeSpeed(0.6000000000000001)], h.Posted);
    }

    [Fact]
    public void Wheel_InLineScrolling_AccumulatesFractionsUntilAWholeLine()
    {
        var h = new RouterHarness();
        h.SetMode(ScrollMode.Line);
        h.Notch(-0.4);
        h.Notch(-0.4);
        Assert.Empty(h.Posted);
        h.Notch(-0.4); // 1.2 notches towards the user
        Assert.Equal([EngineCommand.StepLines(1)], h.Posted);

        h.Notch(-0.8); // 0.2 left over + 0.8 = exactly one more
        Assert.Equal([EngineCommand.StepLines(1), EngineCommand.StepLines(1)], h.Posted);
    }

    [Fact]
    public void Wheel_InLineScrolling_ManyNotchesAtOnce_AndAwayFromTheUser()
    {
        var h = new RouterHarness();
        h.SetMode(ScrollMode.Line);
        h.Notch(-3);
        h.Notch(2);
        Assert.Equal([EngineCommand.StepLines(3), EngineCommand.StepLines(-2)], h.Posted);
    }

    [Fact]
    public void Wheel_Accumulator_CancelsOutWhenTheDirectionReverses()
    {
        var h = new RouterHarness();
        h.SetMode(ScrollMode.Line);
        h.Notch(-0.6);
        h.Notch(0.6);
        h.Notch(-0.6);
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void Wheel_LinesPerUnit_Scales()
    {
        var h = new RouterHarness();
        h.SetMode(ScrollMode.Line);
        h.Configure(i => i with { Wheel = i.Wheel with { Wheel = i.Wheel.Wheel with { LinesPerUnit = 3 } } });
        h.Notch(-1);
        Assert.Equal([EngineCommand.StepLines(3)], h.Posted);
    }

    [Fact]
    public void Wheel_PixelsRole_ScrollsDirectly()
    {
        var h = new RouterHarness();
        h.Configure(i => i with { Wheel = i.Wheel with { Wheel = new RelativeBinding(RelativeRole.Pixels, RelativeRole.Pixels, PixelsPerUnit: 40) } });
        h.Notch(-2);
        h.Notch(1);
        Assert.Equal([EngineCommand.ScrollPixels(80), EngineCommand.ScrollPixels(-40)], h.Posted);
    }

    [Fact]
    public void Wheel_RoleNone_IsNotHandled()
    {
        var h = new RouterHarness();
        h.Configure(i => i with { Wheel = i.Wheel with { Wheel = new RelativeBinding(RelativeRole.None, RelativeRole.None) } });
        Assert.False(h.Notch(-1));
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void WheelButtons_MiddleIsPlayPause_OthersAreUnbound()
    {
        var h = new RouterHarness();
        Assert.True(h.Button(ControllerKind.MouseWheel, ControlIds.MiddleButton, AppSources.App));
        Assert.True(h.Button(ControllerKind.MouseWheel, ControlIds.MiddleButton, AppSources.App, pressed: false));
        Assert.False(h.Button(ControllerKind.MouseWheel, ControlIds.XButton1, AppSources.App));
        Assert.Equal([EngineCommand.TogglePlayPause()], h.Posted);
    }

    [Fact]
    public void AppWheel_IsIgnored_WhenADedicatedMouseIsSet()
    {
        var h = new RouterHarness();
        h.Configure(i => i with { Wheel = i.Wheel with { DedicatedDeviceId = @"\\?\HID#VID_046D" } });
        Assert.False(h.Notch(-1)); // over the preview: ignored to avoid double counting
        Assert.False(h.Button(ControllerKind.MouseWheel, ControlIds.MiddleButton, AppSources.App));
        Assert.Empty(h.Posted);

        Assert.True(h.Notch(-1, device: @"\\?\HID#VID_046D"));
        Assert.True(h.Button(ControllerKind.MouseWheel, ControlIds.MiddleButton, @"\\?\HID#VID_046D"));
        Assert.Equal([EngineCommand.NudgeSpeed(0.5), EngineCommand.TogglePlayPause()], h.Posted);
    }

    [Fact]
    public void RawDeviceWheel_IsIgnored_WithoutADedicatedMouse()
    {
        var h = new RouterHarness();
        Assert.False(h.Notch(-1, device: "some-mouse"));
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void WheelInput_IsIgnored_WhenAnotherControllerIsSelected()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        Assert.False(h.Notch(-1));
        Assert.False(h.Button(ControllerKind.MouseWheel, ControlIds.MiddleButton, AppSources.App));
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void WheelOnAnAxisControl_IsNotHandled()
    {
        var h = new RouterHarness();
        Assert.False(h.Axis(ControllerKind.MouseWheel, ControlIds.Wheel, AppSources.App, 0.5));
    }
}

public class InputRouterControllerTests
{
    private const string Pad = "xinput:0";
    private const string Hid = "hid:shuttle";

    [Fact]
    public void Jog_Clockwise_StepsForwardByLines_InBothModes()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.JogShuttle);
        Assert.True(h.Jog(1));
        Assert.True(h.Jog(-2));
        h.SetMode(ScrollMode.Line);
        Assert.True(h.Jog(3));
        Assert.Equal([EngineCommand.StepLines(1), EngineCommand.StepLines(-2), EngineCommand.StepLines(3)], h.Posted);
    }

    [Fact]
    public void Jog_InvertAndFractions()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.JogShuttle);
        h.Configure(i => i with { Shuttle = i.Shuttle with { Jog = i.Shuttle.Jog with { Invert = true, LinesPerUnit = 0.5 } } });
        h.Jog(1);
        Assert.Empty(h.Posted);
        h.Jog(1);
        Assert.Equal([EngineCommand.StepLines(-1)], h.Posted);
    }

    [Fact]
    public void Jog_SpeedRole_NudgesTheSpeed()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.JogShuttle);
        h.Configure(i => i with { Shuttle = i.Shuttle with { Jog = new RelativeBinding(RelativeRole.Speed, RelativeRole.Speed, SpeedPerUnit: 0.1) } });
        h.Jog(2);
        Assert.Equal([EngineCommand.NudgeSpeed(0.2)], h.Posted);
    }

    [Fact]
    public void ShuttleButtons_AreMapped()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.JogShuttle);
        Assert.True(h.Button(ControllerKind.JogShuttle, ControlIds.ShuttleButton(3), Hid));
        Assert.True(h.Button(ControllerKind.JogShuttle, ControlIds.ShuttleButton(5), Hid));
        Assert.True(h.Button(ControllerKind.JogShuttle, ControlIds.ShuttleButton(1), Hid));
        Assert.True(h.Button(ControllerKind.JogShuttle, ControlIds.ShuttleButton(3), Hid, pressed: false));
        Assert.False(h.Button(ControllerKind.JogShuttle, ControlIds.ShuttleButton(15), Hid));
        Assert.Equal([EngineCommand.TogglePlayPause(), EngineCommand.StepSections(1), EngineCommand.StepSections(-1)], h.Posted);
    }

    [Fact]
    public void ShuttleRing_BecomesAnAxisSpeed()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.JogShuttle);
        Assert.True(h.Axis(ControllerKind.JogShuttle, ControlIds.ShuttleRing, Hid, 7 / 7.0));
        Assert.True(h.Axis(ControllerKind.JogShuttle, ControlIds.ShuttleRing, Hid, -3 / 7.0));
        var expectedForward = AxisCurve.Apply(1.0, DefaultBindings.ShuttleAxes[ControlIds.ShuttleRing]);
        var expectedBack = AxisCurve.Apply(-3 / 7.0, DefaultBindings.ShuttleAxes[ControlIds.ShuttleRing]);
        Assert.Equal([EngineCommand.SetAxisSpeed(0, expectedForward), EngineCommand.SetAxisSpeed(0, expectedBack)], h.Posted);
        Assert.True(expectedBack < 0 && expectedForward == 10.0);
    }

    [Fact]
    public void Xbox_Triggers_MapToSignedAxisSpeeds_PerSlot()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        Assert.True(h.Axis(ControllerKind.Xbox, ControlIds.RightTrigger, Pad, 1.0));
        Assert.True(h.Axis(ControllerKind.Xbox, ControlIds.LeftTrigger, Pad, 1.0));
        Assert.True(h.Axis(ControllerKind.Xbox, ControlIds.RightTrigger, Pad, 0.5));
        Assert.True(h.Axis(ControllerKind.Xbox, ControlIds.RightTrigger, Pad, 0.05)); // inside the deadzone: releases the axis

        var half = AxisCurve.Apply(0.5, DefaultBindings.XboxAxes[ControlIds.RightTrigger]);
        Assert.Equal(
            [
                EngineCommand.SetAxisSpeed(0, 10),
                EngineCommand.SetAxisSpeed(1, -10),
                EngineCommand.SetAxisSpeed(0, half),
                EngineCommand.SetAxisSpeed(0, 0),
            ],
            h.Posted);
    }

    [Fact]
    public void Xbox_LeftStick_IsUnboundByDefault()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        Assert.False(h.Axis(ControllerKind.Xbox, ControlIds.LeftThumbY, Pad, 1.0));
        Assert.False(h.Axis(ControllerKind.Xbox, "Nonsense", Pad, 1.0));
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void Xbox_LeftStick_CanBeRebound()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        h.Configure(i => i with
        {
            Xbox = i.Xbox with
            {
                Axes = new Dictionary<string, AxisBinding> { [ControlIds.LeftThumbY] = new(AxisRole.Speed, Deadzone: 0.0, Exponent: 1.0, MaxSpeed: 8.0, Invert: true) },
            },
        });

        Assert.True(h.Axis(ControllerKind.Xbox, ControlIds.LeftThumbY, Pad, 0.5));
        Assert.Equal([EngineCommand.SetAxisSpeed(0, -4)], h.Posted);
    }

    [Fact]
    public void AxisSlots_AreCappedAtEight_AndFreedOnDisconnect()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        h.Configure(i => i with
        {
            Xbox = i.Xbox with
            {
                Axes = Enumerable.Range(1, 9).ToDictionary(n => "Axis" + n, _ => new AxisBinding(AxisRole.Speed, Deadzone: 0, Exponent: 1, MaxSpeed: 10)),
            },
        });

        for (var n = 1; n <= 8; n++)
        {
            Assert.True(h.Axis(ControllerKind.Xbox, "Axis" + n, Pad, 0.5));
        }

        Assert.False(h.Axis(ControllerKind.Xbox, "Axis9", Pad, 0.5)); // no slot left
        Assert.Equal(Enumerable.Range(0, 8), h.Posted.Select(c => c.Index));

        h.Router.ReportStatus(new DeviceStatus(RouterHarness.Pad, "Pad", DeviceConnection.Disconnected));
        h.Engine.Clear();
        Assert.True(h.Axis(ControllerKind.Xbox, "Axis9", Pad, 0.5));
        Assert.Equal(0, h.Posted.Single().Index); // slots were freed
    }

    [Fact]
    public void Xbox_Buttons_AreMapped_AndDpadRepeats()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        Assert.True(h.Button(ControllerKind.Xbox, ControlIds.A, Pad));
        Assert.True(h.Button(ControllerKind.Xbox, ControlIds.DPadDown, Pad));
        Assert.True(h.Button(ControllerKind.Xbox, ControlIds.DPadDown, Pad, repeat: true));
        Assert.True(h.Button(ControllerKind.Xbox, ControlIds.A, Pad, repeat: true)); // handled, ignored
        Assert.True(h.Button(ControllerKind.Xbox, ControlIds.A, Pad, pressed: false));
        Assert.True(h.Button(ControllerKind.Xbox, ControlIds.View, Pad));
        Assert.False(h.Button(ControllerKind.Xbox, ControlIds.Y, Pad));
        Assert.Equal([EngineCommand.TogglePlayPause(), EngineCommand.StepLines(1), EngineCommand.StepLines(1)], h.Posted);
        Assert.Equal(1, h.Output.SetBlankCalls);
    }

    [Fact]
    public void ControllerInput_FromAnUnselectedKind_IsIgnored()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.JogShuttle);
        Assert.False(h.Button(ControllerKind.Xbox, ControlIds.A, Pad));
        Assert.False(h.Axis(ControllerKind.Xbox, ControlIds.RightTrigger, Pad, 1));
        Assert.DoesNotContain(h.Posted, c => c.Kind == EngineCommandKind.SetAxisSpeed || c.Kind == EngineCommandKind.TogglePlayPause);
    }

    [Fact]
    public void ModeAwareControllerBinding_UsesTheLineAction()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        h.Configure(i => i with
        {
            Xbox = i.Xbox with
            {
                Buttons = new Dictionary<string, BindingAction> { [ControlIds.B] = new(PrompterCommand.SpeedUp, PrompterCommand.LineNext) },
            },
        });

        h.Button(ControllerKind.Xbox, ControlIds.B, Pad);
        h.SetMode(ScrollMode.Line);
        h.Button(ControllerKind.Xbox, ControlIds.B, Pad);
        Assert.Equal([EngineCommand.NudgeSpeed(0.5), EngineCommand.StepLines(1)], h.Posted);
    }

    [Fact]
    public void Disconnect_OfTheSelectedController_FailSafeStops()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        h.Axis(ControllerKind.Xbox, ControlIds.RightTrigger, Pad, 1.0);
        h.Engine.Clear();

        h.Router.ReportStatus(new DeviceStatus(RouterHarness.Pad, "Controller (XBOX 360 For Windows)", DeviceConnection.Disconnected));
        Assert.Equal([EngineCommand.FailSafeStop()], h.Posted);
    }

    [Fact]
    public void Disconnect_OfAnotherDevice_DoesNotStopThePrompt()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        h.Router.ReportStatus(new DeviceStatus(new InputSourceId(ControllerKind.JogShuttle, Hid), "Shuttle", DeviceConnection.Disconnected));
        Assert.Empty(h.Posted);
        h.Router.ReportStatus(new DeviceStatus(RouterHarness.Pad, "Pad", DeviceConnection.NotFound));
        h.Router.ReportStatus(new DeviceStatus(RouterHarness.Pad, "Pad", DeviceConnection.Connected));
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void Disconnect_ClearsTheJogAccumulator()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.JogShuttle);
        h.Configure(i => i with { Shuttle = i.Shuttle with { Jog = i.Shuttle.Jog with { LinesPerUnit = 0.6 } } });
        h.Jog(1);
        Assert.Empty(h.Posted);
        h.Router.ReportStatus(new DeviceStatus(new InputSourceId(ControllerKind.JogShuttle, Hid), "Shuttle", DeviceConnection.Disconnected));
        h.Engine.Clear();
        h.Jog(1); // 0.6 again, not 1.2
        Assert.Empty(h.Posted);
    }

    [Fact]
    public void ConcurrentSubmits_AssignOneSlotPerAxis()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        var threads = Enumerable.Range(0, 4).Select(t => new Thread(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                h.Axis(ControllerKind.Xbox, t % 2 == 0 ? ControlIds.RightTrigger : ControlIds.LeftTrigger, Pad, 0.5 + (i % 2 * 0.1));
            }
        })).ToArray();
        foreach (var t in threads) { t.Start(); }
        foreach (var t in threads) { t.Join(); }

        Assert.Equal(2000, h.Posted.Count);
        var slots = h.Posted.GroupBy(c => c.Index).ToList();
        Assert.Equal(2, slots.Count);
        Assert.All(slots, g => Assert.Equal(1000, g.Count()));
    }
}

public class InputRouterLifecycleTests
{
    [Fact]
    public void Defaults_StartNoProvider_BecauseTheWheelNeedsADedicatedMouse()
    {
        var h = new RouterHarness();
        Assert.Empty(h.Wheel.StartCalls);
        Assert.Empty(h.Xbox.StartCalls);
        Assert.Empty(h.Shuttle.StartCalls);
    }

    [Fact]
    public void SelectingXbox_StartsOnlyTheXboxProvider_WithTheUserIndex()
    {
        var h = new RouterHarness();
        h.Configure(i => i with { SelectedController = ControllerKind.Xbox, Xbox = i.Xbox with { UserIndex = 2 } });
        var call = Assert.Single(h.Xbox.StartCalls);
        Assert.Equal("xinput:2", call.DeviceId);
        Assert.Same(h.Router, call.Sink);
        Assert.True(h.Xbox.IsRunning);
        Assert.Empty(h.Wheel.StartCalls);
        Assert.Empty(h.Shuttle.StartCalls);
    }

    [Fact]
    public void Xbox_AutoIndex_StartsWithANullDeviceId()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        Assert.Null(Assert.Single(h.Xbox.StartCalls).DeviceId);
    }

    [Fact]
    public void SwitchingController_StopsTheOldProvider_AndStartsTheNew()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        h.Configure(i => i with { SelectedController = ControllerKind.JogShuttle, Shuttle = i.Shuttle with { DeviceId = @"\\?\hid#vid_0b33" } });

        Assert.False(h.Xbox.IsRunning);
        Assert.Equal(1, h.Xbox.StopCalls);
        Assert.True(h.Shuttle.IsRunning);
        Assert.Equal(@"\\?\hid#vid_0b33", Assert.Single(h.Shuttle.StartCalls).DeviceId);
    }

    [Fact]
    public void SwitchingAwayFromAnAxisController_ReleasesItsSlots()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        h.Axis(ControllerKind.Xbox, ControlIds.RightTrigger, "xinput:0", 1.0);
        h.Engine.Clear();
        h.Select(ControllerKind.JogShuttle);
        Assert.Equal([EngineCommand.SetAxisSpeed(0, 0)], h.Posted);
    }

    [Fact]
    public void Wheel_StartsOnlyWithADedicatedDevice_AndSwitchesDevice()
    {
        var h = new RouterHarness();
        h.Configure(i => i with { Wheel = i.Wheel with { DedicatedDeviceId = "mouse-1" } });
        Assert.Equal("mouse-1", Assert.Single(h.Wheel.StartCalls).DeviceId);

        h.Configure(i => i with { Wheel = i.Wheel with { DedicatedDeviceId = "mouse-2" } });
        Assert.Equal(["mouse-1", "mouse-2"], h.Wheel.StartCalls.Select(c => c.DeviceId));

        h.Configure(i => i with { Wheel = i.Wheel with { DedicatedDeviceId = null } });
        Assert.False(h.Wheel.IsRunning);
    }

    [Fact]
    public void UnrelatedInputChanges_DoNotRestartTheProvider()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        h.Configure(i => i with { Xbox = i.Xbox with { Axes = DefaultBindings.XboxAxes } }); // new Xbox object, same device
        h.Configure(i => i with { Keyboard = new KeyboardSettings() });
        Assert.Single(h.Xbox.StartCalls);
        Assert.Equal(0, h.Xbox.StopCalls);
    }

    [Fact]
    public void ChangingTheUserIndex_RestartsOnTheNewDevice()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        h.Configure(i => i with { Xbox = i.Xbox with { UserIndex = 1 } });
        Assert.Equal([null, "xinput:1"], h.Xbox.StartCalls.Select(c => c.DeviceId));
    }

    [Fact]
    public void SettingsChangesOutsideInput_AreIgnored()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        h.Settings.Update(s => s with { Scroll = s.Scroll with { StartSpeed = 3 } });
        Assert.Single(h.Xbox.StartCalls);
    }

    [Fact]
    public void Stop_StopsEveryRunningProvider_AndIgnoresLaterSettingsChanges()
    {
        var h = new RouterHarness();
        h.Select(ControllerKind.Xbox);
        h.Router.Stop();
        Assert.False(h.Xbox.IsRunning);

        h.Select(ControllerKind.JogShuttle);
        Assert.Empty(h.Shuttle.StartCalls); // no longer listening to settings

        h.Router.Start();
        Assert.Single(h.Shuttle.StartCalls); // applies the current selection again
        h.Router.Stop();
        h.Router.Stop(); // idempotent
    }

    [Fact]
    public void StartingTwice_DoesNotDoubleSubscribe()
    {
        var h = new RouterHarness();
        h.Router.Start();
        h.Select(ControllerKind.Xbox);
        Assert.Single(h.Xbox.StartCalls);
    }

    [Fact]
    public void ProviderThatThrows_IsLogged_AndOthersStillWork()
    {
        var h = new RouterHarness();
        h.Xbox.ThrowOnStart = new InvalidOperationException("driver exploded");
        h.Select(ControllerKind.Xbox);
        Assert.Contains(h.Log.Lines, l => l.StartsWith("ERROR [input]", StringComparison.Ordinal));

        h.Select(ControllerKind.JogShuttle);
        Assert.True(h.Shuttle.IsRunning);
    }

    [Fact]
    public void StartAppliesTheSavedSelection()
    {
        var saved = PrompterSettings.Default with { Input = PrompterSettings.Default.Input with { SelectedController = ControllerKind.JogShuttle } };
        var h = new RouterHarness(saved, start: false);
        Assert.Empty(h.Shuttle.StartCalls);
        h.Router.Start();
        Assert.Single(h.Shuttle.StartCalls);
    }

    // ---- status ----

    [Fact]
    public void ReportStatus_KeepsTheLatestPerDevice_AndRaisesTheEvent()
    {
        var h = new RouterHarness();
        var raised = new List<DeviceStatus>();
        h.Router.DeviceStatusChanged += (_, s) => raised.Add(s);

        var pad = RouterHarness.Pad;
        h.Router.ReportStatus(new DeviceStatus(pad, "Pad", DeviceConnection.NotFound));
        h.Router.ReportStatus(new DeviceStatus(pad, "Pad", DeviceConnection.Connected, BatteryLevel.Low));
        h.Router.ReportStatus(new DeviceStatus(new InputSourceId(ControllerKind.JogShuttle, "hid"), "Shuttle", DeviceConnection.Connected));

        Assert.Equal(3, raised.Count);
        Assert.Equal(2, h.Router.DeviceStatuses.Count);
        var latest = h.Router.DeviceStatuses.Single(s => s.Source == pad);
        Assert.Equal(DeviceConnection.Connected, latest.Connection);
        Assert.Equal(BatteryLevel.Low, latest.Battery);
    }

    [Fact]
    public void ReportStatus_IgnoresTheKeyboard_AndStartClearsOldStatuses()
    {
        var h = new RouterHarness();
        h.Router.ReportStatus(new DeviceStatus(RouterHarness.App, "Keyboard", DeviceConnection.Connected));
        Assert.Empty(h.Router.DeviceStatuses);

        h.Router.ReportStatus(new DeviceStatus(RouterHarness.Pad, "Pad", DeviceConnection.Connected));
        Assert.Single(h.Router.DeviceStatuses);
        h.Router.Stop();
        h.Router.Start();
        Assert.Empty(h.Router.DeviceStatuses);
    }

    [Fact]
    public void InputObserved_IsRaisedForEverySubmit_BoundOrNot()
    {
        var h = new RouterHarness();
        var seen = new List<InputEvent>();
        h.Router.InputObserved += (_, e) => seen.Add(e);

        h.Key("Space");                // bound
        h.Key("Q");                    // unbound
        h.Notch(-1, device: "other");  // ignored (no dedicated mouse)
        h.Button(ControllerKind.Xbox, ControlIds.A, "xinput:0"); // not the selected controller
        Assert.Equal(4, seen.Count);
        Assert.IsType<ButtonInput>(seen[0]);
        Assert.IsType<RelativeInput>(seen[2]);
    }

    [Fact]
    public void AddIluCore_RegistersTheCoreSingletons()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddIluCore(diagnostics: false);
        foreach (var type in new[]
        {
            typeof(IAppLog), typeof(ISettingsService), typeof(IPromptDocumentService), typeof(IKeyModeState),
            typeof(IScrollEngine), typeof(IInputRouter), typeof(IInputSink), typeof(InputRouter),
        })
        {
            var descriptor = Assert.Single(services, d => d.ServiceType == type);
            Assert.Equal(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton, descriptor.Lifetime);
        }
    }
}
