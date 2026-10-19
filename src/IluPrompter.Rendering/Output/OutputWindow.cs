using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using IluPrompter.Core;
using IluPrompter.Theme;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace IluPrompter.Rendering;

/// <summary>
/// The presenter's window: borderless, topmost, never activated, off Alt+Tab, no cursor, black from the first frame,
/// and placed on the display's full physical bounds with SetWindowPos (not the work area). Shows the script and nothing
/// else (DESIGN "Output window"). Lives on the output thread.
/// </summary>
internal sealed class OutputWindow : Window
{
    private readonly PrompterSurface _surface;
    private readonly Rectangle _blank;
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly IAppLog _log;
    private readonly bool _kiosk;
    private HwndSource? _source;
    private PixelRect _placed;
    private bool _hasPlacement;
    private OutputGeometry _output = OutputGeometry.Default1080;

    public OutputWindow(PrompterSurface surface, IAppLog log, bool kiosk)
    {
        _surface = surface;
        _log = log;
        _kiosk = kiosk;

        var paper = ThemeResources.GetBrush("script-paper");
        Background = paper;                      // black before the first frame: it never flashes white
        Title = kiosk ? "ILU Prompter output" : "ILU Prompter output (dev window)";
        UseLayoutRounding = false;
        SnapsToDevicePixels = false;
        AllowsTransparency = false;
        Focusable = false;
        Cursor = Cursors.None;
        ForceCursor = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

        if (kiosk)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        else
        {
            // --dev-output-on-primary: an ordinary, movable window sized to the surface. Topmost like the real output, or
            // on a single display it disappears behind the (maximised) control window as soon as that is clicked.
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = true;
            ShowActivated = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = 40;
            Top = 40;
        }

        _surface.HorizontalAlignment = HorizontalAlignment.Left;
        _surface.VerticalAlignment = VerticalAlignment.Top;
        _surface.RenderTransformOrigin = new Point(0, 0);
        _surface.RenderTransform = _scale;

        _blank = new Rectangle { Fill = paper, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        var root = new Grid { Background = paper, ClipToBounds = true, IsHitTestVisible = false };
        root.Children.Add(_surface);
        root.Children.Add(_blank);
        Content = root;

        SourceInitialized += OnSourceInitialized;
        DpiChanged += OnDpiChanged;
        SizeChanged += (_, _) => ApplyOutputScale();
    }

    /// <summary>The last rectangle passed to SetWindowPos, in physical pixels.</summary>
    public PixelRect? PlacedBounds => _hasPlacement ? _placed : null;

    /// <summary>Raised after the window was re-placed because Windows changed its DPI.</summary>
    public event EventHandler? Replaced;

    public void SetBlank(bool blank) => _blank.Visibility = blank ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Tell the window which display geometry its content was laid out for, so a DPI mismatch between the
    /// window and the display cannot change what fills the screen.</summary>
    public void SetOutputGeometry(OutputGeometry output)
    {
        _output = output;
        ApplyOutputScale();
    }

    /// <summary>Create the HWND if needed and put it exactly on <paramref name="boundsPx"/> (physical pixels), topmost,
    /// without activating. Safe to call repeatedly.</summary>
    public void PlaceOn(PixelRect boundsPx)
    {
        var hwnd = new HWND(new WindowInteropHelper(this).EnsureHandle());
        _placed = boundsPx;
        _hasPlacement = true;
        PInvoke.SetWindowPos(hwnd, HWND.HWND_TOPMOST, boundsPx.X, boundsPx.Y, boundsPx.Width, boundsPx.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);
        ApplyOutputScale();
    }

    /// <summary>The window rectangle as Windows reports it (physical pixels); for the S5 placement check.</summary>
    public PixelRect? GetActualBounds()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return null;
        return PInvoke.GetWindowRect(new HWND(handle), out var r)
            ? new PixelRect(r.left, r.top, r.right - r.left, r.bottom - r.top)
            : null;
    }

    private void ApplyOutputScale()
    {
        // Surface DIP -> device px must be the display's DPI scale. The window's DIP -> px is its own DPI.
        var windowDpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var s = windowDpi > 0 ? _output.DpiScale / windowDpi : 1.0;
        if (Math.Abs(s - 1.0) < 1e-6) s = 1.0;
        if (_scale.ScaleX != s) _scale.ScaleX = s;
        if (_scale.ScaleY != s) _scale.ScaleY = s;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);

        var hwnd = new HWND(handle);
        if (_kiosk)
        {
            var ex = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            ex |= WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
            PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)ex);
        }

        // No fade-in / fade-out when the window is shown or hidden.
        unsafe
        {
            BOOL disable = true;
            PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, &disable, (uint)sizeof(BOOL));
        }

        ApplyOutputScale();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)PInvoke.WM_MOUSEACTIVATE && _kiosk)
        {
            handled = true;
            return (IntPtr)3;   // MA_NOACTIVATE
        }

        return IntPtr.Zero;
    }

    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
    {
        // WPF applies Windows' suggested rectangle, which can be a few pixels off the display bounds. Put it back.
        if (_hasPlacement && _kiosk)
        {
            var bounds = _placed;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, () =>
            {
                PlaceOn(bounds);
                _log.Info("output", $"DPI changed to {e.NewDpi.DpiScaleX:0.##}x; window re-placed on {bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}");
                Replaced?.Invoke(this, EventArgs.Empty);
            });
        }

        ApplyOutputScale();
    }
}
