using IluPrompter.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using IluPrompter.Theme;

namespace IluPrompter.Documents.Tests;

/// <summary>The geometry builder's contract allows a read-only RichTextBox or a FlowDocumentScrollViewer as the host.</summary>
public class HostingTests
{
    private static (FlowDocument Output, PromptDocumentSnapshot Snapshot) Output()
    {
        var editor = new FlowDocument();
        string[] words = ["welcome", "everyone", "to", "the", "evening", "session", "please", "take", "your", "seats"];
        for (var p = 0; p < 12; p++)
            editor.Blocks.Add(new Paragraph(new Run(string.Join(' ', Enumerable.Range(0, 60).Select(i => words[(p + (i * 7)) % words.Length])))));
        var snapshot = T.Snapshots.Build(editor, 1, 20, "##");
        var output = new OutputDocumentFactory(T.Snapshots).Create(snapshot, Styles.Output(40, false, 1.3, family: ThemeResources.UiFont));
        return (output, snapshot);
    }

    [WpfFact]
    public void FlowDocumentScrollViewer_GivesTheSameKindOfGeometry()
    {
        var (output, snapshot) = Output();
        var viewer = new FlowDocumentScrollViewer
        {
            Document = output,
            Width = 900,
            Height = 600,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
        };
        var window = new Window { Left = -10000, Width = 940, Height = 640, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Content = viewer };
        window.Show();
        viewer.UpdateLayout();
        try
        {
            var g = new LayoutGeometryBuilder(T.Navigator).Build(output, new GeometryBuildOptions("fdsv", snapshot, 52));

            Assert.Equal(12, g.ParagraphCount);
            Assert.True(g.LineCount > 12 * 3, $"{g.LineCount} lines: is the document laid out in several columns?");
            Assert.Equal(0, g.LineTops[0], 1.0);
            for (var i = 1; i < g.LineCount; i++) Assert.True(g.LineTops[i] > g.LineTops[i - 1]);
            Assert.True(g.EndTop > g.LineTops[^1]);
        }
        finally
        {
            window.Close();
        }
    }
}
