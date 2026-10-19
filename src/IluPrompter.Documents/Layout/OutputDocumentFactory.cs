using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). Loads the XAML and applies base font/ink only; no size scaling,
/// no section restyling, no END block.</summary>
public sealed class OutputDocumentFactory : IOutputDocumentFactory
{
    private readonly ISnapshotBuilder _snapshots;

    public OutputDocumentFactory(ISnapshotBuilder snapshots) => _snapshots = snapshots;

    public FlowDocument Create(PromptDocumentSnapshot snapshot, OutputTextStyle style)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = style.FontFamily,
            FontWeight = style.FontWeight,
            FontSize = style.BaseFontSize,
            Foreground = new SolidColorBrush(style.Ink),
            Background = Brushes.Transparent,
            LineHeight = style.BaseFontSize * style.LineSpacing,
            LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            TextAlignment = style.Alignment,
        };
        _snapshots.LoadInto(doc, snapshot.FlowDocumentXaml);
        return doc;
    }
}
