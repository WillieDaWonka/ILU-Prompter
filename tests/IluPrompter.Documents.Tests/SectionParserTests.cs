using IluPrompter.Core;

namespace IluPrompter.Documents.Tests;

public class SectionParserTests
{
    private readonly SectionParser _parser = new();

    [Theory]
    [InlineData("## Welcome", "##", true, "Welcome")]
    [InlineData("##Welcome", "##", true, "Welcome")]
    [InlineData("   ## Indented", "##", true, "Indented")]
    [InlineData("\t## Tabbed", "##", true, "Tabbed")]
    [InlineData("### Third level", "##", true, "Third level")]
    [InlineData("##  # Extra hashes  ", "##", true, "Extra hashes")]
    [InlineData("##", "##", true, "")]
    [InlineData("##   ", "##", true, "")]
    [InlineData("# Single", "##", false, "")]
    [InlineData("Not ## a section", "##", false, "")]
    [InlineData("", "##", false, "")]
    [InlineData("## Two\r\nlines", "##", true, "Two")]
    [InlineData("@@ Custom marker", "@@", true, "Custom marker")]
    [InlineData("## Not this marker", "@@", false, "")]
    [InlineData("## Anything", "", false, "")]
    public void TryParse_ReadsMarkers(string text, string marker, bool expected, string name)
    {
        Assert.Equal(expected, _parser.TryParse(text, marker, out var parsed));
        Assert.Equal(name, parsed);
    }

    [Fact]
    public void Parse_NumbersSectionsAndNamesUnnamedOnes()
    {
        var sections = _parser.Parse(["Intro", "## Welcome", "Text", "##", "More", "### Q and A"], "##");

        Assert.Equal(3, sections.Count);
        Assert.Equal(new SectionInfo(0, "Welcome", 1), sections[0]);
        Assert.Equal(new SectionInfo(1, "Section 2", 3), sections[1]);
        Assert.Equal(new SectionInfo(2, "Q and A", 5), sections[2]);
    }

    [Fact]
    public void Parse_NoMarkers_IsEmpty()
    {
        Assert.Empty(_parser.Parse(["one", "two"], "##"));
        Assert.Empty(_parser.Parse([], "##"));
    }
}
