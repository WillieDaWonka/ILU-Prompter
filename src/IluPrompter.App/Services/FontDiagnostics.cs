using System.Windows;
using System.Windows.Media;
using IluPrompter.Core;
using IluPrompter.Theme;

namespace IluPrompter.App.Services;

/// <summary>
/// Phase-0 S6 (ARCHITECTURE §6): with <c>--diag</c>, log which font file each face and weight the app uses really
/// resolves to, and whether WPF had to simulate it (no bundled Bold or Italic yet, so 700 is a simulated SemiBold).
/// </summary>
public static class FontDiagnostics
{
    public static readonly int[] Weights = [400, 500, 600, 700];

    /// <summary>"Regular 400 -> pack://.../Poppins-Regular.ttf" style lines for one family.</summary>
    public static IReadOnlyList<string> Describe(string name, FontFamily family)
    {
        var lines = new List<string>(Weights.Length);
        foreach (var weight in Weights)
        {
            var typeface = new Typeface(family, FontStyles.Normal, FontWeight.FromOpenTypeWeight(weight), FontStretches.Normal);
            lines.Add(typeface.TryGetGlyphTypeface(out var glyphs)
                ? $"{name} {weight}: {glyphs.FontUri} (simulations: {glyphs.StyleSimulations})"
                : $"{name} {weight}: no glyph typeface (falls back to another family)");
        }

        return lines;
    }

    public static void Log(IAppLog log, PrompterSettings settings)
    {
        if (!log.DiagnosticsEnabled) return;
        try
        {
            foreach (var line in Describe("UI font", ThemeResources.UiFont)) log.Trace("fonts", line);
            foreach (var line in Describe("Script font '" + settings.Text.FontFace + "'", ThemeResources.ScriptFont(settings.Text.FontFace))) log.Trace("fonts", line);
            foreach (var line in Describe("Mono font", ThemeResources.MonoFont)) log.Trace("fonts", line);
        }
        catch (Exception ex)
        {
            log.Warn("fonts", "Font check failed: " + ex.Message);
        }
    }
}
