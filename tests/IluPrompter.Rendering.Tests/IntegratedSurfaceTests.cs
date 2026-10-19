using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering.Tests;

/// <summary>
/// Integration (streams C + E): the real prompter surface with the real snapshot builder, output document factory and
/// geometry builder, at the default 1920 x 1080 output (Poppins, 8 lines, 91.5 DIP). These are the checks that only make
/// sense once the real implementations meet: line tops are line boxes (so a jog puts the text on the reading line) and
/// two surfaces on two UI threads give identical tables (Phase-0 S7).
/// </summary>
public class IntegratedSurfaceTests
{
    private static readonly string[] Script =
    [
        "Good evening, everyone, and welcome to the annual meeting of the Riverside Community Garden Trust.",
        "Please take a seat, find a cup of tea if you can, and let us begin with a look back over the year.",
        "## Money",
        "Then we will talk about the money. There will be one chart, and it has a very simple shape indeed.",
        "Thank you.",
    ];

    private sealed class Rig
    {
        public Rig(PrompterSettings? settings = null)
        {
            Navigator = new DocumentNavigator();
            Snapshots = new SnapshotBuilder(Navigator, new SectionParser());
            Settings = new FakeSettings(settings);
            var services = new SurfaceServices(Engine, Documents, Settings, new OutputDocumentFactory(Snapshots),
                new LayoutGeometryBuilder(Navigator), new RecordingLog(), () => OutputGeometry.Default1080);
            Surface = new PrompterSurface("integrated", services, snapToPixels: false);
            var editor = new FlowDocument();
            foreach (var text in Script) editor.Blocks.Add(new Paragraph(new Run(text)));
            Documents.Publish(Snapshots.Build(editor, Documents.NextVersion(), 20, "##"));
            Surface.Rebuild();
            Surface.Measure(new Size(1920, 1080));
            Surface.Arrange(new Rect(0, 0, 1920, 1080));
            Surface.UpdateLayout();
        }

        public DocumentNavigator Navigator { get; }
        public SnapshotBuilder Snapshots { get; }
        public FakeEngine Engine { get; } = new();
        public PromptDocumentService Documents { get; } = new();
        public FakeSettings Settings { get; }
        public PrompterSurface Surface { get; }
        public LayoutGeometry Geometry => Surface.Geometry!;
    }

    /// <summary>Rows (output DIP, 1:1 at 96 dpi) inside the text area that contain bright pixels, as [start, end) bands.</summary>
    private static List<(int Start, int End)> InkBands(BitmapSource bitmap, TextArea area)
    {
        var width = bitmap.PixelWidth;
        var pixels = new byte[width * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var bands = new List<(int, int)>();
        var start = -1;
        for (var y = (int)area.Top; y < (int)(area.Top + area.Height); y++)
        {
            var ink = false;
            for (var x = (int)area.Left; x < (int)(area.Left + area.Width) && !ink; x++) ink = pixels[((y * width) + x) * 4 + 1] > 100;
            if (ink && start < 0) start = y;
            if (!ink && start >= 0)
            {
                bands.Add((start, y));
                start = -1;
            }
        }

        if (start >= 0) bands.Add((start, (int)(area.Top + area.Height)));
        return bands;
    }

    [WpfFact]
    public void LineTops_AreLineBoxes_StartingAtZeroAndSteppingByTheLineHeight()
    {
        var rig = new Rig();
        var g = rig.Geometry;
        var area = rig.Surface.TextArea;
        Assert.Equal(91.538, area.BaseFontSize, 2);
        Assert.Equal(0, g.LineTops[0], 0.05); // the raw GetCharacterRect top would be -13.4 here
        for (var i = 1; i < g.LineCount; i++)
        {
            if (g.LineParagraphs[i] == g.LineParagraphs[i - 1]) Assert.Equal(area.BaseLineHeight, g.LineTops[i] - g.LineTops[i - 1], 3);
        }
    }

    [WpfFact]
    public void JoggingToALine_PutsItsInkOnTheReadingLine()
    {
        var rig = new Rig();
        var g = rig.Geometry;
        var area = rig.Surface.TextArea;
        var readingLine = area.Top + area.ReadingLineY;
        var tolerance = 0.08 * area.BaseLineHeight; // ink centre vs line-box centre for Poppins is about +0.05 L

        foreach (var line in new[] { 0, 1, 2, g.LineCount - 1 })
        {
            if (g.LineHeights[line] < area.BaseLineHeight) continue; // section marker lines carry no ink
            var position = g.LineCentre(line) - area.ReadingLineY; // what StepLines / GoToSection aim for
            rig.Surface.SetPosition(position);
            rig.Surface.UpdateLayout();
            var bands = InkBands(PreviewTests.Render(rig.Surface, 1920, 1080), area);

            var hit = bands.Where(b => b.Start <= readingLine && b.End >= readingLine).ToList();
            Assert.True(hit.Count == 1, $"line {line}: no ink band crosses the reading line at {readingLine:0.0}; bands {string.Join(", ", bands)}");
            var (start, end) = hit[0];
            var lineTop = area.Top + g.LineTops[line] - position;
            Assert.True(start >= lineTop - 1 && end <= lineTop + g.LineHeights[line] + 1,
                $"line {line}: ink {start}..{end} outside its line box {lineTop:0.0}..{lineTop + g.LineHeights[line]:0.0}");
            Assert.True(Math.Abs(((start + end) / 2.0) - readingLine) < tolerance,
                $"line {line}: ink centre {(start + end) / 2.0:0.0} vs reading line {readingLine:0.0}");
        }
    }

    [WpfFact]
    public void TwoSurfacesOnTwoUiThreads_GiveIdenticalLineTables_S7()
    {
        var ui = new Rig().Geometry;
        LayoutGeometry? other = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                other = new Rig().Geometry;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(30_000), "second UI thread did not finish");
        Assert.Null(failure);

        Assert.Equal(ui.LineCount, other!.LineCount);
        Assert.Equal(ui.LineParagraphs, other.LineParagraphs);
        Assert.Equal(ui.LineCharOffsets, other.LineCharOffsets);
        for (var i = 0; i < ui.LineCount; i++) Assert.True(Math.Abs(ui.LineTops[i] - other.LineTops[i]) <= 0.5, $"line {i}");
        Assert.Equal(ui.EndTop, other.EndTop, 1);
    }
}
