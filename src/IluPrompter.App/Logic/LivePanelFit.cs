namespace IluPrompter.App.Logic;

/// <summary>Where the live panel's body goes (DESIGN.md "Where the body goes").</summary>
public enum LivePanelBodyForm
{
    /// <summary>Under the bar, large: 168 tall, drawing 320 x 160.</summary>
    InlineLarge = 0,

    /// <summary>Under the bar, small: 120 tall, drawing 240 x 120 (0.75).</summary>
    InlineSmall,

    /// <summary>The column keeps only the 32 px bar; the body opens as a flyout over the editor.</summary>
    Flyout,
}

/// <summary>The outcome of fitting the live panel to the height left in the live column.</summary>
public readonly record struct LivePanelFitResult(LivePanelBodyForm Form, bool HideTransportCaptions, bool MovePreviewCaption);

/// <summary>
/// Chooses the live panel's form from the window's height, never from state (DESIGN.md "Live panel (D7)"). The height is
/// the free height below the readouts row, with the LIVE caption under the preview counted as if it were showing, so
/// going LIVE never moves or reshapes the panel. Pure.
/// </summary>
public static class LivePanelFit
{
    /// <summary>Rule 1 + bar 32 + gap 8 + body 168.</summary>
    public const double InlineLargeMinHeight = 209;

    /// <summary>Rule 1 + bar 32 + gap 8 + body 120.</summary>
    public const double InlineSmallMinHeight = 161;

    /// <summary>Bar 32 + gap 8: the least the column may leave the bar.</summary>
    public const double BarMinHeight = 40;

    /// <summary>What hiding the transport captions frees: caption 18 + 2 above it.</summary>
    public const double TransportCaptionsHeight = 20;

    /// <summary>What moving the preview's output caption into its tooltip frees: 20 + 4 above it.</summary>
    public const double PreviewCaptionHeight = 24;

    public const double FlyoutMaxWidth = 600;

    /// <summary>Flyout padding 12 each side.</summary>
    public const double FlyoutPadding = 12;

    /// <summary>The large drawing needs an inner width of at least 576 (320 + 16 + 240 for the rows).</summary>
    public const double LargeInnerMinWidth = 576;

    /// <summary>
    /// <paramref name="freeHeight"/> is measured with no give-way applied. <paramref name="transportCaptionsShown"/> is
    /// true when the tier would show the captions (Wide); at Compact and Narrow step 1 frees nothing.
    /// </summary>
    public static LivePanelFitResult ForHeight(double freeHeight, bool transportCaptionsShown)
    {
        var form = freeHeight >= InlineLargeMinHeight ? LivePanelBodyForm.InlineLarge
            : freeHeight >= InlineSmallMinHeight ? LivePanelBodyForm.InlineSmall
            : LivePanelBodyForm.Flyout;

        // The bar always fits: the transport captions give way first, then the preview's output caption. Nothing else.
        var hideCaptions = false;
        var movePreviewCaption = false;
        if (freeHeight < BarMinHeight)
        {
            hideCaptions = transportCaptionsShown;
            var saved = transportCaptionsShown ? TransportCaptionsHeight : 0;
            movePreviewCaption = freeHeight + saved < BarMinHeight;
        }

        return new LivePanelFitResult(form, hideCaptions, movePreviewCaption);
    }

    /// <summary>The flyout is min(600, editor width - 16) wide.</summary>
    public static double FlyoutWidth(double editorWidth) => Math.Max(0, Math.Min(FlyoutMaxWidth, editorWidth - 16));

    /// <summary>The large drawing when the flyout's inner width is at least 576, else the small one.</summary>
    public static bool FlyoutIsLarge(double editorWidth) => FlyoutWidth(editorWidth) - (2 * FlyoutPadding) >= LargeInnerMinWidth;
}
