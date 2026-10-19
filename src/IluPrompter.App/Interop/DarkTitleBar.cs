using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using IluPrompter.Theme;

namespace IluPrompter.App.Interop;

/// <summary>
/// DESIGN.md "No white, anywhere": every window gets the dark immersive title bar
/// (DWMWA_USE_IMMERSIVE_DARK_MODE = 20) before it is first shown. On Windows 11 the caption is also painted in the
/// `backdrop` colour with `text` ink. Failures are ignored (older Windows 10 builds simply keep their title bar).
/// </summary>
public static class DarkTitleBar
{
    private const int DwmaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmaUseImmersiveDarkMode = 20;
    private const int DwmaBorderColor = 34;
    private const int DwmaCaptionColor = 35;
    private const int DwmaTextColor = 36;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(DarkTitleBar), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject d) => (bool)d.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject d, bool value) => d.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window || e.NewValue is not true) return;
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Apply(window);
        else window.SourceInitialized += (_, _) => Apply(window);
    }

    /// <summary>Apply the dark title bar to a window that already has a handle.</summary>
    public static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        try
        {
            var on = 1;
            if (DwmSetWindowAttribute(hwnd, DwmaUseImmersiveDarkMode, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DwmaUseImmersiveDarkModeBefore20H1, ref on, sizeof(int));

            SetColour(hwnd, DwmaCaptionColor, "backdrop");
            SetColour(hwnd, DwmaBorderColor, "line");
            SetColour(hwnd, DwmaTextColor, "text");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // No dark title bar on this Windows; nothing else to do.
        }
    }

    private static void SetColour(IntPtr hwnd, int attribute, string token)
    {
        if (!ThemeResources.TryGetColor(token, out Color c)) return;
        var colorRef = c.R | (c.G << 8) | (c.B << 16); // COLORREF 0x00BBGGRR
        DwmSetWindowAttribute(hwnd, attribute, ref colorRef, sizeof(int));
    }
}
