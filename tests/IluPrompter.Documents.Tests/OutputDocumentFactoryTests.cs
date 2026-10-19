using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Core;
using IluPrompter.Theme;

namespace IluPrompter.Documents.Tests;

internal static class Styles
{
    public static OutputTextStyle Output(double baseSize = 100, bool showSections = false, double spacing = 1.3, double gap = 0.5, FontFamily? family = null)
    {
        var highlightInks = ThemeResources.Highlights.ToDictionary(
            h => ThemeResources.GetColor(h.Token),
            h => ThemeResources.GetColor(h.InkToken!));
        return new OutputTextStyle(
            family ?? new FontFamily("Segoe UI"),
            FontWeights.Medium,
            baseSize,
            spacing,
            gap,
            TextAlignment.Left,
            ThemeResources.GetColor("ink-white"),
            ThemeResources.GetColor("ink-grey"),
            ThemeResources.GetColor("end-marker"),
            ThemeResources.GetColor("line-strong"),
            showSections,
            "##",
            highlightInks);
    }

    public static Brush Solid(string token) => new SolidColorBrush(ThemeResources.GetColor(token));
}

public class OutputDocumentFactoryTests
{
    private readonly OutputDocumentFactory _factory = new(T.Snapshots);

    private FlowDocument Create(FlowDocument editor, OutputTextStyle? style = null, double editorBase = 20)
    {
        var snapshot = T.Snapshots.Build(editor, 1, editorBase, "##");
        return _factory.Create(snapshot, style ?? Styles.Output());
    }

    [WpfFact]
    public void DocumentLevel_FollowsTheStyle()
    {
        var style = Styles.Output(baseSize: 90, spacing: 1.3);

        var doc = Create(T.Doc("one"), style);

        Assert.Equal(new Thickness(0), doc.PagePadding);
        Assert.Equal(90, doc.FontSize);
        Assert.Equal(FontWeights.Medium, doc.FontWeight);
        Assert.Equal(style.FontFamily, doc.FontFamily);
        Assert.Equal(117, doc.LineHeight, 6);
        Assert.Equal(LineStackingStrategy.BlockLineHeight, doc.LineStackingStrategy);
        Assert.Equal(TextAlignment.Left, doc.TextAlignment);
        Assert.Equal(ThemeResources.GetColor("ink-white"), ((SolidColorBrush)doc.Foreground).Color);
    }

    [WpfFact]
    public void LargeRun_AtEditorBase20_IsOnePointTwoFiveTimesTheOutputBase()
    {
        var editor = new FlowDocument { FontSize = 20 };
        var p = new Paragraph();
        p.Inlines.Add(new Run("normal "));
        p.Inlines.Add(new Run("small ") { FontSize = 16 });
        p.Inlines.Add(new Run("large ") { FontSize = 25 });
        p.Inlines.Add(new Run("huge") { FontSize = 30 });
        editor.Blocks.Add(p);

        var doc = Create(editor, Styles.Output(baseSize: 92));

        Assert.Equal(92 * 1.25, T.RunWithText(doc, "large").FontSize, 6);
        Assert.Equal(92 * 0.8, T.RunWithText(doc, "small").FontSize, 6);
        Assert.Equal(92 * 1.5, T.RunWithText(doc, "huge").FontSize, 6);
        Assert.Equal(92, T.RunWithText(doc, "normal").FontSize, 6);
    }

    [WpfFact]
    public void Scaling_UsesTheSnapshotsEditorBase_NotTwenty()
    {
        var editor = new FlowDocument { FontSize = 40 };
        var p = new Paragraph(new Run("big") { FontSize = 50 });
        editor.Blocks.Add(p);

        var doc = Create(editor, Styles.Output(baseSize: 80), editorBase: 40);

        Assert.Equal(100, T.RunWithText(doc, "big").FontSize, 6);
    }

    [WpfFact]
    public void Highlight_TakesItsOwnInk_WhateverTheTextColour_AndOtherTextIsLeftAlone()
    {
        var yellow = ThemeResources.GetColor("highlight-yellow");
        var blue = ThemeResources.GetColor("highlight-blue");
        var editor = new FlowDocument();
        var p = new Paragraph();
        p.Inlines.Add(new Run("marker") { Background = new SolidColorBrush(yellow), Foreground = Styles.Solid("ink-red") });
        p.Inlines.Add(new Run(" plain "));
        p.Inlines.Add(new Run("blue") { Background = new SolidColorBrush(blue) });
        p.Inlines.Add(new Run(" red") { Foreground = Styles.Solid("ink-red") });
        var span = new Span { Background = new SolidColorBrush(blue) };
        span.Inlines.Add(new Run(" nested"));
        p.Inlines.Add(span);
        p.Inlines.Add(new Run(" odd") { Foreground = new SolidColorBrush(Color.FromRgb(1, 2, 3)) });
        editor.Blocks.Add(p);

        var doc = Create(editor);

        Assert.Equal(ThemeResources.GetColor("canvas"), ((SolidColorBrush)T.RunWithText(doc, "marker").Foreground).Color);
        Assert.Equal(ThemeResources.GetColor("ink-white"), ((SolidColorBrush)T.RunWithText(doc, "blue").Foreground).Color);
        Assert.Equal(ThemeResources.GetColor("ink-white"), ((SolidColorBrush)T.RunWithText(doc, "nested").Foreground).Color);
        Assert.Equal(ThemeResources.GetColor("ink-red"), ((SolidColorBrush)T.RunWithText(doc, " red").Foreground).Color);
        Assert.Equal(Color.FromRgb(1, 2, 3), ((SolidColorBrush)T.RunWithText(doc, " odd").Foreground).Color); // unknown colours keep their value
        Assert.Equal(ThemeResources.GetColor("highlight-yellow"), ((SolidColorBrush)T.RunWithText(doc, "marker").Background).Color);
    }

    [WpfFact]
    public void Weights_AtMostMediumInherit_SemiBoldAndUpAreBold_AndFontFamiliesAreDropped()
    {
        var editor = new FlowDocument();
        var p = new Paragraph();
        p.Inlines.Add(new Run("light ") { FontWeight = FontWeights.Light, FontFamily = new FontFamily("Comic Sans MS") });
        p.Inlines.Add(new Run("medium ") { FontWeight = FontWeights.Medium });
        p.Inlines.Add(new Run("semi ") { FontWeight = FontWeights.SemiBold });
        p.Inlines.Add(new Run("bold") { FontWeight = FontWeights.Bold });
        editor.Blocks.Add(p);

        var doc = Create(editor);

        Assert.Equal(DependencyProperty.UnsetValue, T.RunWithText(doc, "light").ReadLocalValue(TextElement.FontWeightProperty));
        Assert.Equal(DependencyProperty.UnsetValue, T.RunWithText(doc, "light").ReadLocalValue(TextElement.FontFamilyProperty));
        Assert.Equal(DependencyProperty.UnsetValue, T.RunWithText(doc, "medium").ReadLocalValue(TextElement.FontWeightProperty));
        Assert.Equal(FontWeights.Bold, T.RunWithText(doc, "semi").FontWeight);
        Assert.Equal(FontWeights.Bold, T.RunWithText(doc, "bold").FontWeight);
    }

    [WpfFact]
    public void UnderlineAndStrikethrough_BecomeSixHundredthsEmDecorationsInTheRunInk()
    {
        var editor = new FlowDocument();
        var p = new Paragraph();
        p.Inlines.Add(new Run("under ") { TextDecorations = TextDecorations.Underline, Foreground = Styles.Solid("ink-cyan") });
        p.Inlines.Add(new Run("strike ") { TextDecorations = TextDecorations.Strikethrough });
        var span = new Span { TextDecorations = TextDecorations.Underline };
        span.Inlines.Add(new Run("both") { TextDecorations = TextDecorations.Strikethrough, Foreground = Styles.Solid("ink-pink") });
        p.Inlines.Add(span);
        p.Inlines.Add(new Run(" none"));
        editor.Blocks.Add(p);

        var doc = Create(editor);
        var under = T.RunWithText(doc, "under").TextDecorations;
        var strike = T.RunWithText(doc, "strike").TextDecorations;
        var both = T.RunWithText(doc, "both").TextDecorations;

        var u = Assert.Single(under);
        Assert.Equal(TextDecorationLocation.Underline, u.Location);
        Assert.Equal(TextDecorationUnit.FontRenderingEmSize, u.PenThicknessUnit);
        Assert.Equal(0.06, u.Pen.Thickness, 6);
        Assert.Equal(ThemeResources.GetColor("ink-cyan"), ((SolidColorBrush)u.Pen.Brush).Color);
        var s = Assert.Single(strike);
        Assert.Equal(TextDecorationLocation.Strikethrough, s.Location);
        Assert.Equal(ThemeResources.GetColor("ink-white"), ((SolidColorBrush)s.Pen.Brush).Color);
        Assert.Equal(2, both.Count);
        Assert.All(both, d => Assert.Equal(ThemeResources.GetColor("ink-pink"), ((SolidColorBrush)d.Pen.Brush).Color));
        Assert.Empty(T.RunWithText(doc, "none").TextDecorations);
        var outputSpan = T.Navigator.GetParagraphs(doc)[0].Inlines.OfType<Span>().Single();
        Assert.Empty(outputSpan.TextDecorations); // the span itself no longer draws them
    }

    [WpfFact]
    public void ParagraphGap_IsGapLinesOfLineHeight_AndEditorOnlyParagraphLookIsRemoved()
    {
        var editor = T.Doc("a", "b");
        var styled = (Paragraph)editor.Blocks.FirstBlock;
        styled.BorderBrush = Brushes.Red;
        styled.BorderThickness = new Thickness(0, 0, 0, 1);
        styled.Padding = new Thickness(5);
        styled.Margin = new Thickness(40);

        var doc = Create(editor, Styles.Output(baseSize: 100, spacing: 1.3, gap: 0.5));

        var ps = T.Navigator.GetParagraphs(doc);
        Assert.All(ps, p => Assert.Equal(new Thickness(0, 0, 0, 65), p.Margin));
        Assert.Equal(new Thickness(0), ps[0].BorderThickness);
        Assert.Equal(new Thickness(0), ps[0].Padding);
    }

    [WpfFact]
    public void HostDocumentProperties_AreNotCopiedOntoParagraphs()
    {
        var editor = new FlowDocument { FontSize = 20, FontFamily = new FontFamily("Arial"), Foreground = Brushes.White };
        editor.Blocks.Add(new Paragraph(new Run("text")));

        var doc = Create(editor);

        var p = T.Navigator.GetParagraphs(doc)[0];
        Assert.Equal(DependencyProperty.UnsetValue, p.ReadLocalValue(TextElement.ForegroundProperty));
        Assert.Equal(DependencyProperty.UnsetValue, p.ReadLocalValue(TextElement.FontFamilyProperty));
        Assert.Equal(DependencyProperty.UnsetValue, p.ReadLocalValue(TextElement.FontSizeProperty));
        Assert.Equal(100, p.FontSize);
    }

    [WpfFact]
    public void Sections_Hidden_KeepTheirIndexAndShrinkToHalfALine()
    {
        var editor = T.Doc("intro", "## Welcome", "body", "### Wrap up", "bye");
        var style = Styles.Output(baseSize: 100, showSections: false);

        var doc = Create(editor, style);

        var ps = T.Navigator.GetParagraphs(doc);
        Assert.Equal(5, ps.Count);
        Assert.Equal("", T.Text(ps[1]));
        Assert.Equal("", T.Text(ps[3]));
        Assert.Equal(65, ps[1].LineHeight, 6); // half of 130
        Assert.Equal(new Thickness(0), ps[1].Margin);
        Assert.Equal("intro", T.Text(ps[0]));
        Assert.Equal("body", T.Text(ps[2]));
    }

    [WpfFact]
    public void Sections_Shown_AreCapitalsAtFortyPercentInTheSectionInk_WithoutTheMarker()
    {
        var editor = T.Doc("intro", "## Welcome", "body", "##");
        var style = Styles.Output(baseSize: 100, showSections: true);

        var doc = Create(editor, style);

        var ps = T.Navigator.GetParagraphs(doc);
        Assert.Equal("WELCOME", T.Text(ps[1]));
        Assert.Equal("SECTION 2", T.Text(ps[3])); // unnamed sections are numbered
        var run = T.Runs(doc).First(r => r.Text == "WELCOME");
        Assert.Equal(40, run.FontSize, 6);
        Assert.Equal(ThemeResources.GetColor("ink-grey"), ((SolidColorBrush)run.Foreground).Color);
        Assert.Equal(52, ps[1].LineHeight, 6); // 0.4 x size x spacing
    }

    [WpfFact]
    public void EndBlock_IsAppended_TaggedAndSkippedByTheNavigator()
    {
        var doc = Create(T.Doc("one", "two"));

        var last = doc.Blocks.LastBlock;
        var end = Assert.IsType<BlockUIContainer>(last);
        Assert.Equal(DocumentNavigator.GeneratedTag, end.Tag);
        Assert.Equal(["one", "two"], T.Texts(doc));
        var grid = Assert.IsType<Grid>(end.Child);
        var word = grid.Children.OfType<TextBlock>().Single();
        Assert.Equal("END", word.Text);
        Assert.Equal(60, word.FontSize, 6);
        Assert.Equal(ThemeResources.GetColor("end-marker"), ((SolidColorBrush)word.Foreground).Color);
        var rules = grid.Children.OfType<Border>().ToList();
        Assert.Equal(2, rules.Count);
        Assert.All(rules, r =>
        {
            Assert.Equal(2, r.Height);
            Assert.Equal(ThemeResources.GetColor("line-strong"), ((SolidColorBrush)r.Background).Color);
        });
        Assert.Equal(new Thickness(0, 130, 0, 0), end.Margin); // one blank line before END
    }

    [WpfFact]
    public void ListsInTheSnapshotXaml_AreFlattenedSoIndicesStayCanonical()
    {
        var xaml = "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xml:space=\"preserve\">" +
                   "<Paragraph><Run>a</Run></Paragraph><List MarkerStyle=\"Disc\"><ListItem><Paragraph><Run>b</Run></Paragraph></ListItem></List></Section>";
        var snapshot = new PromptDocumentSnapshot(1, xaml, 20, 2, [], 2);

        var doc = _factory.Create(snapshot, Styles.Output());

        Assert.Equal(["a", "• b"], T.Texts(doc));
    }

    [WpfFact]
    public void EmptySnapshot_StillProducesOneParagraphAndEnd()
    {
        var doc = _factory.Create(PromptDocumentSnapshot.Empty, Styles.Output());

        Assert.Equal([""], T.Texts(doc));
        Assert.IsType<BlockUIContainer>(doc.Blocks.LastBlock);
    }

    [WpfFact]
    public void MaliciousSnapshotXaml_IsRejected()
    {
        var snapshot = new PromptDocumentSnapshot(1, "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Button/></Section>", 20, 0, [], 0);

        Assert.Throws<ScriptFileException>(() => _factory.Create(snapshot, Styles.Output()));
    }
}
