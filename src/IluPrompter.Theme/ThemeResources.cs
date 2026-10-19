using System.Windows;
using System.Windows.Media;

namespace IluPrompter.Theme;

/// <summary>A script ink or highlight offered in the editor menus (DESIGN.md "Script colours").</summary>
public sealed record ScriptColourOption(string Token, string Name, string? InkToken = null);

/// <summary>
/// Thread-safe access to design tokens from code (any WPF thread, including the output thread).
/// Colours are read once from Themes/IluTokens.xaml into an immutable map; no hex literals in code.
/// Complete in the scaffold; stream D owns later changes.
/// </summary>
public static class ThemeResources
{
    // Must stay first: touching PackUriHelper registers the pack:// scheme, which is otherwise only
    // registered once a WPF Application exists (unit tests, early startup).
    private static readonly string PackScheme = System.IO.Packaging.PackUriHelper.UriSchemePack;

    public static readonly Uri TokensUri = new("pack://application:,,,/IluPrompter.Theme;component/Themes/IluTokens.xaml", UriKind.Absolute);
    public static readonly Uri ControlsUri = new("pack://application:,,,/IluPrompter.Theme;component/Themes/IluControls.xaml", UriKind.Absolute);
    public static readonly Uri LogoUri = new("pack://application:,,,/IluPrompter.Theme;component/Images/wall-logo.png", UriKind.Absolute);

    private const string BundledFontBase = "pack://application:,,,/IluPrompter.Theme;component/Fonts/";
    private static readonly object Gate = new();
    private static IReadOnlyDictionary<string, Color>? _colors;

    /// <summary>Menu order and names from DESIGN.md.</summary>
    public static IReadOnlyList<ScriptColourOption> Inks { get; } =
    [
        new("ink-white", "White"), new("ink-yellow", "Yellow"), new("ink-amber", "Amber"), new("ink-green", "Green"),
        new("ink-cyan", "Cyan"), new("ink-pink", "Pink"), new("ink-red", "Red"), new("ink-grey", "Grey"),
    ];

    /// <summary>Each highlight carries its own ink (InkToken).</summary>
    public static IReadOnlyList<ScriptColourOption> Highlights { get; } =
    [
        new("highlight-yellow", "Yellow", "canvas"), new("highlight-blue", "Blue", "ink-white"),
        new("highlight-green", "Green", "ink-white"), new("highlight-magenta", "Magenta", "ink-white"),
        new("highlight-grey", "Grey", "ink-white"),
    ];

    public static FontFamily UiFont { get; } = new(new Uri(BundledFontBase), "./#Poppins, Segoe UI");
    public static FontFamily MonoFont { get; } = new(new Uri(BundledFontBase), "./#DM Mono, Cascadia Mono, Consolas");

    /// <summary>"Poppins" resolves to the bundled family; anything else to the installed family by name.</summary>
    public static FontFamily ScriptFont(string face) =>
        string.Equals(face, "Poppins", StringComparison.OrdinalIgnoreCase) ? UiFont : new FontFamily(face + ", Segoe UI");

    /// <summary>Token name ("ink-white", "script-paper") or key ("Ilu.Color.InkWhite").</summary>
    public static Color GetColor(string token) =>
        TryGetColor(token, out var c) ? c : throw new KeyNotFoundException("Unknown colour token: " + token);

    public static bool TryGetColor(string token, out Color color)
    {
        var map = Colors();
        var key = token.StartsWith("Ilu.Color.", StringComparison.Ordinal) ? token : "Ilu.Color." + ToPascal(token);
        return map.TryGetValue(key, out color);
    }

    /// <summary>Frozen brush for a token, created on the calling thread.</summary>
    public static SolidColorBrush GetBrush(string token)
    {
        var b = new SolidColorBrush(GetColor(token));
        b.Freeze();
        return b;
    }

    private static IReadOnlyDictionary<string, Color> Colors()
    {
        var existing = Volatile.Read(ref _colors);
        if (existing is not null) return existing;
        lock (Gate)
        {
            if (_colors is not null) return _colors;
            // LoadComponent works with or without an Application instance (unit tests, output thread).
            var dictionary = (ResourceDictionary)Application.LoadComponent(
                new Uri("/IluPrompter.Theme;component/Themes/IluTokens.xaml", UriKind.Relative));
            var map = new Dictionary<string, Color>(StringComparer.Ordinal);
            foreach (var key in dictionary.Keys)
            {
                if (key is string s && dictionary[key] is Color c) map[s] = c;
            }

            Volatile.Write(ref _colors, map);
            return map;
        }
    }

    private static string ToPascal(string token) =>
        string.Concat(token.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
}
