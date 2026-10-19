using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using IluPrompter.Theme;

namespace IluPrompter.App.Tests;

/// <summary>DESIGN.md "WPF resource dictionary": the token file must match the design file, and nothing else may hold a hex colour.</summary>
public class ThemeTokenTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DESIGN.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("DESIGN.md not found above " + AppContext.BaseDirectory);
    }

    private static IReadOnlyList<string> FrontMatter()
    {
        var lines = File.ReadAllLines(Path.Combine(RepoRoot(), "DESIGN.md"));
        var end = Array.IndexOf(lines, "---", 1);
        return lines[1..end];
    }

    /// <summary>Entries of one top-level block of the front matter (colors:, spacing:, rounded:).</summary>
    private static Dictionary<string, string> Block(string name)
    {
        var result = new Dictionary<string, string>();
        var inside = false;
        foreach (var line in FrontMatter())
        {
            if (!line.StartsWith(' ')) inside = line.TrimEnd() == name + ":";
            else if (inside && Regex.Match(line, @"^\s{2}([a-z0-9-]+):\s*""?([^""]+?)""?\s*$") is { Success: true } m)
                result[m.Groups[1].Value] = m.Groups[2].Value;
        }

        return result;
    }

    private static string Pascal(string token) =>
        string.Concat(token.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    private static Color Parse(string hex)
    {
        var h = hex.TrimStart('#');
        var v = uint.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return h.Length == 8
            ? Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v) // WPF #AARRGGBB
            : Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    private static ResourceDictionary Tokens() =>
        (ResourceDictionary)Application.LoadComponent(new Uri("/IluPrompter.Theme;component/Themes/IluTokens.xaml", UriKind.Relative));

    [WpfFact]
    public void EveryDesignColour_ExistsInTokens_WithTheSameValue()
    {
        var colours = Block("colors");
        // Both ways: the token file has no colour that DESIGN.md lacks, and DESIGN.md lacks none that the file has.
        var inFile = Tokens().Keys.OfType<string>().Count(k => k.StartsWith("Ilu.Color.", StringComparison.Ordinal));
        Assert.True(colours.Count > 40, "front matter parsed: " + colours.Count);
        Assert.Equal(colours.Count, inFile);
        foreach (var (name, hex) in colours)
        {
            Assert.True(ThemeResources.TryGetColor(name, out var actual), "missing token: " + name);
            Assert.Equal(Parse(hex), actual);
        }
    }

    [WpfFact]
    public void EveryDesignSpacingAndRadius_ExistsInTokens_WithTheSameValue()
    {
        var tokens = Tokens();
        foreach (var (name, value) in Block("spacing"))
        {
            var key = "Ilu.Size." + Pascal(name);
            Assert.True(tokens.Contains(key), "missing size: " + key);
            Assert.Equal(double.Parse(value.Replace("px", ""), CultureInfo.InvariantCulture), (double)tokens[key]);
        }

        foreach (var (name, value) in Block("rounded"))
        {
            var key = "Ilu.Radius." + Pascal(name);
            Assert.True(tokens.Contains(key), "missing radius: " + key);
            Assert.Equal(double.Parse(value.Replace("px", ""), CultureInfo.InvariantCulture), ((CornerRadius)tokens[key]).TopLeft);
        }
    }

    [Fact]
    public void NoHexColourLiteral_OutsideTheTokenFile()
    {
        var hex = new Regex("#[0-9A-Fa-f]{6}");
        var offenders = new List<string>();
        var src = Path.Combine(RepoRoot(), "src");
        foreach (var file in Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)))
        {
            var relative = Path.GetRelativePath(src, file);
            var parts = relative.Split(Path.DirectorySeparatorChar);
            if (parts.Contains("bin") || parts.Contains("obj")) continue;
            if (string.Equals(Path.GetFileName(file), "IluTokens.xaml", StringComparison.OrdinalIgnoreCase)) continue;
            var text = File.ReadAllLines(file);
            for (var i = 0; i < text.Length; i++)
                if (hex.IsMatch(text[i])) offenders.Add($"{relative}:{i + 1}: {text[i].Trim()}");
        }

        Assert.True(offenders.Count == 0, "Hex colours outside IluTokens.xaml:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void ScanFindsAHexLiteral_WhenOneExists()
    {
        // The scan must be able to fail: prove the pattern matches the shapes DESIGN.md uses.
        var hex = new Regex("#[0-9A-Fa-f]{6}");
        Assert.Matches(hex, "<Color>#111217</Color>");
        Assert.Matches(hex, "Color=\"#1F3274D9\"");
        Assert.DoesNotMatch(hex, "pack://application:,,,/Fonts/#Poppins");
    }

    [WpfFact]
    public void ControlsDictionary_Loads_AndHasEveryTextStyleAndComponentStyle()
    {
        var controls = (ResourceDictionary)Application.LoadComponent(new Uri("/IluPrompter.Theme;component/Themes/IluControls.xaml", UriKind.Relative));
        string[] keys =
        [
            "Ilu.Text.Title", "Ilu.Text.Heading", "Ilu.Text.Body", "Ilu.Text.Label", "Ilu.Text.Caption", "Ilu.Text.Button",
            "Ilu.Text.ButtonPrimary", "Ilu.Text.State", "Ilu.Text.Mono", "Ilu.Text.Readout", "Ilu.Text.Icon", "Ilu.Text.IconTransport", "Ilu.Text.Editor",
            "Ilu.Button.Secondary", "Ilu.Button.Primary", "Ilu.Button.Transport", "Ilu.Button.Play", "Ilu.Button.Toolbar", "Ilu.ToggleButton.Toolbar",
            "Ilu.RadioButton.Segment", "Ilu.ToggleButton.Blank", "Ilu.Button.Chip", "Ilu.Chip", "Ilu.TextBox", "Ilu.ComboBox", "Ilu.CheckBox",
            "Ilu.RadioButton", "Ilu.Menu", "Ilu.MenuItem", "Ilu.ContextMenu", "Ilu.ToolTip", "Ilu.ScrollBar", "Ilu.ListBoxItem.Section",
            "Ilu.ListBoxItem.Nav", "Ilu.Slider.Speed", "Ilu.Panel", "Ilu.Window", "Ilu.RichTextBox.Paper",
        ];
        foreach (var key in keys) Assert.True(controls.Contains(key), "missing style: " + key);
        // The implicit styles that keep every native control dark.
        foreach (var type in new[] { typeof(System.Windows.Controls.Button), typeof(System.Windows.Controls.TextBox), typeof(System.Windows.Controls.ComboBox),
                     typeof(System.Windows.Controls.CheckBox), typeof(System.Windows.Controls.Menu), typeof(System.Windows.Controls.MenuItem),
                     typeof(System.Windows.Controls.ContextMenu), typeof(System.Windows.Controls.ToolTip), typeof(System.Windows.Controls.Primitives.ScrollBar) })
            Assert.True(controls.Contains(type), "missing implicit style for " + type.Name);
    }

    [WpfFact]
    public void BundledFonts_Resolve_ForUiAndMono_AtTheDesignWeights()
    {
        foreach (var (family, weight) in new[] { (ThemeResources.UiFont, 400), (ThemeResources.UiFont, 500), (ThemeResources.UiFont, 600), (ThemeResources.MonoFont, 500) })
        {
            var typeface = new Typeface(family, FontStyles.Normal, FontWeight.FromOpenTypeWeight(weight), FontStretches.Normal);
            Assert.True(typeface.TryGetGlyphTypeface(out var glyphs), "no glyph typeface for weight " + weight);
            Assert.Contains("IluPrompter.Theme", glyphs.FontUri.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [WpfFact]
    public void BundledPoppins_GroupsItsWeights_EachWeightResolvesToItsOwnFile_S6()
    {
        // Phase-0 S6: "#Poppins" must group Regular / Medium / SemiBold, so FontWeight picks the real file (no simulation).
        var expected = new Dictionary<int, string> { [400] = "Poppins-Regular.ttf", [500] = "Poppins-Medium.ttf", [600] = "Poppins-SemiBold.ttf" };
        foreach (var (weight, file) in expected)
        {
            var typeface = new Typeface(ThemeResources.UiFont, FontStyles.Normal, FontWeight.FromOpenTypeWeight(weight), FontStretches.Normal);
            Assert.True(typeface.TryGetGlyphTypeface(out var glyphs));
            Assert.EndsWith(file, glyphs.FontUri.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(StyleSimulations.None, glyphs.StyleSimulations);
        }

        // The script face "Poppins" is the same bundled family; the diagnostics describe every weight.
        Assert.Same(ThemeResources.UiFont, ThemeResources.ScriptFont("Poppins"));
        Assert.Equal(4, IluPrompter.App.Services.FontDiagnostics.Describe("UI font", ThemeResources.UiFont).Count);
    }

    [WpfFact]
    public void ScriptColourMenus_UseTokens_ThatExist()
    {
        foreach (var ink in ThemeResources.Inks) Assert.True(ThemeResources.TryGetColor(ink.Token, out _), ink.Token);
        foreach (var h in ThemeResources.Highlights)
        {
            Assert.True(ThemeResources.TryGetColor(h.Token, out _), h.Token);
            Assert.True(ThemeResources.TryGetColor(h.InkToken!, out _), h.InkToken);
        }
    }

    [Fact]
    public void CentreFill_RunsFromTheCentreToTheThumb()
    {
        // 220 wide, thumb ends 10 px in: usable 200, centre at 110.
        var zero = CentreFillConverter.Compute(0, -10, 10, 220);
        Assert.Equal(110, zero.Left, 3);
        Assert.Equal(110, zero.Right, 3);
        var forward = CentreFillConverter.Compute(5, -10, 10, 220);
        Assert.Equal(110, forward.Left, 3);
        Assert.Equal(60, forward.Right, 3); // thumb at 160
        var reverse = CentreFillConverter.Compute(-5, -10, 10, 220);
        Assert.Equal(60, reverse.Left, 3);
        Assert.Equal(110, reverse.Right, 3);
    }
}
