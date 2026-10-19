using System.ComponentModel;
using IluPrompter.App.Logic;
using IluPrompter.App.Services;
using IluPrompter.App.ViewModels;
using IluPrompter.Core;

namespace IluPrompter.App.Tests;

/// <summary>
/// The Controller tab (DESIGN.md "Controller drawing"): inputs go in through the fake router exactly as the providers'
/// events do, the 10 Hz poll runs by hand on a clock the test moves, and the lit state, the "what it did" rows, the
/// status states and the last-input line come out. No real device is ever touched.
/// </summary>
public class ControllerPanelTests
{
    private static readonly InputSourceId XboxPad = new(ControllerKind.Xbox, "xinput:0");
    private static readonly InputSourceId Preview = new(ControllerKind.MouseWheel, AppSources.App);
    private static readonly InputSourceId SecondMouse = new(ControllerKind.MouseWheel, "\\\\?\\HID#fake-mouse");
    private static readonly InputSourceId Contour = new(ControllerKind.JogShuttle, "\\\\?\\HID#fake-shuttle");

    private sealed class Rig
    {
        public FakeSettings Settings { get; } = new();
        public FakeEngine Engine { get; } = new();
        public FakeOutput Output { get; } = new();
        public FakeRouter Router { get; } = new();
        public TestClock Clock { get; } = new();
        public ImmediateUi Ui { get; } = new();
        public ControllerPanelViewModel Panel { get; }

        public Rig(ControllerKind kind = ControllerKind.MouseWheel, string? dedicatedMouse = null, bool small = false)
        {
            Settings.Update(s => s with
            {
                Input = s.Input with { SelectedController = kind, Wheel = s.Input.Wheel with { DedicatedDeviceId = dedicatedMouse } },
            });
            Panel = new ControllerPanelViewModel(Router, Settings, Engine, Output, Ui, Clock.Now) { Small = small };
        }

        public ControllerPanelModel Model => Panel.Model;

        public void Poll(long advanceMs = 0)
        {
            Clock.Advance(advanceMs);
            Panel.Poll();
        }

        public void Press(InputSourceId source, string id, bool down = true) => Router.RaiseInput(new ButtonInput(source, id, 0, down));

        public void Axis(InputSourceId source, string id, double value) => Router.RaiseInput(new AxisInput(source, id, 0, value));

        public void Notch(InputSourceId source, string id, double amount) => Router.RaiseInput(new RelativeInput(source, id, 0, amount));

        public void Connect(InputSourceId source, string name, BatteryLevel battery = BatteryLevel.Unknown) =>
            Router.RaiseStatus(new DeviceStatus(source, name, DeviceConnection.Connected, battery));

        public void Bind(Func<InputSettings, InputSettings> change) => Settings.Update(s => s with { Input = change(s.Input) });

        public void SetMode(ScrollMode mode) => Engine.Status = Engine.Status with { Mode = mode };
    }

    // ===== status states =====

    [Fact]
    public void Keyboard_Only_HasNoDrawing_AndSaysSo()
    {
        var rig = new Rig(ControllerKind.Keyboard);
        rig.Poll();
        Assert.Equal(ControllerPanelState.NotChosen, rig.Model.State);
        Assert.Equal(ControllerDrawingKind.None, rig.Model.Frame.Kind);
        Assert.Equal("No controller chosen.", rig.Model.MessageTitle);
        Assert.Equal("The keyboard drives the prompt.", rig.Model.MessageDetail);
        Assert.Equal("No controller", rig.Panel.BarText);
    }

    [Fact]
    public void Wheel_IsAlwaysConnected_AndStartsWithNoInputYet()
    {
        var rig = new Rig();
        rig.Poll();
        Assert.Equal(ControllerPanelState.NoInputYet, rig.Model.State);
        Assert.Equal(ControllerDrawingKind.Mouse, rig.Model.Frame.Kind);
        Assert.False(rig.Model.Frame.Missing);
        Assert.Equal("Mouse wheel · your mouse", rig.Model.DeviceLine);
        Assert.Equal("Over the preview only", rig.Model.DetailLine);
        Assert.Equal("Turn the wheel over the preview.", rig.Model.Hint);
        Assert.Empty(rig.Model.Rows);
        Assert.Equal("No input yet", rig.Panel.BarText);
    }

    [Fact]
    public void ASecondMouse_HasItsOwnWordsAndOnlyItsOwnEvents()
    {
        var rig = new Rig(dedicatedMouse: SecondMouse.DeviceId);
        rig.Router.RaiseStatus(new DeviceStatus(SecondMouse, "Logitech wheel mouse", DeviceConnection.Connected));
        rig.Notch(Preview, ControlIds.Wheel, 1);          // the wheel over the preview does not count with a second mouse
        rig.Poll();
        Assert.Equal(ControllerPanelState.NoInputYet, rig.Model.State);
        Assert.Equal("Mouse wheel · second mouse", rig.Model.DeviceLine);
        Assert.Equal("Logitech wheel mouse", rig.Model.DetailLine);
        Assert.Equal("Turn the wheel.", rig.Model.Hint);

        rig.Notch(SecondMouse, ControlIds.Wheel, 1);
        rig.Poll();
        Assert.Equal(ControllerPanelState.InUse, rig.Model.State);
    }

    [Fact]
    public void YourOwnMouse_CountsOnlyWhatReachedThePreview()
    {
        var rig = new Rig();
        rig.Notch(SecondMouse, ControlIds.Wheel, 1);   // a raw-input mouse that is not the chosen one
        rig.Poll();
        Assert.Equal(ControllerPanelState.NoInputYet, rig.Model.State);
        rig.Notch(Preview, ControlIds.Wheel, 1);
        rig.Poll();
        Assert.Equal(ControllerPanelState.InUse, rig.Model.State);
    }

    [Theory]
    [InlineData(DeviceConnection.NotFound, ControllerPanelState.NotFound, "No Xbox controller found. Connect it, then wait a moment.")]
    [InlineData(DeviceConnection.Disconnected, ControllerPanelState.Lost, "The Xbox controller stopped answering. Reconnect it.")]
    public void AnXboxThatIsGone_ShowsTheMissingDrawing_TheChip_AndTheKeyboardSentence(DeviceConnection connection, ControllerPanelState state, string title)
    {
        var rig = new Rig(ControllerKind.Xbox);
        if (connection == DeviceConnection.Disconnected) rig.Router.RaiseStatus(new DeviceStatus(XboxPad, "Xbox controller", connection));
        rig.Poll();
        Assert.Equal(state, rig.Model.State);
        Assert.True(rig.Model.Frame.Missing);
        Assert.Equal(ControllerDrawingKind.Xbox, rig.Model.Frame.Kind);
        Assert.Equal(title, rig.Model.MessageTitle);
        Assert.Equal("The keyboard still works: Space plays and pauses.", rig.Model.MessageDetail);
        Assert.Equal(ChipKind.Attention, rig.Model.Chip!.Kind);
        Assert.Equal("MISSING", rig.Model.Chip.State);
        Assert.Equal("Xbox missing", rig.Panel.BarText);
        Assert.Empty(rig.Model.Rows);
    }

    [Fact]
    public void ALostController_IsAFault_OnlyWhileTheOutputIsLive()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Output.Set(OutputState.Initial with { IsOpen = true, Display = OutputDisplayStatus.Ready });
        rig.Poll();
        Assert.True(rig.Output.State.IsLive);
        Assert.Equal(ChipKind.Fault, rig.Model.Chip!.Kind);
    }

    [Fact]
    public void ALostController_KeepsItsLastInputLine()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Press(XboxPad, ControlIds.A);
        rig.Poll();
        rig.Router.RaiseStatus(new DeviceStatus(XboxPad, "Xbox controller", DeviceConnection.Disconnected));
        rig.Poll(500);
        Assert.Equal(ControllerPanelState.Lost, rig.Model.State);
        Assert.Equal("Last: A · Play / pause", rig.Model.LastText);
        Assert.Equal("Xbox missing", rig.Panel.BarText);   // the bar says missing, not the last press
    }

    [Fact]
    public void AConnectedXbox_WithNoInput_ShowsTheHint_AndPlayerNumber()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(new InputSourceId(ControllerKind.Xbox, "xinput:2"), "Xbox controller");
        rig.Poll();
        Assert.Equal(ControllerPanelState.NoInputYet, rig.Model.State);
        Assert.Equal("Xbox controller · player 3", rig.Model.DeviceLine);
        Assert.Equal("Press a button or move a stick.", rig.Model.Hint);
        Assert.Equal("No input yet", rig.Panel.BarText);
        Assert.Empty(rig.Model.Rows);
    }

    [Theory]
    [InlineData(BatteryLevel.Full, BatteryGlyph.Full, "Battery full")]
    [InlineData(BatteryLevel.Medium, BatteryGlyph.Medium, "Battery medium")]
    [InlineData(BatteryLevel.Low, BatteryGlyph.Low, "Battery low")]
    [InlineData(BatteryLevel.Empty, BatteryGlyph.Empty, "Battery empty")]
    [InlineData(BatteryLevel.Wired, BatteryGlyph.Wired, "Wired")]
    [InlineData(BatteryLevel.Unknown, BatteryGlyph.None, "")]
    public void TheXboxBattery_ComesFromTheDeviceStatus(BatteryLevel level, BatteryGlyph glyph, string text)
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller", level);
        rig.Poll();
        Assert.Equal(glyph, rig.Model.Battery);
        Assert.Equal(text, rig.Model.BatteryText);
    }

    [Fact]
    public void ContourBatteryAndMouse_NeverShowABattery()
    {
        var rig = new Rig(ControllerKind.JogShuttle);
        rig.Connect(Contour, "Contour ShuttleXpress", BatteryLevel.Low);
        rig.Poll();
        Assert.Equal(BatteryGlyph.None, rig.Model.Battery);
    }

    [Theory]
    [InlineData("Contour ShuttleXpress", ControllerDrawingKind.ShuttleXpress, "Contour ShuttleXpress")]
    [InlineData("Contour ShuttlePRO v2", ControllerDrawingKind.ShuttlePro, "Contour ShuttlePRO v2")]
    [InlineData("contour shuttlepro", ControllerDrawingKind.ShuttlePro, "Contour ShuttlePRO v2")]
    public void TheShuttleDrawing_IsChosenByTheDevicesName(string name, ControllerDrawingKind drawing, string detail)
    {
        var rig = new Rig(ControllerKind.JogShuttle);
        rig.Connect(Contour, name);
        rig.Poll();
        Assert.Equal(drawing, rig.Model.Frame.Kind);
        Assert.Equal("Jog-shuttle", rig.Model.DeviceLine);
        Assert.Equal(detail, rig.Model.DetailLine);
    }

    [Fact]
    public void AButtonAbove5_SwitchesAnUnnamedShuttleToThePro()
    {
        var rig = new Rig(ControllerKind.JogShuttle);
        rig.Connect(Contour, "HID device");
        rig.Poll();
        Assert.Equal(ControllerDrawingKind.ShuttleXpress, rig.Model.Frame.Kind);
        rig.Press(Contour, ControlIds.ShuttleButton(5));
        rig.Poll();
        Assert.Equal(ControllerDrawingKind.ShuttleXpress, rig.Model.Frame.Kind);
        rig.Press(Contour, ControlIds.ShuttleButton(9));
        rig.Poll();
        Assert.Equal(ControllerDrawingKind.ShuttlePro, rig.Model.Frame.Kind);
    }

    // ===== the input thread only records =====

    [Fact]
    public void Inputs_AreNotMarshalled_OnlyDeviceStatusIs()
    {
        var ui = new QueuedUi();
        var settings = new FakeSettings();
        settings.Update(s => s with { Input = s.Input with { SelectedController = ControllerKind.Xbox } });
        var router = new FakeRouter();
        var clock = new TestClock();
        var panel = new ControllerPanelViewModel(router, settings, new FakeEngine(), new FakeOutput(), ui, clock.Now);

        for (var i = 0; i < 200; i++) router.RaiseInput(new AxisInput(XboxPad, ControlIds.RightTrigger, 0, 0.5));
        Assert.Equal(0, ui.Posted);   // 200 events, no UI work

        router.RaiseStatus(new DeviceStatus(XboxPad, "Xbox controller", DeviceConnection.Connected));
        Assert.Equal(1, ui.Posted);
        panel.Poll();
        Assert.Equal(ControllerPanelState.NotFound, panel.Model.State);   // the status has not been applied yet
        ui.RunAll();
        panel.Poll();
        Assert.Equal(ControllerPanelState.InUse, panel.Model.State);
    }

    private sealed class QueuedUi : IUiThread
    {
        private readonly Queue<Action> _queue = new();

        public int Posted { get; private set; }

        public bool CheckAccess() => false;

        public void Post(Action action)
        {
            Posted++;
            _queue.Enqueue(action);
        }

        public void RunAll()
        {
            while (_queue.Count > 0) _queue.Dequeue()();
        }
    }

    [Fact]
    public void ThePoll_ChangesNothing_WhenNothingChanged_AndIgnoresNoise()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Press(XboxPad, ControlIds.A);
        rig.Press(XboxPad, ControlIds.A, false);
        rig.Poll(1000);
        var first = rig.Model;
        var changes = 0;
        rig.Panel.PropertyChanged += (_, _) => changes++;

        rig.Poll(100);
        rig.Axis(XboxPad, ControlIds.LeftThumbX, 0.003);     // jitter of a resting stick: under 1 DIP of dot travel
        rig.Axis(XboxPad, ControlIds.LeftThumbY, -0.004);
        rig.Axis(XboxPad, ControlIds.RightTrigger, 0.004);   // under 1/60 of the meter
        rig.Poll(100);

        Assert.Same(first, rig.Model);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void WhileTheDrawingIsNotOnScreen_ThePollUpdatesOnlyTheBarLine()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Poll();
        var before = rig.Model;
        rig.Panel.Active = false;

        rig.Press(XboxPad, ControlIds.A);
        rig.Poll(100);
        Assert.Same(before, rig.Model);                               // the drawing was not rebuilt
        Assert.Equal("Last: A · Play / pause", rig.Panel.BarText);    // the bar still follows

        rig.Panel.Active = true;
        rig.Poll(100);
        Assert.Equal(PartLook.Lit, rig.Model.Frame.Look(ControlIds.A));
    }

    // ===== only the chosen controller's inputs =====

    [Fact]
    public void TheKeyboardAndOtherControllers_NeverShow()
    {
        var rig = new Rig();
        rig.Press(new InputSourceId(ControllerKind.Keyboard, AppSources.App), "Space");
        rig.Press(XboxPad, ControlIds.A);
        rig.Axis(Contour, ControlIds.ShuttleRing, 0.5);
        rig.Poll();
        Assert.Equal(ControllerPanelState.NoInputYet, rig.Model.State);
        Assert.Equal("", rig.Model.LastText);
    }

    [Fact]
    public void ChoosingAnotherController_ForgetsWhatTheLastOneDid()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Press(XboxPad, ControlIds.A);
        rig.Poll();
        Assert.Equal(ControllerPanelState.InUse, rig.Model.State);

        rig.Bind(i => i with { SelectedController = ControllerKind.JogShuttle });
        rig.Connect(Contour, "Contour ShuttleXpress");
        rig.Poll(10);
        Assert.Equal(ControllerPanelState.NoInputYet, rig.Model.State);
        Assert.Equal("", rig.Model.LastText);
    }

    // ===== buttons: press, release, hold =====

    [Fact]
    public void ABoundButton_IsLitWhileDown_AndForAtLeast300ms_AfterItsLastEdge()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Poll();
        Assert.Equal(PartLook.Bound, rig.Model.Frame.Look(ControlIds.A));

        rig.Press(XboxPad, ControlIds.A);
        rig.Poll(100);
        Assert.Equal(PartLook.Lit, rig.Model.Frame.Look(ControlIds.A));
        rig.Poll(2000);                                   // still down: still lit, however long
        Assert.Equal(PartLook.Lit, rig.Model.Frame.Look(ControlIds.A));

        rig.Press(XboxPad, ControlIds.A, false);
        rig.Poll(100);
        Assert.Equal(PartLook.Lit, rig.Model.Frame.Look(ControlIds.A));   // released 100 ms ago
        rig.Poll(190);
        Assert.Equal(PartLook.Lit, rig.Model.Frame.Look(ControlIds.A));   // 290 ms
        rig.Poll(20);
        Assert.Equal(PartLook.Bound, rig.Model.Frame.Look(ControlIds.A)); // 310 ms: the light goes out, instantly
    }

    [Fact]
    public void AQuickTap_StillHoldsItsLight_ForThreeHundredMilliseconds()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Press(XboxPad, ControlIds.A);
        rig.Press(XboxPad, ControlIds.A, false);          // a tap shorter than any poll
        rig.Poll(100);
        Assert.Equal(PartLook.Lit, rig.Model.Frame.Look(ControlIds.A));
        rig.Poll(100);
        Assert.Equal(PartLook.Lit, rig.Model.Frame.Look(ControlIds.A));
        rig.Poll(150);
        Assert.Equal(PartLook.Bound, rig.Model.Frame.Look(ControlIds.A));
    }

    [Fact]
    public void FastRepeats_ReadAsOneSteadyLight()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        for (var i = 0; i < 6; i++)
        {
            rig.Press(XboxPad, ControlIds.DPadDown);
            rig.Press(XboxPad, ControlIds.DPadDown, false);
            rig.Poll(100);
            Assert.Equal(PartLook.Lit, rig.Model.Frame.Look(ControlIds.DPadDown));
        }
    }

    [Fact]
    public void AnUnboundButton_FlashesNeutrally_AndSaysNotUsed()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Poll();
        Assert.Equal(PartLook.Unbound, rig.Model.Frame.Look(ControlIds.Y));

        rig.Press(XboxPad, ControlIds.Y);
        rig.Poll(100);
        Assert.Equal(PartLook.Flash, rig.Model.Frame.Look(ControlIds.Y));
        Assert.Equal("Last: Y · Not used", rig.Model.LastText);
        var row = Assert.Single(rig.Model.Rows, r => r.Control == "Y");
        Assert.Equal("Not used", row.Action);
        rig.Press(XboxPad, ControlIds.Y, false);
        rig.Poll(500);
        Assert.Equal(PartLook.Unbound, rig.Model.Frame.Look(ControlIds.Y));
    }

    [Fact]
    public void AnUnknownInput_FlashesNothing_ButStillReportsItself()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Press(XboxPad, "Guide");
        rig.Poll(100);
        Assert.Equal(ControllerPanelState.InUse, rig.Model.State);
        Assert.Equal("Last: Guide · Not used", rig.Model.LastText);
        Assert.DoesNotContain("Guide", rig.Model.Frame.Buttons.Keys);
    }

    [Fact]
    public void TheActionLabel_ComesFromTheCurrentBindings_AndTheMode()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Bind(i => i with
        {
            Xbox = i.Xbox with
            {
                Buttons = new Dictionary<string, BindingAction>(i.Xbox.Buttons)
                {
                    [ControlIds.B] = new(PrompterCommand.SpeedUp, PrompterCommand.LineNext),
                    [ControlIds.X] = new(PrompterCommand.PlayPause, PrompterCommand.None),
                },
            },
        });
        rig.Connect(XboxPad, "Xbox controller");
        rig.Press(XboxPad, ControlIds.B);
        rig.Poll(100);
        Assert.Equal("Last: B · Speed +", rig.Model.LastText);

        rig.SetMode(ScrollMode.Line);
        rig.Press(XboxPad, ControlIds.B);
        rig.Poll(100);
        Assert.Equal("Last: B · Next line", rig.Model.LastText);

        // X does nothing in Line scrolling: dim there, bound in Display scrolling
        Assert.Equal(PartLook.Unbound, rig.Model.Frame.Look(ControlIds.X));
        rig.SetMode(ScrollMode.Display);
        rig.Poll(100);
        Assert.Equal(PartLook.Bound, rig.Model.Frame.Look(ControlIds.X));
    }

    [Fact]
    public void RebindingAButton_ChangesItsLabelAtOnce()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Press(XboxPad, ControlIds.A);
        rig.Poll(100);
        Assert.Equal("Last: A · Play / pause", rig.Model.LastText);
        rig.Bind(i => i with { Xbox = i.Xbox with { Buttons = new Dictionary<string, BindingAction>(i.Xbox.Buttons) { [ControlIds.A] = new(PrompterCommand.ToggleBlank) } } });
        rig.Poll(10);
        Assert.Equal("Last: A · Blank output", rig.Model.LastText);
    }

    [Theory]
    [InlineData(PrompterCommand.PlayPause, "Play / pause")]
    [InlineData(PrompterCommand.Play, "Play")]
    [InlineData(PrompterCommand.Pause, "Pause")]
    [InlineData(PrompterCommand.SpeedUp, "Speed +")]
    [InlineData(PrompterCommand.SpeedDown, "Speed −")]
    [InlineData(PrompterCommand.SpeedUpFine, "Speed + fine")]
    [InlineData(PrompterCommand.SpeedDownFine, "Speed − fine")]
    [InlineData(PrompterCommand.SpeedReset, "Start speed")]
    [InlineData(PrompterCommand.LineNext, "Next line")]
    [InlineData(PrompterCommand.LinePrevious, "Previous line")]
    [InlineData(PrompterCommand.ParagraphNext, "Next para")]
    [InlineData(PrompterCommand.ParagraphPrevious, "Previous para")]
    [InlineData(PrompterCommand.SectionNext, "Next section")]
    [InlineData(PrompterCommand.SectionPrevious, "Previous section")]
    [InlineData(PrompterCommand.JumpToStart, "Jump to start")]
    [InlineData(PrompterCommand.JumpToEnd, "Jump to end")]
    [InlineData(PrompterCommand.ToggleScrollMode, "Switch mode")]
    [InlineData(PrompterCommand.SetDisplayScrolling, "Display scrolling")]
    [InlineData(PrompterCommand.SetLineScrolling, "Line scrolling")]
    [InlineData(PrompterCommand.ToggleBlank, "Blank output")]
    [InlineData(PrompterCommand.ToggleOutput, "Output on / off")]
    [InlineData(PrompterCommand.None, "Not used")]
    public void EveryCommand_HasItsActionLabelFromTheDesignTable(PrompterCommand command, string label) =>
        Assert.Equal(label, ControllerLabels.Command(command));

    [Fact]
    public void TheShortNames_FollowTheDesignTable()
    {
        Assert.Equal(["Wheel", "Middle", "Side 1", "Side 2"],
            new[] { ControlIds.Wheel, ControlIds.MiddleButton, ControlIds.XButton1, ControlIds.XButton2 }.Select(ControllerLabels.ShortName));
        Assert.Equal(["LT", "RT", "LB", "RB", "A", "B", "X", "Y", "D-pad ↑", "D-pad ↓", "D-pad ←", "D-pad →", "View", "Menu", "LS press", "RS press"],
            new[]
            {
                ControlIds.LeftTrigger, ControlIds.RightTrigger, ControlIds.LeftShoulder, ControlIds.RightShoulder, ControlIds.A, ControlIds.B, ControlIds.X, ControlIds.Y,
                ControlIds.DPadUp, ControlIds.DPadDown, ControlIds.DPadLeft, ControlIds.DPadRight, ControlIds.View, ControlIds.Menu, ControlIds.LeftThumbPress, ControlIds.RightThumbPress,
            }.Select(ControllerLabels.ShortName));
        Assert.Equal("Ring", ControllerLabels.ShortName(ControlIds.ShuttleRing));
        Assert.Equal("Jog", ControllerLabels.ShortName(ControlIds.JogWheel));
        Assert.Equal("Button 12", ControllerLabels.ShortName(ControlIds.ShuttleButton(12)));
    }

    // ===== rows =====

    [Fact]
    public void Rows_ListThePersistentControlsFirst_ThenAtMostTwoLitButtons()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Axis(XboxPad, ControlIds.RightTrigger, 0.62);
        foreach (var id in new[] { ControlIds.A, ControlIds.B, ControlIds.X, ControlIds.DPadUp }) rig.Press(XboxPad, id);
        rig.Poll(100);

        Assert.Equal(["RT", "LT", "A", "B"], rig.Model.Rows.Select(r => r.Control));
        Assert.Equal("Speed forward", rig.Model.Rows[0].Action);
        Assert.Equal("62 %", rig.Model.Rows[0].Value);
        Assert.True(rig.Model.Rows[0].Lit);
        Assert.Equal("Speed reverse", rig.Model.Rows[1].Action);
        Assert.Equal("0 %", rig.Model.Rows[1].Value);
        Assert.False(rig.Model.Rows[1].Lit);
        Assert.Equal("Play / pause", rig.Model.Rows[2].Action);
        // X and D-pad up are lit too, but only the drawing shows them
        Assert.Equal(PartLook.Lit, rig.Model.Frame.Look(ControlIds.DPadUp));
    }

    [Fact]
    public void TheSmallBody_ShowsOneTransientRow()
    {
        var rig = new Rig(ControllerKind.Xbox, small: true);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Press(XboxPad, ControlIds.A);
        rig.Press(XboxPad, ControlIds.B);
        rig.Poll(100);
        Assert.Equal(["RT", "LT", "A"], rig.Model.Rows.Select(r => r.Control));
    }

    [Fact]
    public void Rows_NeverReorderByTime()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Press(XboxPad, ControlIds.B);
        rig.Poll(50);
        rig.Press(XboxPad, ControlIds.A);   // pressed later, but earlier in the controller's own order
        rig.Poll(50);
        Assert.Equal(["RT", "LT", "A", "B"], rig.Model.Rows.Select(r => r.Control));
    }

    // ===== axes: percentage =====

    [Theory]
    [InlineData(0.62, 37, "62 %")]
    [InlineData(1.0, 60, "100 %")]
    [InlineData(0.0, 0, "0 %")]
    [InlineData(0.5, 30, "50 %")]
    public void ATrigger_FillsFromTheBottom_AndReadsAsAPercentage(double pull, int fillPx, string value)
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Axis(XboxPad, ControlIds.RightTrigger, pull);
        rig.Axis(XboxPad, ControlIds.LeftTrigger, 0.3);   // gives the tab some input even when the pull is 0
        rig.Poll(100);
        var trigger = rig.Model.Frame.Triggers.Single(t => t.Id == ControlIds.RightTrigger);
        Assert.Equal(fillPx, trigger.FillPx);
        Assert.Equal(value, rig.Model.Rows.Single(r => r.Control == "RT").Value);
        Assert.Equal(52.8, trigger.TickOffsetFromTop);   // 60 x (1 - 0.12)
    }

    [Fact]
    public void ATrigger_InsideItsDeadzone_IsNotLit_AndItsFillIsMuted()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Axis(XboxPad, ControlIds.RightTrigger, 0.1);
        rig.Poll(100);
        Assert.Equal(IndicatorLook.Idle, rig.Model.Frame.Triggers.Single(t => t.Id == ControlIds.RightTrigger).Fill);
        Assert.Equal(ControllerPanelState.NoInputYet, rig.Model.State);   // inside the deadzone is not "use"
        rig.Axis(XboxPad, ControlIds.RightTrigger, 0.5);
        rig.Poll(100);
        Assert.Equal(IndicatorLook.Bound, rig.Model.Frame.Triggers.Single(t => t.Id == ControlIds.RightTrigger).Fill);
        Assert.True(rig.Model.Rows.Single(r => r.Control == "RT").Lit);
    }

    [Fact]
    public void AnUnboundStick_StillMovesItsDot_InTheNeutralColour()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Axis(XboxPad, ControlIds.LeftThumbX, 0.5);
        rig.Axis(XboxPad, ControlIds.LeftThumbY, 1.0);   // up and to the right: outside the unit circle, so clamped
        rig.Poll(100);
        var stick = rig.Model.Frame.Sticks.Single(s => s.Id == ControlIds.LeftThumbPress);
        Assert.Equal(IndicatorLook.Unbound, stick.Dot);
        Assert.False(stick.Bound);
        Assert.Equal(6, stick.DotDx);     // 13 x 0.447
        Assert.Equal(-12, stick.DotDy);   // 13 x 0.894, up is negative y
        Assert.Equal(Math.Round(18 * 0.24, 1), stick.DeadzoneRadius);   // XInput's own left deadzone
        Assert.DoesNotContain(rig.Model.Rows, r => r.Control == "LS");
    }

    [Fact]
    public void ABoundStick_ShowsASignedPercentage_AndItsOwnDeadzoneRing()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Bind(i => i with
        {
            Xbox = i.Xbox with
            {
                Axes = new Dictionary<string, AxisBinding>(i.Xbox.Axes) { [ControlIds.LeftThumbY] = new(AxisRole.Speed, Deadzone: 0.1) },
            },
        });
        rig.Connect(XboxPad, "Xbox controller");
        rig.Axis(XboxPad, ControlIds.LeftThumbY, -0.62);
        rig.Poll(100);
        var row = rig.Model.Rows.Single(r => r.Control == "LS");
        Assert.Equal("Speed", row.Action);
        Assert.Equal("−62 %", row.Value);
        Assert.True(row.Lit);
        var stick = rig.Model.Frame.Sticks.Single(s => s.Id == ControlIds.LeftThumbPress);
        Assert.Equal(IndicatorLook.Bound, stick.Dot);
        Assert.Equal(8, stick.DotDy);                          // pulled down: 13 x 0.62, down is positive y
        Assert.Equal(3.0, stick.DeadzoneRadius);               // max(3, 18 x 0.1)
    }

    [Fact]
    public void AStickPress_LightsTheRing()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Press(XboxPad, ControlIds.RightThumbPress);
        rig.Poll(100);
        Assert.Equal(PressLook.Flash, rig.Model.Frame.Sticks.Single(s => s.Id == ControlIds.RightThumbPress).Press);   // unbound by default
        Assert.Equal("Last: RS press · Not used", rig.Model.LastText);
    }

    // ===== the mouse wheel =====

    [Fact]
    public void WheelNotches_AccumulateIntoABurst_ThatEndsAfterTwoSeconds()
    {
        var rig = new Rig();
        rig.Notch(Preview, ControlIds.Wheel, 1);
        rig.Poll(100);
        rig.Notch(Preview, ControlIds.Wheel, 1);
        rig.Poll(100);
        rig.Notch(Preview, ControlIds.Wheel, 1);
        rig.Poll(100);
        var row = rig.Model.Rows.Single();
        Assert.Equal("Wheel", row.Control);
        Assert.Equal("▲ 3", row.Value);                     // away from you
        Assert.Equal("Speed − 1.5", row.Action);            // 3 notches x 0.5, away from you slows
        Assert.True(row.Lit);
        Assert.Equal("Last: Wheel ▲ 3 · Speed − 1.5", rig.Model.LastText);

        rig.Notch(Preview, ControlIds.Wheel, -1);
        rig.Poll(100);
        Assert.Equal("▲ 2", rig.Model.Rows.Single().Value);

        rig.Poll(1000);
        Assert.Equal("▲ 2", rig.Model.Rows.Single().Value);  // 1.1 s after the last notch: still counting
        Assert.False(rig.Model.Rows.Single().Lit);               // the 400 ms light is long out
        rig.Poll(1000);
        Assert.Equal("0", rig.Model.Rows.Single().Value);        // 2.1 s: the burst is over
        Assert.Equal("Speed", rig.Model.Rows.Single().Action);   // a resting row names its role without a direction
        Assert.Equal("Last: Wheel ▲ 2 · Speed − 1.0", rig.Model.LastText);   // the last line keeps the burst
    }

    [Fact]
    public void ATowardsNotch_ReadsDown_AndSpeedsUp()
    {
        var rig = new Rig();
        rig.Notch(Preview, ControlIds.Wheel, -2);
        rig.Poll(100);
        Assert.Equal("▼ 2", rig.Model.Rows.Single().Value);
        Assert.Equal("Speed + 1.0", rig.Model.Rows.Single().Action);
    }

    [Fact]
    public void AHighResolutionWheel_ShowsOneDecimal()
    {
        var rig = new Rig();
        rig.Notch(Preview, ControlIds.Wheel, 0.25);
        rig.Notch(Preview, ControlIds.Wheel, 0.25);
        rig.Notch(Preview, ControlIds.Wheel, 1);
        rig.Poll(100);
        Assert.Equal("▲ 1.5", rig.Model.Rows.Single().Value);
    }

    [Fact]
    public void InLineScrolling_TheWheelStepsLines()
    {
        var rig = new Rig();
        rig.SetMode(ScrollMode.Line);
        rig.Notch(Preview, ControlIds.Wheel, 1);   // away from you: previous line
        rig.Poll(100);
        Assert.Equal("Previous line", rig.Model.Rows.Single().Action);
        rig.Notch(Preview, ControlIds.Wheel, -2);
        rig.Poll(100);
        Assert.Equal("Next line", rig.Model.Rows.Single().Action);
        rig.Poll(5000);
        Assert.Equal("Lines", rig.Model.Rows.Single().Action);
    }

    [Fact]
    public void TheWheelTreads_MoveByNotchesModFour_AndTheChevronsLightFor400ms()
    {
        var rig = new Rig();
        rig.Notch(Preview, ControlIds.Wheel, 1);
        rig.Poll(100);
        var wheel = rig.Model.Frame.Wheel!;
        Assert.Equal(3, wheel.TreadPhase);              // away from you: p - 1, wrapping 0..3
        Assert.True(wheel.UpLit);
        Assert.False(wheel.DownLit);

        rig.Notch(Preview, ControlIds.Wheel, -5);
        rig.Poll(100);
        wheel = rig.Model.Frame.Wheel!;
        Assert.Equal(0, wheel.TreadPhase);              // net -4 notches: one jump, 4 mod 4 = 0
        Assert.True(wheel.UpLit);                       // the away notch was 100 ms ago
        Assert.True(wheel.DownLit);

        rig.Poll(450);
        wheel = rig.Model.Frame.Wheel!;
        Assert.False(wheel.UpLit);
        Assert.False(wheel.DownLit);
    }

    [Fact]
    public void TheMiddleButton_LightsLikeAnyButton_AndTheSideButtonsFlash()
    {
        var rig = new Rig();
        rig.Poll();
        Assert.Equal(PartLook.Bound, rig.Model.Frame.Look(ControlIds.MiddleButton));
        Assert.Equal(PartLook.Unbound, rig.Model.Frame.Look(ControlIds.XButton1));
        rig.Press(Preview, ControlIds.MiddleButton);
        rig.Press(Preview, ControlIds.XButton1);
        rig.Poll(100);
        Assert.Equal(PartLook.Lit, rig.Model.Frame.Look(ControlIds.MiddleButton));
        Assert.Equal(PartLook.Flash, rig.Model.Frame.Look(ControlIds.XButton1));
        Assert.Contains(rig.Model.Rows, r => r is { Control: "Middle", Action: "Play / pause" });
        Assert.Contains(rig.Model.Rows, r => r is { Control: "Side 1", Action: "Not used" });
    }

    // ===== the shuttle =====

    [Theory]
    [InlineData(0.0, 0, "0")]
    [InlineData(3.0 / 7, 3, "+3")]
    [InlineData(1.0, 7, "+7")]
    [InlineData(-1.0, -7, "−7")]
    [InlineData(-5.0 / 7, -5, "−5")]
    [InlineData(1.0 / 7, 1, "+1")]
    public void TheShuttleRing_ReadsItsPosition_MinusSevenToPlusSeven(double value, int position, string text)
    {
        var rig = new Rig(ControllerKind.JogShuttle);
        rig.Connect(Contour, "Contour ShuttleXpress");
        rig.Axis(Contour, ControlIds.ShuttleRing, value == 0 ? 0.5 : value);
        rig.Axis(Contour, ControlIds.ShuttleRing, value);   // returns to centre, or stays
        rig.Poll(100);
        var shuttle = rig.Model.Frame.Shuttle!;
        Assert.Equal(position, shuttle.RingK);
        Assert.Equal(text, rig.Model.Rows.Single(r => r.Control == "Ring").Value);
        Assert.Equal(position == 0 ? IndicatorLook.Idle : IndicatorLook.Bound, shuttle.RingArc);
        Assert.Equal(position != 0, rig.Model.Rows.Single(r => r.Control == "Ring").Lit);
    }

    [Fact]
    public void TheJogWheel_CountsDetents_StepsItsDimple_AndLightsItsEdge()
    {
        var rig = new Rig(ControllerKind.JogShuttle);
        rig.Connect(Contour, "Contour ShuttleXpress");
        rig.Notch(Contour, ControlIds.JogWheel, 4);
        rig.Poll(100);
        var shuttle = rig.Model.Frame.Shuttle!;
        Assert.Equal(4, shuttle.JogDimple);
        Assert.True(shuttle.JogLit);
        var row = rig.Model.Rows.Single(r => r.Control == "Jog");
        Assert.Equal("+4", row.Value);
        Assert.Equal("Next line", row.Action);

        rig.Notch(Contour, ControlIds.JogWheel, -6);
        rig.Poll(100);
        Assert.Equal("−2", rig.Model.Rows.Single(r => r.Control == "Jog").Value);
        Assert.Equal("Previous line", rig.Model.Rows.Single(r => r.Control == "Jog").Action);
        Assert.Equal(8, rig.Model.Frame.Shuttle!.JogDimple);   // total -2, mod 10

        rig.Poll(450);
        Assert.False(rig.Model.Frame.Shuttle!.JogLit);
        rig.Poll(2000);
        Assert.Equal("0", rig.Model.Rows.Single(r => r.Control == "Jog").Value);
        Assert.Equal("Lines", rig.Model.Rows.Single(r => r.Control == "Jog").Action);
    }

    [Fact]
    public void ShuttleButtons_UseTheDefaultBindings_AndTheProHasFifteen()
    {
        var rig = new Rig(ControllerKind.JogShuttle);
        rig.Connect(Contour, "Contour ShuttlePRO v2");
        rig.Press(Contour, ControlIds.ShuttleButton(3));
        rig.Press(Contour, ControlIds.ShuttleButton(9));
        rig.Poll(100);
        var frame = rig.Model.Frame;
        Assert.Equal(15, frame.Buttons.Count);
        Assert.Equal(PartLook.Lit, frame.Look("Button3"));
        Assert.Equal(PartLook.Flash, frame.Look("Button9"));
        Assert.Equal(PartLook.Bound, frame.Look("Button8"));      // Switch mode
        Assert.Equal(PartLook.Unbound, frame.Look("Button15"));
        Assert.Contains(rig.Model.Rows, r => r is { Control: "Button 3", Action: "Play / pause" });
    }

    [Fact]
    public void TheShuttleXpress_HasFiveButtons()
    {
        var rig = new Rig(ControllerKind.JogShuttle);
        rig.Connect(Contour, "Contour ShuttleXpress");
        rig.Poll();
        Assert.Equal(5, rig.Model.Frame.Buttons.Count);
    }

    // ===== last input words =====

    [Fact]
    public void TheLastInputLine_NamesAxisValuesToo()
    {
        var rig = new Rig(ControllerKind.Xbox);
        rig.Connect(XboxPad, "Xbox controller");
        rig.Axis(XboxPad, ControlIds.RightTrigger, 0.62);
        rig.Poll(100);
        Assert.Equal("Last: RT 62 % · Speed forward", rig.Model.LastText);
        Assert.Equal("Last: RT 62 % · Speed forward", rig.Panel.BarText);
    }
}
