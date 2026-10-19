using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using IluPrompter.Core;
using IluPrompter.Theme;

namespace IluPrompter.Documents.Tests;

public class ScriptFileServiceTests
{
    private readonly ScriptFileService _service = new();

    private static string Write(string extension, byte[] bytes)
    {
        var path = T.TempFile(extension);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static string Write(string extension, string text) => Write(extension, new UTF8Encoding(false).GetBytes(text));

    private static string Hex(string token) => ThemeResources.GetColor(token).ToString();

    // ---- .ilup -------------------------------------------------------------------------------------------------

    private static FlowDocument FormattedEditor()
    {
        var doc = new FlowDocument { FontSize = 20 };
        doc.Blocks.Add(new Paragraph(new Run("## Welcome")));
        var p = new Paragraph();
        p.Inlines.Add(new Run("plain "));
        p.Inlines.Add(new Run("big ") { FontSize = 25, FontWeight = FontWeights.Bold });
        p.Inlines.Add(new Run("marked ") { Background = Styles.Solid("highlight-yellow"), Foreground = Styles.Solid("canvas") });
        p.Inlines.Add(new Run("red") { Foreground = Styles.Solid("ink-red"), FontStyle = FontStyles.Italic });
        p.Inlines.Add(new LineBreak());
        p.Inlines.Add(new Run("struck") { TextDecorations = TextDecorations.Strikethrough });
        doc.Blocks.Add(p);
        doc.Blocks.Add(new Paragraph());
        doc.Blocks.Add(new Paragraph(new Run("Unicode: café, “quotes”, — dash, \U0001F600")));
        return doc;
    }

    [WpfFact]
    public void Ilup_RoundTrip_IsLossless()
    {
        var overrides = new ScriptOverrides { LinesVisible = 6, LineSpacing = 1.4, StartSpeed = 3.5, Margins = new MarginSettings() };
        var file = T.Snapshots.ToScriptFile(FormattedEditor(), "Keynote", 22, overrides);
        var path = T.TempFile(".ilup");

        _service.Save(path, file);
        var loaded = _service.Load(path);

        Assert.Equal(file, loaded);
        Assert.Equal(file.DocumentXaml, loaded.DocumentXaml); // formatting XAML byte for byte
        // And once more through the editor: load into a document and save it again.
        var editor = T.Load(loaded.DocumentXaml);
        editor.FontSize = 20;
        var again = T.Snapshots.ToScriptFile(editor, loaded.Title, loaded.EditorBaseFontSize, loaded.Overrides);
        Assert.Equal(T.Texts(FormattedEditor()), T.Texts(editor));
        Assert.Equal(25, T.RunWithText(editor, "big").FontSize);
        Assert.Equal(FontWeights.Bold, T.RunWithText(editor, "big").FontWeight);
        Assert.Equal(FontStyles.Italic, T.RunWithText(editor, "red").FontStyle);
        Assert.Equal(Hex("highlight-yellow"), T.ColourOf(T.RunWithText(editor, "marked").Background));
        Assert.Equal(Hex("canvas"), T.ColourOf(T.RunWithText(editor, "marked").Foreground));
        Assert.Contains(T.RunWithText(editor, "struck").TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);
        Assert.Equal(file.DocumentXaml, again.DocumentXaml);
    }

    [WpfFact]
    public void Ilup_IsUtf8Json_WithCamelCaseNames()
    {
        var path = T.TempFile(".ilup");

        _service.Save(path, new ScriptFile { Title = "T", DocumentXaml = "", EditorBaseFontSize = 20 });

        var bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB, "no BOM");
        var json = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"formatVersion\": 1", json);
        Assert.Contains("\"title\": \"T\"", json);
        Assert.Contains("\"documentXaml\"", json);
        Assert.Contains("\"editorBaseFontSize\": 20", json);
    }

    [WpfFact]
    public void Ilup_Save_IsAtomic_KeepsABackup_AndLeavesNoTempFile()
    {
        var path = T.TempFile(".ilup");
        _service.Save(path, new ScriptFile { Title = "first" });

        _service.Save(path, new ScriptFile { Title = "second" });

        Assert.Equal("second", _service.Load(path).Title);
        Assert.True(File.Exists(path + ".bak"));
        Assert.Contains("first", File.ReadAllText(path + ".bak"));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [WpfFact]
    public void Ilup_Save_IntoAMissingFolder_ThrowsAFriendlyError()
    {
        var path = Path.Combine(T.TempFile(""), "nope", "x.ilup");

        var ex = Assert.Throws<ScriptFileException>(() => _service.Save(path, new ScriptFile()));

        Assert.Contains("x.ilup", ex.Message);
    }

    [WpfFact]
    public void Ilup_Load_RejectsMaliciousXamlBeforeAnythingIsParsedByWpf()
    {
        var evil = new ScriptFile { Title = "evil", DocumentXaml = "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Button Content=\"x\"/></Section>" };
        var path = T.TempFile(".ilup");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(evil, IluJson.Options));

        Assert.Throws<ScriptFileException>(() => _service.Load(path));
    }

    [WpfTheory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("null")]
    [InlineData("{\"formatVersion\": 99, \"title\": \"future\"}")]
    [InlineData("{\"formatVersion\": 0}")]
    public void Ilup_Load_RejectsBadFiles_WithAUserMessage(string json)
    {
        var path = Write(".ilup", json);

        var ex = Assert.Throws<ScriptFileException>(() => _service.Load(path));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [WpfFact]
    public void Ilup_Load_RepairsMissingOrNonsenseFields()
    {
        var path = Write(".ilup", "{\"formatVersion\": 1, \"title\": \"\", \"editorBaseFontSize\": -4}");

        var file = _service.Load(path);

        Assert.Equal("Untitled", file.Title);
        Assert.Equal(20, file.EditorBaseFontSize);
        Assert.Equal("", file.DocumentXaml);
    }

    [WpfFact]
    public void Load_MissingFile_UnknownExtension_AndHugeFiles_AreFriendly()
    {
        Assert.Contains("could not be found", Assert.Throws<ScriptFileException>(() => _service.Load(T.TempFile(".txt"))).Message);
        Assert.Throws<ScriptFileException>(() => _service.Load(Write(".pdf", "x")));
        Assert.Throws<ScriptFileException>(() => _service.Load(Write("", "x")));
    }

    // ---- .txt --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("one\r\ntwo\r\nthree")]
    [InlineData("one\ntwo\nthree")]
    [InlineData("one\rtwo\rthree")]
    [InlineData("one\ntwo\r\nthree\n")]
    public void Txt_LineEndings_GiveOneParagraphPerLine(string text)
    {
        Assert.Equal(["one", "two", "three"], TextImporter.SplitLines(text));
    }

    [WpfFact]
    public void Txt_EmptyLinesAreKept_AndEmptyFileIsOneEmptyParagraph()
    {
        var file = _service.Load(Write(".txt", "a\r\n\r\n\r\nb\r\n"));
        Assert.Equal(["a", "", "", "b"], T.Texts(T.Load(file.DocumentXaml)));

        var empty = _service.Load(Write(".txt", ""));
        Assert.Equal([""], T.Texts(T.Load(empty.DocumentXaml)));
    }

    public static IEnumerable<object[]> Encodings()
    {
        const string text = "café “quoted” — €5\nsecond ü";
        yield return ["utf8", new UTF8Encoding(false).GetBytes(text), text];
        yield return ["utf8-bom", new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray(), text];
        yield return ["utf16le-bom", new UnicodeEncoding(false, true).GetPreamble().Concat(new UnicodeEncoding(false, false).GetBytes(text)).ToArray(), text];
        yield return ["utf16be-bom", new UnicodeEncoding(true, true).GetPreamble().Concat(new UnicodeEncoding(true, false).GetBytes(text)).ToArray(), text];
        yield return ["utf32le-bom", new UTF32Encoding(false, true).GetPreamble().Concat(new UTF32Encoding(false, false).GetBytes(text)).ToArray(), text];
    }

    [WpfTheory]
    [MemberData(nameof(Encodings))]
    public void Txt_DecodesByteOrderMarks_AndPlainUtf8(string label, byte[] bytes, string text)
    {
        var file = _service.Load(Write(".txt", bytes));

        Assert.Equal(text.Split('\n').ToList(), T.Texts(T.Load(file.DocumentXaml)));
        Assert.False(string.IsNullOrEmpty(label));
    }

    [WpfFact]
    public void Txt_WithoutBomAndNotUtf8_FallsBackToTheLegacyEightBitEncoding()
    {
        // 0xE9 = e-acute and 0x93/0x94 = curly quotes in Windows-1252 (Latin1 agrees on the first).
        byte[] bytes = [(byte)'c', (byte)'a', (byte)'f', 0xE9, (byte)' ', 0x93, (byte)'q', 0x94];

        var file = _service.Load(Write(".txt", bytes));

        Assert.Equal(["café “q”"], T.Texts(T.Load(file.DocumentXaml)));
    }

    [WpfFact]
    public void Txt_ControlCharactersAreDropped_SoTheDocumentAlwaysSaves()
    {
        var file = _service.Load(Write(".txt", "a\u0000b\u0001c\td\u000B"));

        Assert.Equal(["abc\td"], T.Texts(T.Load(file.DocumentXaml)));
    }

    [WpfFact]
    public void Txt_TitleIsTheFileName_AndMarkersBecomeSections()
    {
        var path = Write(".txt", "intro\n## First part\ntext\n## Second\nmore");
        var renamed = Path.Combine(Path.GetDirectoryName(path)!, "My Keynote.txt");
        File.Move(path, renamed, overwrite: true);

        var file = _service.Load(renamed);

        Assert.Equal("My Keynote", file.Title);
        var snapshot = T.Snapshots.Build(T.Load(file.DocumentXaml), 1, file.EditorBaseFontSize, "##");
        Assert.Equal(["First part", "Second"], snapshot.Sections.Select(s => s.Name));
    }

    // ---- .rtf --------------------------------------------------------------------------------------------------

    private const string RtfFixture =
        @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}{\f1 Courier New;}}" +
        @"{\colortbl;\red255\green0\blue0;\red0\green0\blue255;\red255\green255\blue0;\red0\green0\blue0;}" +
        @"\pard\f0\fs24 Plain words here. \b Bold\b0 , \i italic\i0 , \ul underlined\ulnone , \strike struck\strike0 .\par" +
        @"\pard\fs48 Big words\fs24\par" +
        @"\pard\fs18 Small words\fs24\par" +
        @"\pard\cf1 Red words\cf4  and black words\cf0\par" +
        @"\pard\highlight3 Highlighted words\highlight0\par" +
        @"\pard\qc\f1 Centred courier\par" +
        @"\pard\ql\par" +
        @"\pard Last line\par}";

    [WpfFact]
    public void Rtf_KeepsBoldItalicUnderlineStrikeInkHighlightAndRelativeSize()
    {
        var file = _service.Load(Write(".rtf", RtfFixture));
        var doc = T.Load(file.DocumentXaml);

        Assert.Equal(FontWeights.Bold, T.RunWithText(doc, "Bold").FontWeight);
        Assert.Equal(FontStyles.Italic, T.RunWithText(doc, "italic").FontStyle);
        Assert.Contains(T.RunWithText(doc, "underlined").TextDecorations, d => d.Location == TextDecorationLocation.Underline);
        Assert.Contains(T.RunWithText(doc, "struck").TextDecorations, d => d.Location == TextDecorationLocation.Strikethrough);
        Assert.Equal(Hex("ink-red"), T.ColourOf(T.RunWithText(doc, "Red words").Foreground));
        var marked = T.RunWithText(doc, "Highlighted");
        Assert.Equal(Hex("highlight-yellow"), T.ColourOf(marked.Background));
        Assert.Equal(Hex("canvas"), T.ColourOf(marked.Foreground)); // the highlight's own ink
        Assert.Equal(20 * 1.5, T.RunWithText(doc, "Big words").FontSize, 6); // 24pt body, 48pt text: Huge
        Assert.Equal(20 * 0.8, T.RunWithText(doc, "Small words").FontSize, 6); // 9pt: Small
        Assert.Equal(20, file.EditorBaseFontSize);
    }

    [WpfFact]
    public void Rtf_NormalisesAway_FontsAutomaticColoursAndNormalSizes()
    {
        var doc = T.Load(_service.Load(Write(".rtf", RtfFixture)).DocumentXaml);

        var plain = T.RunWithText(doc, "Plain words");
        Assert.Equal(DependencyProperty.UnsetValue, plain.ReadLocalValue(TextElement.FontFamilyProperty));
        Assert.Equal(DependencyProperty.UnsetValue, plain.ReadLocalValue(TextElement.FontSizeProperty));
        Assert.Equal(DependencyProperty.UnsetValue, plain.ReadLocalValue(TextElement.FontWeightProperty));
        Assert.Equal(DependencyProperty.UnsetValue, plain.ReadLocalValue(TextElement.ForegroundProperty));
        var black = T.Runs(doc).First(r => r.Text.Contains("black words", StringComparison.Ordinal));
        Assert.Equal(DependencyProperty.UnsetValue, black.ReadLocalValue(TextElement.ForegroundProperty)); // black = automatic
        Assert.All(T.Runs(doc), r => Assert.Equal(DependencyProperty.UnsetValue, r.ReadLocalValue(TextElement.FontFamilyProperty)));
        Assert.All(T.Navigator.GetParagraphs(doc), p =>
        {
            Assert.Equal(DependencyProperty.UnsetValue, p.ReadLocalValue(Block.MarginProperty));
            Assert.Equal(DependencyProperty.UnsetValue, p.ReadLocalValue(TextElement.FontFamilyProperty));
        });
    }

    [WpfFact]
    public void Rtf_KeepsCentredParagraphs_AndEmptyOnes()
    {
        var doc = T.Load(_service.Load(Write(".rtf", RtfFixture)).DocumentXaml);
        var paragraphs = T.Navigator.GetParagraphs(doc);

        var centred = paragraphs.Single(p => T.Text(p).StartsWith("Centred", StringComparison.Ordinal));
        Assert.Equal(TextAlignment.Center, centred.TextAlignment);
        Assert.Equal(DependencyProperty.UnsetValue, paragraphs[0].ReadLocalValue(Block.TextAlignmentProperty));
        Assert.Contains("", T.Texts(doc));
        Assert.Equal("Last line", T.Texts(doc).Last(t => t.Length > 0));
    }

    [WpfFact]
    public void Rtf_Tables_AndLists_BecomeParagraphs()
    {
        var rtf = @"{\rtf1\ansi{\*\listtable{\list\listtemplateid1{\listlevel\levelnfc23{\leveltext\'01\u-3913 ?;}{\levelnumbers;}}\listid1}}{\*\listoverridetable{\listoverride\listid1\ls1}}" +
                  @"\pard{\listtext\u-3913 ?\tab}\ls1 first item\par\pard{\listtext\u-3913 ?\tab}\ls1 second item\par" +
                  @"\trowd\cellx3000\cellx6000\pard\intbl left cell\cell\pard\intbl right cell\cell\row" +
                  @"\pard after\par}";

        var doc = T.Load(_service.Load(Write(".rtf", rtf)).DocumentXaml);

        var texts = T.Texts(doc);
        Assert.All(doc.Blocks, b => Assert.IsType<Paragraph>(b));
        Assert.Contains(texts, t => t.EndsWith("first item", StringComparison.Ordinal));
        Assert.True(texts.IndexOf("left cell") < texts.IndexOf("right cell"));
        Assert.Equal("after", texts[^1]);
    }

    [WpfFact]
    public void Rtf_GarbageIsRejected()
    {
        Assert.Throws<ScriptFileException>(() => _service.Load(Write(".rtf", "this is plain text, not rtf")));
        Assert.Throws<ScriptFileException>(() => _service.Load(Write(".rtf", "")));
    }

    // ---- export ------------------------------------------------------------------------------------------------

    [WpfFact]
    public void ExportTxt_JoinsParagraphsWithCrLf_AndReimportsToTheSameText()
    {
        var doc = T.Doc("alpha", "", "gamma — café");
        var path = T.TempFile(".txt");

        _service.Export(path, doc);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3).Select(b => (int)b));
        Assert.Equal("alpha\r\n\r\ngamma — café", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        Assert.Equal(T.Texts(doc), T.Texts(T.Load(_service.Load(path).DocumentXaml)));
    }

    [WpfFact]
    public void ExportRtf_WritesRtf_ThatImportsBackWithFormatting()
    {
        var doc = new FlowDocument();
        var p = new Paragraph(new Run("normal "));
        p.Inlines.Add(new Run("bold") { FontWeight = FontWeights.Bold });
        doc.Blocks.Add(p);
        var path = T.TempFile(".rtf");

        _service.Export(path, doc);

        Assert.StartsWith("{\\rtf", File.ReadAllText(path));
        var back = T.Load(_service.Load(path).DocumentXaml);
        Assert.Equal(["normal bold"], T.Texts(back));
        Assert.Equal(FontWeights.Bold, T.RunWithText(back, "bold").FontWeight);
    }

    [WpfFact]
    public void Export_RejectsOtherExtensions_AndLeavesNoTempFile()
    {
        var path = T.TempFile(".docx");

        Assert.Throws<ScriptFileException>(() => _service.Export(path, T.Doc("x")));

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Extensions_AreAsContracted()
    {
        Assert.Equal([".ilup", ".rtf", ".txt", ".docx"], _service.OpenExtensions);
        Assert.Equal([".rtf", ".txt"], _service.ExportExtensions);
    }

    // ---- security ----------------------------------------------------------------------------------------------

    [Fact]
    public void NoSourceFile_CallsXamlReaderOnFileData()
    {
        var src = Path.Combine(T.RepoRoot(), "src", "IluPrompter.Documents");
        var offenders = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, line: line.Trim(), i)))
            .Where(x => !x.line.StartsWith("//", StringComparison.Ordinal) && x.line.Contains("XamlReader", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offenders);
    }
}
