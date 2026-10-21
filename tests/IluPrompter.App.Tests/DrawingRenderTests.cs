using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IluPrompter.App.Logic;
using IluPrompter.App.Views.Drawings;
using IluPrompter.Core;

namespace IluPrompter.App.Tests;

/// <summary>
/// Smoke tests for the four controller drawings: each constructs, takes a sample frame built from fake inputs, and renders.
/// Set <c>ILU_DRAWING_PNG_DIR</c> to a folder to also write each drawing as a PNG on the panel's ground (this is how the
/// pictures in docs/streams/screens were made). No device is ever touched.
/// </summary>
[Collection("WpfTheme")]
public class DrawingRenderTests
{
    private static readonly InputSourceId Pad = new(ControllerKind.Xbox, "xinput:0");
    private static readonly InputSourceId Preview = new(ControllerKind.MouseWheel, AppSources.App);
    private static readonly InputSourceId Contour = new(ControllerKind.JogShuttle, "fake-contour");

    private static void EnsureTheme()
    {
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        if (app.Resources.MergedDictionaries.Count > 0) return;
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
            new Uri("/IluPrompter.Theme;component/Themes/IluControls.xaml", UriKind.Relative)));
    }

    private static ControllerPanelModel Build(ControllerKind kind, string deviceName, Action<ControllerInputTable> feed, bool small = false)
    {
        var table = new ControllerInputTable();
        feed(table);
        var input = PrompterSettings.Default.Input with { SelectedController = kind };
        var status = kind == ControllerKind.MouseWheel
            ? null
            : new DeviceStatus(kind == ControllerKind.Xbox ? Pad : Contour, deviceName, DeviceConnection.Connected, BatteryLevel.Medium);
        return ControllerModelBuilder.Build(new ControllerBuildInput(input, ScrollMode.Display, status, false, table.Snapshot(), 10_050, small));
    }

    private static void Render(IControllerDrawing drawing, ControllerFrame frame, string name, bool raised, int zoom = 1)
    {
        drawing.Apply(frame, raised);
        var element = (FrameworkElement)drawing;
        var host = new Border { Width = 360, Height = 200, Padding = new Thickness(20), Child = element };
        host.Background = (Brush)Application.Current.FindResource(raised ? "Ilu.Brush.Raised" : "Ilu.Brush.Surface");
        host.Measure(new Size(360, 200));
        host.Arrange(new Rect(0, 0, 360, 200));
        host.UpdateLayout();
        var bitmap = new RenderTargetBitmap(720 * zoom, 400 * zoom, 192 * zoom, 192 * zoom, PixelFormats.Pbgra32);
        bitmap.Render(host);
        host.Child = null;   // the drawing may be rendered again in another state
        var dir = Environment.GetEnvironmentVariable("ILU_DRAWING_PNG_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(dir, name));
        encoder.Save(stream);
    }

    [WpfFact]
    public void TheMouseDrawing_Renders_WheelTurnedAndMiddleHeld()
    {
        EnsureTheme();
        var model = Build(ControllerKind.MouseWheel, "", t =>
        {
            t.Record(new RelativeInput(Preview, ControlIds.Wheel, 0, 1), 10_000);
            t.Record(new RelativeInput(Preview, ControlIds.Wheel, 0, 1), 10_000);
            t.Record(new ButtonInput(Preview, ControlIds.MiddleButton, 0, true), 10_000);
            t.Record(new ButtonInput(Preview, ControlIds.XButton1, 0, true), 10_000);
        });
        Assert.Equal(ControllerDrawingKind.Mouse, model.Frame.Kind);
        Render(new MouseDrawing(), model.Frame, "drawing-mouse.png", false);
    }

    [WpfFact]
    public void TheXboxDrawing_Renders_ALargeAndASmallBody()
    {
        EnsureTheme();
        var model = Build(ControllerKind.Xbox, "Xbox controller", t =>
        {
            t.Record(new ButtonInput(Pad, ControlIds.A, 0, true), 10_000);
            t.Record(new ButtonInput(Pad, ControlIds.Y, 0, true), 10_000);
            t.Record(new ButtonInput(Pad, ControlIds.DPadUp, 0, true), 10_000);
            t.Record(new AxisInput(Pad, ControlIds.RightTrigger, 0, 0.62), 10_000);
            t.Record(new AxisInput(Pad, ControlIds.LeftThumbX, 0, 0.5), 10_000);
            t.Record(new AxisInput(Pad, ControlIds.LeftThumbY, 0, 0.8), 10_000);
        });
        var drawing = new XboxDrawing();
        Render(drawing, model.Frame, "drawing-xbox.png", false);
        drawing.Small = true;
        Render(drawing, model.Frame, "drawing-xbox-flyout-small.png", true);
        Assert.Equal(PartLook.Lit, model.Frame.Look(ControlIds.A));
        Assert.Equal(PartLook.Flash, model.Frame.Look(ControlIds.Y));
    }

    [WpfFact]
    public void TheShuttleProDrawing_Renders_RingJogAndButtons()
    {
        EnsureTheme();
        // the default look: button 6 lit, button 9 (unbound) flashing, the ring at +3, the jog's own dimples
        var model = Build(ControllerKind.JogShuttle, "Contour ShuttlePRO v2", t =>
        {
            t.Record(new AxisInput(Contour, ControlIds.ShuttleRing, 0, 3.0 / 7), 10_000);
            t.Record(new ButtonInput(Contour, ControlIds.ShuttleButton(6), 0, true), 10_000);
            t.Record(new ButtonInput(Contour, ControlIds.ShuttleButton(9), 0, true), 10_000);
        });
        Assert.Equal(ControllerDrawingKind.ShuttlePro, model.Frame.Kind);
        Render(new ShuttleProDrawing(), model.Frame, "drawing-shuttlepro.png", false);

        // the jog's dimples turned by 3 jog clicks (108 degrees)
        void Jog(ControllerInputTable t)
        {
            t.Record(new AxisInput(Contour, ControlIds.ShuttleRing, 0, 3.0 / 7), 10_000);
            t.Record(new RelativeInput(Contour, ControlIds.JogWheel, 0, 3), 10_000);
            t.Record(new ButtonInput(Contour, ControlIds.ShuttleButton(12), 0, true), 10_000);
        }

        var normal = Build(ControllerKind.JogShuttle, "Contour ShuttlePRO v2", Jog);
        Assert.Equal(108, normal.Frame.Shuttle!.JogAngle);
        Render(new ShuttleProDrawing(), normal.Frame, "drawing-shuttlepro-normal-turned.png", false);

        var negative = Build(ControllerKind.JogShuttle, "Contour ShuttlePRO v2", t =>
            t.Record(new AxisInput(Contour, ControlIds.ShuttleRing, 0, -5.0 / 7), 10_000));
        Render(new ShuttleProDrawing(), negative.Frame, "drawing-shuttlepro-reverse.png", false);

        var missing = Build(ControllerKind.JogShuttle, "Contour ShuttlePRO v2", _ => { });
        var gone = ControllerModelBuilder.Build(new ControllerBuildInput(
            PrompterSettings.Default.Input with { SelectedController = ControllerKind.JogShuttle },
            ScrollMode.Display, new DeviceStatus(Contour, "Contour ShuttlePRO v2", DeviceConnection.Disconnected), false,
            ControllerTableSnapshot.Empty, 10_050, false));
        Assert.True(gone.Frame.Missing);
        Render(new ShuttleProDrawing(), gone.Frame, "drawing-shuttlepro-missing.png", false);
        Assert.False(missing.Frame.Missing);

        // nothing plugged in (the provider's generic name): still the PRO, drawn dimmed
        var none = ControllerModelBuilder.Build(new ControllerBuildInput(
            PrompterSettings.Default.Input with { SelectedController = ControllerKind.JogShuttle },
            ScrollMode.Display, new DeviceStatus(Contour, "Contour ShuttleXpress / PRO", DeviceConnection.NotFound), false,
            ControllerTableSnapshot.Empty, 10_050, false));
        Assert.Equal(ControllerDrawingKind.ShuttlePro, none.Frame.Kind);
        Assert.True(none.Frame.Missing);
        Render(new ShuttleProDrawing(), none.Frame, "drawing-shuttlepro-missing.png", false);
    }

    [WpfFact]
    public void TheShuttleProDrawing_Renders_EveryButtonLit_OneAtATime()
    {
        // a contact sheet of the numbering: each logical button lit in turn (written only with ILU_DRAWING_PNG_DIR)
        EnsureTheme();
        foreach (var n in ShuttleProLayout.Buttons)
        {
            var model = Build(ControllerKind.JogShuttle, "Contour ShuttlePRO v2", t =>
                t.Record(new ButtonInput(Contour, ControlIds.ShuttleButton(n), 0, true), 10_000));
            Render(new ShuttleProDrawing(), model.Frame, $"drawing-shuttlepro-button{n:00}.png", false);
        }
    }

    [WpfFact]
    public void TheShuttleXpressDrawing_Renders()
    {
        EnsureTheme();
        var model = Build(ControllerKind.JogShuttle, "Contour ShuttleXpress", t =>
        {
            t.Record(new AxisInput(Contour, ControlIds.ShuttleRing, 0, 3.0 / 7), 10_000);
            t.Record(new RelativeInput(Contour, ControlIds.JogWheel, 0, 2), 10_000);
            t.Record(new ButtonInput(Contour, ControlIds.ShuttleButton(3), 0, true), 10_000);
        });
        Assert.Equal(ControllerDrawingKind.ShuttleXpress, model.Frame.Kind);
        Render(new ShuttleXpressDrawing(), model.Frame, "drawing-shuttlexpress.png", false);

    }
}

