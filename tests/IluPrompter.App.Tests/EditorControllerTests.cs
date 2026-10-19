using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using IluPrompter.App.Editing;
using IluPrompter.App.ViewModels;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.App.Tests;

/// <summary>Integration: the real editor controller with the real snapshot builder and formatter (streams D + E).</summary>
public class EditorControllerTests
{
    private sealed class Rig
    {
        public Rig()
        {
            var navigator = new DocumentNavigator();
            Snapshots = new SnapshotBuilder(navigator, new SectionParser());
            var log = new FileAppLog(false, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ilu-app-tests-logs"));
            var services = new EditorServices(Snapshots, Documents, Settings, new ScriptFormatter(), navigator, new ScriptFileService(),
                new KeyModeState(), new FakeEngine(), log);
            Controller = new EditorController(Editor, new EditorToolbarViewModel(), services);
        }

        public RichTextBox Editor { get; } = new();
        public PromptDocumentService Documents { get; } = new();
        public FakeSettings Settings { get; } = new();
        public SnapshotBuilder Snapshots { get; }
        public EditorController Controller { get; }

        public void Load(params string[] paragraphs)
        {
            var doc = new FlowDocument();
            foreach (var text in paragraphs) doc.Blocks.Add(new Paragraph(new Run(text)));
            Controller.LoadScript(Snapshots.ToScriptFile(doc, "t", 20, null));
        }

        public Paragraph Paragraph(int index) => (Paragraph)Editor.Document.Blocks.ElementAt(index);

        /// <summary>The published snapshot loaded back into a document, as the output factory sees it.</summary>
        public Paragraph Published(int index)
        {
            var doc = new FlowDocument();
            Snapshots.LoadInto(doc, Documents.Current.FlowDocumentXaml);
            return (Paragraph)doc.Blocks.ElementAt(index);
        }
    }

    [WpfFact]
    public void AMarkerTurnedBackIntoText_IsPublishedWithoutTheSectionLabelLook()
    {
        var rig = new Rig();
        rig.Load("## Intro", "Body text");
        Assert.Single(rig.Documents.Current.Sections);
        Assert.NotEqual(DependencyProperty.UnsetValue, rig.Paragraph(0).ReadLocalValue(TextElement.FontSizeProperty)); // label look in the editor

        // Delete the "## ": the paragraph is ordinary text now.
        var marker = rig.Paragraph(0);
        marker.Inlines.Clear();
        marker.Inlines.Add(new Run("Intro"));
        rig.Controller.PublishNow();

        Assert.Empty(rig.Documents.Current.Sections);
        var published = rig.Published(0);
        Assert.Equal(DependencyProperty.UnsetValue, published.ReadLocalValue(TextElement.FontSizeProperty));
        Assert.Equal(DependencyProperty.UnsetValue, published.ReadLocalValue(TextElement.ForegroundProperty));
    }

    [WpfFact]
    public void ChangingTheSectionMarkerSetting_RepublishesWithTheNewSections()
    {
        var rig = new Rig();
        rig.Load("# One", "text", "## Two");
        Assert.Equal(["Two"], rig.Documents.Current.Sections.Select(s => s.Name));

        rig.Settings.Update(s => s with { Editor = s.Editor with { SectionMarker = "#" } });
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

        Assert.Equal(2, rig.Documents.Current.Sections.Count);
    }
}
