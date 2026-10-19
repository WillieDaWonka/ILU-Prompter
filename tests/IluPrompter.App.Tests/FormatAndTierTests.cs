using IluPrompter.App.Controls;
using IluPrompter.App.Logic;
using IluPrompter.Core;

namespace IluPrompter.App.Tests;

public class FormatAndTierTests
{
    [Theory]
    [InlineData(1920, LayoutTier.Wide)]
    [InlineData(1600, LayoutTier.Wide)]
    [InlineData(1599.5, LayoutTier.Compact)]
    [InlineData(1366, LayoutTier.Compact)]
    [InlineData(1200, LayoutTier.Compact)]
    [InlineData(1199, LayoutTier.Narrow)]
    [InlineData(1093, LayoutTier.Narrow)]
    [InlineData(1024, LayoutTier.Narrow)]
    public void Tier_IsChosenByWidth(double width, LayoutTier expected) => Assert.Equal(expected, LayoutTiers.ForWidth(width));

    [Theory]
    [InlineData(LayoutTier.Wide, 720)]
    [InlineData(LayoutTier.Compact, 480)]
    [InlineData(LayoutTier.Narrow, 440)]
    public void LiveColumn_WidthFollowsTheTier(LayoutTier tier, double width) => Assert.Equal(width, LayoutTiers.LiveColumnWidth(tier));

    [Theory]
    [InlineData(4.5, "4.5")]
    [InlineData(0, "0.0")]
    [InlineData(10, "10.0")]
    [InlineData(-2, "−2.0")]
    [InlineData(-0.04, "0.0")]
    [InlineData(0.25, "0.3")]
    public void Speed_HasOneDecimal_AndASignOnlyWhenNegative(double speed, string expected) => Assert.Equal(expected, ReadoutFormat.Speed(speed));

    [Theory]
    [InlineData(141.2, "≈ 140 wpm")]
    [InlineData(143, "≈ 145 wpm")]
    [InlineData(0, "")]
    [InlineData(double.NaN, "")]
    public void Wpm_RoundsToFive(double wpm, string expected) => Assert.Equal(expected, ReadoutFormat.Wpm(wpm));

    [Fact]
    public void Duration_UsesTheHouseFormat_AndDashesForInfinity()
    {
        Assert.Equal("6m:42s", ReadoutFormat.Duration(402));
        Assert.Equal("--", ReadoutFormat.Duration(double.PositiveInfinity));
        Assert.Equal("--", ReadoutFormat.Duration(double.NaN));
        Assert.Equal("0m:00s", ReadoutFormat.Duration(0));
    }

    [Fact]
    public void StepDelay_ShowsSecondsWithOneDecimal() => Assert.Equal("0.8 s", ReadoutFormat.StepDelay(0.84));

    [Fact]
    public void Readouts_ShowLineAndSectionCounts()
    {
        var s = new EngineStatus { LineIndex = 111, LineCount = 384, SectionIndex = 2, SectionCount = 7 };
        Assert.Equal("Line 112 / 384", ReadoutFormat.Line(s));
        Assert.Equal("Section 3 / 7", ReadoutFormat.Section(s));
        Assert.Equal("Line 112 of 384 · Section 3 of 7 Speaker 1", ReadoutFormat.Position(s, "Speaker 1"));
        Assert.Equal("Section - / 7", ReadoutFormat.Section(s with { SectionIndex = -1 }));
        Assert.Equal("Line 0 / 0", ReadoutFormat.Line(new EngineStatus { LineCount = 0, LineIndex = -1 }));
    }

    [Fact]
    public void Words_GroupThousandsWithANonBreakingSpace()
    {
        Assert.Equal("1 912 words", ReadoutFormat.Words(1912));
        Assert.Equal("1 word", ReadoutFormat.Words(1));
        Assert.Equal("0 words", ReadoutFormat.Words(0));
    }

    [Fact]
    public void SavedText_ReadsAsTheDesignSays()
    {
        Assert.Equal("● Not saved", ReadoutFormat.SavedText(true, null));
        Assert.StartsWith("Saved ", ReadoutFormat.SavedText(false, DateTimeOffset.Now));
    }

    [Theory]
    [InlineData("160", 160.0)]
    [InlineData(" 64 ", 64.0)]
    [InlineData("1,25", 1.25)]
    [InlineData("−2", -2.0)]
    public void NumberBox_ParsesTypedText(string text, double expected) => Assert.Equal(expected, NumberBox.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("NaN")]
    public void NumberBox_RejectsNonNumbers(string text) => Assert.Null(NumberBox.Parse(text));

    [Fact]
    public void NumberBox_FormatsWithTheGivenDecimals()
    {
        Assert.Equal("1.30", NumberBox.Format(1.3, 2));
        Assert.Equal("64", NumberBox.Format(64.4, 0));
    }

    [Fact]
    public void Shortcuts_MirrorTheDesignKeyMap()
    {
        var all = ShortcutCatalog.Groups.SelectMany(g => g.Entries).Select(e => e.Keys).ToList();
        foreach (var expected in new[] { "F5", "F8", "F11", "Space", "Home", "B", "M", "Ctrl+Shift+Enter", "Ctrl+,", "Ctrl+/" })
            Assert.Contains(expected, all);
    }

    [Fact]
    public void Labels_NameBindingsInPlainWords()
    {
        Assert.Equal("Next line", Labels.Command(PrompterCommand.LineNext));
        Assert.Equal("Speed up (Line scrolling: next line)", Labels.Action(new BindingAction(PrompterCommand.SpeedUp, PrompterCommand.LineNext)));
        Assert.Equal("View button", Labels.Control(ControlIds.View));
        Assert.Equal("Ctrl+Shift+Page Down", Labels.Chord("Ctrl+Shift+PageDown"));
    }
}
