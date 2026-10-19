# Stream E: Document model and storage

Branch: `worktree-agent-a2b8545be18f247e1` (from the scaffold commit). Owned paths: `src/IluPrompter.Documents/**`
(except `Contracts/**` and the csproj), `tests/IluPrompter.Documents.Tests/**`, `samples/**`, this file.
Nothing frozen was edited. No `STUB` marker is left in the project.

## Status

| Piece | State |
|---|---|
| `DocumentNavigator` (canonical order, anchors, generated-tag skipping) | done |
| `SectionParser` | done |
| `XamlSanitizer` (new, public) | done |
| `SnapshotBuilder` (+ internal `DocumentFlattener`) | done |
| `ScriptFileService`: `.ilup` JSON, `.txt`, `.rtf`, `.docx` import, `.rtf` / `.txt` export | done |
| `OutputDocumentFactory` | done |
| `LayoutGeometryBuilder` | done (30 ms target not met, see Performance) |
| `ScriptFormatter` | done |
| `samples/` (`sample-short.txt`, `sample-20-pages.txt`) | done |

Tests: 151 in `tests/IluPrompter.Documents.Tests`, all `[WpfFact]`/`[WpfTheory]` where WPF is needed.

## What the other streams need to know

- **D (editor).** `ISnapshotBuilder.LoadInto`, `IScriptFileService.Load` and `ISnapshotBuilder.Build` throw
  `ScriptFileException` with a message that is fit to show. A rejected script leaves the target document untouched.
  Call `Build` for the snapshot (about 25 ms for 6,000 words) and `ToScriptFile` for saving. `ToScriptFile` keeps
  lists and tables but drops images and other embedded UI. Hyperlinks are saved but reopen as plain text.
  `StyleSectionParagraphs` is idempotent (it only writes what differs), so calling it after every change adds nothing to
  the undo stack. `InsertSectionMarker` is one undo step and leaves the name selected.
- **D (editor).** WPF's own underline toggle replaces a run's whole `TextDecorations`, so it also removes a strikethrough
  on the same run. `ToggleStrikethrough` preserves an underline. A custom underline toggle would fix the reverse case.
- **C (rendering).** `IOutputDocumentFactory.Create` must be called on the thread that will own the document. The
  document ends with a tagged `BlockUIContainer` (the END marker). Host it in a read-only `RichTextBox` or a
  `FlowDocumentScrollViewer` (both are tested) with padding 0 and no scrolling, and keep `FlowDocument.FontFamily` as
  the factory set it: `LayoutGeometryBuilder` reads the font's baseline ratio from the document.
  Geometry is exact to about 0.1 px for lines that start in the base font; a line that starts with a smaller run than
  its tallest run can be off by a few pixels, which the reading line cannot show.
- **C/A (END).** WPF collapses the last paragraph's bottom margin into the END block's top margin, so the gap between
  the last line and END is exactly one line. `EndTop` is the top of that block; `DocumentHeight = EndTop + one line`.
- **Anchors.** `DocumentAnchor` offsets count characters with `DocumentNavigator.TextBetween` (a `LineBreak` is CR LF,
  list markers and table separators are not counted). Paragraph indices are identical in the editor, the snapshot and the
  output. Inside a **list item** the snapshot and output paragraph starts with the marker (`1. ` or a bullet and a space),
  so an offset there is larger by that length than the same caret in the editor. Anything that maps an editor anchor to a
  line (jump to caret) is off by at most the marker length in list items only. Pasting lists is rare for prompter
  scripts; if it matters, the editor can convert pasted lists to paragraphs.

## Decisions and WPF findings (all covered by tests)

1. **`TextRange.Text` is not usable for anchors.** For a paragraph inside a list item it prepends the marker
   (`1.<tab>alpha`), and ranges over table cells include `<tab>` and CR LF separators. `DocumentNavigator.TextBetween`
   walks text runs itself. `GetText`, anchors and the geometry builder's character offsets all use it.
2. **`TextRange.Save(Xaml)` writes the host document's properties on the root `<Section>`** (FontFamily, FontSize,
   Foreground, TextAlignment, about 60 `Typography.*` attributes), and `TextRange.Load` copies the ones that differ from
   the target onto **every paragraph**. That would put the editor's Foreground on every output paragraph. The sanitiser
   drops those root attributes while rebuilding the XAML; the host owns them. The factory also loads into a pristine
   `FlowDocument` and styles afterwards.
3. **The sanitiser rebuilds, it does not only validate.** `XamlSanitizer.Sanitize` reads with `XmlReader` (DTD
   prohibited, no resolver, 64 deep, 64 M characters) and writes a new document from an allow-list of elements
   (FlowDocument, Section, Paragraph, Run, Span, Bold, Italic, Underline, LineBreak, List, ListItem, Table,
   TableRowGroup, TableRow, TableCell, TableColumn) and attributes, so WPF never parses the author's bytes. It rejects
   foreign namespaces and prefixes (`x:`, `clr-namespace`), property-element syntax, any attribute value containing
   `{`, and font families that look like a path or address. `Hyperlink` is rewritten to `Span` (target dropped) because
   `TextRange.Save` writes hyperlinks for pasted web text and the file would otherwise never reopen. Nothing in the
   project calls `XamlReader` (a test scans the source for it).
4. **`TextRange.ApplyPropertyValue` cannot clear a single property** (`UnsetValue` throws, `null` would paint nothing).
   `ScriptFormatter` clears one property by remembering each run's other managed values, calling `ClearAllProperties`,
   and re-applying what differs from the new defaults. `ClearFormatting` is a plain `ClearAllProperties`, so Size returns
   to the inherited editor base (Normal).
5. **Line geometry.** (Integration: the fast path reads WPF's line visuals first, checked against the walk and against
   `GetCharacterRect` for the first and last paragraph; anything unexpected falls back to the walk described here.)
   `GetCharacterRect` gives the first character's font box, not the line box. With
   `BlockLineHeight` WPF scales the baseline proportionally, so the font box starts `b * (L - h)` below the line top with
   `b = FontFamily.Baseline / FontFamily.LineSpacing` (checked to 0.01 px for Segoe UI and Poppins). Every line of a
   paragraph is exactly `L` tall, so one rectangle per paragraph is enough and the other tops are arithmetic.
   Documents without a fixed line height fall back to the rectangles.
6. **Flatten is a deep copy** (`DocumentFlattener.CloneFlattened`), never a Save/Load round trip, so embedded UI cannot
   hurt the serialiser and the live editor document is never modified. Documents that are already flat (the usual case)
   skip the copy.
7. **Text files.** BOM-aware (UTF-8, UTF-16 LE/BE, UTF-32). Without a BOM the bytes are read as UTF-8 if valid, otherwise
   as Windows-1252 (a superset of the Latin1 the plan names for printable text: it also maps the curly quotes, dashes
   and euro sign that Word-exported `.txt` files contain). Control characters XML cannot carry are dropped on import.
8. **Imports are rebuilt, not patched.** RTF goes through WPF's loader and is then rebuilt paragraph by paragraph;
   DOCX is read with the OpenXML SDK. Both produce runs that carry only what the editor offers: Normal-relative size
   (nearest of 0.8 / 1 / 1.25 / 1.5 against the dominant size by character count; Normal stays unset so it inherits the
   editor base), bold, italic, underline, strikethrough, one of the eight inks (black and white text mean "automatic" and
   stay unset so the default-ink setting applies), one of the five highlights written together with its own ink. Fonts,
   spacing and indents are dropped; centred and right alignment survive.
9. **RTF headings are not detected.** WPF's RTF reader discards paragraph style names, so only DOCX headings
   (Title, Heading 1-3, by style id or style name) become `## name` sections. RTF imports keep their text and formatting.
10. **END block** is one `BlockUIContainer` (a grid: margin, 2 px rule, "END", 2 px rule, margin) with a one-line top
    margin, tagged `ilu:generated`. The rules are about 30 % of the space beside the word, not an exact 30 % of the text
    width, because the factory does not know the width.

## Performance (this machine, shared with the other streams' builds, so noisy)

- `Build` snapshot, 6,000 words: about 25 ms. `Create` output document: about 75 ms (most of it WPF's
  `TextRange.Load`; the sanitiser is about 2 ms).
- `LayoutGeometryBuilder.Build` (integration update, 19 Oct 2026): **about 8 to 10 ms for the 1,234-line 20-page sample
  in Release** (7 ms per 1,000 lines; target 30 ms). The builder now reads the laid-out line visuals (one per line, offset
  to its line-box top) and finds each line's start offset from its first glyph run's characters; see "Line geometry"
  below. The old line walk (`GetLineStartPosition`, 45 to 120 ms for the same document) remains as the automatic
  fallback and gives identical tables (`LayoutGeometrySourceTests`). The fast path needs the host to be laid out with
  unconstrained height (the contract precondition): a fixed-height host only has visuals for the visible lines, and the
  builder then falls back to the walk.

## Gaps and notes

- Numbered DOCX lists get a bullet like bulleted ones (the contract says "bullet prefix").
- The "Typography" and "NumberSubstitution" attributes of an unusual document are accepted but not interpreted.
- Pasted images are removed at save/snapshot time; they cannot be shown on the output.
- No CONTRACT-REQUEST is needed. The 30 ms geometry target is met at integration (see Performance); list-item anchors
  are fixed at integration (see `docs/streams/INTEGRATION.md`).
