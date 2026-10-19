using System.IO;
using Microsoft.Win32;

namespace IluPrompter.App.Services;

/// <summary>
/// The Windows Open and Save dialogs. They follow the Windows theme (DESIGN.md "No white, anywhere" lists them as the
/// one known exception), so the operator laptop should be set to Windows dark mode.
/// </summary>
public interface IFileDialogService
{
    string? PickOpenPath();

    /// <summary>A path for a native .ilup script.</summary>
    string? PickSavePath(string suggestedName);

    /// <summary>A path for an .rtf or .txt export.</summary>
    string? PickExportPath(string suggestedName);
}

public sealed class FileDialogService : IFileDialogService
{
    private static string InitialDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public string? PickOpenPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open script",
            Filter = "Scripts (*.ilup;*.docx;*.rtf;*.txt)|*.ilup;*.docx;*.rtf;*.txt|ILU Prompter scripts (*.ilup)|*.ilup|Word documents (*.docx)|*.docx|Rich text (*.rtf)|*.rtf|Plain text (*.txt)|*.txt",
            CheckFileExists = true,
            InitialDirectory = InitialDirectory,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSavePath(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save script",
            Filter = "ILU Prompter scripts (*.ilup)|*.ilup",
            DefaultExt = ".ilup",
            AddExtension = true,
            FileName = Sanitise(suggestedName),
            InitialDirectory = InitialDirectory,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickExportPath(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export script",
            Filter = "Rich text (*.rtf)|*.rtf|Plain text (*.txt)|*.txt",
            DefaultExt = ".rtf",
            AddExtension = true,
            FileName = Sanitise(suggestedName),
            InitialDirectory = InitialDirectory,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string Sanitise(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length == 0 ? "Untitled" : cleaned;
    }
}
