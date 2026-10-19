using IluPrompter.App.Logic;
using IluPrompter.Core;

namespace IluPrompter.App.Tests;

/// <summary>Every row of DESIGN.md's output chip table and controller chip table.</summary>
public class ChipMapperTests
{
    private static OutputState Closed(OutputDisplayStatus display = OutputDisplayStatus.Ready) =>
        OutputState.Initial with { Display = display };

    private static OutputState Open(bool blank = false, OutputDisplayStatus display = OutputDisplayStatus.Ready, bool withMonitor = true) =>
        OutputState.Initial with { IsOpen = true, IsBlank = blank, Display = display, Monitor = withMonitor ? FakeMonitors.Display2() : null };

    private static OutputChipContext Ctx(bool pressed = false, bool second = true, bool duplicated = false) =>
        new(pressed, second, duplicated, second ? "Display 2" : null);

    // ----- output chip -----

    [Fact]
    public void Output_Closed_IsIdleOff()
    {
        var chip = ChipMapper.Output(Closed(), Ctx());
        Assert.Equal((ChipKind.Idle, "○", "Output", "OFF"), (chip.Kind, chip.Glyph, chip.Subject, chip.State));
        Assert.Equal("Press F11 to show the script on Display 2.", chip.Tooltip);
    }

    [Fact]
    public void Output_OpenAndShowing_IsLive()
    {
        var chip = ChipMapper.Output(Open(), Ctx());
        Assert.Equal((ChipKind.Live, "▶", "Display 2", "LIVE"), (chip.Kind, chip.Glyph, chip.Subject, chip.State));
        Assert.Equal("The presenter can see this. Changes show on Display 2 now.", chip.Tooltip);
    }

    [Fact]
    public void Output_OpenAndBlanked_IsActiveBlank()
    {
        var chip = ChipMapper.Output(Open(blank: true), Ctx());
        Assert.Equal((ChipKind.Active, "■", "Display 2", "BLANK"), (chip.Kind, chip.Glyph, chip.Subject, chip.State));
        Assert.Equal("The presenter sees black. Press B to show the script.", chip.Tooltip);
    }

    [Fact]
    public void Output_WindowsMirroring_IsAttentionDuplicated()
    {
        var chip = ChipMapper.Output(Open(display: OutputDisplayStatus.Duplicated), Ctx(duplicated: true));
        Assert.Equal((ChipKind.Attention, "▲", "DUPLICATED"), (chip.Kind, chip.Glyph, chip.State));
        Assert.Contains("Extend these displays", chip.Tooltip);
        // Also before you press Show output, as soon as Windows reports it.
        Assert.Equal("DUPLICATED", ChipMapper.Output(Closed(), Ctx(duplicated: true)).State);
    }

    [Fact]
    public void Output_OneDisplay_IsIdle_UntilYouPressShow_ThenAttention()
    {
        var before = ChipMapper.Output(Closed(OutputDisplayStatus.NoSecondDisplay), Ctx(second: false));
        Assert.Equal((ChipKind.Idle, "○", "Output", "NO 2ND DISPLAY"), (before.Kind, before.Glyph, before.Subject, before.State));
        Assert.Equal("Connect the presenter's display, then press F11.", before.Tooltip);

        var after = ChipMapper.Output(Closed(OutputDisplayStatus.NoSecondDisplay), Ctx(pressed: true, second: false));
        Assert.Equal((ChipKind.Attention, "▲", "NO 2ND DISPLAY"), (after.Kind, after.Glyph, after.State));
    }

    [Fact]
    public void Output_ChosenDisplayVanishedWhileOpen_IsFaultNoDisplay()
    {
        var chip = ChipMapper.Output(Open(display: OutputDisplayStatus.Missing), Ctx());
        Assert.Equal((ChipKind.Fault, "✕", "Display 2", "NO DISPLAY"), (chip.Kind, chip.Glyph, chip.Subject, chip.State));
        Assert.Equal("Display 2 disconnected. The output will come back when it does.", chip.Tooltip);
    }

    [Fact]
    public void OutputChip_NeverCarriesMoreThanOneSubject_AndAlwaysAGlyphAndAWord()
    {
        var all = new[]
        {
            ChipMapper.Output(Closed(), Ctx()), ChipMapper.Output(Open(), Ctx()), ChipMapper.Output(Open(blank: true), Ctx()),
            ChipMapper.Output(Open(display: OutputDisplayStatus.Duplicated), Ctx(duplicated: true)),
            ChipMapper.Output(Closed(OutputDisplayStatus.NoSecondDisplay), Ctx(second: false)),
            ChipMapper.Output(Open(display: OutputDisplayStatus.Missing), Ctx()),
        };
        foreach (var chip in all)
        {
            Assert.NotEmpty(chip.Glyph);
            Assert.Equal(chip.State.ToUpperInvariant(), chip.State); // state words are capitals
            Assert.DoesNotContain("·", chip.Subject);
        }
    }

    // ----- controller chip -----

    private static DeviceStatus Status(ControllerKind kind, DeviceConnection connection, BatteryLevel battery = BatteryLevel.Unknown) =>
        new(new InputSourceId(kind, "x"), kind.ToString(), connection, battery);

    [Fact]
    public void Controller_Wheel_IsAlwaysReady() =>
        Assert.Equal((ChipKind.Ok, "✓", "Wheel", "READY"), Tuple(ChipMapper.Controller(ControllerKind.MouseWheel, null, outputIsLive: true)));

    [Theory]
    [InlineData(ControllerKind.Xbox, "Xbox")]
    [InlineData(ControllerKind.JogShuttle, "Shuttle")]
    public void Controller_Connected_IsReady(ControllerKind kind, string name) =>
        Assert.Equal((ChipKind.Ok, "✓", name, "READY"), Tuple(ChipMapper.Controller(kind, Status(kind, DeviceConnection.Connected), false)));

    [Fact]
    public void Controller_LowBattery_IsAttention() =>
        Assert.Equal((ChipKind.Attention, "▲", "Xbox", "BATTERY LOW"),
            Tuple(ChipMapper.Controller(ControllerKind.Xbox, Status(ControllerKind.Xbox, DeviceConnection.Connected, BatteryLevel.Low), false)));

    [Fact]
    public void Controller_LostWhileNotLive_IsAttentionMissing() =>
        Assert.Equal((ChipKind.Attention, "▲", "Xbox", "MISSING"),
            Tuple(ChipMapper.Controller(ControllerKind.Xbox, Status(ControllerKind.Xbox, DeviceConnection.Disconnected), outputIsLive: false)));

    [Fact]
    public void Controller_LostWhileLive_IsFaultMissing() =>
        Assert.Equal((ChipKind.Fault, "✕", "Xbox", "MISSING"),
            Tuple(ChipMapper.Controller(ControllerKind.Xbox, Status(ControllerKind.Xbox, DeviceConnection.Disconnected), outputIsLive: true)));

    [Fact]
    public void Controller_NeverSeen_IsMissingToo() =>
        Assert.Equal("MISSING", ChipMapper.Controller(ControllerKind.JogShuttle, null, false).State);

    [Fact]
    public void EveryControllerTooltip_EndsWithTheKeyboardSentence()
    {
        var tips = new[]
        {
            ChipMapper.Controller(ControllerKind.MouseWheel, null, false),
            ChipMapper.Controller(ControllerKind.Xbox, Status(ControllerKind.Xbox, DeviceConnection.Connected), false),
            ChipMapper.Controller(ControllerKind.Xbox, Status(ControllerKind.Xbox, DeviceConnection.Connected, BatteryLevel.Empty), false),
            ChipMapper.Controller(ControllerKind.Xbox, Status(ControllerKind.Xbox, DeviceConnection.Disconnected), true),
        };
        foreach (var chip in tips) Assert.EndsWith("The keyboard still works: Space plays and pauses.", chip.Tooltip);
    }

    [Fact]
    public void ChipStyleTag_MatchesTheThemeTags()
    {
        Assert.Equal("ok", ChipMapper.Controller(ControllerKind.MouseWheel, null, false).StyleTag);
        Assert.Equal("attention", ChipMapper.Output(Open(display: OutputDisplayStatus.Duplicated), Ctx(duplicated: true)).StyleTag);
    }

    private static (ChipKind, string, string, string) Tuple(ChipModel c) => (c.Kind, c.Glyph, c.Subject, c.State);
}
