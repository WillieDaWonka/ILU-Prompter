using System.Text.Json;

namespace IluPrompter.Core;

/// <summary>
/// STUB (stream A replaces the body; keep the type name and constructor signatures).
/// Loads/saves plainly; no debounce, no corrupt-file backup, no atomic replace.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private readonly object _gate = new();
    private readonly IAppLog _log;
    private PrompterSettings _current = PrompterSettings.Default;

    public JsonSettingsService(IAppLog log)
        : this(log, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ILU Prompter", "settings.json"))
    {
    }

    /// <summary>Tests pass a temp path.</summary>
    public JsonSettingsService(IAppLog log, string settingsPath)
    {
        _log = log;
        SettingsPath = settingsPath;
    }

    public string SettingsPath { get; }

    public PrompterSettings Current => Volatile.Read(ref _current);

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<PrompterSettings>(File.ReadAllText(SettingsPath), IluJson.Options);
                Volatile.Write(ref _current, loaded ?? PrompterSettings.Default);
            }
        }
        catch (Exception ex)
        {
            _log.Warn("settings", "Using defaults: " + ex.Message);
            Volatile.Write(ref _current, PrompterSettings.Default);
        }
    }

    public void Update(Func<PrompterSettings, PrompterSettings> change)
    {
        PrompterSettings old;
        PrompterSettings updated;
        lock (_gate)
        {
            old = _current;
            updated = change(old);
            if (ReferenceEquals(old, updated)) return;
            Volatile.Write(ref _current, updated);
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(old, updated));
    }

    public Task SaveAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, IluJson.Options));
        return Task.CompletedTask;
    }
}
