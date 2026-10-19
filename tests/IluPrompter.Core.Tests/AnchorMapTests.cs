namespace IluPrompter.Core.Tests;

public class AnchorMapTests
{
    private static readonly string[] Base = ["alpha one", "bravo two", "charlie three", "delta four", "echo five", "foxtrot six"];

    private static DocumentAnchor Map(IReadOnlyList<string> from, IReadOnlyList<string> to, int p, int c) =>
        new AnchorMap(from, to).Map(new DocumentAnchor(p, c));

    [Fact]
    public void SameText_IsIdentity()
    {
        var map = new AnchorMap(Base, Base.ToArray());
        Assert.True(map.IsIdentity);
        Assert.Equal(new DocumentAnchor(3, 2), map.Map(new DocumentAnchor(3, 2)));
    }

    [Fact]
    public void InsertingParagraphsAbove_ShiftsTheIndex()
    {
        string[] to = ["alpha one", "new A", "new B", "bravo two", "charlie three", "delta four", "echo five", "foxtrot six"];
        Assert.Equal(new DocumentAnchor(5, 3), Map(Base, to, 3, 3));
        Assert.Equal(new DocumentAnchor(0, 3), Map(Base, to, 0, 3));   // above the insertion: unchanged
    }

    [Fact]
    public void DeletingARangeAbove_ShiftsTheIndexBack()
    {
        string[] to = ["alpha one", "echo five", "foxtrot six"];
        Assert.Equal(new DocumentAnchor(1, 4), Map(Base, to, 4, 4));
        Assert.Equal(new DocumentAnchor(2, 0), Map(Base, to, 5, 0));
    }

    [Fact]
    public void DeletingTheAnchorParagraph_LandsOnTheTextAfterIt()
    {
        string[] to = ["alpha one", "bravo two", "delta four", "echo five", "foxtrot six"];
        Assert.Equal(new DocumentAnchor(2, 0), Map(Base, to, 2, 5));
    }

    [Fact]
    public void TypingBeforeTheAnchorCharacter_InItsParagraph_ShiftsTheOffset()
    {
        var to = Base.ToArray();
        to[3] = "XYZ delta four";
        Assert.Equal(new DocumentAnchor(3, 6 + 4), Map(Base, to, 3, 6));
    }

    [Fact]
    public void TypingAfterTheAnchorCharacter_KeepsTheOffset()
    {
        var to = Base.ToArray();
        to[3] = "delta four and more";
        Assert.Equal(new DocumentAnchor(3, 6), Map(Base, to, 3, 6));
    }

    [Fact]
    public void DeletingTheAnchorCharacter_MapsToWhereTheDeletionWas()
    {
        var to = Base.ToArray();
        to[3] = "delr";          // "delta four" lost "ta fou"
        Assert.Equal(new DocumentAnchor(3, 3), Map(Base, to, 3, 6));
    }

    [Fact]
    public void Enter_SplitsTheParagraph_AndTheAnchorFollowsItsText()
    {
        string[] to = ["alpha one", "bravo two", "charlie three", "delta", " four", "echo five", "foxtrot six"];
        Assert.Equal(new DocumentAnchor(4, 1), Map(Base, to, 3, 6));   // 'f' of "four"
        Assert.Equal(new DocumentAnchor(3, 2), Map(Base, to, 3, 2));
        Assert.Equal(new DocumentAnchor(5, 0), Map(Base, to, 4, 0));
    }

    [Fact]
    public void Backspace_JoinsParagraphs_AndTheAnchorFollowsItsText()
    {
        string[] to = ["alpha one", "bravo two", "charlie threedelta four", "echo five", "foxtrot six"];
        Assert.Equal(new DocumentAnchor(2, 13 + 6), Map(Base, to, 3, 6));
        Assert.Equal(new DocumentAnchor(3, 2), Map(Base, to, 4, 2));
    }

    [Fact]
    public void TwoSeparateEditsInOneStep_DoNotMoveTheTextBetweenThem()
    {
        var from = ScriptLayout.Script(40);
        var to = from.ToList();
        to[2] = to[2] + " edited";
        to.Insert(35, "inserted near the end");
        Assert.Equal(new DocumentAnchor(20, 7), new AnchorMap(from, to).Map(new DocumentAnchor(20, 7)));
        Assert.Equal(new DocumentAnchor(37, 3), new AnchorMap(from, to).Map(new DocumentAnchor(36, 3)));
    }

    [Fact]
    public void MovedParagraph_IsFoundByItsUniqueText()
    {
        string[] to = ["alpha one", "charlie three", "delta four", "bravo two", "echo five", "foxtrot six"];
        var map = new AnchorMap(Base, to);
        Assert.Equal(new DocumentAnchor(2, 1), map.Map(new DocumentAnchor(3, 1)));
    }

    [Fact]
    public void RepeatedParagraphs_UseThePositionalDiff()
    {
        string[] from = ["Thank you.", "x", "Thank you.", "y", "Thank you."];
        string[] to = ["new", "Thank you.", "x", "Thank you.", "y", "Thank you."];
        Assert.Equal(new DocumentAnchor(3, 2), Map(from, to, 2, 2));
    }

    [Fact]
    public void EverythingDeleted_MapsToTheStart()
    {
        Assert.Equal(new DocumentAnchor(0, 0), Map(Base, [""], 4, 2));
    }

    [Fact]
    public void Between_SameSnapshotOrNoTexts_IsIdentity()
    {
        var a = ScriptLayout.Build(Base, 1);
        var b = ScriptLayout.Build(Base, 1, lineHeight: 150);
        Assert.True(AnchorMap.Between(a, b).IsIdentity);
        Assert.True(AnchorMap.Between(TestGeometry.Create(), a).IsIdentity);
        Assert.False(AnchorMap.Between(a, ScriptLayout.Build(Base.Append("x").ToArray(), 2)).IsIdentity);
    }

    [Fact]
    public void Map_DoesNotAllocate()
    {
        var from = ScriptLayout.Script(200);
        var to = from.ToList();
        to[100] = "changed " + to[100];
        to.RemoveRange(10, 5);
        var map = new AnchorMap(from, to);
        _ = map.Map(new DocumentAnchor(100, 4));
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) _ = map.Map(new DocumentAnchor(i % 200, i % 30));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
