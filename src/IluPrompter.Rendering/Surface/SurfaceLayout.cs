using System.Globalization;
using System.Windows;
using IluPrompter.Core;

namespace IluPrompter.Rendering;

/// <summary>Pure layout decisions for the prompter surface (unit tested): the layout key and the reading-line markers.</summary>
internal static class SurfaceLayout
{
    /// <summary>
    /// Identity of everything that changes where lines break: snapshot version, text width, font size, spacing, face,
    /// weight, paragraph gap, alignment, section names. Two surfaces with equal keys have equal layouts.
    /// </summary>
    public static string ComputeLayoutKey(long snapshotVersion, TextArea area, TextSettings text, OutputSettings output) =>
        ComputeLayoutKey(snapshotVersion, area.Width, area.BaseFontSize, text, output.Alignment, output.ShowSectionNames);

    public static string ComputeLayoutKey(long snapshotVersion, double textWidth, double baseFontSize, TextSettings text,
        TextAlignmentSetting alignment, bool showSectionNames)
    {
        var c = CultureInfo.InvariantCulture;
        return string.Create(c,
            $"{snapshotVersion}|{textWidth:R}|{baseFontSize:R}|{text.LineSpacing:R}|{text.FontFace}|{text.FontWeight}|{text.ParagraphGapLines:R}|{alignment}|{showSectionNames}");
    }

    /// <summary>The layout key plus what only changes appearance (ink), so the document is rebuilt for a colour change
    /// without the engine seeing a new layout.</summary>
    public static string ComputeContentKey(string layoutKey, TextSettings text) => layoutKey + "|" + text.DefaultInk;

    public const double MarkerHeightFactor = 0.5;      // DESIGN: height 0.5 x line height
    public const double MarkerWidthFactor = 0.866;     // of its height: equilateral
    public const double MarkerMinMarginFactor = 0.6;   // DESIGN: narrower margin => edge bar
    public const double MarkerGapFactor = 0.1;         // of the line height, between marker and text
    public const double EdgeBarWidth = 6.0;            // DESIGN: 6 px bar

    /// <summary>
    /// Where the two reading-line markers go on the output, in output DIPs. A side whose margin is narrower than
    /// 0.6 x line height gets a 6-DIP bar at the screen edge instead of a triangle.
    /// </summary>
    public static ReadingMarkers ComputeMarkers(double outputWidth, double marginLeft, double marginRight, double lineHeight, double readingLineY)
    {
        var h = lineHeight * MarkerHeightFactor;
        var w = h * MarkerWidthFactor;
        var gap = lineHeight * MarkerGapFactor;
        var minMargin = lineHeight * MarkerMinMarginFactor;
        var top = readingLineY - (h / 2);

        var left = marginLeft >= minMargin
            ? new MarkerShape(MarkerKind.Triangle, new Rect(marginLeft - gap - w, top, w, h))
            : new MarkerShape(MarkerKind.Bar, new Rect(0, top, EdgeBarWidth, h));
        var right = marginRight >= minMargin
            ? new MarkerShape(MarkerKind.Triangle, new Rect(outputWidth - marginRight + gap, top, w, h))
            : new MarkerShape(MarkerKind.Bar, new Rect(outputWidth - EdgeBarWidth, top, EdgeBarWidth, h));
        return new ReadingMarkers(left, right);
    }

    /// <summary>The "dim above the reading line" band, in output DIPs: from the top of the text area down to the top of
    /// the line that sits on the reading line (half a base line above the reading line).</summary>
    public static Rect ComputeDimBand(TextArea area)
    {
        var bottom = Math.Clamp(area.ReadingLineY - (area.BaseLineHeight / 2), 0, area.Height);
        return new Rect(area.Left, area.Top, area.Width, bottom);
    }
}

internal enum MarkerKind
{
    Triangle = 0,
    Bar = 1,
}

internal readonly record struct MarkerShape(MarkerKind Kind, Rect Bounds);

internal readonly record struct ReadingMarkers(MarkerShape Left, MarkerShape Right);
