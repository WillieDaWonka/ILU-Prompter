using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). Minimal, non-throwing behaviour.</summary>
public sealed class ScriptFormatter : IScriptFormatter
{
    public double Factor(ScriptSize size) => size switch
    {
        ScriptSize.Small => 0.80,
        ScriptSize.Large => 1.25,
        ScriptSize.Huge => 1.50,
        _ => 1.00,
    };

    public void ApplySize(TextRange range, ScriptSize size, double editorBaseFontSize) =>
        range.ApplyPropertyValue(TextElement.FontSizeProperty, editorBaseFontSize * Factor(size));

    public ScriptSize? GetSize(TextRange range, double editorBaseFontSize) => ScriptSize.Normal;

    public void ToggleStrikethrough(TextRange range) =>
        range.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Strikethrough);

    public bool? GetStrikethrough(TextRange range) => false;

    public void ApplyInk(TextRange range, Color? ink) =>
        range.ApplyPropertyValue(TextElement.ForegroundProperty, ink is { } c ? new SolidColorBrush(c) : null);

    public void ApplyHighlight(TextRange range, Color? highlight, Color? highlightInk) =>
        range.ApplyPropertyValue(TextElement.BackgroundProperty, highlight is { } c ? new SolidColorBrush(c) : null);

    public void ClearFormatting(TextRange range) => range.ClearAllProperties();

    public Paragraph InsertSectionMarker(RichTextBox editor, string marker, string name)
    {
        var paragraph = new Paragraph(new Run($"{marker} {name}"));
        editor.Document.Blocks.Add(paragraph);
        return paragraph;
    }

    public void StyleSectionParagraphs(FlowDocument document, string marker, Brush labelBrush, Brush ruleBrush)
    {
    }
}
