using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering.Tests;

/// <summary>
/// The live-edit invariant end to end on real surfaces (real snapshot builder, factory, geometry builder and engine):
/// an edit while the output shows never moves the text on the reading line, in the first frame of the new layout, on a
/// surface that is still a frame behind the engine, and in pixels.
/// </summary>
public class LiveEditSurfaceTests
{
    private static readonly DocumentNavigator Navigator = new();
    private static readonly SnapshotBuilder Snapshots = new(Navigator, new SectionParser());

    /// <summary>A picture source the test drives by hand (as the layout thread would), shared by two surfaces.</summary>
    private sealed class ManualPictures : IPictureSource
    {
        private readonly PictureBuilder _builder = new(new OutputDocumentFactory(Snapshots), new LayoutGeometryBuilder(Navigator));
        public PromptPicture? Current { get; private set; }
        public bool IsAutonomous => true;
        public event EventHandler? PictureReady;
        public void Request() { }

        public void Build(PromptDocumentService documents, ISettingsService settings)
        {
            Current = _builder.Build(new PictureInputs(documents.Current, settings.Current, OutputGeometry.Default1080), Current);
            PictureReady?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class Rig
    {
        public Rig()
        {
            Settings = new FakeSettings();
            Engine = new ScrollEngine(Settings);
            Editor = new FlowDocument();
            for (var i = 0; i < 40; i++)
            {
                Editor.Blocks.Add(new Paragraph(new Run($"Paragraph {i}: good evening, everyone, and welcome to the annual meeting of the Riverside Community Garden Trust.")));
            }

            Publish();
            Output = Surface("output");
            Preview = Surface("preview");
        }

        public FakeSettings Settings { get; }
        public ScrollEngine Engine { get; }
        public FlowDocument Editor { get; }
        public PromptDocumentService Documents { get; } = new();
        public ManualPictures Pictures { get; } = new();
        public PrompterSurface Output { get; }
        public PrompterSurface Preview { get; }

        public void Publish() => Documents.Publish(Snapshots.Build(Editor, Documents.NextVersion(), 20, "##"));

        public PrompterSurface Surface(string name)
        {
            var services = new SurfaceServices(Engine, Documents, Settings, new OutputDocumentFactory(Snapshots), new LayoutGeometryBuilder(Navigator),
                new RecordingLog(), () => OutputGeometry.Default1080) { Pictures = Pictures };
            var surface = new PrompterSurface(name, services, snapToPixels: false);
            surface.Measure(new Size(1920, 1080));
            surface.Arrange(new Rect(0, 0, 1920, 1080));
            return surface;
        }

        /// <summary>What a surface shows on the reading line: its own picture's line text at its own position.</summary>
        public static (string Text, double Fraction) OnReadingLine(PrompterSurface surface)
        {
            var g = surface.Geometry!;
            var position = -((TranslateTransform)surface.Presenter.RenderTransform).Y;
            var y = position + surface.TextArea.ReadingLineY;
            var i = g.FindLineAt(y);
            var p = g.LineParagraphs[i];
            var start = g.LineCharOffsets[i];
            var end = i + 1 < g.LineCount && g.LineParagraphs[i + 1] == p ? g.LineCharOffsets[i + 1] : g.ParagraphTexts[p].Length;
            return (g.ParagraphTexts[p][start..end], Math.Round((y - g.LineTops[i]) / g.LineHeights[i], 6));
        }

        public void Frame(PrompterSurface surface)
        {
            var p = Engine.ReadPosition(out var g);
            surface.SetPosition(p, g);
        }
    }

    [WpfFact]
    public void InsertingParagraphsAbove_LeavesTheReadingLine_OnBothSurfaces_EvenWhileOneLagsAFrame()
    {
        var rig = new Rig();
        rig.Pictures.Build(rig.Documents, rig.Settings);
        rig.Output.Rebuild();
        rig.Preview.Rebuild();
        var g = rig.Engine.Geometry!;
        rig.Engine.Post(EngineCommand.SeekToOffset(g.LineTops[g.FindLineForAnchor(new DocumentAnchor(20, 0)) + 1] + 0.3 * g.LineHeights[0] - rig.Output.TextArea.ReadingLineY));
        rig.Engine.Tick(System.Diagnostics.Stopwatch.GetTimestamp());
        rig.Frame(rig.Output);
        rig.Frame(rig.Preview);
        var before = Rig.OnReadingLine(rig.Output);
        Assert.Equal(before, Rig.OnReadingLine(rig.Preview));

        // Edit above the reading line; the layout thread finishes a new picture.
        var first = Navigator.GetParagraphs(rig.Editor)[3];
        for (var i = 0; i < 3; i++) rig.Editor.Blocks.InsertBefore(first, new Paragraph(new Run($"Inserted {i}: a new paragraph typed while live, long enough to wrap.")));
        rig.Publish();
        rig.Pictures.Build(rig.Documents, rig.Settings);

        // The output installs it (and tells the engine) first; the preview is still showing the old picture.
        rig.Output.Rebuild();
        Assert.Equal(before, Rig.OnReadingLine(rig.Output));            // same frame as the new layout appears
        rig.Frame(rig.Preview);
        Assert.NotSame(rig.Engine.Geometry, rig.Preview.Geometry);       // the preview lags...
        Assert.Equal(before, Rig.OnReadingLine(rig.Preview));           // ...and still shows the same text there

        rig.Preview.Rebuild();
        rig.Frame(rig.Output);
        rig.Frame(rig.Preview);
        Assert.Equal(before, Rig.OnReadingLine(rig.Output));
        Assert.Equal(before, Rig.OnReadingLine(rig.Preview));
    }

    [WpfFact]
    public void AnEditAboveTheReadingLine_DoesNotChangeThePixelsAroundIt()
    {
        var rig = new Rig();
        rig.Pictures.Build(rig.Documents, rig.Settings);
        rig.Output.Rebuild();
        var g = rig.Engine.Geometry!;
        rig.Engine.Post(EngineCommand.SeekToOffset(g.LineCentre(g.FindLineForAnchor(new DocumentAnchor(25, 0))) - rig.Output.TextArea.ReadingLineY));
        rig.Engine.Tick(System.Diagnostics.Stopwatch.GetTimestamp());
        rig.Frame(rig.Output);
        var area = rig.Output.TextArea;
        var before = Band(rig.Output, area);

        var p = Navigator.GetParagraphs(rig.Editor);
        rig.Editor.Blocks.Remove(p[5]);
        ((Run)p[10].Inlines.FirstInline).Text += " and a lot more words so this paragraph wraps onto another line of the output";
        rig.Editor.Blocks.InsertBefore(p[12], new Paragraph(new Run("## New part")));
        rig.Publish();
        rig.Pictures.Build(rig.Documents, rig.Settings);
        rig.Output.Rebuild();

        Assert.Equal(before, Band(rig.Output, area));
    }

    /// <summary>The rendered rows from 1.5 lines above to 1.5 lines below the reading line.</summary>
    private static byte[] Band(PrompterSurface surface, TextArea area)
    {
        surface.UpdateLayout();
        var bitmap = PreviewTests.Render(surface, 1920, 1080);
        var top = (int)(area.Top + area.ReadingLineY - (1.5 * area.BaseLineHeight));
        var rows = (int)(3 * area.BaseLineHeight);
        var pixels = new byte[1920 * rows * 4];
        bitmap.CopyPixels(new Int32Rect(0, top, 1920, rows), pixels, 1920 * 4, 0);
        return pixels;
    }

    [WpfFact]
    public void SteadyFrames_DoNotAllocate_AndOnlyTheBlocksNearTheViewAreAttached()
    {
        var rig = new Rig();
        rig.Pictures.Build(rig.Documents, rig.Settings);
        rig.Output.Rebuild();
        rig.Engine.Post(EngineCommand.SetSpeed(5));
        rig.Engine.Post(EngineCommand.Play());
        var t = System.Diagnostics.Stopwatch.GetTimestamp();
        var step = System.Diagnostics.Stopwatch.Frequency / 60;
        for (var i = 0; i < 120; i++)
        {
            t += step;
            rig.Engine.Tick(t);
            rig.Frame(rig.Output);
        }

        Assert.InRange(rig.Output.Presenter.AttachedCount, 1, 25);         // a handful of paragraphs, not 40
        Dispatcher().Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle); // visuals for the rest prepared
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 600; i++)
        {
            t += step;
            rig.Engine.Tick(t);
            rig.Frame(rig.Output);
        }

        // WPF boxes the double when the translate transform's Y is set (24 bytes a frame, as before this change), plus at
        // most a one-off when the attached band moves; nothing that grows with the frames.
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, (600 * 24) + 2048);
        Assert.InRange(rig.Output.Presenter.AttachedCount, 1, 25);

        // The presenter's per-frame check allocates nothing; when the attached band moves (about once per screen of
        // scrolling) WPF's visual tree takes a few dozen bytes per paragraph attached, nothing that grows per frame.
        var presenter = rig.Output.Presenter;
        var height = rig.Output.TextArea.Height;
        var position = rig.Engine.ReadPosition();
        for (var i = 0; i < 2000; i++) presenter.UpdateViewport(position + (i * 3), height);   // every visual attached once
        presenter.UpdateViewport(position, height);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 2000; i++) presenter.UpdateViewport(position + ((i % 100) * 0.5), height);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 2000; i++) presenter.UpdateViewport(position + (i * 3), height);
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 4096);
    }

    private static System.Windows.Threading.Dispatcher Dispatcher() => System.Windows.Threading.Dispatcher.CurrentDispatcher;
}
