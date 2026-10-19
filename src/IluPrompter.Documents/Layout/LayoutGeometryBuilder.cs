using System.Windows.Documents;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). One "line" per paragraph, no END detection.</summary>
public sealed class LayoutGeometryBuilder : ILayoutGeometryBuilder
{
    private readonly IDocumentNavigator _navigator;

    public LayoutGeometryBuilder(IDocumentNavigator navigator) => _navigator = navigator;

    public LayoutGeometry Build(FlowDocument laidOutDocument, GeometryBuildOptions options)
    {
        var paragraphs = _navigator.GetParagraphs(laidOutDocument);
        var tops = new double[paragraphs.Count];
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var rect = paragraphs[i].ContentStart.GetCharacterRect(LogicalDirection.Forward);
            tops[i] = rect.IsEmpty ? i * options.BaseLineHeight : rect.Top;
        }

        var heights = Enumerable.Repeat(options.BaseLineHeight, tops.Length).ToArray();
        var lineParagraphs = Enumerable.Range(0, tops.Length).ToArray();
        var end = tops.Length == 0 ? 0 : tops[^1] + options.BaseLineHeight;
        var sections = options.Snapshot.Sections
            .Where(s => s.ParagraphIndex < tops.Length)
            .Select(s => new SectionMarker(s.Index, s.Name, s.ParagraphIndex, tops[s.ParagraphIndex]))
            .ToList();
        return new LayoutGeometry(options.LayoutKey, options.Snapshot.Version, options.BaseLineHeight, end, end,
            tops, heights, lineParagraphs, new int[tops.Length], (double[])tops.Clone(), sections, options.Snapshot.WordCount);
    }
}
