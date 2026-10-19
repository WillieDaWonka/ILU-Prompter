using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Xml;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>
/// Turns the live editor document into an immutable snapshot and back (ARCHITECTURE §2.11 / §4.E).
/// Stateless; control UI thread only. The editor document is never modified.
/// <para>
/// Anchors: the snapshot has exactly one paragraph per canonical editor paragraph, so paragraph indices are equal.
/// Character offsets are equal too, except inside list items, where the snapshot paragraph starts with the list
/// prefix ("• " or "n. ") and offsets are larger by its length.
/// </para>
/// </summary>
public sealed class SnapshotBuilder : ISnapshotBuilder
{
    private readonly IDocumentNavigator _navigator;
    private readonly ISectionParser _sections;

    public SnapshotBuilder(IDocumentNavigator navigator, ISectionParser sections)
    {
        _navigator = navigator;
        _sections = sections;
    }

    public PromptDocumentSnapshot Build(FlowDocument editorDocument, long version, double editorBaseFontSize, string sectionMarker)
    {
        ArgumentNullException.ThrowIfNull(editorDocument);
        var source = DocumentFlattener.IsFlat(editorDocument) ? editorDocument : DocumentFlattener.CloneFlattened(editorDocument);
        var paragraphs = _navigator.GetParagraphs(source);
        var texts = new string[paragraphs.Count];
        for (var i = 0; i < texts.Length; i++) texts[i] = _navigator.GetText(paragraphs[i]);

        var sections = _sections.Parse(texts, sectionMarker);
        var isSection = new HashSet<int>(sections.Count);
        foreach (var section in sections) isSection.Add(section.ParagraphIndex);

        var words = 0;
        for (var i = 0; i < texts.Length; i++)
        {
            if (!isSection.Contains(i)) words += CountWords(texts[i]);
        }

        return new PromptDocumentSnapshot(version, SaveXaml(source), editorBaseFontSize, texts.Length, sections, words);
    }

    public ScriptFile ToScriptFile(FlowDocument editorDocument, string title, double editorBaseFontSize, ScriptOverrides? overrides)
    {
        ArgumentNullException.ThrowIfNull(editorDocument);
        // Lists and tables survive in the file; only embedded UI (images, buttons) is removed, because it cannot be
        // loaded back through the sanitiser.
        var source = DocumentFlattener.HasEmbeddedUi(editorDocument) ? DocumentFlattener.CloneFlattened(editorDocument) : editorDocument;
        return new ScriptFile
        {
            Title = title,
            DocumentXaml = SaveXaml(source),
            EditorBaseFontSize = editorBaseFontSize,
            Overrides = overrides,
        };
    }

    public void LoadInto(FlowDocument target, string documentXaml)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(documentXaml))
        {
            target.Blocks.Clear();
            target.Blocks.Add(new Paragraph());
            return;
        }

        // File content: always through the sanitiser (which rebuilds the XAML) before TextRange.Load.
        // It runs first, so a rejected script leaves the target untouched.
        var clean = XamlSanitizer.Sanitize(documentXaml);
        target.Blocks.Clear();
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(clean));
            new TextRange(target.ContentStart, target.ContentEnd).Load(stream, DataFormats.Xaml);
        }
        catch (Exception ex) when (ex is System.Windows.Markup.XamlParseException or XmlException or ArgumentException
                                       or InvalidOperationException or NotSupportedException or IOException)
        {
            target.Blocks.Clear();
            target.Blocks.Add(new Paragraph());
            throw new ScriptFileException("The script's formatting data could not be read.", ex);
        }

        if (target.Blocks.Count == 0) target.Blocks.Add(new Paragraph());
    }

    internal static string SaveXaml(FlowDocument document)
    {
        using var stream = new MemoryStream();
        new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.Xaml);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch)) inWord = false;
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }
}
