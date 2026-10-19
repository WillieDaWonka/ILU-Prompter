using System.IO;
using System.Windows.Documents;
using IluPrompter.Theme;

namespace IluPrompter.Documents.Tests;

public class SamplesTests
{
    private readonly ScriptFileService _service = new();

    private static string Sample(string name) => Path.Combine(T.RepoRoot(), "samples", name);

    [WpfFact]
    public void ShortSample_HasThreeSections()
    {
        var file = _service.Load(Sample("sample-short.txt"));
        var snapshot = T.Snapshots.Build(T.Load(file.DocumentXaml), 1, file.EditorBaseFontSize, "##");

        Assert.Equal(["Welcome", "What is happening tonight", "Thank you"], snapshot.Sections.Select(s => s.Name));
        Assert.InRange(snapshot.WordCount, 100, 400);
    }

    [WpfFact]
    public void TwentyPageSample_IsAboutSixThousandWords_InTenSections()
    {
        var file = _service.Load(Sample("sample-20-pages.txt"));
        var snapshot = T.Snapshots.Build(T.Load(file.DocumentXaml), 1, file.EditorBaseFontSize, "##");

        Assert.Equal(10, snapshot.Sections.Count);
        Assert.InRange(snapshot.WordCount, 5500, 6500);
    }

    [WpfFact]
    public void TwentyPageSample_RunsThroughTheWholePipeline_ToALayoutWithTenSectionsInOrder()
    {
        var file = _service.Load(Sample("sample-20-pages.txt"));
        var editor = T.Load(file.DocumentXaml);
        var snapshot = T.Snapshots.Build(editor, 3, file.EditorBaseFontSize, "##");
        var style = Styles.Output(baseSize: 92, spacing: 1.3, family: ThemeResources.UiFont);
        var output = new OutputDocumentFactory(T.Snapshots).Create(snapshot, style);
        var (window, _) = T.HostOffscreen(output, 1600, 1000);
        try
        {
            var geometry = new LayoutGeometryBuilder(T.Navigator).Build(output, new GeometryBuildOptions("sample", snapshot, 92 * 1.3));

            Assert.Equal(10, geometry.Sections.Count);
            Assert.True(geometry.Sections.Select(s => s.Top).SequenceEqual(geometry.Sections.Select(s => s.Top).OrderBy(t => t)));
            Assert.True(geometry.LineCount > 500, $"{geometry.LineCount} lines");
            Assert.True(geometry.EndTop > geometry.LineTops[^1]);
            Assert.Equal(snapshot.ParagraphCount, geometry.ParagraphCount);
            Assert.Equal(snapshot.WordCount, geometry.WordCount);
        }
        finally
        {
            window.Close();
        }
    }
}
