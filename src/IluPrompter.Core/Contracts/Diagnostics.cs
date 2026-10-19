namespace IluPrompter.Core;

/// <summary>
/// Minimal thread-safe log. File: %LOCALAPPDATA%\ILU Prompter\logs\ilu-prompter-yyyyMMdd.log.
/// Never logs script text or file contents beyond the file name.
/// </summary>
public interface IAppLog
{
    /// <summary>True when started with --diag: enables frame-timing and input trace logs.</summary>
    bool DiagnosticsEnabled { get; }

    string LogDirectory { get; }

    void Info(string area, string message);

    void Warn(string area, string message);

    void Error(string area, string message, Exception? exception = null);

    /// <summary>Only written when DiagnosticsEnabled.</summary>
    void Trace(string area, string message);
}
