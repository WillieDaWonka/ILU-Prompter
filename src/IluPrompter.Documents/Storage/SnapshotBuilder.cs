using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). No flattening of lists/tables.</summary>
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
        var texts = _navigator.GetParagraphs(editorDocument).Select(_navigator.GetText).ToList();
        var words = texts.Sum(t => t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length);
        return new PromptDocumentSnapshot(version, SaveXaml(editorDocument), editorBaseFontSize, texts.Count, _sections.Parse(texts, sectionMarker), words);
    }

    public ScriptFile ToScriptFile(FlowDocument editorDocument, string title, double editorBaseFontSize, ScriptOverrides? overrides) =>
        new() { Title = title, DocumentXaml = SaveXaml(editorDocument), EditorBaseFontSize = editorBaseFontSize, Overrides = overrides };

    public void LoadInto(FlowDocument target, string documentXaml)
    {
        target.Blocks.Clear();
        if (string.IsNullOrEmpty(documentXaml))
        {
            target.Blocks.Add(new Paragraph());
            return;
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(documentXaml));
        new TextRange(target.ContentStart, target.ContentEnd).Load(stream, DataFormats.Xaml);
    }

    internal static string SaveXaml(FlowDocument document)
    {
        using var stream = new MemoryStream();
        new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.Xaml);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
