using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>The native .ilup file (UTF-8 JSON via IluJson.Options).</summary>
public sealed record ScriptFile
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;
    public string Title { get; init; } = "Untitled";

    /// <summary>TextRange.Save(DataFormats.Xaml) of the editor document. Loaded ONLY via TextRange.Load,
    /// never XamlReader.Load (untrusted input).</summary>
    public string DocumentXaml { get; init; } = "";

    /// <summary>Editor Normal size when saved; run sizes are relative to it (Small 0.8, Normal 1, Large 1.25, Huge 1.5).</summary>
    public double EditorBaseFontSize { get; init; } = 20.0;

    public ScriptOverrides? Overrides { get; init; }
}

/// <summary>Optional per-script layout that overrides app settings when the script is opened (all nullable).</summary>
public sealed record ScriptOverrides
{
    public double? LinesVisible { get; init; }
    public double? LineSpacing { get; init; }
    public MarginSettings? Margins { get; init; }
    public double? StartSpeed { get; init; }
}

public sealed class ScriptFileException : Exception
{
    public ScriptFileException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Open/save/import/export. All members require an STA thread with a Dispatcher (they use FlowDocument
/// and TextRange); call them on the control UI thread.
/// </summary>
public interface IScriptFileService
{
    /// <summary>".ilup", ".rtf", ".txt", ".docx" (lower case, with dot).</summary>
    IReadOnlyList<string> OpenExtensions { get; }

    /// <summary>".rtf", ".txt".</summary>
    IReadOnlyList<string> ExportExtensions { get; }

    /// <summary>Load any supported file. Imports (rtf/txt/docx) are converted to a flattened, canonical
    /// FlowDocument XAML with Heading-styled paragraphs turned into "## Name" section paragraphs.
    /// Throws ScriptFileException with a user-presentable message on failure.</summary>
    ScriptFile Load(string path);

    /// <summary>Save .ilup atomically (write .tmp then replace). Throws ScriptFileException.</summary>
    void Save(string path, ScriptFile file);

    /// <summary>Export the editor document to .rtf or .txt (by extension). Throws ScriptFileException.</summary>
    void Export(string path, FlowDocument document);
}

/// <summary>Canonical paragraph order and anchors. Call on the thread that owns the document.</summary>
public interface IDocumentNavigator
{
    /// <summary>Every Paragraph in document order, descending into List/ListItem/Section/Table/TableCell.
    /// BlockUIContainers and paragraphs tagged as generated (END marker) are skipped.</summary>
    IReadOnlyList<Paragraph> GetParagraphs(FlowDocument document);

    /// <summary>Plain text of a paragraph (TextRange.Text without the trailing newline).</summary>
    string GetText(Paragraph paragraph);

    DocumentAnchor GetAnchor(FlowDocument document, TextPointer pointer);

    /// <summary>Clamps to the nearest valid paragraph/offset.</summary>
    TextPointer GetPointer(FlowDocument document, DocumentAnchor anchor);
}

/// <summary>Turns the live editor document into an immutable snapshot and back. Control UI thread.</summary>
public interface ISnapshotBuilder
{
    /// <summary>Flatten to canonical paragraphs, save as XAML, parse sections, count words.</summary>
    PromptDocumentSnapshot Build(FlowDocument editorDocument, long version, double editorBaseFontSize, string sectionMarker);

    ScriptFile ToScriptFile(FlowDocument editorDocument, string title, double editorBaseFontSize, ScriptOverrides? overrides);

    /// <summary>Replace the content of target with documentXaml via TextRange.Load(DataFormats.Xaml).
    /// Empty string = one empty paragraph.</summary>
    void LoadInto(FlowDocument target, string documentXaml);
}

/// <summary>Section-marker parsing ("## Name"). Pure.</summary>
public interface ISectionParser
{
    /// <summary>True if the text (after leading whitespace) starts with the marker. Name = the rest with
    /// leading '#' and whitespace trimmed; empty name becomes "" (caller numbers it "Section n").</summary>
    bool TryParse(string paragraphText, string marker, out string name);

    IReadOnlyList<SectionInfo> Parse(IReadOnlyList<string> paragraphTexts, string marker);
}

/// <summary>Everything the output document needs, already resolved to WPF values by the renderer.
/// Brushes/colours must be usable on the calling thread (frozen or created there).</summary>
public sealed record OutputTextStyle(
    FontFamily FontFamily,
    FontWeight FontWeight,
    double BaseFontSize,
    double LineSpacing,
    double ParagraphGapLines,
    TextAlignment Alignment,
    Color Ink,
    Color SectionInk,
    Color EndInk,
    Color EndRule,
    bool ShowSectionNames,
    string SectionMarker,
    IReadOnlyDictionary<Color, Color> HighlightInks);

/// <summary>Builds the read-only output/preview FlowDocument from a snapshot.</summary>
public interface IOutputDocumentFactory
{
    /// <summary>
    /// Call on the thread that will own and display the document. Rules:
    /// PagePadding 0; FontFamily/FontWeight/Foreground/LineHeight (BlockLineHeight) from style;
    /// every run FontSize scaled by style.BaseFontSize / snapshot.EditorBaseFontSize; paragraph Margin bottom
    /// = ParagraphGapLines * BaseFontSize * LineSpacing; text inside a known highlight takes HighlightInks[bg];
    /// section paragraphs are kept (1:1 paragraph indices) but restyled: ShowSectionNames ? 0.4 x size,
    /// SectionInk, capitals, marker stripped : text hidden, height 0.5 line; then a generated END block
    /// (blank line + centred "END" at 0.6 x size in EndInk with 2 px EndRule rules at 30 % width), tagged
    /// so IDocumentNavigator skips it.
    /// </summary>
    FlowDocument Create(PromptDocumentSnapshot snapshot, OutputTextStyle style);
}

public sealed record GeometryBuildOptions(string LayoutKey, PromptDocumentSnapshot Snapshot, double BaseLineHeight);

/// <summary>Builds line/paragraph/section tables from a laid-out output document.</summary>
public interface ILayoutGeometryBuilder
{
    /// <summary>
    /// Precondition: the document is hosted (read-only RichTextBox or FlowDocumentScrollViewer, no scrolling,
    /// measured with unconstrained height) and layout has completed at the output text width (host.UpdateLayout()),
    /// with PagePadding 0 so y = 0 is the top of the first block (TextPointer.GetCharacterRect coordinates).
    /// Enumerates visual lines via TextPointer.GetLineStartPosition, records tops/heights/paragraph/char offsets,
    /// paragraph tops (canonical order), section tops and the END block top. O(lines). Owner thread only.
    /// </summary>
    LayoutGeometry Build(FlowDocument laidOutDocument, GeometryBuildOptions options);
}

/// <summary>Relative script sizes (DESIGN.md: the editor size menu offers only these).</summary>
public enum ScriptSize
{
    Small = 0,   // 0.80
    Normal = 1,  // 1.00
    Large = 2,   // 1.25
    Huge = 3,    // 1.50
}

/// <summary>Formatting helpers for the editor toolbar. Control UI thread. Bold/italic/underline use the
/// built-in EditingCommands; these cover what WPF lacks.</summary>
public interface IScriptFormatter
{
    double Factor(ScriptSize size);

    void ApplySize(TextRange range, ScriptSize size, double editorBaseFontSize);

    /// <summary>Null when the range mixes sizes.</summary>
    ScriptSize? GetSize(TextRange range, double editorBaseFontSize);

    void ToggleStrikethrough(TextRange range);

    /// <summary>Null when mixed.</summary>
    bool? GetStrikethrough(TextRange range);

    /// <summary>ink null = remove explicit colour (falls back to the default ink).</summary>
    void ApplyInk(TextRange range, Color? ink);

    /// <summary>highlight null = remove. A highlight carries its own ink (DESIGN.md): highlightInk is applied too.</summary>
    void ApplyHighlight(TextRange range, Color? highlight, Color? highlightInk);

    /// <summary>Remove size, ink, highlight, bold, italic, underline, strikethrough.</summary>
    void ClearFormatting(TextRange range);

    /// <summary>Insert a new paragraph "marker name" before the caret's paragraph (or split at the caret)
    /// and return it.</summary>
    Paragraph InsertSectionMarker(RichTextBox editor, string marker, string name);

    /// <summary>Editor-only look for section paragraphs (muted label + bottom rule). Paragraph-level properties
    /// only; the snapshot builder and output factory ignore/override them.</summary>
    void StyleSectionParagraphs(FlowDocument document, string marker, Brush labelBrush, Brush ruleBrush);
}
