# Stream E: Document model and storage

Branch: `worktree-agent-a2b8545be18f247e1`. Owner paths: `src/IluPrompter.Documents/**` (except `Contracts/**` and the
csproj), `tests/IluPrompter.Documents.Tests/**`, `samples/**`, this file.

## Status

| Piece | State |
|---|---|
| `DocumentNavigator` | done |
| `SectionParser` | done |
| `XamlSanitizer` | done |
| `SnapshotBuilder` (+ `DocumentFlattener`) | done |
| `ScriptFileService` (.ilup / .txt / .rtf / .docx, export) | in progress |
| `OutputDocumentFactory` | pending |
| `LayoutGeometryBuilder` | pending |
| `ScriptFormatter` | pending |
| `samples/` | pending |

## Decisions and WPF findings (verified by tests)

1. **`TextRange.Text` is not usable for anchors.** For a paragraph inside a list item it prepends the marker
   (`1.<tab>alpha`), and ranges over table cells include `<tab>` and CR LF separators. `DocumentNavigator.TextBetween`
   walks text runs itself (a `LineBreak` counts as CR LF, embedded UI as nothing). Everything that counts characters
   (anchors, `GetText`, geometry char offsets) uses it, so offsets agree between the editor document, the snapshot
   document and the output document.
2. **`TextRange.Save(Xaml)` writes the host document's properties on the root `<Section>`** (FontFamily, FontSize,
   Foreground, TextAlignment, 60 `Typography.*` attributes, ...), and `TextRange.Load` copies the ones that differ from
   the target onto **every paragraph**. That would put the editor's Foreground on every output paragraph. The sanitiser
   therefore drops those root attributes while rebuilding the XAML; the host owns them.
3. **Sanitiser rebuilds, it does not just validate.** `XamlSanitizer.Sanitize` reads with `XmlReader` (DTD
   prohibited, no resolver) and writes a new document from an allow-list, so WPF never parses the author's bytes.
   `Hyperlink` is rewritten to `Span` (target dropped) so scripts pasted from the web still open. `LoadInto`
   sanitises first and clears the target only afterwards, so a rejected script leaves the editor untouched.
4. **Flatten is a deep copy** (`DocumentFlattener.CloneFlattened`), never a Save/Load round trip, so embedded UI cannot
   hurt the serialiser and the live editor document is never modified. Already-flat documents (the common case) skip
   the copy.
5. **List anchors.** Paragraph indices are identical between editor and snapshot. Inside a list item the snapshot
   paragraph starts with its marker (`1. ` or a bullet and a space), so character offsets there are larger by the
   marker length.
