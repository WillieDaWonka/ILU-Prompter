using System.Globalization;

namespace IluPrompter.Core;

/// <summary>Complete in the scaffold (frozen). Appends lines under a lock; never throws.</summary>
public sealed class FileAppLog : IAppLog
{
    private readonly object _gate = new();

    public FileAppLog(bool diagnosticsEnabled)
        : this(diagnosticsEnabled, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ILU Prompter", "logs"))
    {
    }

    public FileAppLog(bool diagnosticsEnabled, string logDirectory)
    {
        DiagnosticsEnabled = diagnosticsEnabled;
        LogDirectory = logDirectory;
    }

    public bool DiagnosticsEnabled { get; }
    public string LogDirectory { get; }

    public void Info(string area, string message) => Write("INFO", area, message);

    public void Warn(string area, string message) => Write("WARN", area, message);

    public void Error(string area, string message, Exception? exception = null) =>
        Write("ERROR", area, exception is null ? message : message + " | " + exception);

    public void Trace(string area, string message)
    {
        if (DiagnosticsEnabled) Write("TRACE", area, message);
    }

    private void Write(string level, string area, string message)
    {
        try
        {
            var now = DateTime.Now;
            var line = string.Create(CultureInfo.InvariantCulture, $"{now:HH:mm:ss.fff} {level} [{area}] {message}{Environment.NewLine}");
            lock (_gate)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(Path.Combine(LogDirectory, $"ilu-prompter-{now:yyyyMMdd}.log"), line);
            }
        }
        catch (IOException)
        {
            // Logging must never take the app down.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
