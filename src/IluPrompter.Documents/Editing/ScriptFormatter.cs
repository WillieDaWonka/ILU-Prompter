using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Theme;

namespace IluPrompter.Documents;

/// <summary>
/// Editor toolbar helpers (ARCHITECTURE §2.11 / §4.E): relative script sizes, strikethrough, ink, highlight (which
/// carries its own ink), clear formatting, section markers. Control UI thread only; stateless.
/// <para>
/// Sizes are always written as explicit <c>editorBase * factor</c> values, so "Normal" is a real 1.0 and never depends
/// on what the host document inherits. Bold, italic and underline use the built-in <see cref="EditingCommands"/>;
/// note that WPF's own underline toggle replaces a run's whole TextDecorations value and so also removes a strikethrough.
/// </para>
/// </summary>
public sealed class ScriptFormatter : IScriptFormatter
{
    private const double LabelFontSize = 13; // DESIGN.md "label"
    private static readonly ScriptSize[] AllSizes = [ScriptSize.Small, ScriptSize.Normal, ScriptSize.Large, ScriptSize.Huge];

    /// <summary>The character formatting this class manages (everything else is left alone).</summary>
    private static readonly DependencyProperty[] Managed =
    [
        TextElement.FontSizeProperty, TextElement.FontWeightProperty, TextElement.FontStyleProperty,
        TextElement.ForegroundProperty, TextElement.BackgroundProperty, Inline.TextDecorationsProperty,
    ];

    public double Factor(ScriptSize size) => size switch
    {
        ScriptSize.Small => 0.80,
        ScriptSize.Large => 1.25,
        ScriptSize.Huge => 1.50,
        _ => 1.00,
    };

    // ---- size ------------------------------------------------------------------------------------------------

    public void ApplySize(TextRange range, ScriptSize size, double editorBaseFontSize)
    {
        ArgumentNullException.ThrowIfNull(range);
        range.ApplyPropertyValue(TextElement.FontSizeProperty, editorBaseFontSize * Factor(size));
    }

    public ScriptSize? GetSize(TextRange range, double editorBaseFontSize)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (editorBaseFontSize <= 0) return ScriptSize.Normal;
        if (range.GetPropertyValue(TextElement.FontSizeProperty) is not double size) return null; // UnsetValue: mixed

        var ratio = size / editorBaseFontSize;
        var best = ScriptSize.Normal;
        var bestDistance = double.MaxValue;
        foreach (var candidate in AllSizes)
        {
            var distance = Math.Abs(ratio - Factor(candidate));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    // ---- strikethrough ---------------------------------------------------------------------------------------

    public bool? GetStrikethrough(TextRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (range.IsEmpty)
            return range.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection caret && Has(caret, TextDecorationLocation.Strikethrough);

        // Judge run by run: a range struck through everywhere is "on" even when its runs differ in underline.
        var any = false;
        var all = true;
        foreach (var segment in TextSegments(range))
        {
            var struck = segment.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection decorations
                         && Has(decorations, TextDecorationLocation.Strikethrough);
            any |= struck;
            all &= struck;
        }

        if (!any) return false;
        return all ? true : null;
    }

    public void ToggleStrikethrough(TextRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        var on = GetStrikethrough(range) != true; // off or mixed: switch on
        if (range.IsEmpty)
        {
            SetStrikethrough(range, on);
            return;
        }

        // Run by run, so underlines on some of the runs survive.
        foreach (var segment in TextSegments(range)) SetStrikethrough(segment, on);
    }

    private static void SetStrikethrough(TextRange range, bool on)
    {
        var next = new TextDecorationCollection();
        if (range.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection current)
        {
            foreach (var decoration in current)
            {
                if (decoration.Location != TextDecorationLocation.Strikethrough) next.Add(decoration.CloneCurrentValue());
            }
        }

        if (on)
        {
            foreach (var decoration in TextDecorations.Strikethrough) next.Add(decoration.CloneCurrentValue());
        }

        range.ApplyPropertyValue(Inline.TextDecorationsProperty, next);
    }

    private static bool Has(TextDecorationCollection decorations, TextDecorationLocation location)
    {
        foreach (var decoration in decorations)
        {
            if (decoration.Location == location) return true;
        }

        return false;
    }

    // ---- ink and highlight -----------------------------------------------------------------------------------

    public void ApplyInk(TextRange range, Color? ink)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (ink is { } colour) range.ApplyPropertyValue(TextElement.ForegroundProperty, Frozen(colour));
        else ClearProperties(range, _ => [TextElement.ForegroundProperty]);
    }

    public void ApplyHighlight(TextRange range, Color? highlight, Color? highlightInk)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (highlight is { } fill)
        {
            range.ApplyPropertyValue(TextElement.BackgroundProperty, Frozen(fill));
            if (highlightInk is { } ink) range.ApplyPropertyValue(TextElement.ForegroundProperty, Frozen(ink));
            return;
        }

        // Removing a highlight also removes the ink it brought along (text coloured by the highlight itself would
        // otherwise stay dark on black), but never an ink the author chose.
        ClearProperties(range, segment => BroughtItsOwnInk(segment)
            ? [TextElement.BackgroundProperty, TextElement.ForegroundProperty]
            : [TextElement.BackgroundProperty]);
    }

    private static bool BroughtItsOwnInk(TextRange segment)
    {
        if (segment.GetPropertyValue(TextElement.BackgroundProperty) is not SolidColorBrush background) return false;
        if (segment.GetPropertyValue(TextElement.ForegroundProperty) is not SolidColorBrush foreground) return false;
        foreach (var option in ThemeResources.Highlights)
        {
            if (option.InkToken is null) continue;
            if (ThemeResources.GetColor(option.Token) == background.Color && ThemeResources.GetColor(option.InkToken) == foreground.Color) return true;
        }

        return false;
    }

    public void ClearFormatting(TextRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        // Size, weight, style, ink, highlight and decorations all go; size falls back to the inherited editor base (Normal).
        range.ClearAllProperties();
    }

    // ---- clearing one property -------------------------------------------------------------------------------

    /// <summary>
    /// <c>TextRange.ApplyPropertyValue</c> cannot clear a single property (UnsetValue is rejected and null would paint
    /// nothing), and only <c>ClearAllProperties</c> restructures spans correctly. So: remember each run's other managed
    /// values, clear everything, and put back whatever differs from the new defaults.
    /// </summary>
    private static void ClearProperties(TextRange range, Func<TextRange, DependencyProperty[]> clearedFor)
    {
        if (range.IsEmpty)
        {
            // A caret: set the typing formatting to the paragraph's own (inherited) value.
            var paragraph = range.Start.Paragraph;
            foreach (var property in clearedFor(range))
            {
                var fallback = paragraph?.GetValue(property);
                if (property == Inline.TextDecorationsProperty) fallback ??= new TextDecorationCollection();
                range.ApplyPropertyValue(property, fallback);
            }

            return;
        }

        var saved = new List<(TextRange Segment, List<(DependencyProperty Property, object Value)> Values)>();
        foreach (var segment in TextSegments(range))
        {
            var cleared = clearedFor(segment);
            var values = new List<(DependencyProperty, object)>();
            foreach (var property in Managed)
            {
                if (Array.IndexOf(cleared, property) >= 0) continue;
                var value = segment.GetPropertyValue(property);
                if (value != DependencyProperty.UnsetValue) values.Add((property, value));
            }

            saved.Add((segment, values));
        }

        range.ClearAllProperties();

        foreach (var (segment, values) in saved)
        {
            foreach (var (property, value) in values)
            {
                if (!ValuesEqual(value, segment.GetPropertyValue(property))) segment.ApplyPropertyValue(property, value);
            }
        }
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is SolidColorBrush x && b is SolidColorBrush y) return x.Color == y.Color;
        if (a is TextDecorationCollection c && b is TextDecorationCollection d)
        {
            return c.Count == d.Count
                   && Has(c, TextDecorationLocation.Strikethrough) == Has(d, TextDecorationLocation.Strikethrough)
                   && Has(c, TextDecorationLocation.Underline) == Has(d, TextDecorationLocation.Underline);
        }

        return Equals(a, b);
    }

    // ---- sections --------------------------------------------------------------------------------------------

    public Paragraph InsertSectionMarker(RichTextBox editor, string marker, string name)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var label = name.Length == 0 ? marker + " " : marker + " " + name;
        var paragraph = new Paragraph(new Run(label));

        editor.BeginChange();
        try
        {
            var caret = editor.CaretPosition;
            var current = caret.Paragraph;
            if (current is null)
            {
                editor.Document.Blocks.Add(paragraph);
            }
            else
            {
                var length = DocumentNavigator.TextBetween(current.ContentStart, current.ContentEnd).Length;
                var offset = DocumentNavigator.TextBetween(current.ContentStart, caret).Length;
                if (length == 0 || offset <= 0)
                {
                    current.SiblingBlocks.InsertBefore(current, paragraph);
                }
                else if (offset >= length)
                {
                    current.SiblingBlocks.InsertAfter(current, paragraph);
                }
                else
                {
                    // Split at the caret; the marker goes between the two halves.
                    var second = caret.InsertParagraphBreak().Paragraph;
                    if (second is null) current.SiblingBlocks.InsertAfter(current, paragraph);
                    else second.SiblingBlocks.InsertBefore(second, paragraph);
                }
            }
        }
        finally
        {
            editor.EndChange();
        }

        // Select the name so typing replaces it; with no name, park the caret after the marker.
        var nameStart = DocumentNavigator.PointerAtCharacter(paragraph, marker.Length + 1);
        var nameEnd = DocumentNavigator.PointerAtCharacter(paragraph, label.Length);
        editor.Selection.Select(nameStart, nameEnd);
        return paragraph;
    }

    public void StyleSectionParagraphs(FlowDocument document, string marker, Brush labelBrush, Brush ruleBrush)
    {
        ArgumentNullException.ThrowIfNull(document);
        var navigator = new DocumentNavigator();
        var parser = new SectionParser();
        foreach (var paragraph in navigator.GetParagraphs(document))
        {
            if (parser.TryParse(navigator.GetText(paragraph), marker, out _)) StyleAsSection(paragraph, labelBrush, ruleBrush);
            else RemoveSectionStyle(paragraph, ruleBrush);
        }
    }

    private static void StyleAsSection(Paragraph paragraph, Brush label, Brush rule)
    {
        // Only touch what differs, so repeated calls add nothing to the undo stack.
        if (!SameBrush(paragraph.ReadLocalValue(TextElement.ForegroundProperty) as Brush, label)) paragraph.Foreground = label;
        if (!SameBrush(paragraph.ReadLocalValue(Block.BorderBrushProperty) as Brush, rule)) paragraph.BorderBrush = rule;
        SetIfDifferent(paragraph, Block.BorderThicknessProperty, new Thickness(0, 0, 0, 1));
        SetIfDifferent(paragraph, Block.PaddingProperty, new Thickness(0, 0, 0, 4));
        SetIfDifferent(paragraph, Block.MarginProperty, new Thickness(0, 12, 0, 0));
        SetIfDifferent(paragraph, TextElement.FontSizeProperty, LabelFontSize);
    }

    private static void RemoveSectionStyle(Paragraph paragraph, Brush rule)
    {
        // Recognise our own styling by the rule; never strip a border the author put there.
        if (!SameBrush(paragraph.ReadLocalValue(Block.BorderBrushProperty) as Brush, rule)) return;
        foreach (var property in new[]
                 {
                     TextElement.ForegroundProperty, Block.BorderBrushProperty, Block.BorderThicknessProperty,
                     Block.PaddingProperty, Block.MarginProperty, TextElement.FontSizeProperty,
                 })
        {
            if (paragraph.ReadLocalValue(property) != DependencyProperty.UnsetValue) paragraph.ClearValue(property);
        }
    }

    private static void SetIfDifferent(DependencyObject target, DependencyProperty property, object value)
    {
        if (!Equals(target.ReadLocalValue(property), value)) target.SetValue(property, value);
    }

    private static bool SameBrush(Brush? a, Brush? b) =>
        ReferenceEquals(a, b) || (a is SolidColorBrush x && b is SolidColorBrush y && x.Color == y.Color);

    // ---- helpers ---------------------------------------------------------------------------------------------

    /// <summary>The text runs inside the range, each as its own range (so a property can be edited run by run).</summary>
    private static List<TextRange> TextSegments(TextRange range)
    {
        var segments = new List<TextRange>();
        var position = range.Start;
        var end = range.End;
        while (position is not null && position.CompareTo(end) < 0)
        {
            if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var length = Math.Min(position.GetTextRunLength(LogicalDirection.Forward), position.GetOffsetToPosition(end));
                if (length <= 0) break;
                var next = position.GetPositionAtOffset(length);
                if (next is null) break;
                segments.Add(new TextRange(position, next));
                position = next;
            }
            else
            {
                position = position.GetNextContextPosition(LogicalDirection.Forward);
            }
        }

        return segments;
    }

    private static SolidColorBrush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }
}
