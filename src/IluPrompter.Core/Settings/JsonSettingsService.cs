using System.Globalization;
using System.Text;
using System.Text.Json;

namespace IluPrompter.Core;

/// <summary>
/// Persists <see cref="PrompterSettings"/> as camelCase JSON (ARCHITECTURE §2.8).
/// Atomic save (tmp file then File.Replace with a .bak), 500 ms debounce after the last Update, corrupt files are
/// set aside as settings.json.bad-yyyyMMddHHmmss, unknown fields are ignored and missing ones take their defaults.
/// Values outside their documented ranges are clamped on load so a hand-edited file cannot produce NaN layouts.
/// </summary>
public sealed class JsonSettingsService : ISettingsService, IDisposable
{
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(500);

    private readonly object _gate = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly IAppLog _log;
    private readonly TimeSpan _debounce;
    private readonly Timer _timer;
    private PrompterSettings _current = PrompterSettings.Default;
    private int _dirty;
    private int _disposed;
    private long _writeCount;

    public JsonSettingsService(IAppLog log)
        : this(log, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ILU Prompter", "settings.json"))
    {
    }

    /// <summary>Tests pass a temp path.</summary>
    public JsonSettingsService(IAppLog log, string settingsPath)
        : this(log, settingsPath, DefaultDebounce)
    {
    }

    /// <summary>Tests shorten the debounce. Internal so that DI keeps using the public constructors.</summary>
    internal JsonSettingsService(IAppLog log, string settingsPath, TimeSpan debounce)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _log = log;
        SettingsPath = settingsPath;
        _debounce = debounce;
        _timer = new Timer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public string SettingsPath { get; }

    public PrompterSettings Current => Volatile.Read(ref _current);

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    /// <summary>Number of times settings.json was actually written (tests assert debouncing with it).</summary>
    internal long WriteCount => Interlocked.Read(ref _writeCount);

    // ---- Load -----------------------------------------------------------------------------------------------

    public void Load()
    {
        PrompterSettings loaded;
        try
        {
            if (!File.Exists(SettingsPath))
            {
                SetCurrent(PrompterSettings.Default);
                return;
            }

            var text = File.ReadAllText(SettingsPath, Encoding.UTF8);
            var parsed = JsonSerializer.Deserialize<PrompterSettings>(text, IluJson.Options)
                ?? throw new JsonException("The settings file does not contain a settings object.");

            if (parsed.SchemaVersion > PrompterSettings.CurrentSchemaVersion)
            {
                _log.Warn("settings", $"settings.json has a newer schema ({parsed.SchemaVersion}); unknown fields are ignored and the file is kept until the next save.");
            }

            loaded = Sanitize(parsed) with { SchemaVersion = PrompterSettings.CurrentSchemaVersion };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException
                                       or InvalidOperationException or ArgumentException or FormatException)
        {
            QuarantineBadFile(ex);
            loaded = PrompterSettings.Default;
        }

        SetCurrent(loaded);
    }

    private void SetCurrent(PrompterSettings value)
    {
        lock (_gate)
        {
            Volatile.Write(ref _current, value);
        }
    }

    private void QuarantineBadFile(Exception reason)
    {
        _log.Warn("settings", "settings.json could not be read, using defaults: " + reason.Message);
        var stamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var target = SettingsPath + ".bad-" + stamp + (attempt == 0 ? "" : "-" + attempt.ToString(CultureInfo.InvariantCulture));
            try
            {
                if (File.Exists(target))
                {
                    continue;
                }

                File.Move(SettingsPath, target);
                _log.Warn("settings", "The unreadable file was moved to " + Path.GetFileName(target) + ".");
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warn("settings", "Could not set the unreadable settings file aside: " + ex.Message);
                return;
            }
        }
    }

    // ---- Update and save ------------------------------------------------------------------------------------

    public void Update(Func<PrompterSettings, PrompterSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        PrompterSettings old;
        PrompterSettings updated;
        lock (_gate)
        {
            old = _current;
            updated = change(old);
            if (updated is null || ReferenceEquals(old, updated))
            {
                return;
            }

            Volatile.Write(ref _current, updated);
        }

        ScheduleSave();
        Changed?.Invoke(this, new SettingsChangedEventArgs(old, updated));
    }

    private void ScheduleSave()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        Volatile.Write(ref _dirty, 1);
        try
        {
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void OnTimer(object? state)
    {
        if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _dirty) == 0)
        {
            return;
        }

        try
        {
            SaveAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _log.Error("settings", "Debounced save failed.", ex);
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }

        Volatile.Write(ref _dirty, 0);
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(WriteCurrent).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>Serialise to settings.json.tmp, then File.Replace (leaving settings.json.bak) or File.Move.
    /// IO problems are logged, never thrown: a failed save must not take the app down.</summary>
    private void WriteCurrent()
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(Current, IluJson.Options);
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tmp = SettingsPath + ".tmp";
            using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(SettingsPath))
            {
                File.Replace(tmp, SettingsPath, SettingsPath + ".bak");
            }
            else
            {
                File.Move(tmp, SettingsPath);
            }

            Interlocked.Increment(ref _writeCount);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _log.Error("settings", "Saving settings.json failed.", ex);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _timer.Dispose();
        if (Volatile.Read(ref _dirty) != 0)
        {
            _saveLock.Wait();
            try
            {
                WriteCurrent();
            }
            finally
            {
                _saveLock.Release();
            }
        }

        _saveLock.Dispose();
    }

    // ---- Sanitising -----------------------------------------------------------------------------------------

    /// <summary>Replace nulls (from "null" in a hand-edited file) with defaults and clamp numbers to their ranges.</summary>
    internal static PrompterSettings Sanitize(PrompterSettings s)
    {
        var scroll = SanitizeScroll(s.Scroll ?? new ScrollSettings());
        return s with
        {
            Output = SanitizeOutput(s.Output ?? new OutputSettings()),
            Margins = SanitizeMargins(s.Margins ?? new MarginSettings()),
            Text = SanitizeText(s.Text ?? new TextSettings()),
            Scroll = scroll,
            Input = SanitizeInput(s.Input ?? new InputSettings()),
            Editor = SanitizeEditor(s.Editor ?? new EditorSettings()),
            ControlWindow = SanitizeWindow(s.ControlWindow ?? new WindowSettings()),
            RecentFiles = s.RecentFiles ?? Array.Empty<string>(),
        };
    }

    private static OutputSettings SanitizeOutput(OutputSettings o) => o with
    {
        LastWidthPx = o.LastWidthPx >= 1 ? o.LastWidthPx : 1920,
        LastHeightPx = o.LastHeightPx >= 1 ? o.LastHeightPx : 1080,
        LastDpiScale = Positive(o.LastDpiScale, 1.0),
        LastRefreshHz = Positive(o.LastRefreshHz, 60.0),
        ReadingLineFraction = Range(o.ReadingLineFraction, 0.10, 0.60, 0.33),
    };

    private static MarginSettings SanitizeMargins(MarginSettings m) => m with
    {
        Left = Range(m.Left, 0.0, MarginMath.MaxFraction, 160.0 / 1920.0),
        Top = Range(m.Top, 0.0, MarginMath.MaxFraction, 64.0 / 1080.0),
        Right = Range(m.Right, 0.0, MarginMath.MaxFraction, 160.0 / 1920.0),
        Bottom = Range(m.Bottom, 0.0, MarginMath.MaxFraction, 64.0 / 1080.0),
    };

    private static TextSettings SanitizeText(TextSettings t) => t with
    {
        FontFace = string.IsNullOrWhiteSpace(t.FontFace) ? "Poppins" : t.FontFace,
        FontWeight = Math.Clamp(t.FontWeight, 100, 900),
        LinesVisible = Range(t.LinesVisible, 4.0, 16.0, 8.0),
        LineSpacing = Range(t.LineSpacing, 1.0, 2.0, 1.3),
        ParagraphGapLines = Range(t.ParagraphGapLines, 0.0, 3.0, 0.5),
        DefaultInk = string.IsNullOrWhiteSpace(t.DefaultInk) ? "ink-white" : t.DefaultInk,
    };

    private static ScrollSettings SanitizeScroll(ScrollSettings s)
    {
        var min = Range(s.MinSpeed, -10.0, 0.0, -10.0);
        var max = Range(s.MaxSpeed, 0.0, 10.0, 10.0);
        if (max <= 0.0)
        {
            max = 10.0;
        }

        var curve = s.Curve ?? new SpeedCurveSettings();
        return s with
        {
            MinSpeed = min,
            MaxSpeed = max,
            StartSpeed = Range(s.StartSpeed, min, max, 4.5),
            SpeedStep = Positive(s.SpeedStep, 0.5),
            FineSpeedStep = Positive(s.FineSpeedStep, 0.1),
            Curve = curve with
            {
                LinesPerMinuteAtMax = Positive(curve.LinesPerMinuteAtMax, 80.0),
                Exponent = Positive(curve.Exponent, 1.3),
            },
        };
    }

    private static InputSettings SanitizeInput(InputSettings i)
    {
        var wheel = i.Wheel ?? new WheelSettings();
        var xbox = i.Xbox ?? new XboxSettings();
        var shuttle = i.Shuttle ?? new ShuttleSettings();
        var keyboard = i.Keyboard ?? new KeyboardSettings();
        return i with
        {
            Wheel = wheel with
            {
                Wheel = wheel.Wheel ?? DefaultBindings.Wheel,
                Buttons = wheel.Buttons ?? DefaultBindings.WheelButtons,
            },
            Xbox = xbox with
            {
                UserIndex = Math.Clamp(xbox.UserIndex, -1, 3),
                Axes = xbox.Axes ?? DefaultBindings.XboxAxes,
                Buttons = xbox.Buttons ?? DefaultBindings.XboxButtons,
            },
            Shuttle = shuttle with
            {
                Axes = shuttle.Axes ?? DefaultBindings.ShuttleAxes,
                Jog = shuttle.Jog ?? DefaultBindings.Jog,
                Buttons = shuttle.Buttons ?? DefaultBindings.ShuttleButtons,
            },
            Keyboard = keyboard with
            {
                Global = keyboard.Global ?? DefaultBindings.KeyboardGlobal,
                Transport = keyboard.Transport ?? DefaultBindings.KeyboardTransport,
            },
        };
    }

    private static EditorSettings SanitizeEditor(EditorSettings e) => e with
    {
        SectionMarker = string.IsNullOrWhiteSpace(e.SectionMarker) ? "##" : e.SectionMarker,
        BaseFontSize = Range(e.BaseFontSize, 8.0, 96.0, 20.0),
        ZoomPercent = Math.Clamp(e.ZoomPercent, 25, 500),
        RecoverySeconds = Math.Max(0, e.RecoverySeconds),
    };

    private static WindowSettings SanitizeWindow(WindowSettings w) => w with
    {
        Width = Positive(w.Width, 1280),
        Height = Positive(w.Height, 800),
    };

    private static double Range(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    private static double Positive(double value, double fallback) =>
        double.IsFinite(value) && value > 0 ? value : fallback;
}
