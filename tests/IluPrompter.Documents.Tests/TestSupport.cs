using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace IluPrompter.Documents.Tests;

internal static class T
{
    public static readonly DocumentNavigator Navigator = new();
    public static readonly SectionParser Parser = new();
    public static readonly SnapshotBuilder Snapshots = new(Navigator, Parser);

    public static FlowDocument Doc(params string[] paragraphs)
    {
        var doc = new FlowDocument();
        foreach (var text in paragraphs) doc.Blocks.Add(new Paragraph(new Run(text)));
        return doc;
    }

    public static string Text(Paragraph paragraph) => Navigator.GetText(paragraph);

    public static List<string> Texts(FlowDocument document) => Navigator.GetParagraphs(document).Select(Navigator.GetText).ToList();

    public static string Xaml(FlowDocument document) => SnapshotBuilder.SaveXaml(document);

    public static FlowDocument Load(string xaml)
    {
        var doc = new FlowDocument();
        Snapshots.LoadInto(doc, xaml);
        return doc;
    }

    public static IEnumerable<Run> Runs(FlowDocument document)
    {
        foreach (var paragraph in Navigator.GetParagraphs(document))
            foreach (var run in RunsIn(paragraph.Inlines)) yield return run;
    }

    public static IEnumerable<Run> RunsIn(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run run) yield return run;
            else if (inline is Span span)
                foreach (var inner in RunsIn(span.Inlines)) yield return inner;
        }
    }

    public static Run RunWithText(FlowDocument document, string text) => Runs(document).First(r => r.Text.Contains(text, StringComparison.Ordinal));

    public static string? ColourOf(Brush? brush) => brush is SolidColorBrush s ? s.Color.ToString() : null;

    /// <summary>Finds the repository root (the folder holding IluPrompter.sln).</summary>
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IluPrompter.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    public static string TempFile(string extension)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ilu-documents-tests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, Guid.NewGuid().ToString("N") + extension);
    }

    /// <summary>Hosts the document in a read-only RichTextBox inside an off-screen window and lays it out.</summary>
    public static (Window Window, RichTextBox Host) HostOffscreen(FlowDocument document, double width, double height = 600)
    {
        var host = new RichTextBox
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Width = width,
            Height = height,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Document = document,
        };
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Left = -10000,
            Top = 0,
            Width = width + 40,
            Height = height + 40,
            Content = host,
        };
        window.Show();
        host.UpdateLayout();
        return (window, host);
    }
}
