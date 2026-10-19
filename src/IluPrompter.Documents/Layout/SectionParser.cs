using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>
/// Section-marker parsing ("## Name"), ARCHITECTURE §2.11. Pure and stateless.
/// A paragraph is a marker when its text, after leading whitespace, starts with the marker (so "###" counts for "##").
/// The name is the first line of the rest with leading '#' and whitespace trimmed.
/// </summary>
public sealed class SectionParser : ISectionParser
{
    public bool TryParse(string paragraphText, string marker, out string name)
    {
        name = "";
        if (string.IsNullOrEmpty(paragraphText) || string.IsNullOrWhiteSpace(marker)) return false;

        var text = paragraphText.AsSpan().TrimStart();
        if (!text.StartsWith(marker.AsSpan(), StringComparison.Ordinal)) return false;

        var rest = text[marker.Length..];
        var lineEnd = rest.IndexOfAny('\r', '\n', '\v');
        if (lineEnd >= 0) rest = rest[..lineEnd];
        var start = 0;
        while (start < rest.Length && (rest[start] == '#' || char.IsWhiteSpace(rest[start]))) start++;
        name = rest[start..].Trim().ToString();
        return true;
    }

    public IReadOnlyList<SectionInfo> Parse(IReadOnlyList<string> paragraphTexts, string marker)
    {
        ArgumentNullException.ThrowIfNull(paragraphTexts);
        var result = new List<SectionInfo>();
        for (var i = 0; i < paragraphTexts.Count; i++)
        {
            if (!TryParse(paragraphTexts[i], marker, out var name)) continue;
            var index = result.Count;
            result.Add(new SectionInfo(index, name.Length == 0 ? $"Section {index + 1}" : name, i));
        }

        return result;
    }
}
