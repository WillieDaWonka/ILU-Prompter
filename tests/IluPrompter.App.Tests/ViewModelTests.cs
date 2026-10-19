using IluPrompter.App.Logic;
using IluPrompter.App.Services;
using IluPrompter.App.ViewModels;
using IluPrompter.Core;
using IluPrompter.Theme;

namespace IluPrompter.App.Tests;

public class LiveViewModelTests
{
    private sealed class Rig
    {
        public FakeSettings Settings { get; } = new();
        public FakeEngine Engine { get; } = new();
        public FakeOutput Output { get; } = new();
        public FakeRouter Router { get; } = new();
        public FakeMonitors Monitors { get; } = new();
        public FakeDialogs Dialogs { get; } = new();
        public ImmediateUi Ui { get; } = new();
        public OutputController Controller { get; }
        public MarginEditorViewModel Margins { get; }
        public LiveViewModel Live { get; }

        public Rig()
        {
            Monitors.Monitors.Add(FakeMonitors.Display2());
            Controller = new OutputController(Output, Dialogs, new NullLog());
            Margins = new MarginEditorViewModel(Settings, Output, Ui);
            var layout = new OutputLayoutViewModel(Settings, Ui, Margins);
            Live = new LiveViewModel(Engine, Output, Router, Settings, Monitors, Controller, Ui, layout);
        }
    }

    [Fact]
    public void Speed_ShowsOneDecimal_AndFollowsTheEngine()
    {
        var rig = new Rig();
        rig.Engine.Status = new EngineStatus { SetSpeed = 4.5, WordsPerMinute = 141 };
        rig.Live.Poll();
        Assert.Equal("4.5", rig.Live.SpeedText);
        Assert.Equal("≈ 140 wpm", rig.Live.WpmText);
        rig.Engine.Status = rig.Engine.Status with { SetSpeed = -2 };
        rig.Live.Poll();
        Assert.Equal("−2.0", rig.Live.SpeedText);
        Assert.Empty(rig.Engine.Posted); // following the engine never posts a command back
    }

    [Fact]
    public void DraggingTheSlider_PostsSetSpeed()
    {
        var rig = new Rig();
        rig.Live.Speed = 6.25;
        var posted = Assert.Single(rig.Engine.Posted);
        Assert.Equal(EngineCommandKind.SetSpeed, posted.Kind);
        Assert.Equal(6.3, posted.Value, 3);
        Assert.Equal("6.3", rig.Live.SpeedText);
    }

    [Fact]
    public void Speed_IsClampedToTheSettingsRange()
    {
        var rig = new Rig();
        rig.Live.Speed = 42;
        Assert.Equal(10, rig.Live.Speed);
        rig.Live.Speed = -42;
        Assert.Equal(-10, rig.Live.Speed);
    }

    [Theory]
    [InlineData(1, false, PrompterCommand.SpeedUp)]
    [InlineData(1, true, PrompterCommand.SpeedUpFine)]
    [InlineData(-1, false, PrompterCommand.SpeedDown)]
    [InlineData(-1, true, PrompterCommand.SpeedDownFine)]
    public void StepButtons_Step_ByHalf_OrByATenthWithShift(int direction, bool fine, PrompterCommand expected)
    {
        var rig = new Rig();
        rig.Live.StepSpeed(direction, fine);
        Assert.Equal(expected, Assert.Single(rig.Router.Executed));
    }

    [Fact]
    public void Play_ShowsWhatAPressDoes_AndTheFillSaysWhatIsHappening()
    {
        var rig = new Rig();
        rig.Engine.Status = new EngineStatus { Transport = TransportState.Paused };
        rig.Live.Poll();
        Assert.Equal(("Paused", "Play", true), (rig.Live.PlayState, rig.Live.PlayLabel, rig.Live.CanPlay));

        rig.Engine.Status = new EngineStatus { Transport = TransportState.Playing };
        rig.Live.Poll();
        Assert.Equal(("Playing", "Pause", true), (rig.Live.PlayState, rig.Live.PlayLabel, rig.Live.CanPlay));

        rig.Engine.Status = new EngineStatus { Transport = TransportState.AtEnd, IsAtEnd = true };
        rig.Live.Poll();
        Assert.Equal(("AtEnd", "Play", false), (rig.Live.PlayState, rig.Live.PlayLabel, rig.Live.CanPlay));
        Assert.Equal("At end", rig.Live.PlayCaption);
        Assert.Equal("At end", rig.Live.TimeCaption);
        Assert.False(rig.Live.CanStepForward);
    }

    [Fact]
    public void PreviousButtons_AreDisabledAtTheStart_AndNextAtTheEnd()
    {
        var rig = new Rig();
        rig.Engine.Status = new EngineStatus { IsAtStart = true, IsAtEnd = false };
        rig.Live.Poll();
        Assert.False(rig.Live.CanStepBack);
        Assert.True(rig.Live.CanStepForward);
        rig.Engine.Status = new EngineStatus { IsAtStart = false, IsAtEnd = true };
        rig.Live.Poll();
        Assert.True(rig.Live.CanStepBack);
        Assert.False(rig.Live.CanStepForward);
    }

    [Fact]
    public void TransportButtons_RunTheCommandThroughTheRouter()
    {
        var rig = new Rig();
        rig.Live.RunCommand.Execute(PrompterCommand.SectionNext);
        rig.Live.RunCommand.Execute(PrompterCommand.PlayPause);
        Assert.Equal([PrompterCommand.SectionNext, PrompterCommand.PlayPause], rig.Router.Executed);
    }

    [Fact]
    public void ModeSwitch_PostsSetMode_OnlyWhenItChanges()
    {
        var rig = new Rig();
        rig.Live.IsLineScrollingSelected = true;
        Assert.Equal(EngineCommand.SetMode(ScrollMode.Line), Assert.Single(rig.Engine.Posted));
        rig.Engine.Status = new EngineStatus { Mode = ScrollMode.Line };
        rig.Live.Poll();
        Assert.True(rig.Live.IsLineScrolling);
        Assert.False(rig.Live.IsDisplayScrolling);
        rig.Live.IsLineScrollingSelected = true; // already there
        Assert.Single(rig.Engine.Posted);
        rig.Live.IsDisplayScrolling = true;
        Assert.Equal(EngineCommand.SetMode(ScrollMode.Display), rig.Engine.Posted[^1]);
    }

    [Fact]
    public void LineScrolling_ShowsStepDelay_WhenPlaying_AndManualOtherwise()
    {
        var rig = new Rig();
        rig.Engine.Status = new EngineStatus { Mode = ScrollMode.Line, Transport = TransportState.Playing, StepIntervalSeconds = 0.84 };
        rig.Live.Poll();
        Assert.Equal(("Step", "0.8 s"), (rig.Live.StepCaption, rig.Live.StepValue));
        rig.Engine.Status = new EngineStatus { Mode = ScrollMode.Line, Transport = TransportState.Paused };
        rig.Live.Poll();
        Assert.Equal(("Manual", ""), (rig.Live.StepCaption, rig.Live.StepValue));
    }

    [Fact]
    public void Time_IsLeftWhilePlaying_AndAtTheSetSpeedWhilePaused()
    {
        var rig = new Rig();
        rig.Engine.Status = new EngineStatus { Transport = TransportState.Playing, SecondsRemaining = 402, SetSpeed = 4.5 };
        rig.Live.Poll();
        Assert.Equal(("6m:42s", "left"), (rig.Live.TimeText, rig.Live.TimeCaption));
        rig.Engine.Status = new EngineStatus { Transport = TransportState.Paused, SecondsRemaining = 402, SetSpeed = 4.5 };
        rig.Live.Poll();
        Assert.Equal("at 4.5", rig.Live.TimeCaption);
    }

    [Fact]
    public void BlankToggle_GoesThroughTheOutputController_AndFollowsTheOutput()
    {
        var rig = new Rig();
        rig.Live.IsBlank = true;
        Assert.True(rig.Output.State.IsBlank);
        Assert.Equal(1, rig.Output.BlankCalls);

        // The output changing (for example B on the keyboard) updates the toggle without calling SetBlank again.
        rig.Output.Set(rig.Output.State with { IsBlank = false });
        Assert.False(rig.Live.IsBlank);
        Assert.Equal(1, rig.Output.BlankCalls);
    }

    [Fact]
    public void Chips_FollowTheOutputAndTheDevices()
    {
        var rig = new Rig();
        Assert.Equal("OFF", rig.Live.OutputChip.State);
        rig.Output.Set(rig.Output.State with { IsOpen = true, Display = OutputDisplayStatus.Ready, Monitor = FakeMonitors.Display2() });
        Assert.Equal("LIVE", rig.Live.OutputChip.State);
        Assert.True(rig.Live.IsLive);

        rig.Settings.Update(s => s with { Input = s.Input with { SelectedController = ControllerKind.Xbox } });
        rig.Router.RaiseStatus(new DeviceStatus(new InputSourceId(ControllerKind.Xbox, "xinput:0"), "Xbox", DeviceConnection.Disconnected));
        Assert.Equal(("Xbox", "MISSING", ChipKind.Fault), (rig.Live.ControllerChip.Subject, rig.Live.ControllerChip.State, rig.Live.ControllerChip.Kind));
        rig.Output.Set(rig.Output.State with { IsBlank = true });
        Assert.Equal(ChipKind.Attention, rig.Live.ControllerChip.Kind); // not live now: attention, not fault
    }

    [Fact]
    public async Task ShowOutput_ThenNoSecondDisplay_TurnsTheChipToAttention()
    {
        var rig = new Rig();
        rig.Monitors.Monitors.Clear();
        Assert.Equal(ChipKind.Idle, rig.Live.OutputChip.Kind);
        await rig.Controller.ToggleAsync(); // the fake output opens regardless; the real one stays shut with no second display
        rig.Output.Set(OutputState.Initial with { Display = OutputDisplayStatus.NoSecondDisplay });
        Assert.Equal(("NO 2ND DISPLAY", ChipKind.Attention), (rig.Live.OutputChip.State, rig.Live.OutputChip.Kind));
    }

    [Fact]
    public void Preview_CaptionAndFrame_DescribeTheOutput()
    {
        var rig = new Rig();
        rig.Output.Set(rig.Output.State with { IsOpen = true, Display = OutputDisplayStatus.Ready, Monitor = FakeMonitors.Display2() });
        Assert.Equal("Output · 1920 × 1080 · 60 Hz · Display 2 (LG 55UH5)", rig.Live.PreviewCaption);
        Assert.Equal("Changes show on Display 2 now", rig.Live.LiveNote);
        Assert.Equal(16.0 / 9.0, rig.Live.PreviewAspect, 3);
        rig.Output.Set(rig.Output.State with { IsBlank = true });
        Assert.False(rig.Live.IsLive);
        Assert.Equal("", rig.Live.LiveNote);
    }

    [Fact]
    public void WheelHint_AppliesOnlyToTheWheelOverThePreview()
    {
        var rig = new Rig();
        Assert.True(rig.Live.WheelHintApplies);
        rig.Settings.Update(s => s with { Input = s.Input with { Wheel = s.Input.Wheel with { DedicatedDeviceId = "mouse2" } } });
        Assert.False(rig.Live.WheelHintApplies);
        rig.Settings.Update(s => s with { Input = s.Input with { Wheel = s.Input.Wheel with { DedicatedDeviceId = null }, SelectedController = ControllerKind.Xbox } });
        Assert.False(rig.Live.WheelHintApplies);
    }

    [Theory]
    [InlineData(LayoutTier.Wide, true, true, "Display scrolling", 96)]
    [InlineData(LayoutTier.Compact, false, false, "Display", 72)]
    [InlineData(LayoutTier.Narrow, false, false, "Display", 56)]
    public void Tier_ChangesWhatTheColumnShows(LayoutTier tier, bool captions, bool layout, string modeLabel, double playWidth)
    {
        var rig = new Rig();
        rig.Live.Tier = tier;
        Assert.Equal(captions, rig.Live.ShowTransportCaptions);
        Assert.Equal(captions, rig.Live.ShowWpm);
        Assert.Equal(layout, rig.Live.ShowOutputLayout);
        Assert.Equal(modeLabel, rig.Live.DisplayModeLabel);
        Assert.Equal(playWidth, rig.Live.PlayWidth);
        Assert.Equal(tier != LayoutTier.Narrow, rig.Live.ChipsInLiveColumn);
        Assert.Equal(tier == LayoutTier.Narrow, rig.Live.TimeInSpeedRow);
    }

    [Fact]
    public void Wpm_MovesToTheTooltip_BelowWide()
    {
        var rig = new Rig();
        rig.Engine.Status = new EngineStatus { SetSpeed = 4.5, WordsPerMinute = 140 };
        rig.Live.Poll();
        Assert.Equal("≈ 140 wpm", rig.Live.WpmDisplay);
        rig.Live.Tier = LayoutTier.Compact;
        Assert.Equal("", rig.Live.WpmDisplay);
        Assert.Contains("≈ 140 wpm", rig.Live.SpeedTooltip);
    }

    [Fact]
    public void Chips_OpenTheMatchingSettingsPage()
    {
        var rig = new Rig();
        var pages = new List<SettingsPageId>();
        rig.Live.SettingsPageRequested += (_, p) => pages.Add(p);
        rig.Live.ChipClickedCommand.Execute("output");
        rig.Live.ChipClickedCommand.Execute("controller");
        Assert.Equal([SettingsPageId.Output, SettingsPageId.Controller], pages);
    }
}

public class MarginEditorViewModelTests
{
    private static (MarginEditorViewModel vm, FakeSettings settings) Make()
    {
        var settings = new FakeSettings();
        return (new MarginEditorViewModel(settings, new FakeOutput(), new ImmediateUi()), settings);
    }

    [Fact]
    public void ShowsMarginsInPixelsOfTheOutput()
    {
        var (vm, _) = Make();
        Assert.Equal((160, 64, 160, 64), (vm.Left, vm.Top, vm.Right, vm.Bottom));
        Assert.Equal("Link", vm.LinkCaption);
    }

    [Fact]
    public void EditingAnEdge_GoesThroughMarginMath()
    {
        var (vm, settings) = Make();
        vm.Left = 200;
        var expected = MarginMath.SetEdgePixels(MarginSettings_Default(), MarginEdge.Left, 200, new PixelSize(1920, 1080));
        Assert.Equal(expected.Left, settings.Current.Margins.Left, 6);
        Assert.Equal(MarginEdge.Left, settings.Current.Margins.LastEdited);
        Assert.Equal(200, vm.Left);
    }

    [Fact]
    public void LinkToggle_AndLinkedEdits_AreMarginMathsResult()
    {
        var (vm, settings) = Make();
        var size = new PixelSize(1920, 1080);
        vm.Top = 100;
        vm.IsLinked = true;
        var linked = MarginMath.SetLinked(MarginMath.SetEdgePixels(MarginSettings_Default(), MarginEdge.Top, 100, size), true, size);
        Assert.True(settings.Current.Margins.IsLinked);
        Assert.Equal("Linked", vm.LinkCaption);
        Assert.Equal(linked.Left, settings.Current.Margins.Left, 6);
        Assert.Equal(linked.Bottom, settings.Current.Margins.Bottom, 6);

        vm.Right = 120;
        var after = MarginMath.SetEdgePixels(linked, MarginEdge.Right, 120, size);
        Assert.Equal((after.Left, after.Top, after.Right, after.Bottom), (settings.Current.Margins.Left, settings.Current.Margins.Top, settings.Current.Margins.Right, settings.Current.Margins.Bottom));
    }

    [Fact]
    public void LimitsFollowMarginMaxFraction()
    {
        var (vm, _) = Make();
        Assert.Equal(Math.Floor(1920 * MarginMath.MaxFraction), vm.MaxHorizontal);
        Assert.Equal(Math.Floor(1080 * MarginMath.MaxFraction), vm.MaxVertical);
    }

    [Fact]
    public void ChangingTheOutputResolution_KeepsTheProportions()
    {
        var settings = new FakeSettings();
        var output = new FakeOutput();
        var vm = new MarginEditorViewModel(settings, output, new ImmediateUi());
        output.Set(output.State with { Geometry = new OutputGeometry(3840, 2160, 1.5, 60) });
        Assert.Equal((320, 128, 320, 128), (vm.Left, vm.Top, vm.Right, vm.Bottom));
    }

    [Fact]
    public void OutsideChanges_Refresh_WithoutWritingBack()
    {
        var (vm, settings) = Make();
        var before = settings.UpdateCount;
        settings.Update(s => s with { Margins = s.Margins with { Left = 100.0 / 1920.0 } });
        Assert.Equal(100, vm.Left);
        Assert.Equal(before + 1, settings.UpdateCount);
    }

    private static MarginSettings MarginSettings_Default() => PrompterSettings.Default.Margins;
}

public class SectionsViewModelTests
{
    private sealed class Rig
    {
        public PromptDocumentService Documents { get; } = new();
        public FakeEngine Engine { get; } = new();
        public SectionsViewModel Vm { get; }

        public Rig() => Vm = new SectionsViewModel(Documents, Engine, new FakeSettings(), new ImmediateUi());

        public void Publish(params string[] names) =>
            Documents.Publish(new PromptDocumentSnapshot(Documents.NextVersion(), "", 20, 10,
                names.Select((n, i) => new SectionInfo(i, n, i * 2)).ToArray(), 100));
    }

    [Fact]
    public void RowsFollowThePublishedSnapshot()
    {
        var rig = new Rig();
        Assert.True(rig.Vm.IsEmpty);
        Assert.Contains("Ctrl+Shift+Enter", rig.Vm.EmptyText);
        rig.Publish("Welcome", "Sponsors", "Q&A");
        Assert.Equal(["Welcome", "Sponsors", "Q&A"], rig.Vm.Rows.Select(r => r.Name));
        Assert.Equal("2", rig.Vm.Rows[1].Number);
        Assert.False(rig.Vm.IsEmpty);
    }

    [Fact]
    public void TheCurrentRow_IsTheEnginesSectionAtTheReadingLine()
    {
        var rig = new Rig();
        rig.Publish("A", "B", "C");
        rig.Engine.Status = new EngineStatus { SectionIndex = 1 };
        rig.Vm.Poll();
        Assert.Equal([false, true, false], rig.Vm.Rows.Select(r => r.IsCurrent));
        Assert.Equal("current", rig.Vm.Rows[1].CurrentTag);
        rig.Engine.Status = new EngineStatus { SectionIndex = -1 };
        rig.Vm.Poll();
        Assert.DoesNotContain(rig.Vm.Rows, r => r.IsCurrent);
    }

    [Fact]
    public void DoubleClick_JumpsThePrompt_AndSingleClickOnlyShowsTheSection()
    {
        var rig = new Rig();
        rig.Publish("A", "B");
        var shown = new List<int>();
        rig.Vm.Attach(new RecordingEditing(shown));
        rig.Vm.ShowInEditor(rig.Vm.Rows[1]);
        Assert.Equal([1], shown);
        Assert.Empty(rig.Engine.Posted);

        rig.Vm.IsFlyoutOpen = true;
        rig.Vm.JumpTo(rig.Vm.Rows[1]);
        Assert.Equal(EngineCommand.GoToSection(1), Assert.Single(rig.Engine.Posted));
        Assert.False(rig.Vm.IsFlyoutOpen); // a jump closes the flyout
    }

    [Fact]
    public void RenameAndDelete_AskTheEditor()
    {
        var rig = new Rig();
        rig.Publish("A");
        var log = new List<string>();
        rig.Vm.Attach(new LoggingEditing(log));
        rig.Vm.Rename(rig.Vm.Rows[0], "  Opening  ");
        rig.Vm.Rename(rig.Vm.Rows[0], "   "); // blank names are ignored
        rig.Vm.DeleteMarker(rig.Vm.Rows[0]);
        Assert.Equal(["rename 0 Opening", "delete 0"], log);
    }

    [Fact]
    public void RebuildingRows_KeepsTheSelectionByIndex()
    {
        var rig = new Rig();
        rig.Publish("A", "B");
        rig.Vm.Selected = rig.Vm.Rows[1];
        rig.Publish("A", "B", "C");
        Assert.Equal(1, rig.Vm.Selected?.Index);
    }

    [Fact]
    public void RebuildingRows_DoesNotShowTheSectionInTheEditor_SoTypingKeepsTheCaret()
    {
        var rig = new Rig();
        rig.Publish("A", "B");
        var shown = new List<int>();
        rig.Vm.Attach(new RecordingEditing(shown));
        // What the panel does: a selection change shows that section in the editor.
        rig.Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SectionsViewModel.Selected)) rig.Vm.ShowInEditor(rig.Vm.Selected);
        };
        rig.Vm.Selected = rig.Vm.Rows[1];
        Assert.Equal([1], shown);

        rig.Publish("A", "B", "C"); // typing above a marker changed the list: rows are rebuilt, selection restored
        Assert.Equal(1, rig.Vm.Selected?.Index);
        Assert.Equal([1], shown);
    }

    private sealed class RecordingEditing(List<int> shown) : ISectionEditing
    {
        public void ShowSection(int sectionIndex) => shown.Add(sectionIndex);

        public void RenameSection(int sectionIndex, string newName)
        {
        }

        public void DeleteSectionMarker(int sectionIndex)
        {
        }
    }

    private sealed class LoggingEditing(List<string> log) : ISectionEditing
    {
        public void ShowSection(int sectionIndex)
        {
        }

        public void RenameSection(int sectionIndex, string newName) => log.Add($"rename {sectionIndex} {newName}");

        public void DeleteSectionMarker(int sectionIndex) => log.Add($"delete {sectionIndex}");
    }
}

public class OutputControllerTests
{
    [Fact]
    public async Task Toggle_OpensTheOutput_AndRemembersYouPressedShow()
    {
        var output = new FakeOutput();
        var dialogs = new FakeDialogs();
        var controller = new OutputController(output, dialogs, new NullLog());
        await controller.ToggleAsync();
        Assert.True(output.State.IsOpen);
        Assert.True(controller.UserPressedShow);
        Assert.Empty(dialogs.Asked);
    }

    [Fact]
    public async Task ClosingWhileLive_Asks_WithTheSafeChoiceAsDefault()
    {
        var output = new FakeOutput();
        output.Set(output.State with { IsOpen = true, Display = OutputDisplayStatus.Ready });
        var dialogs = new FakeDialogs { Answer = DialogChoice.Secondary };
        var controller = new OutputController(output, dialogs, new NullLog());
        await controller.ToggleAsync();
        Assert.True(output.State.IsOpen); // "Keep output"
        var asked = Assert.Single(dialogs.Asked);
        Assert.Equal(("Close the output?", "Close output", "Keep output", false), (asked.Title, asked.PrimaryLabel, asked.SecondaryLabel, asked.PrimaryIsDefault));

        dialogs.Answer = DialogChoice.Primary;
        await controller.ToggleAsync();
        Assert.False(output.State.IsOpen);
        Assert.False(controller.UserPressedShow);
    }

    [Fact]
    public async Task ClosingWhileBlank_DoesNotAsk()
    {
        var output = new FakeOutput();
        output.Set(output.State with { IsOpen = true, IsBlank = true, Display = OutputDisplayStatus.Ready });
        var dialogs = new FakeDialogs();
        await new OutputController(output, dialogs, new NullLog()).ToggleAsync();
        Assert.False(output.State.IsOpen);
        Assert.Empty(dialogs.Asked);
    }

    [Fact]
    public void Blank_OnlyCallsTheOutputOnAChange()
    {
        var output = new FakeOutput();
        var controller = new OutputController(output, new FakeDialogs(), new NullLog());
        controller.SetBlank(false);
        Assert.Equal(0, output.BlankCalls);
        controller.ToggleBlank();
        Assert.True(output.State.IsBlank);
    }
}

public class SettingsPageViewModelTests
{
    [Fact]
    public void ControllerPage_ShowsCardsChipsAndAMappingForTheChosenController()
    {
        var settings = new FakeSettings();
        var page = new ControllerPageViewModel(settings, new ImmediateUi(), new FakeRouter(), new FakeOutput(), []);
        Assert.True(page.WheelSelected);
        Assert.Equal("READY", page.WheelChip.State);
        Assert.Equal("NOT IN USE", page.XboxChip.State);
        Assert.Contains(page.Mapping, r => r.Control == "Middle click" && r.Action == "Play or pause");

        page.XboxSelected = true;
        Assert.Equal(ControllerKind.Xbox, settings.Current.Input.SelectedController);
        Assert.False(page.WheelSelected);
        Assert.Contains(page.Mapping, r => r.Control == "Right trigger" && r.Action == "Speed, forward");
        Assert.Contains(page.Mapping, r => r.Control == "View button" && r.Action == "Blank output");
        Assert.Equal("MISSING", page.XboxChip.State);
    }

    [Fact]
    public void ControllerPage_DevicePickerWritesTheChoiceBack_AndIgnoresRebuilds()
    {
        var settings = new FakeSettings();
        var page = new ControllerPageViewModel(settings, new ImmediateUi(), new FakeRouter(), new FakeOutput(), []);
        page.XboxSelected = true;
        page.SelectedDeviceId = "xinput:2";
        Assert.Equal(2, settings.Current.Input.Xbox.UserIndex);
        page.SelectedDeviceId = null!; // what a combo box does while its list is rebuilt
        Assert.Equal(2, settings.Current.Input.Xbox.UserIndex);
        page.SelectedDeviceId = ControllerPageViewModel.AnyDevice;
        Assert.Equal(-1, settings.Current.Input.Xbox.UserIndex);
    }

    private sealed class CountingWheelProvider : IScrollInputProvider
    {
        public int Enumerations;

        public ControllerKind Kind => ControllerKind.MouseWheel;

        public bool IsRunning => false;

        public IReadOnlyList<InputDeviceInfo> EnumerateDevices()
        {
            Enumerations++;
            return Array.Empty<InputDeviceInfo>();
        }

        public void Start(IInputSink sink, string? deviceId) => throw new InvalidOperationException("the page never starts a provider");

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public void ControllerPage_ListsDevicesAgainOnlyWhenTheInputSettingsChange()
    {
        var settings = new FakeSettings();
        var provider = new CountingWheelProvider();
        var page = new ControllerPageViewModel(settings, new ImmediateUi(), new FakeRouter(), new FakeOutput(), [provider]);
        var listed = provider.Enumerations;

        // Margins, speed, lines: nothing to do with input devices.
        settings.Update(s => s with { Text = s.Text with { LinesVisible = 6 } });
        settings.Update(s => s with { Margins = s.Margins with { Left = 0.2 } });
        Assert.Equal(listed, provider.Enumerations);

        settings.Update(s => s with { Input = s.Input with { Wheel = s.Input.Wheel with { DedicatedDeviceId = "fake-mouse" } } }); // fake settings and router: nothing starts
        Assert.Equal(listed + 1, provider.Enumerations);
        Assert.NotNull(page);
    }

    [Fact]
    public void ControllerPage_SensitivityEditsTheRightBinding()
    {
        var settings = new FakeSettings();
        var page = new ControllerPageViewModel(settings, new ImmediateUi(), new FakeRouter(), new FakeOutput(), []);
        page.SensitivityValue = 1.5;
        Assert.Equal(1.5, settings.Current.Input.Wheel.Wheel.SpeedPerUnit);
        page.XboxSelected = true;
        page.SensitivityValue = 6;
        Assert.Equal(6, settings.Current.Input.Xbox.Axes[ControlIds.RightTrigger].MaxSpeed);
        Assert.Equal(6, settings.Current.Input.Xbox.Axes[ControlIds.LeftTrigger].MaxSpeed);
    }

    [Fact]
    public void ControllerPage_TestReadoutDescribesTheLatestInput()
    {
        var router = new FakeRouter();
        var page = new ControllerPageViewModel(new FakeSettings(), new ImmediateUi(), router, new FakeOutput(), []);
        Assert.False(page.HasTestInput);
        router.RaiseInput(new ButtonInput(new InputSourceId(ControllerKind.Keyboard, "app"), "Space", 0, true)); // keys are not controllers
        page.Poll();
        Assert.False(page.HasTestInput);
        router.RaiseInput(new AxisInput(new InputSourceId(ControllerKind.Xbox, "xinput:0"), ControlIds.RightTrigger, 0, 0.62));
        page.Poll();
        Assert.Equal("Right trigger · 0.62", page.TestReadout);
    }

    [Fact]
    public void OutputLayout_ClampsAndWritesSettings()
    {
        var settings = new FakeSettings();
        var margins = new MarginEditorViewModel(settings, new FakeOutput(), new ImmediateUi());
        var layout = new OutputLayoutViewModel(settings, new ImmediateUi(), margins);
        layout.LinesVisible = 40;
        Assert.Equal(16, settings.Current.Text.LinesVisible);
        layout.LinesVisible = 1;
        Assert.Equal(4, settings.Current.Text.LinesVisible);
        layout.LineSpacing = 1.37;
        Assert.Equal(1.35, settings.Current.Text.LineSpacing, 3);
        layout.ReadingLinePercent = 90;
        Assert.Equal(0.60, settings.Current.Output.ReadingLineFraction, 3);
        layout.ShowSectionNames = true;
        Assert.True(settings.Current.Output.ShowSectionNames);
    }

    [WpfFact]
    public void ResetThisPage_AsksFirst_AndRestoresDefaults()
    {
        var settings = new FakeSettings();
        var ui = new ImmediateUi();
        var margins = new MarginEditorViewModel(settings, new FakeOutput(), ui);
        var layout = new OutputLayoutViewModel(settings, ui, margins);
        var dialogs = new FakeDialogs { Answer = DialogChoice.Secondary };
        var vm = new SettingsViewModel(
            new ControllerPageViewModel(settings, ui, new FakeRouter(), new FakeOutput(), []),
            new OutputPageViewModel(settings, ui, new FakeMonitors(), layout),
            new TextPageViewModel(settings, ui),
            new ScrollingPageViewModel(settings, ui),
            new ShortcutsPageViewModel(),
            new AboutPageViewModel(),
            dialogs);

        vm.Select(SettingsPageId.Scrolling);
        settings.Update(s => s with { Scroll = s.Scroll with { StartSpeed = 7, PauseAtSections = true } });
        vm.ResetPageCommand.Execute(null);
        Assert.Equal("Reset this page to defaults?", Assert.Single(dialogs.Asked).Title);
        Assert.Equal(7, settings.Current.Scroll.StartSpeed); // "Keep settings"

        dialogs.Answer = DialogChoice.Primary;
        vm.ResetPageCommand.Execute(null);
        Assert.Equal(4.5, settings.Current.Scroll.StartSpeed);
        Assert.False(settings.Current.Scroll.PauseAtSections);

        vm.Select(SettingsPageId.Shortcuts);
        Assert.False(vm.CanResetPage);
        vm.Select(SettingsPageId.About);
        Assert.False(vm.CanResetPage);
    }

    [WpfFact]
    public void TextPage_OtherFont_ShowsTheBoldAndItalicWarning()
    {
        var settings = new FakeSettings();
        var page = new TextPageViewModel(settings, new ImmediateUi());
        Assert.Equal("Poppins", page.SelectedFace);
        Assert.False(page.IsOtherFace);
        page.SelectedFace = TextPageViewModel.OtherFace;
        Assert.True(page.IsOtherFace);
        Assert.Equal("▲ Check bold and italic on the output", page.OtherWarning);
        page.SelectedFace = "Arial";
        Assert.Equal("Arial", settings.Current.Text.FontFace);
        page.IsRegularWeight = true;
        Assert.Equal(400, settings.Current.Text.FontWeight);
    }

    [WpfFact]
    public void Pages_AreNamedAsDesignListsThem()
    {
        var settings = new FakeSettings();
        var ui = new ImmediateUi();
        var margins = new MarginEditorViewModel(settings, new FakeOutput(), ui);
        var vm = new SettingsViewModel(
            new ControllerPageViewModel(settings, ui, new FakeRouter(), new FakeOutput(), []),
            new OutputPageViewModel(settings, ui, new FakeMonitors(), new OutputLayoutViewModel(settings, ui, margins)),
            new TextPageViewModel(settings, ui), new ScrollingPageViewModel(settings, ui), new ShortcutsPageViewModel(), new AboutPageViewModel(), new FakeDialogs());
        Assert.Equal(["Controller", "Output", "Text", "Scrolling", "Shortcuts", "About"], vm.Pages.Select(p => p.Title));
    }

    [WpfFact]
    public void TextPage_ListsTheEightInks()
    {
        var page = new TextPageViewModel(new FakeSettings(), new ImmediateUi());
        Assert.Equal(ThemeResources.Inks.Count, page.Inks.Count);
        Assert.Equal(8, page.Inks.Count);
    }
}
