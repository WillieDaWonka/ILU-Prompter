using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using IluPrompter.Core;
using IluPrompter.Rendering;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace IluPrompter.Rendering.Tests;

/// <summary>A WPF test that creates a real, briefly visible window; runs only with <c>ILU_VISUAL_TESTS=1</c>.</summary>
public sealed class VisualFactAttribute : WpfFactAttribute
{
    public const string Variable = "ILU_VISUAL_TESTS";

    public VisualFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) != "1")
        {
            Skip = "Shows a real window for a moment. Set " + Variable + "=1 to run.";
        }
    }
}

/// <summary>
/// Checks the real kiosk window mechanics (SetWindowPos in physical pixels, WS_EX_NOACTIVATE / TOOLWINDOW / TOPMOST,
/// not activated, hidden until placed) with a small 640 x 360 rectangle so nothing covers the operator's screen.
/// The full-screen placement on a second display is a manual check (docs/streams/C-rendering.md).
/// </summary>
public class OutputWindowHardwareTests
{
    private static WINDOW_EX_STYLE ExStyle(OutputWindow w) =>
        (WINDOW_EX_STYLE)PInvoke.GetWindowLong(new HWND(new System.Windows.Interop.WindowInteropHelper(w).Handle), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

    private static PrompterSurface MakeSurface() =>
        new("hw", new SurfaceServices(new FakeEngine(), new PromptDocumentService(), new FakeSettings(), new FakeDocumentFactory(),
            new FakeGeometryBuilder(), new RecordingLog(), () => new OutputGeometry(640, 360, 1.0, 60)), snapToPixels: true);

    [VisualFact]
    public async Task KioskWindow_IsPlacedExactly_NeverActivated_AndHasTheRightStyles()
    {
        var window = new OutputWindow(MakeSurface(), new RecordingLog(), kiosk: true);
        try
        {
            var rect = new PixelRect(120, 140, 640, 360);
            window.PlaceOn(rect);
            Assert.False(window.IsVisible);                                  // placed while hidden
            Assert.Equal(rect, window.GetActualBounds());

            window.Show();
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            await Task.Delay(300);

            Assert.Equal(rect, window.GetActualBounds());                    // Show did not move it
            var ex = ExStyle(window);
            Assert.True(ex.HasFlag(WINDOW_EX_STYLE.WS_EX_NOACTIVATE));
            Assert.True(ex.HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW));       // out of Alt+Tab
            Assert.True(ex.HasFlag(WINDOW_EX_STYLE.WS_EX_TOPMOST));
            Assert.False(window.IsActive);
            Assert.False(window.ShowInTaskbar);
            Assert.Equal(WindowStyle.None, window.WindowStyle);
            Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
            Assert.Equal(Cursors.None, window.Cursor);
            Assert.Equal(Theme.ThemeResources.GetColor("script-paper"), ((System.Windows.Media.SolidColorBrush)window.Background).Color);

            // Move it again (as after a display change) and hide it.
            var moved = new PixelRect(200, 200, 640, 360);
            window.PlaceOn(moved);
            Assert.Equal(moved, window.GetActualBounds());
            window.Hide();
            Assert.False(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [VisualFact]
    public async Task DevWindow_IsAnOrdinaryWindow()
    {
        var window = new OutputWindow(MakeSurface(), new RecordingLog(), kiosk: false);
        try
        {
            window.Show();
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            Assert.True(window.ShowInTaskbar);
            Assert.False(window.Topmost);
            var ex = ExStyle(window);
            Assert.False(ex.HasFlag(WINDOW_EX_STYLE.WS_EX_NOACTIVATE));
            Assert.False(ex.HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW));
        }
        finally
        {
            window.Close();
        }
    }

    [VisualFact]
    public async Task OutputScale_CompensatesForAWindowDpiThatDiffersFromTheDisplays()
    {
        var window = new OutputWindow(MakeSurface(), new RecordingLog(), kiosk: true);
        try
        {
            window.PlaceOn(new PixelRect(120, 140, 640, 360));
            window.Show();
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            var windowDpi = System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX;

            window.SetOutputGeometry(new OutputGeometry(640, 360, windowDpi, 60));
            Assert.Equal(1.0, Scale(window));
            window.SetOutputGeometry(new OutputGeometry(640, 360, windowDpi * 2, 60));
            Assert.Equal(2.0, Scale(window), 6);
        }
        finally
        {
            window.Close();
        }
    }

    private static double Scale(OutputWindow w)
    {
        var root = (System.Windows.Controls.Grid)w.Content;
        var surface = (FrameworkElement)root.Children[0];
        return ((System.Windows.Media.ScaleTransform)surface.RenderTransform).ScaleX;
    }
}
