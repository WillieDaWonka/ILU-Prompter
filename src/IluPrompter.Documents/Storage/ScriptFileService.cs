using System.IO;
using System.IO.Packaging;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>
/// Open / save / import / export (ARCHITECTURE §2.11 / §4.E). Stateless; control UI thread only (it uses FlowDocument
/// and TextRange).
/// <list type="bullet">
/// <item><c>.ilup</c>: UTF-8 JSON via <see cref="IluJson"/>. Saved atomically (temp file, then <c>File.Replace</c> keeping
/// a <c>.bak</c>). The XAML inside is only validated here; <see cref="ISnapshotBuilder.LoadInto"/> sanitises it again
/// before WPF parses it.</item>
/// <item><c>.txt</c>, <c>.rtf</c>, <c>.docx</c>: imported into a canonical document (see the importers) and returned
/// as a <see cref="ScriptFile"/> with the file name as title.</item>
/// </list>
/// Nothing here ever calls <c>XamlReader</c>. All failures surface as <see cref="ScriptFileException"/> with a message
/// fit to show the user.
/// </summary>
public sealed class ScriptFileService : IScriptFileService
{
    private const long MaxFileBytes = 64L * 1024 * 1024;
    private const double DefaultEditorBase = 20.0;
    private const string BackupExtension = ".bak";

    public IReadOnlyList<string> OpenExtensions { get; } = [".ilup", ".rtf", ".txt", ".docx"];

    public IReadOnlyList<string> ExportExtensions { get; } = [".rtf", ".txt"];

    // ---- load --------------------------------------------------------------------------------------------------

    public ScriptFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (!OpenExtensions.Contains(extension))
            throw new ScriptFileException("ILU Prompter can open .ilup, .rtf, .txt and .docx files, not " + (extension.Length == 0 ? "this file" : extension) + ".");

        var bytes = ReadAllBytes(path);
        try
        {
            switch (extension)
            {
                case ".ilup":
                    return ParseNative(bytes);
                case ".txt":
                    return Imported(path, TextImporter.Import(bytes));
                case ".rtf":
                    return Imported(path, RtfImporter.Import(bytes, DefaultEditorBase));
                default:
                    return Imported(path, DocxImporter.Import(bytes, DefaultEditorBase));
            }
        }
        catch (ScriptFileException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or XmlException or InvalidDataException or FileFormatException
                                       or OpenXmlPackageException or ArgumentException or InvalidOperationException
                                       or NotSupportedException or IOException or System.Windows.Markup.XamlParseException)
        {
            throw new ScriptFileException($"\"{Path.GetFileName(path)}\" could not be read: it is damaged or not a valid {extension} file.", ex);
        }
    }

    private static ScriptFile ParseNative(byte[] bytes)
    {
        ScriptFile? file;
        try
        {
            var json = new UTF8Encoding(false, false).GetString(bytes).TrimStart('﻿');
            file = JsonSerializer.Deserialize<ScriptFile>(json, IluJson.Options);
        }
        catch (JsonException ex)
        {
            throw new ScriptFileException("That file is not a valid ILU Prompter script.", ex);
        }

        if (file is null) throw new ScriptFileException("That script file is empty.");
        if (file.FormatVersion > ScriptFile.CurrentFormatVersion)
            throw new ScriptFileException("That script was saved by a newer version of ILU Prompter. Update the app to open it.");
        if (file.FormatVersion < 1) throw new ScriptFileException("That file is not a valid ILU Prompter script.");

        var xaml = file.DocumentXaml ?? "";
        if (xaml.Length > 0) XamlSanitizer.Validate(xaml); // refuse early; LoadInto sanitises again before WPF sees it

        var baseSize = double.IsFinite(file.EditorBaseFontSize) && file.EditorBaseFontSize > 0 ? file.EditorBaseFontSize : DefaultEditorBase;
        return file with
        {
            Title = string.IsNullOrWhiteSpace(file.Title) ? "Untitled" : file.Title,
            DocumentXaml = xaml,
            EditorBaseFontSize = baseSize,
        };
    }

    private static ScriptFile Imported(string path, FlowDocument document) => new()
    {
        Title = Path.GetFileNameWithoutExtension(path),
        DocumentXaml = SnapshotBuilder.SaveXaml(document),
        EditorBaseFontSize = DefaultEditorBase,
    };

    private static byte[] ReadAllBytes(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) throw new ScriptFileException($"\"{Path.GetFileName(path)}\" could not be found.");
            if (info.Length > MaxFileBytes) throw new ScriptFileException($"\"{Path.GetFileName(path)}\" is too large to open (over 64 MB).");
            return File.ReadAllBytes(path);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ScriptFileException($"ILU Prompter is not allowed to read \"{Path.GetFileName(path)}\".", ex);
        }
        catch (IOException ex)
        {
            throw new ScriptFileException($"\"{Path.GetFileName(path)}\" could not be read: {ex.Message}", ex);
        }
    }

    // ---- save --------------------------------------------------------------------------------------------------

    public void Save(string path, ScriptFile file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(file);
        byte[] bytes;
        try
        {
            bytes = new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(file, IluJson.Options));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            throw new ScriptFileException("The script could not be prepared for saving.", ex);
        }

        WriteAtomically(path, bytes, path + BackupExtension);
    }

    // ---- export ------------------------------------------------------------------------------------------------

    public void Export(string path, FlowDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        byte[] bytes;
        switch (extension)
        {
            case ".rtf":
                try
                {
                    using var stream = new MemoryStream();
                    new TextRange(document.ContentStart, document.ContentEnd).Save(stream, DataFormats.Rtf);
                    bytes = stream.ToArray();
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
                {
                    throw new ScriptFileException("The script could not be converted to RTF.", ex);
                }

                break;
            case ".txt":
                var navigator = new DocumentNavigator();
                var text = string.Join("\r\n", navigator.GetParagraphs(document).Select(navigator.GetText));
                bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
                break;
            default:
                throw new ScriptFileException("ILU Prompter can export .rtf and .txt files, not " + (extension.Length == 0 ? "this file type" : extension) + ".");
        }

        WriteAtomically(path, bytes, backupPath: null);
    }

    // ---- shared ------------------------------------------------------------------------------------------------

    /// <summary>Write next to the target, then swap: a crash or full disk leaves the old file intact.</summary>
    private static void WriteAtomically(string path, byte[] bytes, string? backupPath)
    {
        var temp = path + ".tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.Replace(temp, path, backupPath);
            else File.Move(temp, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            TryDelete(temp);
            throw new ScriptFileException($"\"{Path.GetFileName(path)}\" could not be saved: {ex.Message}", ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best effort: the stray .tmp is harmless
        }
    }
}
