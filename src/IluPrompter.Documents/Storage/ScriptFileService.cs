using System.IO;
using System.Text.Json;
using System.Windows.Documents;
using IluPrompter.Core;

namespace IluPrompter.Documents;

/// <summary>STUB (stream E replaces the body). .ilup load/save and plain .txt import only.</summary>
public sealed class ScriptFileService : IScriptFileService
{
    public IReadOnlyList<string> OpenExtensions { get; } = [".ilup", ".rtf", ".txt", ".docx"];

    public IReadOnlyList<string> ExportExtensions { get; } = [".rtf", ".txt"];

    public ScriptFile Load(string path)
    {
        try
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".ilup":
                    return JsonSerializer.Deserialize<ScriptFile>(File.ReadAllText(path), IluJson.Options)
                        ?? throw new ScriptFileException("The script file is empty.");
                case ".txt":
                    var doc = new FlowDocument();
                    foreach (var line in File.ReadAllLines(path)) doc.Blocks.Add(new Paragraph(new Run(line)));
                    return new ScriptFile { Title = Path.GetFileNameWithoutExtension(path), DocumentXaml = SnapshotBuilder.SaveXaml(doc) };
                default:
                    throw new ScriptFileException("This file type can't be opened yet.");
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new ScriptFileException("The script could not be opened: " + ex.Message, ex);
        }
    }

    public void Save(string path, ScriptFile file) =>
        File.WriteAllText(path, JsonSerializer.Serialize(file, IluJson.Options));

    public void Export(string path, FlowDocument document) =>
        throw new ScriptFileException("Export is not available yet.");
}
