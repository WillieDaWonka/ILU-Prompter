using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace IluPrompter.Documents;

/// <summary>What an importer found out about one run of text (already resolved through styles and inheritance).</summary>
internal readonly record struct RunFormat(
    double Size,
    bool Bold,
    bool Italic,
    bool Underline,
    bool Strike,
    Color? Ink,
    Color? Highlight);

/// <summary>Turns imported formatting into the editor's canonical run (relative sizes, palette inks, no fonts).</summary>
internal static class ScriptRuns
{
    private static readonly double[] Factors = [0.8, 1.0, 1.25, 1.5];

    /// <summary>The size carrying the most characters (sizes are bucketed to half a point); 0 when there is no text.</summary>
    public static double DominantSize(IEnumerable<(double Size, int Characters)> samples)
    {
        var weight = new Dictionary<double, long>();
        foreach (var (size, characters) in samples)
        {
            if (size <= 0 || characters <= 0) continue;
            var bucket = Math.Round(size * 2, MidpointRounding.AwayFromZero) / 2;
            weight[bucket] = weight.GetValueOrDefault(bucket) + characters;
        }

        double best = 0;
        long bestWeight = 0;
        foreach (var (size, characters) in weight)
        {
            if (characters > bestWeight || (characters == bestWeight && size < best))
            {
                best = size;
                bestWeight = characters;
            }
        }

        return best;
    }

    /// <summary>Nearest of Small/Normal/Large/Huge for <paramref name="size"/> against <paramref name="dominant"/>.</summary>
    public static double NearestFactor(double size, double dominant)
    {
        if (size <= 0 || dominant <= 0) return 1.0;
        var ratio = size / dominant;
        var best = 1.0;
        var bestDistance = double.MaxValue;
        foreach (var factor in Factors)
        {
            var distance = Math.Abs(ratio - factor);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = factor;
            }
        }

        return best;
    }

    /// <summary>
    /// A run with only what the editor offers: Normal size is left unset (it inherits the editor base), other sizes are
    /// <c>editorBase * factor</c>; a highlight is written with its own ink; colours are palette inks; no font family,
    /// and no weight below bold.
    /// </summary>
    public static Run Create(string text, RunFormat format, double dominantSize, double editorBase)
    {
        var run = new Run(text);
        var factor = NearestFactor(format.Size, dominantSize);
        if (Math.Abs(factor - 1.0) > 1e-9) run.FontSize = editorBase * factor;
        if (format.Bold) run.FontWeight = FontWeights.Bold;
        if (format.Italic) run.FontStyle = FontStyles.Italic;
        if (format.Underline || format.Strike)
        {
            var decorations = new TextDecorationCollection();
            if (format.Underline) decorations.Add(TextDecorations.Underline[0].Clone());
            if (format.Strike) decorations.Add(TextDecorations.Strikethrough[0].Clone());
            run.TextDecorations = decorations;
        }

        var highlight = format.Highlight is { } colour ? ScriptColours.NearestHighlight(colour) : null;
        if (highlight is { } chosen)
        {
            run.Background = Frozen(chosen.Fill);
            run.Foreground = Frozen(chosen.Ink); // a highlight carries its own ink
        }
        else if (format.Ink is { } ink && ScriptColours.NearestInk(ink) is { } mapped)
        {
            run.Foreground = Frozen(mapped);
        }

        return run;
    }

    private static SolidColorBrush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }
}
