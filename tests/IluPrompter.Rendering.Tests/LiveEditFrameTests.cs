using System.Diagnostics;
using System.IO;
using System.Windows.Documents;
using System.Windows.Threading;
using IluPrompter.Core;
using IluPrompter.Documents;
using Xunit.Abstractions;

namespace IluPrompter.Rendering.Tests;

/// <summary>
/// Measurement, opt-in (<c>ILU_VISUAL_TESTS=1</c> and <c>ILU_PERF=1</c>): the real output host in its dev window
/// (960 x 540 on the primary display) with the real factory, geometry builder and engine, the 20-page sample, playing
/// at speed 3. Phases: quiet play, script edits above the reading line every 600 ms (published like the editor does),
/// then layout changes (lines visible 8 / 7) every 1.5 s. Prints the output thread's frame intervals per phase (from the
/// frame pulse, raised after each moving frame) and the slow-rebuild warnings. No input, no network.
/// Uses only APIs that also exist before the live-edit fix, so the same file measures the old pipeline.
/// </summary>
public class LiveEditFrameTests
{
    private readonly ITestOutputHelper _out;

    public LiveEditFrameTests(ITestOutputHelper output) => _out = output;

    // The test host has no foreground window, so Windows 11 may run it as a background (EcoQoS) process and pace its
    // frames down. Opt this process out, as a foreground app is.
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottlingState info, int size);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    private static void NoPowerThrottling()
    {
        var state = new PowerThrottlingState { Version = 1, ControlMask = 1 | 4, StateMask = 0 }; // execution speed, timer resolution: off
        SetProcessInformation(Process.GetCurrentProcess().Handle, 4 /* ProcessPowerThrottling */, ref state, System.Runtime.InteropServices.Marshal.SizeOf<PowerThrottlingState>());
    }

    [VisualFact]
    public async Task EditsAndLayoutChangesWhilePlaying_FrameIntervals()
    {
        if (Environment.GetEnvironmentVariable("ILU_PERF") != "1") return;
        NoPowerThrottling();

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "IluPrompter.sln"))) dir = dir.Parent;
        var navigator = new DocumentNavigator();
        var snapshots = new SnapshotBuilder(navigator, new SectionParser());
        var file = new ScriptFileService().Load(Path.Combine(dir.FullName, "samples", "sample-20-pages.txt"));
        var editor = new FlowDocument();
        snapshots.LoadInto(editor, file.DocumentXaml);

        var log = new RecordingLog { DiagnosticsEnabled = true };
        var settings = new FakeSettings(PrompterSettings.Default with { Scroll = PrompterSettings.Default.Scroll with { PauseAtSections = false } });
        var documents = new PromptDocumentService();
        var engine = new ScrollEngine(settings);
        var monitors = new FakeMonitors(new MonitorInfo("primary", 1, "Built-in", new PixelRect(0, 0, 1920, 1200), new PixelRect(0, 0, 1920, 1152), 1.0, 60, true, false));
        var host = new OutputHost(engine, documents, settings, monitors, new OutputDocumentFactory(snapshots), new LayoutGeometryBuilder(navigator), log,
            new OutputHostOptions(DevOutputOnPrimary: true));

        void Publish() => documents.Publish(snapshots.Build(editor, documents.NextVersion(), file.EditorBaseFontSize, "##"));

        var started = Stopwatch.StartNew();
        Publish();
        await host.InitializeAsync();
        await host.ShowAsync();
        var lines = 0;
        while (started.Elapsed < TimeSpan.FromSeconds(20))
        {
            lines = host.OutputDispatcher!.Invoke(() => host.Surface?.Geometry?.LineCount ?? 0);
            if (lines > 500) break;
            await Task.Delay(20);
        }

        _out.WriteLine($"render tier {System.Windows.Media.RenderCapability.Tier >> 16}, GC pause so far {GC.GetTotalPauseDuration().TotalMilliseconds:0} ms");
        _out.WriteLine($"first picture on the output after {started.Elapsed.TotalMilliseconds:0} ms ({lines} lines)");

        var stamps = new long[20000];
        var count = 0;
        var recording = false;
        EventHandler pulse = (_, _) =>
        {
            if (Volatile.Read(ref recording) && count < stamps.Length) stamps[count++] = Stopwatch.GetTimestamp();
        };
        ((IFramePulse)host).FramePulse += pulse;

        // The longest operation on the output thread per phase: what an edit or a layout change costs the output itself,
        // whatever the display rate of this machine.
        long opStart = 0, opMax = 0;
        int opsOver8 = 0, opsOver16 = 0;
        var hooks = host.OutputDispatcher!.Hooks;
        DispatcherHookEventHandler opStarted = (_, _) => opStart = Stopwatch.GetTimestamp();
        DispatcherHookEventHandler completed = (_, _) =>
        {
            if (!Volatile.Read(ref recording) || opStart == 0) return;
            var d = Stopwatch.GetTimestamp() - opStart;
            if (d > opMax) opMax = d;
            if (d > Stopwatch.Frequency * 8 / 1000) opsOver8++;
            if (d > Stopwatch.Frequency * 16 / 1000) opsOver16++;
        };
        hooks.OperationStarted += opStarted;
        hooks.OperationCompleted += completed;

        engine.Post(EngineCommand.SeekToOffset(engine.Geometry!.LineTops[200]));
        engine.Post(EngineCommand.SetSpeed(3));
        engine.Post(EngineCommand.Play());
        await Task.Delay(1000);

        async Task<string> Phase(string name, double seconds, Func<int, Task>? step, double every)
        {
            count = 0;
            opMax = 0;
            opsOver8 = opsOver16 = 0;
            Volatile.Write(ref recording, true);
            var clock = Stopwatch.StartNew();
            var gcPause = GC.GetTotalPauseDuration();
            var gcCounts = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
            var n = 0;
            long lastGc = GC.GetGCMemoryInfo(GCKind.Any).Index;
            var maxGcPause = TimeSpan.Zero;
            var allocated = GC.GetTotalAllocatedBytes();
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                if (step is not null && clock.Elapsed.TotalSeconds >= n * every)
                {
                    await step(n);
                    n++;
                }

                var info = GC.GetGCMemoryInfo(GCKind.Any);
                if (info.Index != lastGc)
                {
                    lastGc = info.Index;
                    var pause = info.PauseDurations[0] + info.PauseDurations[1];
                    if (pause > maxGcPause) maxGcPause = pause;
                }

                await Task.Delay(5);
            }

            Volatile.Write(ref recording, false);
            await Task.Delay(50);
            var intervals = new List<double>();
            for (var i = 1; i < count; i++) intervals.Add((stamps[i] - stamps[i - 1]) * 1000.0 / Stopwatch.Frequency);
            intervals.Sort();
            if (intervals.Count == 0) return $"{name}: no frames";
            var over = intervals.Count(v => v > 25.0);
            var line = $"{name} [{engine.Status.Transport}]: {intervals.Count} frames, mean {intervals.Average():0.00} ms, p95 {intervals[(int)(intervals.Count * 0.95)]:0.0}, p99 {intervals[(int)(intervals.Count * 0.99)]:0.0}, max {intervals[^1]:0.0}, over 25 ms {over} ({100.0 * over / intervals.Count:0.00} %), steps {n}, GC pause {(GC.GetTotalPauseDuration() - gcPause).TotalMilliseconds:0} ms (+{GC.CollectionCount(0) - gcCounts.Item1}/{GC.CollectionCount(1) - gcCounts.Item2}/{GC.CollectionCount(2) - gcCounts.Item3})";
            _out.WriteLine(line);
            _out.WriteLine($"    output thread: longest operation {opMax * 1000.0 / Stopwatch.Frequency:0.0} ms, operations over 8 ms {opsOver8}, over 16 ms {opsOver16}; longest GC pause seen {maxGcPause.TotalMilliseconds:0.0} ms; allocated {(GC.GetTotalAllocatedBytes() - allocated) / 1048576.0:0} MB");
            return line;
        }

        await Phase("quiet play", 4, null, 1);

        var inserted = new List<Paragraph>();
        await Phase("edits above the reading line", 12, i =>
        {
            var paragraphs = navigator.GetParagraphs(editor);
            var at = Math.Clamp(engine.Status.ParagraphIndex - 4, 1, paragraphs.Count - 1);
            while (at > 1 && paragraphs[at].Inlines.FirstInline is not Run) at--;
            switch (i % 4)
            {
                case 0:
                {
                    var p = new Paragraph(new Run("A new paragraph typed while live, long enough to wrap onto a second line of the output."));
                    editor.Blocks.InsertBefore(paragraphs[at], p);
                    inserted.Add(p);
                    break;
                }

                case 1:
                    if (paragraphs[at].Inlines.FirstInline is Run run) run.Text += " and some more words typed at the end of it";
                    break;
                case 2:
                    if (inserted.Count > 0)
                    {
                        editor.Blocks.Remove(inserted[^1]);
                        inserted.RemoveAt(inserted.Count - 1);
                    }

                    break;
                default:
                    editor.Blocks.InsertBefore(paragraphs[at], new Paragraph(new Run("Short.")));
                    break;
            }

            Publish();
            return Task.CompletedTask;
        }, 0.6);

        await Phase("layout changes (lines visible 8/7)", 9, i =>
        {
            settings.Update(s => s with { Text = s.Text with { LinesVisible = i % 2 == 0 ? 7 : 8 } });
            return Task.CompletedTask;
        }, 1.5);

        await Phase("quiet play again", 3, null, 1);

        ((IFramePulse)host).FramePulse -= pulse;
        hooks.OperationStarted -= opStarted;
        hooks.OperationCompleted -= completed;
        engine.Post(EngineCommand.Pause());
        foreach (var l in log.Lines.Where(l => l.Contains("Slow rebuild", StringComparison.Ordinal) || l.Contains("[frames", StringComparison.Ordinal) || l.Contains("[layout]", StringComparison.Ordinal) || l.Contains(":install]", StringComparison.Ordinal)).Take(60))
        {
            _out.WriteLine(l);
        }

        await host.ShutdownAsync();
    }
}
