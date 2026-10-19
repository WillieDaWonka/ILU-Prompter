using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). Basic prefix match.</summary>
public sealed class SectionParser : ISectionParser
{
    public bool TryParse(string paragraphText, string marker, out string name)
    {
        var t = paragraphText.TrimStart();
        if (marker.Length > 0 && t.StartsWith(marker, StringComparison.Ordinal))
        {
            name = t[marker.Length..].TrimStart('#', ' ', '\t').Trim();
            return true;
        }

        name = "";
        return false;
    }

    public IReadOnlyList<SectionInfo> Parse(IReadOnlyList<string> paragraphTexts, string marker)
    {
        var result = new List<SectionInfo>();
        for (var i = 0; i < paragraphTexts.Count; i++)
        {
            if (TryParse(paragraphTexts[i], marker, out var name))
                result.Add(new SectionInfo(result.Count, name.Length == 0 ? $"Section {result.Count + 1}" : name, i));
        }

        return result;
    }
}
