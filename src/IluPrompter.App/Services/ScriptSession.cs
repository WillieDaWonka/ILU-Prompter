using System.IO;
using System.Windows.Documents;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.App.Services;

/// <summary>What the script session needs from the editor (implemented by EditorController; faked in tests).</summary>
public interface IScriptEditor
{
    FlowDocument Document { get; }

    /// <summary>Editor Normal font size in DIP at 100 % zoom.</summary>
    double BaseFontSize { get; }

    /// <summary>Replace the script. Resets undo, styles section markers and publishes a snapshot at once.</summary>
    void LoadScript(ScriptFile file);

    /// <summary>Raised on the UI thread after each user edit.</summary>
    event EventHandler? ContentChanged;
}

/// <summary>
/// File New / Open / Save / Save as / Export, recent files and the unsaved-changes question.
/// Owns the file info in <see cref="IPromptDocumentService"/> (title, path, dirty flag, last saved).
/// Every failure is shown in the app dialog skin, in plain words; nothing is thrown at the operator.
/// </summary>
public sealed class ScriptSession
{
    public const int MaxRecentFiles = 8;
    public const string DefaultTitle = "Untitled";

    private readonly IScriptFileService _files;
    private readonly ISnapshotBuilder _snapshots;
    private readonly IPromptDocumentService _documents;
    private readonly ISettingsService _settings;
    private readonly IDialogService _dialogs;
    private readonly IFileDialogService _fileDialogs;
    private readonly IAppLog _log;
    private IScriptEditor? _editor;
    private bool _loading;

    public ScriptSession(
        IScriptFileService files,
        ISnapshotBuilder snapshots,
        IPromptDocumentService documents,
        ISettingsService settings,
        IDialogService dialogs,
        IFileDialogService fileDialogs,
        IAppLog log)
    {
        _files = files;
        _snapshots = snapshots;
        _documents = documents;
        _settings = settings;
        _dialogs = dialogs;
        _fileDialogs = fileDialogs;
        _log = log;
    }

    public string Title => _documents.Title;

    public bool IsDirty => _documents.IsDirty;

    /// <summary>Raised on the UI thread after a transient note should show in the status bar ("Saved").</summary>
    public event EventHandler<string>? Note;

    public void Attach(IScriptEditor editor)
    {
        _editor = editor;
        editor.ContentChanged += (_, _) =>
        {
            if (_loading || _documents.IsDirty) return;
            _documents.SetFileInfo(_documents.FilePath, _documents.Title, true, _documents.LastSaved);
        };
    }

    /// <summary>Startup: the most recent script that still exists, else a new one.</summary>
    public void OpenLastOrNew()
    {
        foreach (var path in _settings.Current.RecentFiles)
        {
            if (File.Exists(path) && OpenCore(path, quiet: true)) return;
        }

        NewCore();
    }

    /// <summary>Returns false if you cancelled.</summary>
    public bool New()
    {
        if (!ConfirmCanDiscard()) return false;
        NewCore();
        return true;
    }

    public bool Open()
    {
        if (!ConfirmCanDiscard()) return false;
        var path = _fileDialogs.PickOpenPath();
        return path is not null && OpenCore(path, quiet: false);
    }

    public bool OpenPath(string path)
    {
        if (!ConfirmCanDiscard()) return false;
        if (!File.Exists(path))
        {
            _dialogs.Inform("That script is gone", $"{Path.GetFileName(path)} is no longer there. It has been taken off the recent list.");
            RemoveRecent(path);
            return false;
        }

        return OpenCore(path, quiet: false);
    }

    public bool Save()
    {
        var path = _documents.FilePath;
        return path is not null && string.Equals(Path.GetExtension(path), ".ilup", StringComparison.OrdinalIgnoreCase)
            ? SaveTo(path)
            : SaveAs();
    }

    public bool SaveAs()
    {
        var path = _fileDialogs.PickSavePath(_documents.Title);
        return path is not null && SaveTo(path);
    }

    public void Export()
    {
        if (_editor is null) return;
        var path = _fileDialogs.PickExportPath(_documents.Title);
        if (path is null) return;
        try
        {
            _files.Export(path, _editor.Document);
            Note?.Invoke(this, "Exported " + Path.GetFileName(path));
        }
        catch (ScriptFileException ex)
        {
            _log.Warn("file", "Export failed: " + ex.Message);
            _dialogs.Inform("The script wasn't exported", ex.Message);
        }
    }

    /// <summary>
    /// Asks what to do with unsaved changes. True means carry on (saved, or you chose to discard);
    /// false means stay (cancelled, or the save failed).
    /// </summary>
    public bool ConfirmCanDiscard()
    {
        if (!_documents.IsDirty) return true;
        var choice = _dialogs.Ask(new DialogRequest(
            "Save the changes to " + _documents.Title + "?",
            "Your changes are not saved yet.",
            "Save script",
            "Discard changes",
            "Cancel"));
        return choice switch
        {
            DialogChoice.Primary => Save(),
            DialogChoice.Secondary => true,
            _ => false,
        };
    }

    private void NewCore()
    {
        _loading = true;
        try
        {
            _editor?.LoadScript(new ScriptFile { Title = DefaultTitle, EditorBaseFontSize = _settings.Current.Editor.BaseFontSize });
            _documents.SetFileInfo(null, DefaultTitle, false, null);
        }
        finally
        {
            _loading = false;
        }
    }

    private bool OpenCore(string path, bool quiet)
    {
        if (_editor is null) return false;
        try
        {
            var file = _files.Load(path);
            ApplyOverrides(file.Overrides);
            _loading = true;
            try
            {
                _editor.LoadScript(file);
            }
            finally
            {
                _loading = false;
            }

            var isNative = string.Equals(Path.GetExtension(path), ".ilup", StringComparison.OrdinalIgnoreCase);
            var title = isNative ? Path.GetFileNameWithoutExtension(path) : (string.IsNullOrWhiteSpace(file.Title) ? Path.GetFileNameWithoutExtension(path) : file.Title);
            // An imported file (rtf, txt, docx) has no .ilup home yet: it counts as not saved until you save it.
            _documents.SetFileInfo(isNative ? path : null, title, !isNative, isNative ? File.GetLastWriteTime(path) : null);
            if (isNative) AddRecent(path);
            return true;
        }
        catch (ScriptFileException ex)
        {
            _log.Warn("file", "Open failed: " + ex.Message);
            if (!quiet) _dialogs.Inform("The script wasn't opened", ex.Message);
            return false;
        }
    }

    private bool SaveTo(string path)
    {
        if (_editor is null) return false;
        try
        {
            var title = Path.GetFileNameWithoutExtension(path);
            var overrides = CurrentOverrides();
            var file = _snapshots.ToScriptFile(_editor.Document, title, _editor.BaseFontSize, overrides);
            _files.Save(path, file);
            _documents.SetFileInfo(path, title, false, DateTimeOffset.Now);
            AddRecent(path);
            Note?.Invoke(this, "Saved");
            return true;
        }
        catch (ScriptFileException ex)
        {
            _log.Warn("file", "Save failed: " + ex.Message);
            _dialogs.Inform("The script wasn't saved", ex.Message);
            return false;
        }
    }

    private ScriptOverrides CurrentOverrides()
    {
        var s = _settings.Current;
        return new ScriptOverrides
        {
            LinesVisible = s.Text.LinesVisible,
            LineSpacing = s.Text.LineSpacing,
            Margins = s.Margins,
            StartSpeed = s.Scroll.StartSpeed,
        };
    }

    private void ApplyOverrides(ScriptOverrides? overrides)
    {
        if (overrides is null) return;
        _settings.Update(s => s with
        {
            Text = s.Text with
            {
                LinesVisible = overrides.LinesVisible ?? s.Text.LinesVisible,
                LineSpacing = overrides.LineSpacing ?? s.Text.LineSpacing,
            },
            Margins = overrides.Margins ?? s.Margins,
            Scroll = s.Scroll with { StartSpeed = overrides.StartSpeed ?? s.Scroll.StartSpeed },
        });
    }

    private void AddRecent(string path) =>
        _settings.Update(s =>
        {
            var list = new List<string> { path };
            list.AddRange(s.RecentFiles.Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase)));
            return s with { RecentFiles = list.Take(MaxRecentFiles).ToArray() };
        });

    private void RemoveRecent(string path) =>
        _settings.Update(s => s with
        {
            RecentFiles = s.RecentFiles.Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase)).ToArray(),
        });
}
