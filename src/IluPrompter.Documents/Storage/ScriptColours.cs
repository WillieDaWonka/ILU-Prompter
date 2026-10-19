using System.Windows.Media;
using IluPrompter.Theme;

namespace IluPrompter.Documents;

/// <summary>
/// Maps arbitrary imported colours onto the script palette (DESIGN.md "Script colours"): eight inks and five
/// highlights, each highlight with its own ink. Distances are plain RGB. Tokens come from <see cref="ThemeResources"/>;
/// there are no colour literals here.
/// </summary>
internal static class ScriptColours
{
    private static readonly Lazy<(string Token, Color Colour)[]> Inks = new(() =>
        ThemeResources.Inks.Select(i => (i.Token, ThemeResources.GetColor(i.Token))).ToArray());

    private static readonly Lazy<(Color Fill, Color Ink)[]> Highlights = new(() =>
        ThemeResources.Highlights
            .Select(h => (ThemeResources.GetColor(h.Token), ThemeResources.GetColor(h.InkToken ?? "ink-white")))
            .ToArray());

    /// <summary>
    /// The nearest of the eight inks, or null when the colour means "ordinary text": transparent, near black
    /// (Word's automatic colour on white paper), near white, or nearest to the default ink itself. Null means the run
    /// inherits the default ink, so the user's default-ink setting still applies to it.
    /// </summary>
    public static Color? NearestInk(Color colour)
    {
        if (colour.A < 16) return null;
        var max = Math.Max(colour.R, Math.Max(colour.G, colour.B));
        var min = Math.Min(colour.R, Math.Min(colour.G, colour.B));
        if (max <= 0x50 || min >= 0xE0) return null;

        var best = Inks.Value[0];
        var bestDistance = int.MaxValue;
        foreach (var candidate in Inks.Value)
        {
            var distance = Distance(colour, candidate.Colour);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best.Token == "ink-white" ? null : best.Colour;
    }

    /// <summary>The nearest of the five highlights with the ink it carries, or null for no highlight (transparent,
    /// white paper shading, or black).</summary>
    public static (Color Fill, Color Ink)? NearestHighlight(Color colour)
    {
        if (colour.A < 16) return null;
        var max = Math.Max(colour.R, Math.Max(colour.G, colour.B));
        var min = Math.Min(colour.R, Math.Min(colour.G, colour.B));
        if (min >= 0xF0 || max <= 0x20) return null;

        var best = Highlights.Value[0];
        var bestDistance = int.MaxValue;
        foreach (var candidate in Highlights.Value)
        {
            var distance = Distance(colour, candidate.Fill);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private static int Distance(Color a, Color b)
    {
        int dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return (dr * dr) + (dg * dg) + (db * db);
    }
}
