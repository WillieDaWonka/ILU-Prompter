using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using IluPrompter.Core;
using IluPrompter.Rendering;

namespace IluPrompter.Rendering.Tests;

public class SurfaceTests
{
    private sealed class Rig
    {
        public FakeEngine Engine { get; } = new();
        public PromptDocumentService Documents { get; } = new();
        public FakeSettings Settings { get; }
        public FakeDocumentFactory Factory { get; } = new();
        public FakeGeometryBuilder Builder { get; } = new();
        public RecordingLog Log { get; } = new();
        public OutputGeometry Output { get; set; } = OutputGeometry.Default1080;
        public PrompterSurface Surface { get; }

        public Rig(PrompterSettings? settings = null, bool snap = true)
        {
            Settings = new FakeSettings(settings);
            var services = new SurfaceServices(Engine, Documents, Settings, Factory, Builder, Log, () => Output);
            Surface = new PrompterSurface("test", services, snap);
        }

        public void Publish(long version = 0)
        {
            var v = version == 0 ? Documents.NextVersion() : version;
            Documents.Publish(new PromptDocumentSnapshot(v, "<x/>", 20, 6, Array.Empty<SectionInfo>(), 60));
        }

        public TextArea Area =>
            TypographyMath.ComputeTextArea(Output, Settings.Current.Margins, Settings.Current.Text, Settings.Current.Output);
    }

    [WpfFact]
    public void Rebuild_BuildsGeometryAndCallsSetLayoutOnTheEngine()
    {
        var rig = new Rig();
        rig.Publish();

        rig.Surface.Rebuild();

        Assert.Equal(1, rig.Factory.Calls);
        Assert.Equal(1, rig.Builder.Calls);
        Assert.Equal(1, rig.Engine.LayoutCount);
        var (geometry, viewport) = rig.Engine.Layouts[0];
        var area = rig.Area;
        Assert.Equal(area.ToViewport(), viewport);
        Assert.Equal(area.BaseLineHeight, geometry.BaseLineHeight, 6);
        Assert.Equal(6, geometry.ParagraphCount);
        Assert.Equal(rig.Surface.LayoutKey, geometry.LayoutKey);
        Assert.Equal(SurfaceLayout.ComputeLayoutKey(1, area, rig.Settings.Current.Text, rig.Settings.Current.Output), geometry.LayoutKey);
        Assert.Equal(area.BaseLineHeight, rig.Builder.LastOptions!.BaseLineHeight, 6);
        Assert.DoesNotContain(rig.Log.Lines, l => l.StartsWith("ERROR", StringComparison.Ordinal));
    }

    [WpfFact]
    public void Rebuild_HostsTheDocumentWithAValidTextLayoutAtTheTextAreaWidth()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Surface.Rebuild();

        var area = rig.Area;
        var host = ((InlinePictureSource)rig.Surface.Pictures).Builder.Host;   // the picture's layout host
        Assert.Equal(area.Width, host.Width, 6);
        Assert.Equal(area.Width, host.ActualWidth, 3);
        Assert.True(host.ActualHeight > area.BaseLineHeight, "the host is unconstrained in height");
        Assert.Equal(area.Width, rig.Surface.Presenter.Picture!.Area.Width, 6);

        // Every paragraph top came from a valid TextView layout and increases downwards. (GetCharacterRect is the glyph
        // box, which for a line height below the font's natural height sits a little above the line box: not exactly 0.)
        var tops = rig.Builder.LastParagraphTops;
        Assert.Equal(6, tops.Count);
        Assert.All(tops, t => Assert.False(double.IsNaN(t)));
        Assert.True(Math.Abs(tops[0]) < area.BaseLineHeight);
        for (var i = 1; i < tops.Count; i++) Assert.True(tops[i] > tops[i - 1]);
    }

    [WpfFact]
    public void Rebuild_SizesTheSurfaceToTheOutputAndPlacesTheClippedTextArea()
    {
        var rig = new Rig();
        rig.Output = new OutputGeometry(3840, 2160, 1.5, 60);   // 2560 x 1440 DIP
        rig.Publish();
        rig.Surface.Rebuild();

        var area = rig.Area;
        Assert.Equal(2560, rig.Surface.Width, 6);
        Assert.Equal(1440, rig.Surface.Height, 6);
        var canvas = (Canvas)((Canvas)rig.Surface.Content).Children[0];
        Assert.True(canvas.ClipToBounds);
        Assert.Equal(area.Left, Canvas.GetLeft(canvas), 6);
        Assert.Equal(area.Top, Canvas.GetTop(canvas), 6);
        Assert.Equal(area.Width, canvas.Width, 6);
        Assert.Equal(area.Height, canvas.Height, 6);
        Assert.Equal(area.Width, rig.Surface.Presenter.Picture!.Area.Width, 6);
    }

    [WpfFact]
    public void Rebuild_StyleComesFromSettingsAndTokens()
    {
        var rig = new Rig(PrompterSettings.Default with
        {
            Text = new TextSettings { FontFace = "Arial", FontWeight = 400, DefaultInk = "ink-yellow", ParagraphGapLines = 0.75 },
            Output = new OutputSettings { ShowSectionNames = true, Alignment = TextAlignmentSetting.Centre },
        });
        rig.Publish();
        rig.Surface.Rebuild();

        var style = rig.Factory.LastStyle!;
        Assert.Equal(rig.Area.BaseFontSize, style.BaseFontSize, 6);
        Assert.Equal(1.3, style.LineSpacing);
        Assert.Equal(0.75, style.ParagraphGapLines);
        Assert.Equal(TextAlignment.Center, style.Alignment);
        Assert.Equal(400, style.FontWeight.ToOpenTypeWeight());
        Assert.Equal(Theme.ThemeResources.GetColor("ink-yellow"), style.Ink);
        Assert.Equal(Theme.ThemeResources.GetColor("ink-grey"), style.SectionInk);
        Assert.Equal(Theme.ThemeResources.GetColor("end-marker"), style.EndInk);
        Assert.Equal(Theme.ThemeResources.GetColor("line-strong"), style.EndRule);
        Assert.True(style.ShowSectionNames);
        Assert.Equal("##", style.SectionMarker);
        Assert.Equal(Theme.ThemeResources.GetColor("ink-white"), style.HighlightInks[Theme.ThemeResources.GetColor("highlight-blue")]);
        Assert.Equal(Theme.ThemeResources.GetColor("canvas"), style.HighlightInks[Theme.ThemeResources.GetColor("highlight-yellow")]);
        Assert.Equal(5, style.HighlightInks.Count);
        Assert.Contains("Arial", style.FontFamily.Source);
    }

    [WpfFact]
    public void Rebuild_UnknownInkToken_FallsBackToWhite()
    {
        var rig = new Rig(PrompterSettings.Default with { Text = new TextSettings { DefaultInk = "no-such-token" } });
        rig.Surface.Rebuild();
        Assert.Equal(Theme.ThemeResources.GetColor("ink-white"), rig.Factory.LastStyle!.Ink);
    }

    [WpfFact]
    public void Rebuild_WithNothingChanged_OnlyRefreshesTheViewport()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Surface.Rebuild();
        var first = rig.Engine.Layouts[0].Geometry;

        rig.Surface.Rebuild();

        Assert.Equal(1, rig.Factory.Calls);
        Assert.Equal(1, rig.Builder.Calls);
        Assert.Equal(2, rig.Engine.LayoutCount);
        Assert.Same(first, rig.Engine.Layouts[1].Geometry);   // same tables, same key: the engine only updates its viewport
    }

    [WpfFact]
    public void ReadingLineChange_UpdatesTheViewportWithoutRebuildingTheDocument()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Surface.Rebuild();

        rig.Settings.Update(s => s with { Output = s.Output with { ReadingLineFraction = 0.5 } });
        rig.Surface.Rebuild();

        Assert.Equal(1, rig.Factory.Calls);
        Assert.Equal(rig.Area.Height * 0.5, rig.Engine.Viewport.ReadingLineY, 6);
        Assert.Same(rig.Engine.Layouts[0].Geometry, rig.Engine.Layouts[1].Geometry);
    }

    [WpfFact]
    public void NewSnapshot_RebuildsTheDocumentWithANewLayoutKey()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Surface.Rebuild();
        var firstKey = rig.Surface.LayoutKey;

        rig.Publish();
        rig.Surface.Rebuild();

        Assert.Equal(2, rig.Factory.Calls);
        Assert.NotEqual(firstKey, rig.Surface.LayoutKey);
        Assert.StartsWith("2|", rig.Surface.LayoutKey);
        Assert.Equal(2, rig.Surface.DocumentBuildCount);
    }

    [WpfFact]
    public void MarginChange_ChangesWidthAndFontSize_SoTheDocumentIsRebuilt()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Surface.Rebuild();
        var before = rig.Factory.LastStyle!.BaseFontSize;

        rig.Settings.Update(s => s with { Margins = s.Margins with { Top = 0.15, Bottom = 0.15 } });
        rig.Surface.Rebuild();

        Assert.Equal(2, rig.Factory.Calls);
        Assert.True(rig.Factory.LastStyle!.BaseFontSize < before);
        Assert.Equal(rig.Area.BaseFontSize, rig.Factory.LastStyle.BaseFontSize, 6);
    }

    [WpfFact]
    public void OutputGeometryChange_RebuildsAtTheNewSize()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Surface.Rebuild();

        rig.Output = new OutputGeometry(3840, 2160, 2.0, 60);   // 1920 x 1080 DIP again, but different pixels: same layout
        rig.Surface.Rebuild();
        Assert.Equal(1, rig.Factory.Calls);

        rig.Output = new OutputGeometry(1366, 768, 1.0, 60);
        rig.Surface.Rebuild();
        Assert.Equal(2, rig.Factory.Calls);
        Assert.Equal(1366, rig.Surface.Width, 6);
        Assert.Equal(rig.Area.BaseFontSize, rig.Factory.LastStyle!.BaseFontSize, 6);
    }

    [WpfFact]
    public void InkChange_RebuildsTheDocumentButKeepsTheLayoutKey()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Surface.Rebuild();
        var key = rig.Surface.LayoutKey;

        rig.Settings.Update(s => s with { Text = s.Text with { DefaultInk = "ink-cyan" } });
        rig.Surface.Rebuild();

        Assert.Equal(2, rig.Factory.Calls);
        Assert.Equal(key, rig.Surface.LayoutKey);
        Assert.Equal(Theme.ThemeResources.GetColor("ink-cyan"), rig.Factory.LastStyle!.Ink);
    }

    [WpfFact]
    public async Task RequestRebuild_Coalesces_AndRunsAtBackgroundPriority()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Surface.Start();                      // queues the first rebuild
        for (var i = 0; i < 10; i++) rig.Surface.RequestRebuild();
        Assert.Equal(0, rig.Surface.RebuildCount);

        await Dispatcher.Yield(DispatcherPriority.ContextIdle);

        Assert.Equal(1, rig.Surface.RebuildCount);
        rig.Surface.RequestRebuild();
        rig.Surface.RequestRebuild();
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        Assert.Equal(2, rig.Surface.RebuildCount);
        rig.Surface.Stop();
    }

    [WpfFact]
    public async Task SnapshotAndSettingsEvents_TriggerARebuild_UntilStopped()
    {
        var rig = new Rig();
        rig.Surface.Start();
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        var baseline = rig.Surface.RebuildCount;

        rig.Publish();
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        Assert.Equal(baseline + 1, rig.Surface.RebuildCount);
        Assert.Equal(1, rig.Surface.Geometry!.SnapshotVersion);

        rig.Settings.Update(s => s with { Margins = s.Margins with { Left = 0.2 } });
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        Assert.Equal(baseline + 2, rig.Surface.RebuildCount);

        // Changes to unrelated settings do not rebuild.
        rig.Settings.Update(s => s with { Scroll = s.Scroll with { StartSpeed = 5 } });
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        Assert.Equal(baseline + 2, rig.Surface.RebuildCount);

        rig.Surface.Stop();
        rig.Publish();
        await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        Assert.Equal(baseline + 2, rig.Surface.RebuildCount);
    }

    [WpfFact]
    public void FactoryFailure_IsLogged_NotThrown_AndTheOldPictureStays()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Surface.Rebuild();
        var good = rig.Surface.Geometry;

        rig.Factory.Throw = true;
        rig.Publish();
        rig.Surface.Rebuild();

        Assert.Same(good, rig.Surface.Geometry);
        Assert.Contains(rig.Log.Lines, l => l.StartsWith("ERROR [surface:test]", StringComparison.Ordinal));
    }

    [WpfFact]
    public void ReadingMarkers_FollowTheSettingsAndTheMarginRule()
    {
        var rig = new Rig();
        rig.Surface.Rebuild();
        var (left, right) = Markers(rig.Surface);
        Assert.Equal(Visibility.Visible, left.Visibility);
        Assert.Equal(Visibility.Visible, right.Visibility);

        var area = rig.Area;
        var readingY = area.Top + area.ReadingLineY;
        var lb = left.Data.Bounds;
        Assert.Equal(area.BaseLineHeight * 0.5, lb.Height, 3);
        Assert.Equal(readingY, lb.Top + (lb.Height / 2), 3);
        Assert.True(lb.Right < area.Left);                       // in the left margin
        Assert.True(right.Data.Bounds.Left > area.Left + area.Width);   // in the right margin

        // 30 px margins are below 0.6 x 119 = 71 px: edge bars.
        rig.Settings.Update(s => s with { Margins = s.Margins with { Left = 30.0 / 1920, Right = 30.0 / 1920 } });
        rig.Surface.Rebuild();
        Assert.Equal(6, left.Data.Bounds.Width, 3);
        Assert.Equal(0, left.Data.Bounds.Left, 3);
        Assert.Equal(1920, right.Data.Bounds.Right, 3);

        rig.Settings.Update(s => s with { Output = s.Output with { ShowReadingMarker = false } });
        rig.Surface.Rebuild();
        Assert.Equal(Visibility.Collapsed, left.Visibility);
        Assert.Equal(Visibility.Collapsed, right.Visibility);
    }

    [WpfFact]
    public void DimAboveReadingLine_OnlyWhenEnabled()
    {
        var rig = new Rig();
        rig.Surface.Rebuild();
        var dim = Dim(rig.Surface);
        Assert.Equal(Visibility.Collapsed, dim.Visibility);

        rig.Settings.Update(s => s with { Output = s.Output with { DimAboveReadingLine = true } });
        rig.Surface.Rebuild();
        Assert.Equal(Visibility.Visible, dim.Visibility);
        var area = rig.Area;
        Assert.Equal(area.Top, Canvas.GetTop(dim), 6);
        Assert.Equal(area.ReadingLineY - (area.BaseLineHeight / 2), dim.Height, 6);
        Assert.Equal(0.55, ((SolidColorBrush)dim.Fill).Opacity, 6);
        Assert.Equal(Colors.Black, ((SolidColorBrush)dim.Fill).Color);
    }

    [WpfFact]
    public void Surface_UsesScriptPaperAndGrayscaleIdealText()
    {
        var rig = new Rig();
        Assert.Equal(Theme.ThemeResources.GetColor("script-paper"), ((SolidColorBrush)((Canvas)rig.Surface.Content).Background).Color);
        Assert.Equal(TextFormattingMode.Ideal, TextOptions.GetTextFormattingMode(rig.Surface));
        Assert.Equal(TextRenderingMode.Grayscale, TextOptions.GetTextRenderingMode(rig.Surface));
        Assert.Equal(TextHintingMode.Animated, TextOptions.GetTextHintingMode(rig.Surface));
    }

    [WpfFact]
    public void LayoutHost_IsAReadOnlyInertRichTextBox_AndThePresenterIsInert()
    {
        var surface = new Rig().Surface;
        var host = ((InlinePictureSource)surface.Pictures).Builder.Host;
        Assert.True(host.IsReadOnly);
        Assert.False(host.Focusable);
        Assert.False(host.IsHitTestVisible);
        Assert.Equal(new Thickness(0), host.BorderThickness);
        Assert.Equal(new Thickness(0), host.Padding);
        Assert.Equal(ScrollBarVisibility.Disabled, host.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, host.VerticalScrollBarVisibility);
        Assert.Equal(TextFormattingMode.Ideal, TextOptions.GetTextFormattingMode(host));
        Assert.False(surface.Presenter.IsHitTestVisible);
        Assert.False(surface.Presenter.Focusable);
        Assert.IsType<TranslateTransform>(surface.Presenter.RenderTransform);
    }

    [WpfFact]
    public void SetPosition_MovesTheHostUpByThePosition_ThenSnapsToDevicePixelsWhenStill()
    {
        var rig = new Rig();
        rig.Output = new OutputGeometry(2880, 1620, 1.5, 60);   // 1920 x 1080 DIP; top margin 64 px = 42.67 DIP
        rig.Surface.Rebuild();
        var translate = (TranslateTransform)rig.Surface.Presenter.RenderTransform;
        var area = rig.Area;

        rig.Surface.SetPosition(100.3);
        Assert.Equal(-100.3, translate.Y, 9);
        Assert.Equal(TextHintingMode.Animated, TextOptions.GetTextHintingMode(rig.Surface.Presenter));

        rig.Surface.SetPosition(100.3);                 // still, frame 1
        Assert.Equal(-100.3, translate.Y, 9);
        rig.Surface.SetPosition(100.3);                 // still, frame 2: snap
        var devicePx = (area.Top + translate.Y) * 1.5;
        Assert.Equal(Math.Round(devicePx), devicePx, 6);
        Assert.Equal(TextHintingMode.Fixed, TextOptions.GetTextHintingMode(rig.Surface.Presenter));
        Assert.True(Math.Abs(translate.Y + 100.3) <= 0.5 / 1.5 + 1e-9);

        rig.Surface.SetPosition(101.1);                 // moving again: exact and animated
        Assert.Equal(-101.1, translate.Y, 9);
        Assert.Equal(TextHintingMode.Animated, TextOptions.GetTextHintingMode(rig.Surface.Presenter));
    }

    [WpfFact]
    public void SetPosition_WithoutSnapping_NeverChangesTheOffsetOrHinting()
    {
        var rig = new Rig(snap: false);
        rig.Surface.Rebuild();
        for (var i = 0; i < 5; i++) rig.Surface.SetPosition(42.123);
        Assert.Equal(-42.123, ((TranslateTransform)rig.Surface.Presenter.RenderTransform).Y, 9);
        Assert.Equal(TextHintingMode.Animated, TextOptions.GetTextHintingMode(rig.Surface.Presenter));
    }

    [WpfFact]
    public void Rebuild_AppliesTheEnginesCurrentPosition()
    {
        var rig = new Rig();
        rig.Engine.SetPosition(250);
        rig.Surface.Rebuild();
        Assert.Equal(-250, ((TranslateTransform)rig.Surface.Presenter.RenderTransform).Y, 9);
    }

    [WpfFact]
    public void DiagnosticsEnabled_LogsTheFirstLineTopsPerLayoutKey()
    {
        var log = new RecordingLog { DiagnosticsEnabled = true };
        var engine = new FakeEngine();
        var docs = new PromptDocumentService();
        var surface = new PrompterSurface("output", new SurfaceServices(engine, docs, new FakeSettings(), new FakeDocumentFactory(), new FakeGeometryBuilder(), log, () => OutputGeometry.Default1080), true);
        docs.Publish(new PromptDocumentSnapshot(1, "<x/>", 20, 6, Array.Empty<SectionInfo>(), 60));

        surface.Rebuild();

        var line = Assert.Single(log.Lines, l => l.StartsWith("TRACE [surface:output]", StringComparison.Ordinal));
        Assert.Contains("layout key 1|1600|", line);
        Assert.Contains("first 50 tops [", line);
    }

    [WpfFact]
    public void RenderedOutput_HasInkInTheTextArea_GreyMarkersOnTheReadingLine_AndBlackMargins()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Surface.Rebuild();
        rig.Surface.Measure(new Size(1920, 1080));
        rig.Surface.Arrange(new Rect(0, 0, 1920, 1080));
        rig.Surface.UpdateLayout();

        var bitmap = PreviewTests.Render(rig.Surface, 1920, 1080);
        var area = rig.Area;
        var markerColour = Theme.ThemeResources.GetColor("reading-marker");
        var readingY = (int)Math.Round(area.Top + area.ReadingLineY);

        // Markers: solid reading-marker grey, vertically centred on the reading line, inside the margins.
        var left = Markers(rig.Surface).Left.Data.Bounds;
        var right = Markers(rig.Surface).Right.Data.Bounds;
        Assert.Equal(markerColour, PreviewTests.PixelAt(bitmap, (int)(left.X + (left.Width * 0.25)), readingY));
        Assert.Equal(markerColour, PreviewTests.PixelAt(bitmap, (int)(right.Right - (right.Width * 0.25)), readingY));

        // Margins and the strip above/below the text area are pure script-paper black (no chrome, no cursor, no overlay).
        foreach (var (x, y) in new[] { (5, 100), (30, 900), (1900, 500), (960, 10), (960, 1070), (100, 1075) })
        {
            Assert.Equal(Colors.Black, PreviewTests.PixelAt(bitmap, x, y));
        }

        // Ink in the text area.
        var bright = 0;
        for (var y = (int)area.Top; y < (int)(area.Top + area.Height); y += 2)
        {
            for (var x = (int)area.Left; x < (int)(area.Left + area.Width); x += 2)
            {
                if (PreviewTests.Brightness(PreviewTests.PixelAt(bitmap, x, y)) > 128) bright++;
            }
        }

        Assert.True(bright > 500, $"expected visible text, found {bright} bright samples");
    }

    [WpfFact]
    public void RenderedOutput_ScrollsTheTextUpByThePosition_AndClipsAtTheTextArea()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Surface.Rebuild();
        var area = rig.Area;

        // Scroll far enough that the top of the document is well above the text area: nothing may leak into the top margin.
        rig.Surface.SetPosition(area.BaseLineHeight * 3);
        rig.Surface.Measure(new Size(1920, 1080));
        rig.Surface.Arrange(new Rect(0, 0, 1920, 1080));
        rig.Surface.UpdateLayout();
        var bitmap = PreviewTests.Render(rig.Surface, 1920, 1080);

        for (var y = 0; y < (int)area.Top - 1; y += 3)
        {
            for (var x = 0; x < 1920; x += 20)
            {
                Assert.Equal(Colors.Black, PreviewTests.PixelAt(bitmap, x, y));
            }
        }
    }

    private static (Path Left, Path Right) Markers(PrompterSurface surface)
    {
        var paths = ((Canvas)surface.Content).Children.OfType<Path>().ToList();
        Assert.Equal(2, paths.Count);
        return (paths[0], paths[1]);
    }

    private static Rectangle Dim(PrompterSurface surface) =>
        Assert.Single(((Canvas)surface.Content).Children.OfType<Rectangle>());
}
