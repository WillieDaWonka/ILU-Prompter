using System.Windows.Documents;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering.Tests;

/// <summary>The layout thread on its own: follows snapshots and settings, patches edits, never runs on the caller's thread.</summary>
public class PictureWorkerTests
{
    private static readonly DocumentNavigator Navigator = new();
    private static readonly SnapshotBuilder Snapshots = new(Navigator, new SectionParser());

    [WpfFact]
    public void FollowsSnapshotsAndSettings_OnItsOwnThread_PatchingEdits()
    {
        var documents = new PromptDocumentService();
        var settings = new FakeSettings();
        var log = new RecordingLog();
        using var worker = new PictureWorker(documents, settings, new OutputDocumentFactory(Snapshots), new LayoutGeometryBuilder(Navigator), log,
            () => OutputGeometry.Default1080);
        var threads = new List<int>();
        worker.PictureReady += (_, _) => { lock (threads) threads.Add(Environment.CurrentManagedThreadId); };

        var editor = new FlowDocument();
        for (var i = 0; i < 20; i++) editor.Blocks.Add(new Paragraph(new Run($"Paragraph {i}: good evening, everyone, and welcome to the meeting.")));
        documents.Publish(Snapshots.Build(editor, documents.NextVersion(), 20, "##"));
        worker.Start();

        Assert.True(Wait.For(() => worker.Current?.Geometry.SnapshotVersion == 1, 10_000), "no first picture");
        var first = worker.Current!;
        Assert.True(first.Geometry.LineCount >= 20);
        Assert.All(first.Blocks, b => Assert.True(b.Drawing.IsFrozen));

        // An edit: patched, with the map back to the picture it replaces.
        editor.Blocks.InsertBefore(editor.Blocks.FirstBlock, new Paragraph(new Run("A new first paragraph.")));
        documents.Publish(Snapshots.Build(editor, documents.NextVersion(), 20, "##"));
        Assert.True(Wait.For(() => worker.Current?.Geometry.SnapshotVersion == 2, 10_000), "edit not laid out");
        var second = worker.Current!;
        Assert.Equal(1, second.PatchedParagraphs);
        Assert.Same(first.Geometry, second.PreviousGeometry);
        Assert.Equal(new DocumentAnchor(5, 3), second.MapToPrevious!.Map(new DocumentAnchor(6, 3)));

        // A text setting: the whole script again, same snapshot.
        settings.Update(s => s with { Text = s.Text with { LinesVisible = 6 } });
        Assert.True(Wait.For(() => worker.Current?.ContentKey != second.ContentKey, 10_000), "settings not followed");
        Assert.Equal(-1, worker.Current!.PatchedParagraphs);

        // Unrelated settings and the same snapshot do not build anything.
        var builds = worker.BuildCount;
        settings.Update(s => s with { Scroll = s.Scroll with { StartSpeed = 5 } });
        worker.Request();
        Thread.Sleep(300);
        Assert.Equal(builds, worker.BuildCount);

        lock (threads) Assert.DoesNotContain(Environment.CurrentManagedThreadId, threads);
        Assert.DoesNotContain(log.Lines, l => l.StartsWith("ERROR", StringComparison.Ordinal));
    }
}
