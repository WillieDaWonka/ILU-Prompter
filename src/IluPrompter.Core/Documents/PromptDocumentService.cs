namespace IluPrompter.Core;

/// <summary>Complete in the scaffold (frozen). Thread-safe holder of the current snapshot and file info.</summary>
public sealed class PromptDocumentService : IPromptDocumentService
{
    private readonly object _gate = new();
    private PromptDocumentSnapshot _current = PromptDocumentSnapshot.Empty;
    private long _version;

    public PromptDocumentSnapshot Current => Volatile.Read(ref _current);
    public string? FilePath { get; private set; }
    public string Title { get; private set; } = "Untitled";
    public bool IsDirty { get; private set; }
    public DateTimeOffset? LastSaved { get; private set; }

    public event EventHandler<PromptDocumentSnapshot>? SnapshotPublished;
    public event EventHandler? FileInfoChanged;

    public long NextVersion() => Interlocked.Increment(ref _version);

    public void Publish(PromptDocumentSnapshot snapshot)
    {
        lock (_gate)
        {
            if (snapshot.Version <= _current.Version) return;
            Volatile.Write(ref _current, snapshot);
        }

        SnapshotPublished?.Invoke(this, snapshot);
    }

    public void SetFileInfo(string? filePath, string title, bool isDirty, DateTimeOffset? lastSaved)
    {
        lock (_gate)
        {
            FilePath = filePath;
            Title = title;
            IsDirty = isDirty;
            LastSaved = lastSaved;
        }

        FileInfoChanged?.Invoke(this, EventArgs.Empty);
    }
}
