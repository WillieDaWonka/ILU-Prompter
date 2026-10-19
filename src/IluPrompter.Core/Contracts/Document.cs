namespace IluPrompter.Core;

/// <summary>A section marker paragraph ("## Name") found in the script.</summary>
public sealed record SectionInfo(int Index, string Name, int ParagraphIndex);

/// <summary>
/// An immutable, thread-agnostic copy of the script: FlowDocument XAML as produced by
/// TextRange.Save(DataFormats.Xaml) over a flattened document (paragraphs only, canonical order).
/// Each WPF thread that renders it loads its own FlowDocument from this string.
/// </summary>
public sealed record PromptDocumentSnapshot(
    long Version,
    string FlowDocumentXaml,
    double EditorBaseFontSize,
    int ParagraphCount,
    IReadOnlyList<SectionInfo> Sections,
    int WordCount)
{
    public static PromptDocumentSnapshot Empty { get; } =
        new(0, "", 20.0, 0, Array.Empty<SectionInfo>(), 0);
}

/// <summary>
/// Holds the current snapshot and file metadata. The editor (App) owns the live FlowDocument and
/// publishes snapshots here (debounced 250 ms); renderers subscribe.
/// </summary>
public interface IPromptDocumentService
{
    PromptDocumentSnapshot Current { get; }

    string? FilePath { get; }
    string Title { get; }
    bool IsDirty { get; }
    DateTimeOffset? LastSaved { get; }

    /// <summary>Monotonic version source for snapshots (thread-safe).</summary>
    long NextVersion();

    /// <summary>Replace the current snapshot (any thread). Snapshots with Version not greater than the
    /// current one are ignored. Raises SnapshotPublished synchronously on the calling thread.</summary>
    void Publish(PromptDocumentSnapshot snapshot);

    void SetFileInfo(string? filePath, string title, bool isDirty, DateTimeOffset? lastSaved);

    /// <summary>Raised on the publishing thread. Subscribers on other threads marshal and coalesce
    /// (only the newest snapshot matters).</summary>
    event EventHandler<PromptDocumentSnapshot>? SnapshotPublished;

    /// <summary>Raised on the calling thread of SetFileInfo.</summary>
    event EventHandler? FileInfoChanged;
}
