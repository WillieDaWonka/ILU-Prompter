namespace IluPrompter.App.Logic;

/// <summary>DESIGN.md "Reference windows": the responsive tiers of the control window.</summary>
public enum LayoutTier
{
    /// <summary>1600 DIP and over: 240 px sections panel, 720 px live column, transport captions, margin editor in the live column.</summary>
    Wide = 0,

    /// <summary>1200 to 1599: 44 px sections rail, 480 px live column, icon-only transport, margin editor in Settings.</summary>
    Compact = 1,

    /// <summary>1024 to 1199: rail, 440 px live column, chips move to the app bar, readouts into the status bar.</summary>
    Narrow = 2,
}

public static class LayoutTiers
{
    public const double WideMinWidth = 1600;
    public const double CompactMinWidth = 1200;

    public static LayoutTier ForWidth(double widthDip) =>
        widthDip >= WideMinWidth ? LayoutTier.Wide
        : widthDip >= CompactMinWidth ? LayoutTier.Compact
        : LayoutTier.Narrow;

    /// <summary>Width of the live column per tier (DESIGN.md spacing tokens).</summary>
    public static double LiveColumnWidth(LayoutTier tier) => tier switch
    {
        LayoutTier.Wide => 720,
        LayoutTier.Compact => 480,
        _ => 440,
    };
}
