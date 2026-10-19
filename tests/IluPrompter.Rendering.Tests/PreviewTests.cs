using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IluPrompter.Core;
using IluPrompter.Rendering;
using IluPrompter.Theme;

namespace IluPrompter.Rendering.Tests;

public class PreviewTests
{
    private sealed class Rig
    {
        public FakeEngine Engine { get; } = new();
        public PromptDocumentService Documents { get; } = new();
        public FakeSettings Settings { get; } = new();
        public FakeOutput Output { get; } = new();
        public FakeDocumentFactory Factory { get; } = new();
        public FakeGeometryBuilder Builder { get; } = new();
        public RecordingLog Log { get; } = new();
        public PrompterPreview Preview { get; } = new();

        public Rig(double width = 696, double height = 392)
        {
            Preview.Attach(new PreviewServices(Engine, Documents, Settings, Output, Factory, Builder, Log));
            Resize(width, height);
        }

        public void Resize(double width, double height)
        {
            Preview.Measure(new Size(width, height));
            Preview.Arrange(new Rect(0, 0, width, height));
            Preview.UpdateLayout();
        }

        public void Publish() =>
            Documents.Publish(new PromptDocumentSnapshot(Documents.NextVersion(), "<x/>", 20, 6, Array.Empty<SectionInfo>(), 60));

        public Point ControlPoint(double outputX, double outputY)
        {
            var l = Preview.Scene.Layout;
            return new Point(l.Stage.X + (outputX * l.Scale), l.Stage.Y + (outputY * l.Scale));
        }
    }

    private static async Task Pump() => await Dispatcher.Yield(DispatcherPriority.ContextIdle);

    [WpfFact]
    public void ConstructsWithoutServices_AndAttachRebuildsStraightAway()
    {
        var rig = new Rig();
        Assert.False(rig.Preview.IsPointerOverScript);
        Assert.NotNull(rig.Preview.Surface);
        Assert.Equal(1, rig.Engine.LayoutCount);                 // the engine has a geometry from the first frame
        Assert.Equal(1920, rig.Preview.Surface!.Output.WidthDip);
    }

    [WpfFact]
    public void Layout_ScalesWithATransform_NeverByLayingOutSmaller()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Preview.Surface!.Rebuild();

        var layout = rig.Preview.Scene.Layout;
        Assert.Equal((392 - 4) / 1080.0, layout.Scale, 9);

        var surface = rig.Preview.Surface;
        Assert.Equal(1920, surface.ActualWidth, 6);              // laid out at the output's own DIP size
        Assert.Equal(1080, surface.ActualHeight, 6);
        var scale = Assert.IsType<ScaleTransform>(surface.RenderTransform);
        Assert.Equal(layout.Scale, scale.ScaleX, 9);
        Assert.Equal(layout.Scale, scale.ScaleY, 9);

        // Same text-area width and font size the output computes: identical line breaks.
        var area = TypographyMath.ComputeTextArea(OutputGeometry.Default1080, rig.Settings.Current.Margins, rig.Settings.Current.Text, rig.Settings.Current.Output);
        Assert.Equal(area.Width, surface.Presenter.Picture!.Area.Width, 6);
        Assert.Equal(area.BaseFontSize, rig.Factory.LastStyle!.BaseFontSize, 6);
    }

    [WpfFact]
    public void Letterbox_FollowsTheControlSize()
    {
        var rig = new Rig(400, 600);
        var l = rig.Preview.Scene.Layout;
        Assert.Equal((400 - 4) / 1920.0, l.Scale, 9);
        Assert.Equal(2, l.Stage.X, 6);
        Assert.True(l.Stage.Y > 100);                             // bars above and below

        rig.Resize(1000, 300);
        var wide = rig.Preview.Scene.Layout;
        Assert.Equal((300 - 4) / 1080.0, wide.Scale, 9);
        Assert.Equal(2, wide.Stage.Y, 6);
        Assert.True(wide.Stage.X > 100);                          // bars left and right
    }

    [WpfFact]
    public async Task OutputGeometryChange_RelayoutsAtTheNewSizeAndAspect()
    {
        var rig = new Rig();
        rig.Publish();
        rig.Preview.Surface!.Rebuild();
        var builds = rig.Factory.Calls;

        rig.Output.State = rig.Output.State with { Geometry = new OutputGeometry(1366, 768, 1.0, 60) };
        await Pump();
        await Pump();

        var surface = rig.Preview.Surface;
        Assert.Equal(1366, surface.Output.WidthDip, 6);
        Assert.Equal(768, surface.Output.HeightDip, 6);
        Assert.True(rig.Factory.Calls > builds);
        Assert.Equal((392 - 4) / 768.0, rig.Preview.Scene.Layout.Scale, 6);
        Assert.Equal(1366.0 / 768.0, rig.Preview.Scene.Layout.Stage.Width / rig.Preview.Scene.Layout.Stage.Height, 6);
    }

    [WpfFact]
    public async Task Blank_DimsTheScriptTo35Percent_AndShowsTheChipState_LiveFollowsTheOutput()
    {
        var rig = new Rig();
        Assert.Equal(1.0, rig.Preview.Surface!.Opacity);
        Assert.False(rig.Preview.Scene.Overlay.State.IsBlank);

        rig.Output.State = rig.Output.State with { IsOpen = true, Display = OutputDisplayStatus.Ready };
        await Pump();
        Assert.True(rig.Preview.Scene.Overlay.State.IsLive);

        rig.Output.SetBlank(true);
        await Pump();
        Assert.Equal(0.35, rig.Preview.Surface.Opacity, 6);
        Assert.True(rig.Preview.Scene.Overlay.State.IsBlank);
        Assert.False(rig.Preview.Scene.Overlay.State.IsLive);

        rig.Output.SetBlank(false);
        await Pump();
        Assert.Equal(1.0, rig.Preview.Surface.Opacity, 6);
        Assert.True(rig.Preview.Scene.Overlay.State.IsLive);
    }

    [WpfFact]
    public void DraggingAMarginEdge_UpdatesThatMarginInSettings()
    {
        var rig = new Rig();
        var before = rig.Settings.Current.Margins;

        rig.Preview.DragForTest(PreviewHandle.Left, rig.ControlPoint(200, 500), snap: false);

        Assert.Equal(200.0 / 1920, rig.Settings.Current.Margins.Left, 9);
        Assert.Equal(before.Right, rig.Settings.Current.Margins.Right);
        Assert.Equal(before.Top, rig.Settings.Current.Margins.Top);
    }

    [WpfFact]
    public void DraggingEachEdge_MeasuresFromItsOwnSideOfTheOutput()
    {
        var rig = new Rig();
        rig.Preview.DragForTest(PreviewHandle.Right, rig.ControlPoint(1900, 500), snap: false);
        rig.Preview.DragForTest(PreviewHandle.Top, rig.ControlPoint(900, 100), snap: false);
        rig.Preview.DragForTest(PreviewHandle.Bottom, rig.ControlPoint(900, 1000), snap: false);

        var m = rig.Settings.Current.Margins;
        Assert.Equal(20.0 / 1920, m.Right, 9);
        Assert.Equal(100.0 / 1080, m.Top, 9);
        Assert.Equal(80.0 / 1080, m.Bottom, 9);
    }

    [WpfFact]
    public void MarginDrag_MovesTheOverlayAtOnce_ButChangesTheLayoutOnlyOnRelease()
    {
        var rig = new Rig();
        var before = rig.Settings.Current.Margins;
        var changes = 0;
        rig.Settings.Changed += (_, _) => changes++;

        rig.Preview.DragForTest(PreviewHandle.Left, rig.ControlPoint(150, 500), snap: false, release: false);
        rig.Preview.DragForTest(PreviewHandle.Left, rig.ControlPoint(180, 500), snap: false, release: false);
        rig.Preview.DragForTest(PreviewHandle.Left, rig.ControlPoint(200, 500), snap: false, release: false);
        Assert.Equal(0, changes);                                        // no relayout per mouse move
        Assert.Equal(before, rig.Settings.Current.Margins);
        Assert.Equal(200, rig.Preview.OverlayAreaForTest.Left, 6);       // but the overlay follows the pointer

        rig.Preview.DragForTest(PreviewHandle.Left, rig.ControlPoint(200, 500), snap: false);   // release
        Assert.Equal(1, changes);
        Assert.Equal(200.0 / 1920, rig.Settings.Current.Margins.Left, 9);
    }

    [WpfFact]
    public void ShiftSnapsMarginsToEightPixels()
    {
        var rig = new Rig();
        rig.Preview.DragForTest(PreviewHandle.Left, rig.ControlPoint(203, 500), snap: true);
        Assert.Equal(200.0 / 1920, rig.Settings.Current.Margins.Left, 9);
        rig.Preview.DragForTest(PreviewHandle.Left, rig.ControlPoint(205, 500), snap: true);
        Assert.Equal(208.0 / 1920, rig.Settings.Current.Margins.Left, 9);
    }

    [WpfFact]
    public void DraggingTheReadingLine_SetsTheFractionOfTheTextArea_Clamped()
    {
        var rig = new Rig();
        var area = rig.Preview.Surface!.TextArea;

        rig.Preview.DragForTest(PreviewHandle.ReadingLine, rig.ControlPoint(900, area.Top + (area.Height * 0.45)), snap: false);
        Assert.Equal(0.45, rig.Settings.Current.Output.ReadingLineFraction, 4);

        rig.Preview.DragForTest(PreviewHandle.ReadingLine, rig.ControlPoint(900, 1070), snap: false);
        Assert.Equal(0.60, rig.Settings.Current.Output.ReadingLineFraction, 6);

        rig.Preview.DragForTest(PreviewHandle.ReadingLine, rig.ControlPoint(900, 2), snap: false);
        Assert.Equal(0.10, rig.Settings.Current.Output.ReadingLineFraction, 6);
    }

    [WpfFact]
    public void DraggingOntoTheCurrentValue_DoesNotChurnSettings()
    {
        var rig = new Rig();
        var changes = 0;
        rig.Settings.Changed += (_, _) => changes++;
        rig.Preview.DragForTest(PreviewHandle.Left, rig.ControlPoint(160, 500), snap: false);
        rig.Preview.DragForTest(PreviewHandle.ReadingLine, rig.ControlPoint(900, 64 + (952 * 0.33)), snap: false);
        Assert.Equal(0, changes);
    }

    [WpfFact]
    public void HitTesting_UsesTheLiveLayoutAndMargins()
    {
        var rig = new Rig();
        Assert.Equal(PreviewHandle.Left, rig.Preview.HitTestForTest(rig.ControlPoint(160, 500)));
        Assert.Equal(PreviewHandle.None, rig.Preview.HitTestForTest(rig.ControlPoint(900, 500)));

        rig.Settings.Update(s => s with { Margins = s.Margins with { Left = 300.0 / 1920 } });
        rig.Preview.Surface!.Rebuild();
        Assert.Equal(PreviewHandle.Left, rig.Preview.HitTestForTest(rig.ControlPoint(300, 500)));
        Assert.Equal(PreviewHandle.None, rig.Preview.HitTestForTest(rig.ControlPoint(160, 500)));
    }

    [WpfFact]
    public async Task Detach_StopsFollowingDocumentsSettingsAndOutput()
    {
        var rig = new Rig();
        await Pump();
        var rebuilds = rig.Preview.Surface!.RebuildCount;
        var surface = rig.Preview.Surface;

        rig.Preview.Detach();
        rig.Publish();
        rig.Settings.Update(s => s with { Margins = s.Margins with { Left = 0.2 } });
        rig.Output.State = rig.Output.State with { IsOpen = true };
        await Pump();

        Assert.Equal(rebuilds, surface.RebuildCount);
        Assert.Null(rig.Preview.Surface);
    }

    [WpfFact]
    public async Task AttachTwice_ReplacesTheSurfaceInsteadOfLeakingTheFirst()
    {
        var rig = new Rig();
        var first = rig.Preview.Surface!;
        await Pump();
        rig.Preview.Attach(new PreviewServices(rig.Engine, rig.Documents, rig.Settings, rig.Output, rig.Factory, rig.Builder, rig.Log));
        Assert.NotSame(first, rig.Preview.Surface);

        var before = first.RebuildCount;
        rig.Publish();
        await Pump();
        Assert.Equal(before, first.RebuildCount);                        // the first surface no longer follows documents
        Assert.True(rig.Preview.Surface!.RebuildCount >= 1);
    }

    private static OutputState Live(OutputState s) => s with { IsOpen = true, Display = OutputDisplayStatus.Ready };

    [WpfFact]
    public async Task WithoutAPulseSource_ThePreviewAlwaysDrivesItselfFromRendering()
    {
        var rig = new Rig();                                        // FakeOutput has no frame pulse
        Assert.Equal(PreviewDriveMode.Frames, rig.Preview.DriveMode);
        rig.Output.State = Live(rig.Output.State);
        await Pump();
        Assert.Equal(PreviewDriveMode.Frames, rig.Preview.DriveMode);
    }

    [WpfFact]
    public async Task WhileTheOutputRenders_ThePreviewDropsItsRenderingSubscription_AndFollowsPulses()
    {
        var output = new FakePulseOutput();
        var engine = new FakeEngine();
        var preview = new PrompterPreview();
        preview.Attach(new PreviewServices(engine, new PromptDocumentService(), new FakeSettings(), output, new FakeDocumentFactory(), new FakeGeometryBuilder(), new RecordingLog()));
        preview.Measure(new Size(696, 392));
        preview.Arrange(new Rect(0, 0, 696, 392));

        Assert.Equal(PreviewDriveMode.Frames, preview.DriveMode);   // output closed: the preview is the only ticker
        Assert.False(output.HasPulseSubscribers);

        output.State = Live(output.State);
        await Pump();
        Assert.Equal(PreviewDriveMode.Pulses, preview.DriveMode);
        Assert.True(output.HasPulseSubscribers);

        // A pulse from the output thread repaints the preview at the engine's position, without the preview ticking.
        var translate = (TranslateTransform)preview.Surface!.Presenter.RenderTransform;
        engine.SetPosition(321);
        var ticksBefore = engine.Ticks + engine.StaleTicks;
        await Task.Run(output.Pulse);
        await Pump();
        Assert.Equal(-321, translate.Y, 6);
        Assert.Equal(ticksBefore, engine.Ticks + engine.StaleTicks);

        // Many pulses before the UI thread gets to run coalesce into one repaint at the newest position.
        engine.SetPosition(400);
        engine.SetPosition(410);
        await Task.Run(() => { for (var i = 0; i < 50; i++) output.Pulse(); });
        await Pump();
        Assert.Equal(-410, translate.Y, 6);

        output.State = output.State with { IsOpen = false };
        await Pump();
        Assert.Equal(PreviewDriveMode.Frames, preview.DriveMode);
        Assert.False(output.HasPulseSubscribers);

        output.State = Live(output.State);
        await Pump();
        Assert.Equal(PreviewDriveMode.Pulses, preview.DriveMode);
        preview.Detach();
        Assert.Equal(PreviewDriveMode.Idle, preview.DriveMode);
        Assert.False(output.HasPulseSubscribers);
    }

    [WpfFact]
    public async Task OutputMissingOrHidden_PutsThePreviewBackInChargeOfTicking()
    {
        var output = new FakePulseOutput();
        var preview = new PrompterPreview();
        preview.Attach(new PreviewServices(new FakeEngine(), new PromptDocumentService(), new FakeSettings(), output, new FakeDocumentFactory(), new FakeGeometryBuilder(), new RecordingLog()));
        output.State = Live(output.State);
        await Pump();
        Assert.Equal(PreviewDriveMode.Pulses, preview.DriveMode);

        output.State = output.State with { Display = OutputDisplayStatus.Missing, Monitor = null };   // still "open", window hidden
        await Pump();
        Assert.Equal(PreviewDriveMode.Frames, preview.DriveMode);
        preview.Detach();
    }

    [WpfFact]
    public async Task IfPulsesStop_AWatchdogFallsBackToRendering_AndPulsesResumeControl()
    {
        var output = new FakePulseOutput();
        var log = new RecordingLog();
        var preview = new PrompterPreview();
        preview.Attach(new PreviewServices(new FakeEngine(), new PromptDocumentService(), new FakeSettings(), output, new FakeDocumentFactory(), new FakeGeometryBuilder(), log));
        output.State = Live(output.State);
        await Pump();
        Assert.Equal(PreviewDriveMode.Pulses, preview.DriveMode);

        var until = DateTime.UtcNow.AddSeconds(3);
        while (preview.DriveMode == PreviewDriveMode.Pulses && DateTime.UtcNow < until)
        {
            await Task.Delay(50);
            await Pump();
        }

        Assert.Equal(PreviewDriveMode.Frames, preview.DriveMode);
        Assert.Contains(log.Lines, l => l.Contains("No frame pulse"));

        await Task.Run(output.Pulse);
        await Pump();
        Assert.Equal(PreviewDriveMode.Pulses, preview.DriveMode);
        preview.Detach();
    }

    [WpfFact]
    public void RenderedPreview_ShowsBackdropBars_ScaledBlackOutput_MarginTint_AndText()
    {
        var rig = new Rig(400, 600);
        rig.Publish();
        rig.Preview.Surface!.Rebuild();
        rig.Resize(400, 600);

        var bitmap = Render(rig.Preview, 400, 600);
        var l = rig.Preview.Scene.Layout;

        // Letterbox bar above the stage is the backdrop colour; the stage itself starts black (script-paper) plus tint in the margins.
        Assert.Equal(ThemeResources.GetColor("backdrop"), PixelAt(bitmap, 200, (int)(l.Stage.Y / 2)));
        var inTextArea = PixelAt(bitmap, 200, (int)(l.Stage.Y + (l.Stage.Height * 0.9)));
        Assert.True(Brightness(inTextArea) < 40, "empty text area stays black: " + inTextArea);

        // Some pixels inside the text area are script ink: the document shows through the scaling and the clip.
        var bright = 0;
        var area = rig.Preview.Surface.TextArea;
        for (var y = (int)(l.Stage.Y + (area.Top * l.Scale)); y < (int)(l.Stage.Y + ((area.Top + area.Height) * l.Scale)); y++)
        {
            for (var x = (int)(l.Stage.X + (area.Left * l.Scale)); x < (int)(l.Stage.X + ((area.Left + area.Width) * l.Scale)); x++)
            {
                if (Brightness(PixelAt(bitmap, x, y)) > 128) bright++;
            }
        }

        Assert.True(bright > 200, $"expected visible text, found {bright} bright pixels");

        // The left margin is tinted (blue-ish over black), so not pure black.
        var margin = PixelAt(bitmap, (int)(l.Stage.X + (30 * l.Scale)), (int)(l.Stage.Y + (l.Stage.Height / 2)));
        Assert.True(margin.B > margin.R, "margin tint expected: " + margin);
    }

    [WpfFact]
    public void ClickingThePreview_TakesFocus_SoTheKeysGoToTheTransport()
    {
        var rig = new Rig();
        var text = new System.Windows.Controls.TextBox();
        var panel = new System.Windows.Controls.StackPanel();
        panel.Children.Add(text);
        panel.Children.Add(rig.Preview);
        var window = new Window
        {
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = 0,
            Width = 800, Height = 600, Content = panel,
        };
        window.Show();
        try
        {
            text.Focus();
            Assert.Same(text, System.Windows.Input.FocusManager.GetFocusedElement(window));
            rig.Preview.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            });
            Assert.Same(rig.Preview, System.Windows.Input.FocusManager.GetFocusedElement(window));
        }
        finally
        {
            window.Close();
        }
    }

    internal static BitmapSource Render(FrameworkElement element, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        return bitmap;
    }

    internal static Color PixelAt(BitmapSource bitmap, int x, int y)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    internal static double Brightness(Color c) => (0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B);
}
