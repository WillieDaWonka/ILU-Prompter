using IluPrompter.Core;

namespace IluPrompter.App.Logic;

/// <summary>Merging the control window's bounds into its remembered settings without losing the rest of them.</summary>
public static class WindowBounds
{
    /// <summary>
    /// <paramref name="boundsValid"/> is false for the empty <c>RestoreBounds</c> of a window that opened maximised and was
    /// never restored: then only the maximised flag changes. Everything else in <paramref name="current"/> (the live
    /// panel's tab and whether it is hidden) is kept: the settings record is copied with <c>with</c>, never rebuilt.
    /// </summary>
    public static WindowSettings Apply(WindowSettings current, double left, double top, double width, double height, bool maximised, bool boundsValid) =>
        boundsValid
            ? current with { Left = left, Top = top, Width = width, Height = height, IsMaximized = maximised }
            : current with { IsMaximized = maximised };
}
