using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Wpf = System.Windows.Documents;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace IluPrompter.Documents;

/// <summary>
/// Reads a .docx with the OpenXML SDK (never Word, never XAML) and builds a canonical FlowDocument
/// (ARCHITECTURE §4.E): Title and Heading 1-3 paragraphs become "## text" section paragraphs, list paragraphs get a
/// "• " prefix, table cells are emitted row by row, images and text boxes are skipped. Run formatting
/// (bold, italic, underline, strikethrough, highlight, colour, size) is resolved through run, character style,
/// paragraph style and document defaults, then normalised by <see cref="ScriptRuns"/>.
/// </summary>
internal static class DocxImporter
{
    private const string SectionMarker = "##";
    private const double DefaultSizePoints = 11;
    private static readonly string[] HeadingNames = ["title", "heading 1", "heading 2", "heading 3"];
    private static readonly string[] HeadingIds = ["title", "heading1", "heading2", "heading3"];

    private sealed class Piece
    {
        public string Text = "";
        public bool IsBreak;
        public RunFormat Format;
    }

    private sealed class Item
    {
        public bool IsHeading;
        public bool IsBullet;
        public TextAlignment? Alignment;
        public List<Piece> Pieces = [];
    }

    public static Wpf.FlowDocument Import(byte[] bytes, double editorBase)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var package = WordprocessingDocument.Open(stream, false, new OpenSettings { AutoSave = false });
        var main = package.MainDocumentPart ?? throw new ScriptFileException("That Word document has no content.");
        var body = main.Document?.Body ?? throw new ScriptFileException("That Word document has no content.");
        var styles = new StyleSheet(main.StyleDefinitionsPart?.Styles);

        var items = new List<Item>();
        Walk(body, styles, items);

        var dominant = ScriptRuns.DominantSize(items
            .Where(i => !i.IsHeading)
            .SelectMany(i => i.Pieces)
            .Where(p => !p.IsBreak)
            .Select(p => (p.Format.Size, p.Text.Length)));

        var document = new Wpf.FlowDocument();
        foreach (var item in items) document.Blocks.Add(Build(item, dominant, editorBase));
        if (document.Blocks.Count == 0) document.Blocks.Add(new Wpf.Paragraph());
        return document;
    }

    // ---- structure -------------------------------------------------------------------------------------------

    private static void Walk(OpenXmlElement container, StyleSheet styles, List<Item> into)
    {
        foreach (var child in container.ChildElements)
        {
            switch (child)
            {
                case W.Paragraph paragraph:
                    var item = ReadParagraph(paragraph, styles);
                    if (item is not null) into.Add(item);
                    break;
                case W.Table table:
                    foreach (var row in table.Elements<W.TableRow>())
                        foreach (var cell in row.Elements<W.TableCell>())
                            Walk(cell, styles, into);
                    break;
                case W.SdtBlock sdt when sdt.SdtContentBlock is { } content:
                    Walk(content, styles, into);
                    break;
                case W.CustomXmlBlock custom:
                    Walk(custom, styles, into);
                    break;
            }
        }
    }

    private static Item? ReadParagraph(W.Paragraph paragraph, StyleSheet styles)
    {
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var item = new Item();

        var runs = paragraph.Descendants<W.Run>().Where(r => ReferenceEquals(r.Ancestors<W.Paragraph>().FirstOrDefault(), paragraph));
        foreach (var run in runs)
        {
            var format = styles.Resolve(run, styleId);
            foreach (var part in run.ChildElements)
            {
                switch (part)
                {
                    case W.Text text:
                        item.Pieces.Add(new Piece { Text = text.Text, Format = format });
                        break;
                    case W.TabChar:
                        item.Pieces.Add(new Piece { Text = "\t", Format = format });
                        break;
                    case W.NoBreakHyphen:
                        item.Pieces.Add(new Piece { Text = "-", Format = format });
                        break;
                    case W.CarriageReturn:
                        item.Pieces.Add(new Piece { IsBreak = true, Format = format });
                        break;
                    case W.Break br when br.Type is null || br.Type.Value == W.BreakValues.TextWrapping:
                        item.Pieces.Add(new Piece { IsBreak = true, Format = format });
                        break;
                }
            }
        }

        if (styles.IsHeadingStyle(styleId))
        {
            var heading = string.Concat(item.Pieces.Select(p => p.IsBreak ? " " : p.Text)).Trim();
            if (heading.Length == 0) return null; // an empty heading is not a section
            item.IsHeading = true;
            item.Pieces = [new Piece { Text = heading }];
            return item;
        }

        item.IsBullet = IsListParagraph(paragraph, styles, styleId);
        var justification = paragraph.ParagraphProperties?.Justification?.Val?.Value;
        if (justification == W.JustificationValues.Center) item.Alignment = TextAlignment.Center;
        else if (justification == W.JustificationValues.Right) item.Alignment = TextAlignment.Right;
        return item;
    }

    private static bool IsListParagraph(W.Paragraph paragraph, StyleSheet styles, string? styleId)
    {
        var direct = paragraph.ParagraphProperties?.NumberingProperties;
        if (direct is not null) return direct.NumberingId?.Val?.Value != 0;
        return styles.StyleHasNumbering(styleId);
    }

    private static Wpf.Paragraph Build(Item item, double dominant, double editorBase)
    {
        var paragraph = new Wpf.Paragraph();
        if (item.IsHeading)
        {
            paragraph.Inlines.Add(new Wpf.Run(SectionMarker + " " + item.Pieces[0].Text));
            return paragraph;
        }

        if (item.Alignment is { } alignment) paragraph.TextAlignment = alignment;
        if (item.IsBullet) paragraph.Inlines.Add(new Wpf.Run("• "));
        foreach (var piece in item.Pieces)
        {
            if (piece.IsBreak) paragraph.Inlines.Add(new Wpf.LineBreak());
            else if (piece.Text.Length > 0) paragraph.Inlines.Add(ScriptRuns.Create(Clean(piece.Text), piece.Format, dominant, editorBase));
        }

        return paragraph;
    }

    /// <summary>Drops characters XML cannot carry (control codes) so the editor document can always be saved.</summary>
    internal static string Clean(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                sb.Append(ch).Append(text[++i]);
            }
            else if (System.Xml.XmlConvert.IsXmlChar(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    // ---- styles ----------------------------------------------------------------------------------------------

    /// <summary>The slice of styles.xml that matters here: character formatting inheritance and heading/list detection.</summary>
    private sealed class StyleSheet
    {
        private readonly Dictionary<string, W.Style> _styles = new(StringComparer.OrdinalIgnoreCase);
        private readonly OpenXmlElement? _defaults;

        public StyleSheet(W.Styles? styles)
        {
            if (styles is null) return;
            foreach (var style in styles.Elements<W.Style>())
            {
                if (style.StyleId?.Value is { } id) _styles[id] = style;
            }

            _defaults = styles.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle;
        }

        public bool IsHeadingStyle(string? styleId)
        {
            if (styleId is null) return false;
            if (HeadingIds.Contains(styleId, StringComparer.OrdinalIgnoreCase)) return true;
            return _styles.TryGetValue(styleId, out var style)
                   && style.StyleName?.Val?.Value is { } name
                   && HeadingNames.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        public bool StyleHasNumbering(string? styleId)
        {
            foreach (var style in Chain(styleId))
            {
                var numbering = style.StyleParagraphProperties?.NumberingProperties;
                if (numbering is not null) return numbering.NumberingId?.Val?.Value != 0;
            }

            return false;
        }

        public RunFormat Resolve(W.Run run, string? paragraphStyleId)
        {
            var sources = Sources(run, paragraphStyleId).ToList();

            bool? On<T>() where T : W.OnOffType
            {
                foreach (var source in sources)
                {
                    if (source.GetFirstChild<T>() is { } element) return element.Val?.Value ?? true;
                }

                return null;
            }

            var underline = false;
            foreach (var source in sources)
            {
                if (source.GetFirstChild<W.Underline>() is { } u)
                {
                    underline = !string.Equals(u.Val?.InnerText, "none", StringComparison.OrdinalIgnoreCase);
                    break;
                }
            }

            Color? ink = null;
            foreach (var source in sources)
            {
                if (source.GetFirstChild<W.Color>() is { } c)
                {
                    ink = ParseHex(c.Val?.Value);
                    break;
                }
            }

            Color? highlight = null;
            foreach (var source in sources)
            {
                if (source.GetFirstChild<W.Highlight>() is { } h)
                {
                    highlight = HighlightColour(h.Val?.InnerText);
                    break;
                }
            }

            var size = DefaultSizePoints;
            foreach (var source in sources)
            {
                if (source.GetFirstChild<W.FontSize>() is { } fs && double.TryParse(fs.Val?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var halfPoints) && halfPoints > 0)
                {
                    size = halfPoints / 2;
                    break;
                }
            }

            return new RunFormat(
                size,
                On<W.Bold>() ?? false,
                On<W.Italic>() ?? false,
                underline,
                (On<W.Strike>() ?? false) || (On<W.DoubleStrike>() ?? false),
                ink,
                highlight);
        }

        /// <summary>Property holders in precedence order: the run, its character style chain, the paragraph style chain, the defaults.</summary>
        private IEnumerable<OpenXmlElement> Sources(W.Run run, string? paragraphStyleId)
        {
            if (run.RunProperties is { } direct) yield return direct;
            foreach (var style in Chain(run.RunProperties?.RunStyle?.Val?.Value))
                if (style.StyleRunProperties is { } characterProperties) yield return characterProperties;
            foreach (var style in Chain(paragraphStyleId))
                if (style.StyleRunProperties is { } paragraphProperties) yield return paragraphProperties;
            if (_defaults is not null) yield return _defaults;
        }

        private IEnumerable<W.Style> Chain(string? styleId)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var id = styleId;
            while (id is not null && seen.Add(id) && _styles.TryGetValue(id, out var style))
            {
                yield return style;
                id = style.BasedOn?.Val?.Value;
            }
        }
    }

    // ---- colours ---------------------------------------------------------------------------------------------

    private static Color? ParseHex(string? hex)
    {
        if (hex is null || hex.Length != 6) return null; // "auto" and anything odd
        return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)
            ? Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)
            : null;
    }

    /// <summary>Word's sixteen highlight colours (ST_HighlightColor).</summary>
    private static Color? HighlightColour(string? name) => name?.ToLowerInvariant() switch
    {
        "black" => Color.FromRgb(0x00, 0x00, 0x00),
        "blue" => Color.FromRgb(0x00, 0x00, 0xFF),
        "cyan" => Color.FromRgb(0x00, 0xFF, 0xFF),
        "darkblue" => Color.FromRgb(0x00, 0x00, 0x80),
        "darkcyan" => Color.FromRgb(0x00, 0x80, 0x80),
        "darkgray" => Color.FromRgb(0x80, 0x80, 0x80),
        "darkgreen" => Color.FromRgb(0x00, 0x80, 0x00),
        "darkmagenta" => Color.FromRgb(0x80, 0x00, 0x80),
        "darkred" => Color.FromRgb(0x80, 0x00, 0x00),
        "darkyellow" => Color.FromRgb(0x80, 0x80, 0x00),
        "green" => Color.FromRgb(0x00, 0xFF, 0x00),
        "lightgray" => Color.FromRgb(0xC0, 0xC0, 0xC0),
        "magenta" => Color.FromRgb(0xFF, 0x00, 0xFF),
        "red" => Color.FromRgb(0xFF, 0x00, 0x00),
        "white" => Color.FromRgb(0xFF, 0xFF, 0xFF),
        "yellow" => Color.FromRgb(0xFF, 0xFF, 0x00),
        _ => null,
    };
}
