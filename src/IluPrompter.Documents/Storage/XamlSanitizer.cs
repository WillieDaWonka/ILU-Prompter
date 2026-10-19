using System.IO;
using System.Text;
using System.Xml;

namespace IluPrompter.Documents;

/// <summary>
/// Security gate for FlowDocument XAML that came from a file (ARCHITECTURE §4.E). Nothing read from disk may reach
/// <c>TextRange.Load(DataFormats.Xaml)</c> without passing through <see cref="Sanitize"/>, and nothing in this
/// project ever calls <c>XamlReader.Load/Parse</c>.
/// <para>
/// The input is parsed with <see cref="XmlReader"/> (DTDs prohibited, no resolver) and a brand-new document is
/// written from an allow-list, so the string handed to WPF is rebuilt by us rather than the author of the file.
/// Rejected with <see cref="ScriptFileException"/>: any element outside the allow-list (Button,
/// ObjectDataProvider, InlineUIContainer, property-element syntax...), any foreign XML namespace or prefix
/// (<c>x:</c>, <c>clr-namespace</c>), any attribute outside the allow-list, any attribute value containing a markup
/// extension (<c>{...}</c>), a DTD, and over-deep or over-large input. <c>Hyperlink</c> is rewritten to a plain
/// <c>Span</c> (its target is dropped) so a script pasted from the web still opens.
/// </para>
/// <para>
/// Document-level attributes that <c>TextRange.Save</c> writes on the root element (font, size, foreground,
/// alignment, Typography.*...) are dropped: the host document owns them, and WPF would otherwise copy them onto
/// every paragraph.
/// </para>
/// </summary>
public static class XamlSanitizer
{
    public const string PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private const int MaxDepth = 64;
    private const long MaxCharacters = 64_000_000;
    private const int MaxAttributeLength = 4096;

    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "FlowDocument", "Section", "Paragraph", "Run", "Span", "Bold", "Italic", "Underline", "LineBreak",
        "List", "ListItem", "Table", "TableRowGroup", "TableRow", "TableCell", "TableColumn",
    };

    /// <summary>Text-formatting, paragraph and table attributes (no namespace). Anything else is rejected.</summary>
    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.Ordinal)
    {
        "FontFamily", "FontSize", "FontWeight", "FontStyle", "FontStretch", "Foreground", "Background",
        "TextDecorations", "TextAlignment", "TextIndent", "LineHeight", "LineStackingStrategy",
        "IsHyphenationEnabled", "FlowDirection", "Margin", "Padding", "BorderBrush", "BorderThickness",
        "KeepTogether", "KeepWithNext", "MinOrphanLines", "MinWidowLines", "BaselineAlignment",
        "MarkerStyle", "MarkerOffset", "StartIndex", "ColumnSpan", "RowSpan", "Width", "CellSpacing",
        "HasTrailingParagraphEnd", "HasTrailingParagraphBreakOnPaste",
    };

    /// <summary>Attributes that only describe the host document; dropped from the root element.</summary>
    private static readonly HashSet<string> RootDocumentAttributes = new(StringComparer.Ordinal)
    {
        "FontFamily", "FontSize", "FontWeight", "FontStyle", "FontStretch", "Foreground", "Background",
        "TextAlignment", "LineHeight", "LineStackingStrategy", "IsHyphenationEnabled", "FlowDirection",
    };

    /// <summary>Attributes of a Hyperlink that are dropped when it is rewritten to a Span.</summary>
    private static readonly HashSet<string> HyperlinkOnlyAttributes = new(StringComparer.Ordinal)
    {
        "NavigateUri", "TargetName", "Command", "CommandParameter", "CommandTarget", "ToolTip", "Cursor",
        "Focusable", "IsEnabled", "Name", "Uid",
    };

    /// <summary>Returns the rebuilt XAML. Throws <see cref="ScriptFileException"/> on any violation.</summary>
    public static string Sanitize(string xaml)
    {
        ArgumentNullException.ThrowIfNull(xaml);
        try
        {
            return Rewrite(xaml);
        }
        catch (ScriptFileException)
        {
            throw;
        }
        catch (Exception ex) when (ex is XmlException or ArgumentException or InvalidOperationException or IOException)
        {
            throw new ScriptFileException("The script's formatting data is not valid and was not opened.", ex);
        }
    }

    /// <summary>Throws <see cref="ScriptFileException"/> when <paramref name="xaml"/> would be rejected.</summary>
    public static void Validate(string xaml) => _ = Sanitize(xaml);

    private static string Rewrite(string xaml)
    {
        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
            CheckCharacters = true,
            MaxCharactersInDocument = MaxCharacters,
            MaxCharactersFromEntities = 0,
        };
        var output = new StringBuilder(xaml.Length + 64);
        var writerSettings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            CheckCharacters = true,
            ConformanceLevel = ConformanceLevel.Document,
        };

        using var stringReader = new StringReader(xaml);
        using var reader = XmlReader.Create(stringReader, readerSettings);
        using (var writer = XmlWriter.Create(output, writerSettings))
        {
            var sawRoot = false;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.XmlDeclaration:
                        break;
                    case XmlNodeType.Element:
                        if (reader.Depth >= MaxDepth) throw Reject("The script's formatting is nested too deeply.");
                        sawRoot = true;
                        WriteElement(reader, writer, isRoot: reader.Depth == 0);
                        if (reader.IsEmptyElement) writer.WriteEndElement();

                        break;
                    case XmlNodeType.EndElement:
                        writer.WriteEndElement();
                        break;
                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        if (reader.Depth == 0) break; // whitespace around the root; real text outside it is a parse error anyway
                        writer.WriteString(reader.Value);
                        break;
                    default:
                        throw Reject("The script's formatting contains unsupported content (" + reader.NodeType + ").");
                }
            }

            if (!sawRoot) throw Reject("The script's formatting data is empty.");
        }

        return output.ToString();
    }

    private static void WriteElement(XmlReader reader, XmlWriter writer, bool isRoot)
    {
        if (!string.IsNullOrEmpty(reader.Prefix))
            throw Reject("The script uses a prefixed element (" + reader.Name + "), which is not allowed.");
        if (!string.Equals(reader.NamespaceURI, PresentationNamespace, StringComparison.Ordinal))
            throw Reject("The script uses an element from another namespace (" + reader.Name + "), which is not allowed.");

        var name = reader.LocalName;
        var isHyperlink = string.Equals(name, "Hyperlink", StringComparison.Ordinal);
        if (!isHyperlink && !AllowedElements.Contains(name))
            throw Reject("The script contains an element that is not allowed: " + name + ".");

        writer.WriteStartElement(isHyperlink ? "Span" : name, PresentationNamespace);

        if (!reader.HasAttributes) return;
        reader.MoveToFirstAttribute();
        do
        {
            WriteAttribute(reader, writer, name, isRoot, isHyperlink);
        }
        while (reader.MoveToNextAttribute());
        reader.MoveToElement();
    }

    private static void WriteAttribute(XmlReader reader, XmlWriter writer, string element, bool isRoot, bool isHyperlink)
    {
        var prefix = reader.Prefix;
        var local = reader.LocalName;
        var value = reader.Value;

        // Namespace declarations: only the default presentation namespace.
        if (prefix == "xmlns" || (prefix.Length == 0 && local == "xmlns"))
        {
            if (prefix == "xmlns" || !string.Equals(value, PresentationNamespace, StringComparison.Ordinal))
                throw Reject("The script declares an XML namespace that is not allowed (" + reader.Name + ").");
            return; // the writer declares it once on the root
        }

        if (prefix == "xml")
        {
            if (local == "space")
            {
                if (value is not ("preserve" or "default")) throw Reject("The script has an invalid xml:space value.");
                writer.WriteAttributeString("xml", "space", null, value);
                return;
            }

            if (local == "lang")
            {
                if (!isRoot) writer.WriteAttributeString("xml", "lang", null, CheckValue(reader.Name, value));
                return;
            }

            throw Reject("The script uses an attribute that is not allowed (" + reader.Name + ").");
        }

        if (prefix.Length != 0 || reader.NamespaceURI.Length != 0)
            throw Reject("The script uses a prefixed attribute that is not allowed (" + reader.Name + ").");

        if (isHyperlink && HyperlinkOnlyAttributes.Contains(local)) return;

        var dotted = local.StartsWith("Typography.", StringComparison.Ordinal) || local.StartsWith("NumberSubstitution.", StringComparison.Ordinal);
        if (!dotted && !AllowedAttributes.Contains(local))
            throw Reject("The script uses an attribute that is not allowed: " + local + " on " + element + ".");

        CheckValue(local, value);
        if (isRoot && (dotted || RootDocumentAttributes.Contains(local))) return;
        if (local == "FontFamily" && (value.Contains('/') || value.Contains('\\') || value.Contains(':')))
            throw Reject("The script names a font by path or address, which is not allowed.");

        writer.WriteAttributeString(local, value);
    }

    private static string CheckValue(string name, string value)
    {
        if (value.Length > MaxAttributeLength)
            throw Reject("The script has an unreasonably long value for " + name + ".");
        if (value.Contains('{'))
            throw Reject("The script contains a markup extension in " + name + ", which is not allowed.");
        if (value.Contains("clr-namespace", StringComparison.OrdinalIgnoreCase) || value.Contains("assembly=", StringComparison.OrdinalIgnoreCase))
            throw Reject("The script refers to a code namespace in " + name + ", which is not allowed.");
        return value;
    }

    private static ScriptFileException Reject(string message) => new(message);
}
