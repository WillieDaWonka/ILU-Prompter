using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IluPrompter.Core.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ilu-core-tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<JsonSettingsService> _services = [];
    private readonly TestLog _log = new();

    private string SettingsFile => Path.Combine(_dir, "ILU Prompter", "settings.json");

    public void Dispose()
    {
        foreach (var s in _services) { s.Dispose(); }

        try
        {
            if (Directory.Exists(_dir)) { Directory.Delete(_dir, recursive: true); }
        }
        catch (IOException)
        {
        }
    }

    private JsonSettingsService Create(string? path = null, int debounceMs = 100)
    {
        var service = new JsonSettingsService(_log, path ?? SettingsFile, TimeSpan.FromMilliseconds(debounceMs));
        _services.Add(service);
        return service;
    }

    private void WriteRaw(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, text, new UTF8Encoding(false));
    }

    private static string Json(PrompterSettings s) => JsonSerializer.Serialize(s, IluJson.Options);

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var end = Environment.TickCount64 + timeoutMs;
        while (!condition() && Environment.TickCount64 < end)
        {
            Thread.Sleep(10);
        }
    }

    // ---- load ----

    [Fact]
    public void MissingFile_GivesDefaults_AndCreatesNothing()
    {
        var s = Create();
        s.Load();
        Assert.Same(PrompterSettings.Default, s.Current);
        Assert.False(File.Exists(SettingsFile));
        Assert.False(Directory.Exists(Path.GetDirectoryName(SettingsFile)));
    }

    [Fact]
    public async Task DefaultSettings_RoundTripByteIdentical()
    {
        var first = Create();
        await first.SaveAsync();
        var original = File.ReadAllBytes(SettingsFile);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(PrompterSettings.Default, IluJson.Options), original);

        var secondPath = Path.Combine(_dir, "second", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(secondPath)!);
        File.WriteAllBytes(secondPath, original);
        var second = Create(secondPath);
        second.Load();
        await second.SaveAsync();

        Assert.Equal(original, File.ReadAllBytes(secondPath));
        Assert.Equal(Json(PrompterSettings.Default), Json(second.Current));
    }

    [Fact]
    public async Task SavedJson_IsCamelCase_WithTokenNames_AndNoBom()
    {
        var s = Create();
        await s.SaveAsync();
        var bytes = File.ReadAllBytes(SettingsFile);
        Assert.NotEqual(0xEF, bytes[0]); // no UTF-8 BOM
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"schemaVersion\": 1", text);
        Assert.Contains("\"startSpeed\": 4.5", text);
        Assert.Contains("\"defaultInk\": \"ink-white\"", text);
        Assert.Contains("\"PlayPause\"", text);
        Assert.DoesNotMatch(new Regex("#[0-9A-Fa-f]{6}"), text); // colours are token names, never hex
    }

    [Fact]
    public async Task EditedValues_SurviveARestart()
    {
        var a = Create();
        a.Update(s => s with
        {
            Scroll = s.Scroll with { StartSpeed = 6.5, PauseAtSections = true, Acceleration = Acceleration.Quick },
            Margins = MarginMath.SetEdgePixels(MarginMath.SetLinked(s.Margins, true, new PixelSize(1920, 1080)), MarginEdge.Left, 120, new PixelSize(1920, 1080)),
            Input = s.Input with
            {
                SelectedController = ControllerKind.Xbox,
                Xbox = s.Xbox() with { UserIndex = 1 },
                Keyboard = s.Input.Keyboard with
                {
                    Transport = new Dictionary<string, BindingAction> { ["K"] = new(PrompterCommand.SpeedUp, PrompterCommand.LineNext) },
                },
            },
            RecentFiles = ["a.ilup", "b.rtf"],
        });
        await a.SaveAsync();

        var b = Create();
        b.Load();
        Assert.Equal(Json(a.Current), Json(b.Current));
        Assert.Equal(6.5, b.Current.Scroll.StartSpeed);
        Assert.Equal(PrompterCommand.LineNext, b.Current.Input.Keyboard.Transport["K"].LineMode);
        Assert.Equal(["a.ilup", "b.rtf"], b.Current.RecentFiles);
        Assert.True(b.Current.Margins.IsLinked);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("   \n  ")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"scroll\":{\"defaultMode\":\"Sideways\"}}")]
    [InlineData("{\"scroll\":{\"startSpeed\":\"fast\"}}")]
    public void CorruptFile_IsSetAside_AndDefaultsAreUsed(string content)
    {
        WriteRaw(content);
        var s = Create();
        s.Load();

        Assert.Equal(Json(PrompterSettings.Default), Json(s.Current));
        Assert.False(File.Exists(SettingsFile));
        var bad = Directory.GetFiles(Path.GetDirectoryName(SettingsFile)!, "settings.json.bad-*");
        var file = Assert.Single(bad);
        Assert.Matches(new Regex(@"settings\.json\.bad-\d{14}$"), file);
        Assert.Equal(content, File.ReadAllText(file)); // kept as it was, for support
        Assert.Contains(_log.Lines, l => l.StartsWith("WARN [settings]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CorruptFile_ThenSave_WritesAFreshFile()
    {
        WriteRaw("garbage");
        var s = Create();
        s.Load();
        s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 7 } });
        await s.SaveAsync();

        var again = Create();
        again.Load();
        Assert.Equal(7, again.Current.Scroll.StartSpeed);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(SettingsFile)!, "settings.json.bad-*"));
    }

    [Fact]
    public void UnknownFields_AreIgnored_AndMissingFieldsTakeDefaults()
    {
        WriteRaw("""
            {
              "somethingFromTheFuture": { "a": 1 },
              "scroll": { "startSpeed": 6, "newKnob": true },
              "text": { "fontFace": "Arial" }
            }
            """);
        var s = Create();
        s.Load();

        Assert.Equal(6, s.Current.Scroll.StartSpeed);
        Assert.Equal(0.5, s.Current.Scroll.SpeedStep); // missing: default
        Assert.Equal("Arial", s.Current.Text.FontFace);
        Assert.Equal(8, s.Current.Text.LinesVisible);
        Assert.Equal(PrompterCommand.PlayPause, s.Current.Input.Keyboard.Transport["Space"].Display);
        Assert.True(File.Exists(SettingsFile));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(SettingsFile)!, "*.bad-*"));
    }

    [Fact]
    public void NewerSchemaVersion_IsReadLeniently_AndTheFileIsKept()
    {
        WriteRaw("{ \"schemaVersion\": 99, \"scroll\": { \"startSpeed\": 6 }, \"holo\": 1 }");
        var s = Create();
        s.Load();
        Assert.Equal(6, s.Current.Scroll.StartSpeed);
        Assert.True(File.Exists(SettingsFile));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(SettingsFile)!, "*.bad-*"));
        Assert.Contains(_log.Lines, l => l.Contains("newer schema", StringComparison.Ordinal));
    }

    [Fact]
    public void NullSections_AreRepairedWithDefaults()
    {
        WriteRaw("{ \"output\": null, \"input\": { \"keyboard\": null, \"xbox\": { \"buttons\": null } }, \"recentFiles\": null }");
        var s = Create();
        s.Load();
        Assert.NotNull(s.Current.Output);
        Assert.NotNull(s.Current.Input.Keyboard);
        Assert.NotNull(s.Current.Input.Xbox.Buttons);
        Assert.Contains("Space", s.Current.Input.Keyboard.Transport.Keys);
        Assert.NotNull(s.Current.RecentFiles);
    }

    [Fact]
    public void OutOfRangeValues_AreClamped()
    {
        WriteRaw("""
            {
              "output": { "readingLineFraction": 5, "lastWidthPx": 0, "lastDpiScale": -1 },
              "margins": { "left": 0.9, "top": -1 },
              "text": { "linesVisible": 1, "lineSpacing": 9, "fontFace": "" },
              "scroll": { "startSpeed": 50, "minSpeed": -99, "maxSpeed": 99, "speedStep": 0, "curve": { "exponent": 0 } },
              "input": { "xbox": { "userIndex": 9 } },
              "editor": { "zoomPercent": 5, "sectionMarker": "" }
            }
            """);
        var s = Create();
        s.Load();
        var c = s.Current;
        Assert.Equal(0.60, c.Output.ReadingLineFraction);
        Assert.Equal(1920, c.Output.LastWidthPx);
        Assert.Equal(1.0, c.Output.LastDpiScale);
        Assert.Equal(0.40, c.Margins.Left);
        Assert.Equal(0.0, c.Margins.Top);
        Assert.Equal(4.0, c.Text.LinesVisible);
        Assert.Equal(2.0, c.Text.LineSpacing);
        Assert.Equal("Poppins", c.Text.FontFace);
        Assert.Equal(10.0, c.Scroll.StartSpeed);
        Assert.Equal(-10.0, c.Scroll.MinSpeed);
        Assert.Equal(10.0, c.Scroll.MaxSpeed);
        Assert.Equal(0.5, c.Scroll.SpeedStep);
        Assert.Equal(1.3, c.Scroll.Curve.Exponent);
        Assert.Equal(3, c.Input.Xbox.UserIndex);
        Assert.Equal(25, c.Editor.ZoomPercent);
        Assert.Equal("##", c.Editor.SectionMarker);
    }

    [Fact]
    public async Task TheWindowPosition_NaN_RoundTrips()
    {
        var s = Create();
        await s.SaveAsync();
        Assert.Contains("\"NaN\"", File.ReadAllText(SettingsFile));
        var b = Create();
        b.Load();
        Assert.True(double.IsNaN(b.Current.ControlWindow.Left));
    }

    // ---- live panel (DESIGN.md D7): two window settings added with the panel ----

    [Fact]
    public void AnOldSettingsFile_WithoutTheLivePanelFields_LoadsAsControllerAndOpen()
    {
        WriteRaw("""
            { "schemaVersion": 1, "controlWindow": { "left": 10, "top": 20, "width": 1500, "height": 900, "isMaximized": false } }
            """);
        var s = Create();
        s.Load();
        Assert.Equal(LivePanelTab.Controller, s.Current.ControlWindow.LivePanelTab);
        Assert.True(s.Current.ControlWindow.LivePanelOpen);
        Assert.Equal(1500, s.Current.ControlWindow.Width); // the rest of the old file is untouched
        Assert.False(s.Current.ControlWindow.IsMaximized);
    }

    [Fact]
    public async Task TheLivePanelTab_AndHiddenFlag_SurviveARestart_AsNames()
    {
        var a = Create();
        a.Update(s => s with { ControlWindow = s.ControlWindow with { LivePanelTab = LivePanelTab.OutputLayout, LivePanelOpen = false } });
        await a.SaveAsync();
        var text = File.ReadAllText(SettingsFile);
        Assert.Contains("\"livePanelTab\": \"OutputLayout\"", text);
        Assert.Contains("\"livePanelOpen\": false", text);

        var b = Create();
        b.Load();
        Assert.Equal(LivePanelTab.OutputLayout, b.Current.ControlWindow.LivePanelTab);
        Assert.False(b.Current.ControlWindow.LivePanelOpen);
        Assert.Equal(Json(a.Current), Json(b.Current));
    }

    [Fact]
    public void AnOldSettingsFile_WithFieldsNoLongerRead_StillLoads()
    {
        // two window fields an older build wrote and this one no longer has: unknown fields are ignored
        var old1 = "jog" + "FaceStep";
        var old2 = "jog" + "FaceShown";
        WriteRaw("{ \"controlWindow\": { \"" + old1 + "\": 2, \"" + old2 + "\": false, \"width\": 1400, \"livePanelOpen\": false } }");
        var s = Create();
        s.Load();
        Assert.Equal(1400, s.Current.ControlWindow.Width);   // the unknown fields are ignored; the rest of the file is read
        Assert.False(s.Current.ControlWindow.LivePanelOpen);
    }

    [Fact]
    public void AnUnknownTabNumber_FallsBackToController()
    {
        WriteRaw("""{ "controlWindow": { "livePanelTab": 7, "livePanelOpen": false } }""");
        var s = Create();
        s.Load();
        Assert.Equal(LivePanelTab.Controller, s.Current.ControlWindow.LivePanelTab);
        Assert.False(s.Current.ControlWindow.LivePanelOpen);
    }

    // ---- update ----

    [Fact]
    public void Update_RaisesChanged_WithOldAndNew_OnTheCallingThread()
    {
        var s = Create();
        SettingsChangedEventArgs? args = null;
        var thread = -1;
        s.Changed += (_, e) => { args = e; thread = Environment.CurrentManagedThreadId; };
        var before = s.Current;
        s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 3 } });

        Assert.NotNull(args);
        Assert.Same(before, args!.Old);
        Assert.Same(s.Current, args.New);
        Assert.Equal(3, s.Current.Scroll.StartSpeed);
        Assert.Equal(Environment.CurrentManagedThreadId, thread);
    }

    [Fact]
    public void Update_ReturningTheSameInstance_IsANoOp_NoEvent_NoWrite()
    {
        var s = Create(debounceMs: 50);
        var raised = 0;
        s.Changed += (_, _) => raised++;
        s.Update(x => x);
        Thread.Sleep(300);
        Assert.Equal(0, raised);
        Assert.Equal(0, s.WriteCount);
        Assert.False(File.Exists(SettingsFile));
    }

    [Fact]
    public void Update_FromManyThreads_LosesNothing()
    {
        var s = Create(debounceMs: 20);
        var raised = 0;
        s.Changed += (_, _) => Interlocked.Increment(ref raised);
        var threads = Enumerable.Range(0, 8).Select(_ => new Thread(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                s.Update(x => x with { Editor = x.Editor with { ZoomPercent = x.Editor.ZoomPercent + 1 } });
            }
        })).ToArray();
        foreach (var t in threads) { t.Start(); }
        foreach (var t in threads) { t.Join(); }

        Assert.Equal(100 + 1600, s.Current.Editor.ZoomPercent);
        Assert.Equal(1600, raised);
    }

    [Fact]
    public void Changed_IsRaisedOutsideTheLock()
    {
        var s = Create();
        var nested = false;
        s.Changed += (_, e) =>
        {
            if (e.New.Scroll.StartSpeed != 3) { return; }

            // Another thread updating while this handler runs would deadlock if the lock were still held.
            var other = Task.Run(() => s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 4 } }));
            nested = other.Wait(TimeSpan.FromSeconds(5));
        };
        s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 3 } });
        Assert.True(nested);
        Assert.Equal(4, s.Current.Scroll.StartSpeed);
    }

    [Fact]
    public void Changed_HandlerThatUpdatesAgain_OnTheSameThread_Works()
    {
        var s = Create();
        s.Changed += (_, e) =>
        {
            if (e.New.Scroll.StartSpeed == 3) { s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 5 } }); }
        };
        s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 3 } });
        Assert.Equal(5, s.Current.Scroll.StartSpeed);
    }

    // ---- saving ----

    [Fact]
    public async Task FirstSave_CreatesTheDirectoryAndFile_WithoutABakOrTmp()
    {
        var s = Create();
        await s.SaveAsync();
        var dir = Path.GetDirectoryName(SettingsFile)!;
        Assert.True(File.Exists(SettingsFile));
        Assert.False(File.Exists(SettingsFile + ".bak"));
        Assert.False(File.Exists(SettingsFile + ".tmp"));
        Assert.Equal(["settings.json"], Directory.GetFiles(dir).Select(Path.GetFileName));
    }

    [Fact]
    public async Task SecondSave_ReplacesAtomically_AndLeavesTheBak()
    {
        var s = Create();
        s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 7 } });
        await s.SaveAsync();
        s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 8 } });
        await s.SaveAsync();

        Assert.False(File.Exists(SettingsFile + ".tmp"));
        Assert.Contains("\"startSpeed\": 8", File.ReadAllText(SettingsFile));
        Assert.Contains("\"startSpeed\": 7", File.ReadAllText(SettingsFile + ".bak"));
    }

    [Fact]
    public void Debounce_CoalescesTenUpdatesIntoOneWrite()
    {
        var s = Create(debounceMs: 150);
        for (var i = 1; i <= 10; i++)
        {
            var speed = i;
            s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = speed } });
        }

        Assert.Equal(0, s.WriteCount); // nothing written inside the debounce window
        WaitUntil(() => s.WriteCount >= 1);
        Thread.Sleep(500);
        Assert.Equal(1, s.WriteCount);
        Assert.Contains("\"startSpeed\": 10", File.ReadAllText(SettingsFile));
    }

    [Fact]
    public void Debounce_RestartsOnEachUpdate()
    {
        var s = Create(debounceMs: 250);
        for (var i = 1; i <= 4; i++)
        {
            var speed = i;
            s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = speed } });
            Thread.Sleep(100); // each gap is shorter than the debounce, so no write can happen yet
        }

        Assert.Equal(0, s.WriteCount);
        WaitUntil(() => s.WriteCount >= 1);
        Assert.Equal(1, s.WriteCount);
    }

    [Fact]
    public async Task SaveAsync_FlushesPendingChanges_AndCancelsTheDebouncedWrite()
    {
        var s = Create(debounceMs: 200);
        s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 9 } });
        await s.SaveAsync();
        Assert.Equal(1, s.WriteCount);
        Assert.Contains("\"startSpeed\": 9", File.ReadAllText(SettingsFile));
        Thread.Sleep(500);
        Assert.Equal(1, s.WriteCount); // the timer did not write a second time
    }

    [Fact]
    public void Dispose_FlushesAPendingWrite()
    {
        var s = Create(debounceMs: 5000);
        s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 2 } });
        s.Dispose();
        Assert.Contains("\"startSpeed\": 2", File.ReadAllText(SettingsFile));
        s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 3 } }); // after dispose: no timer, no throw
    }

    [Fact]
    public async Task ConcurrentSaves_AreSerialised()
    {
        var s = Create();
        s.Update(x => x with { Scroll = x.Scroll with { StartSpeed = 5 } });
        var tasks = Enumerable.Range(0, 8).Select(_ => s.SaveAsync()).ToArray();
        await Task.WhenAll(tasks);
        Assert.Equal(8, s.WriteCount);
        var again = Create();
        again.Load();
        Assert.Equal(5, again.Current.Scroll.StartSpeed);
    }

    [Fact]
    public async Task SaveFailure_IsLogged_NotThrown()
    {
        // A file where the settings directory should be makes CreateDirectory fail.
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");
        var s = Create(Path.Combine(blocker, "settings.json"));
        await s.SaveAsync();
        Assert.Equal(0, s.WriteCount);
        Assert.Contains(_log.Lines, l => l.StartsWith("ERROR [settings]", StringComparison.Ordinal));
    }

    [Fact]
    public void PublicConstructors_UseTheAppDataLocation_ByDefault()
    {
        var s = new JsonSettingsService(_log);
        _services.Add(s);
        Assert.EndsWith(Path.Combine("ILU Prompter", "settings.json"), s.SettingsPath);
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), s.SettingsPath);
    }
}

internal static class SettingsTestExtensions
{
    public static XboxSettings Xbox(this PrompterSettings s) => s.Input.Xbox;
}
