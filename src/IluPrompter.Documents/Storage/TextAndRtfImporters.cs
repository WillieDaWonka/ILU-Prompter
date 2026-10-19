using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace IluPrompter.Documents;

/// <summary>.txt import: encoding detection and one paragraph per line.</summary>
internal static class TextImporter
{
    private static readonly Lazy<Encoding> Legacy = new(() =>
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252); // Latin1 plus the printable 0x80-0x9F range (curly quotes, dashes, euro)
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Encoding.Latin1;
        }
    });

    /// <summary>BOM-aware (UTF-8, UTF-16, UTF-32); no BOM means UTF-8 if the bytes are valid UTF-8, else a legacy 8-bit encoding.</summary>
    public static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0)
            return new UTF32Encoding(false, true).GetString(bytes, 4, bytes.Length - 4);
        if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            return new UTF32Encoding(true, true).GetString(bytes, 4, bytes.Length - 4);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(false, false).GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return new UnicodeEncoding(false, false).GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return new UnicodeEncoding(true, false).GetString(bytes, 2, bytes.Length - 2);

        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Legacy.Value.GetString(bytes);
        }
    }

    /// <summary>Lines split on CR LF, LF or CR. Empty lines stay as empty paragraphs; a final line break adds nothing.</summary>
    public static List<string> SplitLines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        if (lines.Count > 1 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    public static FlowDocument Import(byte[] bytes)
    {
        var document = new FlowDocument();
        foreach (var line in SplitLines(Decode(bytes)))
        {
            var clean = DocxImporter.Clean(line);
            document.Blocks.Add(clean.Length == 0 ? new Paragraph() : new Paragraph(new Run(clean)));
        }

        if (document.Blocks.Count == 0) document.Blocks.Add(new Paragraph());
        return document;
    }
}

/// <summary>.rtf import: WPF's loader, then rebuild every paragraph in canonical form.</summary>
internal static class RtfImporter
{
    public static FlowDocument Import(byte[] bytes, double editorBase)
    {
        if (!LooksLikeRtf(bytes)) throw new ScriptFileException("That file is not a valid RTF document.");

        var source = new FlowDocument();
        using (var stream = new MemoryStream(bytes, writable: false))
        {
            new TextRange(source.ContentStart, source.ContentEnd).Load(stream, DataFormats.Rtf);
        }

        // Lists, tables and sections become plain paragraphs first; then the document-level defaults the loader set
        // are carried over so effective run values still resolve the same way.
        var flat = DocumentFlattener.CloneFlattened(source);
        flat.FontSize = source.FontSize;
        flat.FontWeight = source.FontWeight;
        flat.FontStyle = source.FontStyle;
        flat.Foreground = source.Foreground;
        return Normalise(flat, editorBase);
    }

    private static bool LooksLikeRtf(byte[] bytes)
    {
        var start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        while (start < bytes.Length && bytes[start] is (byte)' ' or (byte)'\r' or (byte)'\n' or (byte)'\t') start++;
        var header = "{\\rtf"u8;
        return bytes.Length - start >= header.Length && bytes.AsSpan(start, header.Length).SequenceEqual(header);
    }

    /// <summary>
    /// A new document in which every paragraph holds only runs and line breaks carrying what the editor offers:
    /// Normal-relative sizes (against the dominant size), palette inks and highlights, bold, italic, underline,
    /// strikethrough. Font families, paragraph spacing and indents are dropped; centred and right-aligned paragraphs
    /// keep their alignment.
    /// </summary>
    internal static FlowDocument Normalise(FlowDocument source, double editorBase)
    {
        var paragraphs = new DocumentNavigator().GetParagraphs(source);
        var samples = new List<(double, int)>();
        foreach (var paragraph in paragraphs)
            foreach (var run in RunsIn(paragraph.Inlines)) samples.Add((run.FontSize, run.Text.Length));
        var dominant = ScriptRuns.DominantSize(samples);

        var result = new FlowDocument();
        foreach (var paragraph in paragraphs)
        {
            var copy = new Paragraph();
            if (paragraph.ReadLocalValue(Block.TextAlignmentProperty) is TextAlignment.Center or TextAlignment.Right)
                copy.TextAlignment = paragraph.TextAlignment;
            Copy(paragraph.Inlines, copy.Inlines, default, dominant, editorBase);
            result.Blocks.Add(copy);
        }

        if (result.Blocks.Count == 0) result.Blocks.Add(new Paragraph());
        return result;
    }

    private readonly record struct Inherited(bool Underline, bool Strike, Color? Highlight);

    private static void Copy(InlineCollection from, InlineCollection to, Inherited state, double dominant, double editorBase)
    {
        foreach (var inline in from)
        {
            switch (inline)
            {
                case Run run:
                {
                    if (run.Text.Length == 0) break;
                    var (underline, strike) = Decorations(run.TextDecorations);
                    var highlight = (run.ReadLocalValue(TextElement.BackgroundProperty) as SolidColorBrush)?.Color ?? state.Highlight;
                    var format = new RunFormat(
                        run.FontSize,
                        run.FontWeight.ToOpenTypeWeight() >= 600,
                        run.FontStyle == FontStyles.Italic || run.FontStyle == FontStyles.Oblique,
                        underline || state.Underline,
                        strike || state.Strike,
                        (run.Foreground as SolidColorBrush)?.Color,
                        highlight);
                    to.Add(ScriptRuns.Create(DocxImporter.Clean(run.Text), format, dominant, editorBase));
                    break;
                }

                case LineBreak:
                    to.Add(new LineBreak());
                    break;
                case Span span:
                {
                    var (underline, strike) = Decorations(span.TextDecorations);
                    var next = new Inherited(
                        state.Underline || underline || span is Underline,
                        state.Strike || strike,
                        (span.ReadLocalValue(TextElement.BackgroundProperty) as SolidColorBrush)?.Color ?? state.Highlight);
                    Copy(span.Inlines, to, next, dominant, editorBase);
                    break;
                }
            }
        }
    }

    private static (bool Underline, bool Strike) Decorations(TextDecorationCollection? decorations)
    {
        bool underline = false, strike = false;
        if (decorations is null) return (false, false);
        foreach (var decoration in decorations)
        {
            underline |= decoration.Location == TextDecorationLocation.Underline;
            strike |= decoration.Location == TextDecorationLocation.Strikethrough;
        }

        return (underline, strike);
    }

    private static IEnumerable<Run> RunsIn(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run run) yield return run;
            else if (inline is Span span)
                foreach (var inner in RunsIn(span.Inlines)) yield return inner;
        }
    }
}
