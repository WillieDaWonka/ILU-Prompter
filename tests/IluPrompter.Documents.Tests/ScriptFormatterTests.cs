using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Theme;

namespace IluPrompter.Documents.Tests;

public class ScriptFormatterTests
{
    private readonly ScriptFormatter _formatter = new();

    private static (FlowDocument Doc, TextRange All) Plain(string text)
    {
        var doc = T.Doc(text);
        return (doc, new TextRange(doc.ContentStart, doc.ContentEnd));
    }

    private static TextRange Range(FlowDocument doc, string text)
    {
        var run = T.RunWithText(doc, text);
        var start = run.ContentStart.GetPositionAtOffset(run.Text.IndexOf(text, StringComparison.Ordinal));
        return new TextRange(start, start.GetPositionAtOffset(text.Length));
    }

    [Theory]
    [InlineData(ScriptSize.Small, 0.8)]
    [InlineData(ScriptSize.Normal, 1.0)]
    [InlineData(ScriptSize.Large, 1.25)]
    [InlineData(ScriptSize.Huge, 1.5)]
    public void Factor_IsTheDesignScale(ScriptSize size, double factor) => Assert.Equal(factor, _formatter.Factor(size));

    [WpfFact]
    public void ApplySize_WritesEditorBaseTimesFactor_AndGetSizeReadsItBack()
    {
        foreach (var size in Enum.GetValues<ScriptSize>())
        {
            var (_, all) = Plain("some text");

            _formatter.ApplySize(all, size, 22);

            Assert.Equal(22 * _formatter.Factor(size), (double)all.GetPropertyValue(TextElement.FontSizeProperty), 6);
            Assert.Equal(size, _formatter.GetSize(all, 22));
        }
    }

    [WpfFact]
    public void GetSize_MapsToTheNearestFactor_AndIsNullWhenMixed()
    {
        var doc = new FlowDocument { FontSize = 20 };
        var p = new Paragraph();
        p.Inlines.Add(new Run("aaa ") { FontSize = 16.5 });
        p.Inlines.Add(new Run("bbb ") { FontSize = 24 });
        p.Inlines.Add(new Run("ccc") { FontSize = 31 });
        doc.Blocks.Add(p);

        Assert.Equal(ScriptSize.Small, _formatter.GetSize(Range(doc, "aaa"), 20));
        Assert.Equal(ScriptSize.Large, _formatter.GetSize(Range(doc, "bbb"), 20));
        Assert.Equal(ScriptSize.Huge, _formatter.GetSize(Range(doc, "ccc"), 20));
        Assert.Null(_formatter.GetSize(new TextRange(doc.ContentStart, doc.ContentEnd), 20));
    }

    [WpfFact]
    public void GetSize_OnUnformattedText_IsNormal()
    {
        var doc = T.Doc("plain");
        doc.FontSize = 20;

        Assert.Equal(ScriptSize.Normal, _formatter.GetSize(new TextRange(doc.ContentStart, doc.ContentEnd), 20));
    }

    [WpfFact]
    public void ToggleStrikethrough_TurnsOnAndOff()
    {
        var (_, all) = Plain("strike me");

        Assert.False(_formatter.GetStrikethrough(all));
        _formatter.ToggleStrikethrough(all);
        Assert.True(_formatter.GetStrikethrough(all));
        _formatter.ToggleStrikethrough(all);
        Assert.False(_formatter.GetStrikethrough(all));
    }

    [WpfFact]
    public void ToggleStrikethrough_OnAMixedRange_SwitchesOnEverywhere_AndKeepsUnderlines()
    {
        var doc = new FlowDocument();
        var p = new Paragraph();
        p.Inlines.Add(new Run("plain "));
        p.Inlines.Add(new Run("under ") { TextDecorations = TextDecorations.Underline });
        p.Inlines.Add(new Run("tail"));
        doc.Blocks.Add(p);
        var all = new TextRange(doc.ContentStart, doc.ContentEnd);
        Assert.False(_formatter.GetStrikethrough(all));
        _formatter.ToggleStrikethrough(Range(doc, "plain"));
        Assert.Null(_formatter.GetStrikethrough(all)); // struck in one run only: mixed

        _formatter.ToggleStrikethrough(all);

        Assert.True(_formatter.GetStrikethrough(all));
        var under = T.RunWithText(doc, "under").TextDecorations;
        Assert.Contains(under, d => d.Location == TextDecorationLocation.Underline);
        Assert.Contains(under, d => d.Location == TextDecorationLocation.Strikethrough);

        _formatter.ToggleStrikethrough(all);

        Assert.False(_formatter.GetStrikethrough(all));
        Assert.Contains(T.RunWithText(doc, "under").TextDecorations, d => d.Location == TextDecorationLocation.Underline);
        Assert.Empty(T.RunWithText(doc, "plain").TextDecorations);
    }

    [WpfFact]
    public void ApplyInk_SetsAndRemovesTheExplicitColour()
    {
        var (doc, all) = Plain("coloured");
        var red = ThemeResources.GetColor("ink-red");

        _formatter.ApplyInk(all, red);
        Assert.Equal(red, ((SolidColorBrush)all.GetPropertyValue(TextElement.ForegroundProperty)).Color);

        _formatter.ApplyInk(all, null);
        Assert.Equal(DependencyProperty.UnsetValue, T.Runs(doc).First().ReadLocalValue(TextElement.ForegroundProperty));
    }

    [WpfFact]
    public void ApplyHighlight_CarriesItsInk_AndRemovingItRestoresTheDefaultInk()
    {
        var (doc, all) = Plain("marked");
        var yellow = ThemeResources.GetColor("highlight-yellow");
        var canvas = ThemeResources.GetColor("canvas");

        _formatter.ApplyHighlight(all, yellow, canvas);
        var run = T.Runs(doc).First();
        Assert.Equal(yellow, ((SolidColorBrush)run.Background).Color);
        Assert.Equal(canvas, ((SolidColorBrush)run.Foreground).Color);

        _formatter.ApplyHighlight(all, null, null);
        run = T.Runs(doc).First();
        Assert.Equal(DependencyProperty.UnsetValue, run.ReadLocalValue(TextElement.BackgroundProperty));
        Assert.Equal(DependencyProperty.UnsetValue, run.ReadLocalValue(TextElement.ForegroundProperty));
    }

    [WpfFact]
    public void RemovingAHighlight_KeepsAnInkTheAuthorChose()
    {
        var (doc, all) = Plain("marked");
        var blue = ThemeResources.GetColor("highlight-blue");
        _formatter.ApplyHighlight(all, blue, ThemeResources.GetColor("ink-white"));
        _formatter.ApplyInk(all, ThemeResources.GetColor("ink-cyan")); // the author then picks cyan text

        _formatter.ApplyHighlight(all, null, null);

        Assert.Equal(ThemeResources.GetColor("ink-cyan"), ((SolidColorBrush)T.Runs(doc).First().Foreground).Color);
    }

    [WpfFact]
    public void ClearFormatting_RemovesSizeInkHighlightWeightStyleAndDecorations()
    {
        var (doc, all) = Plain("busy");
        _formatter.ApplySize(all, ScriptSize.Huge, 20);
        _formatter.ApplyInk(all, ThemeResources.GetColor("ink-red"));
        _formatter.ApplyHighlight(all, ThemeResources.GetColor("highlight-green"), ThemeResources.GetColor("ink-white"));
        _formatter.ToggleStrikethrough(all);
        all.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
        all.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
        doc.FontSize = 20;

        _formatter.ClearFormatting(all);

        var run = T.Runs(doc).First();
        foreach (var property in new[] { TextElement.FontSizeProperty, TextElement.ForegroundProperty, TextElement.BackgroundProperty, TextElement.FontWeightProperty, TextElement.FontStyleProperty, Inline.TextDecorationsProperty })
            Assert.Equal(DependencyProperty.UnsetValue, run.ReadLocalValue(property));
        Assert.Equal(ScriptSize.Normal, _formatter.GetSize(all, 20));
    }

    // ---- sections -----------------------------------------------------------------------------------------

    private static (RichTextBox Editor, Window Window) Editor(params string[] paragraphs)
    {
        var editor = new RichTextBox { Document = T.Doc(paragraphs), Width = 400, Height = 300 };
        var window = new Window { Left = -10000, Width = 440, Height = 340, ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Content = editor };
        window.Show();
        editor.UpdateLayout();
        return (editor, window);
    }

    [WpfFact]
    public void InsertSectionMarker_AtTheStartOfAParagraph_GoesBeforeIt_AndSelectsTheName()
    {
        var (editor, window) = Editor("first", "second");
        try
        {
            editor.CaretPosition = T.Navigator.GetPointer(editor.Document, new IluPrompter.Core.DocumentAnchor(1, 0));

            var marker = _formatter.InsertSectionMarker(editor, "##", "Sponsors");

            Assert.Equal(["first", "## Sponsors", "second"], T.Texts(editor.Document));
            Assert.Same(T.Navigator.GetParagraphs(editor.Document)[1], marker);
            Assert.Equal("Sponsors", editor.Selection.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void InsertSectionMarker_InTheMiddle_SplitsTheParagraph()
    {
        var (editor, window) = Editor("before after");
        try
        {
            editor.CaretPosition = T.Navigator.GetPointer(editor.Document, new IluPrompter.Core.DocumentAnchor(0, 7));

            _formatter.InsertSectionMarker(editor, "##", "Middle");

            Assert.Equal(["before ", "## Middle", "after"], T.Texts(editor.Document));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void InsertSectionMarker_AtTheEnd_GoesAfterTheParagraph()
    {
        var (editor, window) = Editor("one", "two");
        try
        {
            editor.CaretPosition = T.Navigator.GetPointer(editor.Document, new IluPrompter.Core.DocumentAnchor(0, 3));

            _formatter.InsertSectionMarker(editor, "##", "After one");

            Assert.Equal(["one", "## After one", "two"], T.Texts(editor.Document));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void InsertSectionMarker_WithNoName_LeavesTheCaretAfterTheMarker_AndIsOneUndoStep()
    {
        var (editor, window) = Editor("only");
        try
        {
            editor.CaretPosition = T.Navigator.GetPointer(editor.Document, new IluPrompter.Core.DocumentAnchor(0, 0));

            var marker = _formatter.InsertSectionMarker(editor, "##", "");

            Assert.Equal("## ", T.Text(marker));
            Assert.True(editor.Selection.IsEmpty);
            editor.Undo();
            Assert.Equal(["only"], T.Texts(editor.Document));
        }
        finally
        {
            window.Close();
        }
    }

    [WpfFact]
    public void StyleSectionParagraphs_StylesMarkers_IsIdempotent_AndUnstylesEditedOnes()
    {
        var doc = T.Doc("intro", "## One", "text", "### Two");
        var label = Brushes.Gray;
        var rule = Brushes.DimGray;

        _formatter.StyleSectionParagraphs(doc, "##", label, rule);
        var ps = T.Navigator.GetParagraphs(doc);
        var xamlAfterFirst = T.Xaml(doc);
        _formatter.StyleSectionParagraphs(doc, "##", label, rule);

        Assert.Equal(new Thickness(0, 0, 0, 1), ps[1].BorderThickness);
        Assert.Equal(new Thickness(0, 0, 0, 1), ps[3].BorderThickness);
        Assert.Same(label, ps[1].Foreground);
        Assert.Equal(new Thickness(0), ps[0].BorderThickness);
        Assert.Equal(new Thickness(0), ps[2].BorderThickness);
        Assert.Equal(xamlAfterFirst, T.Xaml(doc));

        // The author turns "## One" into ordinary text.
        new TextRange(ps[1].ContentStart, ps[1].ContentEnd).Text = "One";
        _formatter.StyleSectionParagraphs(doc, "##", label, rule);

        Assert.Equal(new Thickness(0), ps[1].BorderThickness);
        Assert.Equal(DependencyProperty.UnsetValue, ps[1].ReadLocalValue(TextElement.ForegroundProperty));
        Assert.Equal(new Thickness(0, 0, 0, 1), ps[3].BorderThickness);
    }

    [WpfFact]
    public void SectionStyling_NeverReachesTheOutput()
    {
        var doc = T.Doc("intro", "## One", "body");
        _formatter.StyleSectionParagraphs(doc, "##", Brushes.Gray, Brushes.DimGray);
        var snapshot = T.Snapshots.Build(doc, 1, 20, "##");

        var output = new OutputDocumentFactory(T.Snapshots).Create(snapshot, Styles.Output(showSections: false));

        var section = T.Navigator.GetParagraphs(output)[1];
        Assert.Equal(new Thickness(0), section.BorderThickness);
        Assert.Equal(new Thickness(0), section.Padding);
        Assert.Equal(DependencyProperty.UnsetValue, section.ReadLocalValue(TextElement.ForegroundProperty));
        Assert.Equal(65, section.LineHeight, 6);
    }
}
