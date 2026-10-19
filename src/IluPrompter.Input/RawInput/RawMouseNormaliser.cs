using System.Globalization;
using IluPrompter.Core;
using Linearstar.Windows.RawInput.Native;

namespace IluPrompter.Input;

/// <summary>One mouse button edge decoded from a Raw Input record.</summary>
internal readonly record struct RawButtonEdge(string ControlId, bool IsPressed);

/// <summary>Pure conversions for the dedicated Raw Input mouse (no devices touched).</summary>
internal static class RawMouseNormaliser
{
    /// <summary>Largest number of edges one record can carry (Middle, X1, X2, each down and up).</summary>
    public const int MaxEdges = 6;

    /// <summary>
    /// Wheel notches from RAWMOUSE.usButtonData: <c>(short)data / 120.0</c>. Fractions are kept (free-spinning
    /// and high-resolution wheels send less than 120); the router accumulates them.
    /// </summary>
    public static double WheelNotches(int buttonData) => unchecked((short)buttonData) / 120.0;

    /// <summary>True when the record carries a vertical wheel movement (the horizontal wheel is ignored).</summary>
    public static bool HasVerticalWheel(RawMouseButtonFlags flags) => (flags & RawMouseButtonFlags.MouseWheel) != 0;

    /// <summary>
    /// Writes the Middle / XButton1 / XButton2 edges of a record into <paramref name="edges"/> and returns the
    /// count. Left and right buttons are not forwarded (the prompter mouse is a wheel device).
    /// </summary>
    public static int ButtonEdges(RawMouseButtonFlags flags, Span<RawButtonEdge> edges)
    {
        var n = 0;
        AddEdges(flags, RawMouseButtonFlags.MiddleButtonDown, RawMouseButtonFlags.MiddleButtonUp, ControlIds.MiddleButton, edges, ref n);
        AddEdges(flags, RawMouseButtonFlags.Button4Down, RawMouseButtonFlags.Button4Up, ControlIds.XButton1, edges, ref n);
        AddEdges(flags, RawMouseButtonFlags.Button5Down, RawMouseButtonFlags.Button5Up, ControlIds.XButton2, edges, ref n);
        return n;
    }

    private static void AddEdges(
        RawMouseButtonFlags flags, RawMouseButtonFlags down, RawMouseButtonFlags up, string id, Span<RawButtonEdge> edges, ref int n)
    {
        if ((flags & down) != 0)
        {
            edges[n++] = new RawButtonEdge(id, true);
        }

        if ((flags & up) != 0)
        {
            edges[n++] = new RawButtonEdge(id, false);
        }
    }

    /// <summary>Device paths are compared case-insensitively (Windows reports them in either case).</summary>
    public static bool SamePath(string? a, string? b) =>
        !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static string VidPid(int vendorId, int productId) =>
        string.Create(CultureInfo.InvariantCulture, $"VID_{vendorId:X4}&PID_{productId:X4}");

    /// <summary>Human name for the device list: the product name when Windows has one, otherwise the
    /// manufacturer, otherwise VID/PID.</summary>
    public static string DisplayName(string? productName, string? manufacturerName, int vendorId, int productId)
    {
        var product = productName?.Trim();
        var maker = manufacturerName?.Trim();
        var ids = VidPid(vendorId, productId);
        if (!string.IsNullOrEmpty(product))
        {
            return product + " (" + ids + ")";
        }

        return !string.IsNullOrEmpty(maker) ? maker + " mouse (" + ids + ")" : "Mouse (" + ids + ")";
    }
}
