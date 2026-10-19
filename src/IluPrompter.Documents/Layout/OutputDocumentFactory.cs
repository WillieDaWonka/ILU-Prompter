using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>
/// Builds the read-only output/preview FlowDocument from a snapshot (ARCHITECTURE §2.11 / §4.E). Stateless; call on
/// the thread that will own the document, because every element and brush is created there.
/// <list type="bullet">
/// <item>PagePadding 0; face, weight, ink, line height (BlockLineHeight) and alignment come from the style.</item>
/// <item>Every explicit run/paragraph size is scaled by <c>style.BaseFontSize / snapshot.EditorBaseFontSize</c>, so
/// Small/Normal/Large/Huge stay 0.8/1/1.25/1.5 of the output base. Normal text inherits the base.</item>
/// <item>Weights at or below 500 inherit the style weight, weights from 600 are Bold (700). Explicit font families are
/// dropped (the output face is a setting). Underline and strikethrough become <see cref="TextDecoration"/>s of
/// thickness 0.06 em in the run's ink. Unknown ink colours keep their value.</item>
/// <item>Text on a known highlight takes that highlight's ink (<see cref="OutputTextStyle.HighlightInks"/>).</item>
/// <item>Section paragraphs keep their index but are restyled: the name at 0.4 x size in the section ink and capitals,
/// or, when names are hidden, an empty paragraph half a line high.</item>
/// <item>A generated END block (one blank line, then a centred END between two 2 px rules) is appended, tagged
/// <see cref="DocumentNavigator.GeneratedTag"/> so <see cref="IDocumentNavigator"/> skips it.</item>
/// </list>
/// </summary>
public sealed class OutputDocumentFactory : IOutputDocumentFactory
{
    private const double UnderlineThicknessEm = 0.06;
    private const double SectionSizeFactor = 0.4;
    private const double EndSizeFactor = 0.6;
    private const double HiddenSectionLines = 0.5;
    private const double DefaultEditorBase = 20.0;

    private readonly ISnapshotBuilder _snapshots;
    private readonly DocumentNavigator _navigator = new();

    public OutputDocumentFactory(ISnapshotBuilder snapshots) => _snapshots = snapshots;

    public FlowDocument Create(PromptDocumentSnapshot snapshot, OutputTextStyle style)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(style);

        // Load into a pristine document first so WPF has nothing to copy onto the paragraphs, then apply the style.
        var doc = new FlowDocument();
        _snapshots.LoadInto(doc, snapshot.FlowDocumentXaml);
        if (!DocumentFlattener.IsFlat(doc)) doc = DocumentFlattener.CloneFlattened(doc);

        var lineHeight = style.BaseFontSize * style.LineSpacing;
        var inkBrush = Frozen(style.Ink);
        doc.PagePadding = new Thickness(0);
        doc.FontFamily = style.FontFamily;
        doc.FontWeight = style.FontWeight;
        doc.FontSize = style.BaseFontSize;
        doc.Foreground = inkBrush;
        doc.Background = Brushes.Transparent;
        doc.LineHeight = lineHeight;
        doc.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        doc.TextAlignment = style.Alignment;
        doc.IsHyphenationEnabled = false;
        doc.IsOptimalParagraphEnabled = false;

        var editorBase = snapshot.EditorBaseFontSize > 0 ? snapshot.EditorBaseFontSize : DefaultEditorBase;
        var context = new StyleContext(style, style.BaseFontSize / editorBase, lineHeight, style.ParagraphGapLines * lineHeight);

        var names = new Dictionary<int, string>();
        foreach (var section in snapshot.Sections) names[section.ParagraphIndex] = section.Name;

        var paragraphs = _navigator.GetParagraphs(doc);
        for (var i = 0; i < paragraphs.Count; i++)
        {
            if (names.TryGetValue(i, out var name)) StyleSection(paragraphs[i], name, context);
            else StyleParagraph(paragraphs[i], context);
        }

        doc.Blocks.Add(CreateEndBlock(context));
        return doc;
    }

    // ---- paragraphs ------------------------------------------------------------------------------------------

    private static void StyleParagraph(Paragraph paragraph, StyleContext context)
    {
        ResetParagraph(paragraph);
        paragraph.Margin = new Thickness(0, 0, 0, context.GapPixels);
        var state = Inherit(paragraph, default, context);
        StyleInlines(paragraph.Inlines, state, context);
    }

    /// <summary>Paragraph-level look from the editor (section rule, padding, fixed line height...) never reaches the output.</summary>
    private static void ResetParagraph(Paragraph paragraph)
    {
        paragraph.ClearValue(Block.BorderBrushProperty);
        paragraph.ClearValue(Block.BorderThicknessProperty);
        paragraph.ClearValue(Block.PaddingProperty);
        paragraph.ClearValue(Paragraph.TextIndentProperty);
        paragraph.ClearValue(Block.LineHeightProperty);
        paragraph.ClearValue(Block.LineStackingStrategyProperty);
        paragraph.ClearValue(Paragraph.MinOrphanLinesProperty);
        paragraph.ClearValue(Paragraph.MinWidowLinesProperty);
        paragraph.ClearValue(Paragraph.KeepTogetherProperty);
        paragraph.ClearValue(Paragraph.KeepWithNextProperty);
    }

    private static void StyleSection(Paragraph paragraph, string name, StyleContext context)
    {
        var style = context.Style;
        ResetParagraph(paragraph);
        paragraph.Inlines.Clear();
        foreach (var property in new[]
                 {
                     TextElement.FontSizeProperty, TextElement.FontWeightProperty, TextElement.FontStyleProperty,
                     TextElement.FontFamilyProperty, TextElement.ForegroundProperty, TextElement.BackgroundProperty,
                     Paragraph.TextDecorationsProperty,
                 })
        {
            paragraph.ClearValue(property);
        }

        if (style.ShowSectionNames)
        {
            var size = style.BaseFontSize * SectionSizeFactor;
            paragraph.Margin = new Thickness(0, 0, 0, context.GapPixels);
            paragraph.LineHeight = size * style.LineSpacing;
            paragraph.Inlines.Add(new Run(name.ToUpperInvariant())
            {
                FontSize = size,
                Foreground = Frozen(style.SectionInk),
            });
        }
        else
        {
            // Text hidden, but the paragraph stays so indices match: half a line of black.
            paragraph.Margin = new Thickness(0);
            paragraph.LineHeight = context.LineHeight * HiddenSectionLines;
            paragraph.FontSize = Math.Max(1, style.BaseFontSize * 0.1);
        }
    }

    // ---- inlines ---------------------------------------------------------------------------------------------

    /// <summary>What an element passes down to its children.</summary>
    private readonly record struct InlineState(Color? Ink, Color? Background, bool Underline, bool Strike);

    private static void StyleInlines(InlineCollection inlines, InlineState parent, StyleContext context)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    StyleRun(run, parent, context);
                    break;
                case Span span:
                {
                    var state = Inherit(span, parent, context);
                    if (span is Underline) state = state with { Underline = true };
                    // Decorations are re-created on the leaves with the right pen; the span itself must not draw them.
                    if (span.ReadLocalValue(Inline.TextDecorationsProperty) != DependencyProperty.UnsetValue)
                        span.ClearValue(Inline.TextDecorationsProperty);
                    StyleInlines(span.Inlines, state, context);
                    break;
                }
            }
        }
    }

    private static void StyleRun(Run run, InlineState parent, StyleContext context)
    {
        var state = Inherit(run, parent, context);
        var highlightInk = state.Background is { } background && context.Style.HighlightInks.TryGetValue(background, out var ink)
            ? ink
            : (Color?)null;
        if (highlightInk is { } enforced)
        {
            run.Foreground = Frozen(enforced);
            state = state with { Ink = enforced };
        }

        if (state.Underline || state.Strike)
        {
            var pen = new Pen(Frozen(state.Ink ?? context.Style.Ink), UnderlineThicknessEm);
            pen.Freeze();
            var decorations = new TextDecorationCollection();
            if (state.Underline) decorations.Add(Decoration(TextDecorationLocation.Underline, pen));
            if (state.Strike) decorations.Add(Decoration(TextDecorationLocation.Strikethrough, pen));
            decorations.Freeze();
            run.TextDecorations = decorations;
        }
        else if (run.ReadLocalValue(Inline.TextDecorationsProperty) != DependencyProperty.UnsetValue)
        {
            run.ClearValue(Inline.TextDecorationsProperty);
        }
    }

    private static TextDecoration Decoration(TextDecorationLocation location, Pen pen) =>
        new(location, pen, 0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRenderingEmSize);

    /// <summary>Normalises the local values of one element and returns the state its children inherit.</summary>
    private static InlineState Inherit(TextElement element, InlineState parent, StyleContext context)
    {
        // Size: relative to the editor base, so scale the explicit ones. Normal text has none and inherits the base.
        if (element.ReadLocalValue(TextElement.FontSizeProperty) is double size)
            element.SetValue(TextElement.FontSizeProperty, size * context.Scale);

        // Face and weight: the output face is a setting. Light/Regular/Medium inherit it, SemiBold and up are Bold.
        element.ClearValue(TextElement.FontFamilyProperty);
        if (element.ReadLocalValue(TextElement.FontWeightProperty) is FontWeight weight)
        {
            if (weight.ToOpenTypeWeight() <= 500) element.ClearValue(TextElement.FontWeightProperty);
            else element.SetValue(TextElement.FontWeightProperty, FontWeights.Bold);
        }

        var state = parent;
        if (element.ReadLocalValue(TextElement.ForegroundProperty) is SolidColorBrush foreground)
            state = state with { Ink = foreground.Color };
        if (element.ReadLocalValue(TextElement.BackgroundProperty) is SolidColorBrush background)
            state = state with { Background = background.Color };

        var decorationProperty = element is Paragraph ? Paragraph.TextDecorationsProperty : Inline.TextDecorationsProperty;
        if (element.ReadLocalValue(decorationProperty) is TextDecorationCollection decorations)
        {
            foreach (var decoration in decorations)
            {
                if (decoration.Location == TextDecorationLocation.Underline) state = state with { Underline = true };
                else if (decoration.Location == TextDecorationLocation.Strikethrough) state = state with { Strike = true };
            }

            if (element is Paragraph) element.ClearValue(decorationProperty);
        }

        return state;
    }

    // ---- END block -------------------------------------------------------------------------------------------

    private static BlockUIContainer CreateEndBlock(StyleContext context)
    {
        var style = context.Style;
        var rule = Frozen(style.EndRule);
        var textSize = style.BaseFontSize * EndSizeFactor;

        var grid = new Grid
        {
            Height = context.LineHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SnapsToDevicePixels = true,
        };
        // Margin | rule | END | rule | margin. Star weights give each rule about 30 % of the space left beside the word.
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.65, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.65, GridUnitType.Star) });

        grid.Children.Add(Place(Rule(rule), 1));
        grid.Children.Add(Place(
            new TextBlock
            {
                Text = "END",
                FontFamily = style.FontFamily,
                FontSize = textSize,
                FontWeight = FontWeights.SemiBold,
                Foreground = Frozen(style.EndInk),
                Margin = new Thickness(textSize * 0.6, 0, textSize * 0.6, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            2));
        grid.Children.Add(Place(Rule(rule), 3));

        return new BlockUIContainer(grid)
        {
            Tag = DocumentNavigator.GeneratedTag,
            // One blank line between the script and END.
            Margin = new Thickness(0, context.LineHeight, 0, 0),
        };
    }

    private static Border Rule(Brush brush) => new()
    {
        Height = 2,
        Background = brush,
        VerticalAlignment = VerticalAlignment.Center,
        SnapsToDevicePixels = true,
    };

    private static UIElement Place(UIElement element, int column)
    {
        Grid.SetColumn(element, column);
        return element;
    }

    private static SolidColorBrush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }

    private sealed record StyleContext(OutputTextStyle Style, double Scale, double LineHeight, double GapPixels);
}
