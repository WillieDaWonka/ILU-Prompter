using System.Diagnostics;

namespace IluPrompter.Core.Tests;

public class EngineThreadingTests
{
    [Fact]
    public void PostFromFourThreads_WhileTicking_LosesNoCommands()
    {
        const int threads = 4;
        const int perThread = 2000;
        var h = new EngineHarness(TestGeometry.Create(lines: 20000));
        var start = h.Pos;
        using var go = new ManualResetEventSlim();
        var workers = Enumerable.Range(0, threads).Select(_ => new Thread(() =>
        {
            go.Wait();
            for (var i = 0; i < perThread; i++)
            {
                h.Engine.Post(EngineCommand.ScrollPixels(1.0));
            }
        })).ToArray();

        foreach (var w in workers) { w.Start(); }
        go.Set();
        while (workers.Any(w => w.IsAlive))
        {
            h.Step(0.001);
        }

        h.Step(0.001);
        h.Step(0.001);
        Assert.Equal(start + (threads * perThread), h.Pos, 6);
        Assert.Equal(0, h.Engine.DroppedCommands);
    }

    [Fact]
    public void PostFromFourThreads_NudgesCancelOut()
    {
        var h = new EngineHarness();
        using var go = new ManualResetEventSlim();
        var workers = Enumerable.Range(0, 4).Select(_ => new Thread(() =>
        {
            go.Wait();
            for (var i = 0; i < 1000; i++)
            {
                h.Engine.Post(EngineCommand.NudgeSpeed(0.001));
                h.Engine.Post(EngineCommand.NudgeSpeed(-0.001));
            }
        })).ToArray();

        foreach (var w in workers) { w.Start(); }
        go.Set();
        while (workers.Any(w => w.IsAlive))
        {
            h.Step(0.001);
        }

        h.Step(0.001);
        h.Step(0.001);
        Assert.Equal(4.5, h.Status.SetSpeed, 5);
    }

    [Fact]
    public void CommandsFromOneThread_AreAppliedInOrder()
    {
        var h = new EngineHarness();
        for (var i = 0; i < 500; i++)
        {
            h.Engine.Post(EngineCommand.SetSpeed(i % 10));
        }

        h.Step(0.001);
        Assert.Equal(9.0, h.Status.SetSpeed); // 499 % 10
    }

    [Fact]
    public void QueueBeyondCapacity_DropsAndCounts_InsteadOfBlocking()
    {
        var h = new EngineHarness();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 20000; i++)
        {
            h.Engine.Post(EngineCommand.ScrollPixels(0.001));
        }

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(20));
        Assert.True(h.Engine.DroppedCommands > 0);
        h.Step(0.001);
        Assert.True(double.IsFinite(h.Pos));
        // The ring is usable again after a drain.
        var dropped = h.Engine.DroppedCommands;
        h.Engine.Post(EngineCommand.Play());
        h.Step(0.001);
        Assert.Equal(TransportState.Playing, h.Status.Transport);
        Assert.Equal(dropped, h.Engine.DroppedCommands);
    }

    [Fact]
    public void TickIfStale_TicksOnlyAfterFiftyMilliseconds()
    {
        var h = new EngineHarness();
        h.Play();
        var t = h.Now;
        Assert.False(h.Engine.TickIfStale(h.Timestamp(t + 0.040)));
        Assert.True(h.Engine.TickIfStale(h.Timestamp(t + 0.051)));
        Assert.False(h.Engine.TickIfStale(h.Timestamp(t + 0.060))); // only 9 ms after the tick above
        Assert.True(h.Engine.TickIfStale(h.Timestamp(t + 0.110)));
    }

    [Fact]
    public void TickIfStale_AdvancesTheEngineLikeTick()
    {
        var h = new EngineHarness();
        h.Play();
        h.Run(2);
        var p = h.Pos;
        var t = h.Now;
        Assert.True(h.Engine.TickIfStale(h.Timestamp(t + 0.06)));
        Assert.True(h.Pos > p);
    }

    [Fact]
    public void TickIfStale_UsesACustomThreshold_AndFirstCallAlwaysTicks()
    {
        var engine = new ScrollEngine(new FakeSettings());
        var ts = Stopwatch.GetTimestamp();
        Assert.True(engine.TickIfStale(ts));
        Assert.False(engine.TickIfStale(ts + (Stopwatch.Frequency / 100), 0.5));
        Assert.True(engine.TickIfStale(ts + Stopwatch.Frequency, 0.5));
    }

    [Fact]
    public void TickersOnTwoThreads_AreSerialised_AndTimeNeverGoesBackwards()
    {
        var h = new EngineHarness();
        h.Apply(EngineCommand.SetSpeed(10));
        h.Play();
        using var stop = new CancellationTokenSource();
        var errors = new List<string>();
        var baseTs = h.Timestamp(0);
        var counter = 0L;

        void Worker(int offset)
        {
            var last = double.NegativeInfinity;
            while (!stop.IsCancellationRequested)
            {
                var n = Interlocked.Increment(ref counter);
                h.Engine.Tick(baseTs + (n * (Stopwatch.Frequency / 1000)) + offset);
                var p = h.Engine.ReadPosition();
                if (!double.IsFinite(p))
                {
                    lock (errors) { errors.Add("non-finite " + p); }
                }

                if (p < last - 1e-9 && h.Status.Transport != TransportState.AtEnd)
                {
                    // Position may only move backwards via commands; none are posted here.
                    lock (errors) { errors.Add($"went backwards {last} -> {p}"); }
                }

                last = Math.Max(last, p);
            }
        }

        var a = new Thread(() => Worker(0));
        var b = new Thread(() => Worker(1));
        a.Start();
        b.Start();
        Thread.Sleep(200);
        stop.Cancel();
        a.Join();
        b.Join();
        Assert.Empty(errors);
    }

    [Fact]
    public void SetLayout_FromAnotherThread_WhileTicking_IsSafe()
    {
        var h = new EngineHarness(TestGeometry.Create(key: "a"));
        h.Play();
        using var stop = new CancellationTokenSource();
        var failures = new List<string>();
        var layouts = new Thread(() =>
        {
            var i = 0;
            while (!stop.IsCancellationRequested)
            {
                var g = TestGeometry.Create(lines: 80 + (i % 40), lineHeight: 100 + (i % 7 * 10), key: "L" + i);
                h.Engine.SetLayout(g, TestGeometry.Viewport(250 + (i % 5 * 20)));
                i++;
            }
        });
        layouts.Start();
        var end = Stopwatch.StartNew();
        while (end.ElapsedMilliseconds < 300)
        {
            h.Step(1.0 / 120);
            var p = h.Pos;
            if (!double.IsFinite(p))
            {
                failures.Add("non-finite");
            }

            _ = h.Engine.Status;
            _ = h.Engine.Geometry;
        }

        stop.Cancel();
        layouts.Join();
        Assert.Empty(failures);
    }

    [Fact]
    public void SteadyStateTick_Allocates_ZeroBytes()
    {
        var h = new EngineHarness(HugeLines());
        h.Play();
        h.Run(3);
        AssertZeroAllocation(() => h.Step(1.0 / 60));
        Assert.Equal(TransportState.Playing, h.Status.Transport);
    }

    [Fact]
    public void SteadyStateTick_WhilePaused_Allocates_ZeroBytes()
    {
        var h = new EngineHarness(HugeLines());
        AssertZeroAllocation(() => h.Step(1.0 / 60));
    }

    [Fact]
    public void SteadyStateTick_WithAxisInput_Allocates_ZeroBytes()
    {
        var h = new EngineHarness(HugeLines());
        h.Apply(EngineCommand.SetAxisSpeed(0, 3));
        h.Run(3);
        AssertZeroAllocation(() => h.Step(1.0 / 60));
    }

    [Fact]
    public void TickWithQueuedCommands_Allocates_ZeroBytes()
    {
        var h = new EngineHarness(HugeLines());
        AssertZeroAllocation(() =>
        {
            h.Engine.Post(EngineCommand.ScrollPixels(0.25));
            h.Engine.Post(EngineCommand.NudgeSpeed(0));
            h.Step(1.0 / 60);
        });
    }

    /// <summary>
    /// Warms up, then measures several windows of 1,000 ticks with GC.GetAllocatedBytesForCurrentThread. A real
    /// per-tick allocation shows in every window; the runtime's own one-off work (tier-up, type loads) can land in
    /// at most a window or two, so the quietest window must be exactly zero.
    /// </summary>
    private static void AssertZeroAllocation(Action tick)
    {
        for (var i = 0; i < 3000; i++) { tick(); }

        var quietest = long.MaxValue;
        for (var window = 0; window < 8 && quietest != 0; window++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                tick();
            }

            quietest = Math.Min(quietest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(0, quietest);
    }

    /// <summary>Three lines a billion DIPs tall: motion never changes the line, so the status never changes.</summary>
    private static LayoutGeometry HugeLines() =>
        new("huge", 1, 100, 3e9, 3e9, [0.0, 1e9, 2e9], [1e9, 1e9, 1e9], [0, 1, 2], [0, 0, 0], [0.0, 1e9, 2e9], Array.Empty<SectionMarker>(), 15);
}
