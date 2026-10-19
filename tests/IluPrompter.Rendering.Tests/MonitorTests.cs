using IluPrompter.Core;
using IluPrompter.Rendering;

namespace IluPrompter.Rendering.Tests;

internal static class Mon
{
    public static MonitorInfo Make(string id, int number, bool primary = false, bool duplicated = false, int w = 1920, int h = 1080, double dpi = 1.0, double hz = 60) =>
        new(id, number, "Panel " + number, new PixelRect(primary ? 0 : 1920, 0, w, h), new PixelRect(primary ? 0 : 1920, 0, w, h - 40), dpi, hz, primary, duplicated);
}

public class MonitorResolverTests
{
    private static readonly MonitorInfo Primary = Mon.Make("primary", 1, primary: true);
    private static readonly MonitorInfo Second = Mon.Make("second", 2);
    private static readonly MonitorInfo Third = Mon.Make("third", 3);

    [Fact]
    public void SavedDisplayPresent_IsReturned()
    {
        var result = MonitorResolver.Resolve(new[] { Primary, Second, Third }, "third");
        Assert.Equal("third", result?.StableId);
    }

    [Fact]
    public void SavedDisplayMissing_FallsBackToFirstNonPrimary()
    {
        var result = MonitorResolver.Resolve(new[] { Primary, Third, Second }, "gone");
        Assert.Equal("second", result?.StableId);   // lowest DisplayNumber, whatever the list order
    }

    [Fact]
    public void NoSavedId_PicksFirstNonPrimary()
    {
        Assert.Equal("second", MonitorResolver.Resolve(new[] { Primary, Second, Third }, null)?.StableId);
    }

    [Fact]
    public void OnlyPrimary_ReturnsNull()
    {
        Assert.Null(MonitorResolver.Resolve(new[] { Primary }, null));
        Assert.Null(MonitorResolver.Resolve(new[] { Primary }, "primary"));
        Assert.Equal(OutputDisplayStatus.NoSecondDisplay, MonitorResolver.Classify(new[] { Primary }, null));
    }

    [Fact]
    public void NoMonitorsAtAll_ReturnsNullAndNoSecondDisplay()
    {
        Assert.Null(MonitorResolver.Resolve(Array.Empty<MonitorInfo>(), "x"));
        Assert.Equal(OutputDisplayStatus.NoSecondDisplay, MonitorResolver.Classify(Array.Empty<MonitorInfo>(), "x"));
    }

    [Fact]
    public void SavedDisplayThatIsNowPrimary_IsNeverReturned()
    {
        // The operator swapped which display is primary: the saved one is now the operator display.
        var nowPrimary = Mon.Make("second", 2, primary: true);
        var wasPrimary = Mon.Make("primary", 1);
        var result = MonitorResolver.Resolve(new[] { wasPrimary, nowPrimary }, "second");
        Assert.Equal("primary", result?.StableId);
        Assert.False(result!.IsPrimary);
    }

    [Fact]
    public void Duplicated_IsNotUsableAndIsClassifiedAsDuplicated()
    {
        var a = Mon.Make("panelA", 1, primary: true, duplicated: true);
        var b = Mon.Make("panelB", 1, primary: false, duplicated: true);
        Assert.Null(MonitorResolver.Resolve(new[] { a, b }, "panelB"));
        Assert.Equal(OutputDisplayStatus.Duplicated, MonitorResolver.Classify(new[] { a, b }, "panelB"));
    }

    [Fact]
    public void ReorderedList_ResolvesTheSameDisplay()
    {
        var forward = MonitorResolver.Resolve(new[] { Primary, Second, Third }, "third");
        var reversed = MonitorResolver.Resolve(new[] { Third, Second, Primary }, "third");
        Assert.Equal(forward, reversed);
        Assert.Equal(MonitorResolver.Resolve(new[] { Primary, Second, Third }, null), MonitorResolver.Resolve(new[] { Third, Second, Primary }, null));
    }

    [Fact]
    public void Classify_ReadyWhenAnExtendedDisplayExists()
    {
        Assert.Equal(OutputDisplayStatus.Ready, MonitorResolver.Classify(new[] { Primary, Second }, null));
    }

    [Fact]
    public void ClassifyOpen_NeverFallsBackToAnotherDisplay()
    {
        var list = new[] { Primary, Third };
        Assert.Null(MonitorResolver.Find(list, "second"));
        Assert.Equal(OutputDisplayStatus.Missing, MonitorResolver.ClassifyOpen(list, "second"));
        Assert.Equal(OutputDisplayStatus.Ready, MonitorResolver.ClassifyOpen(new[] { Primary, Second }, "second"));
        Assert.Equal(OutputDisplayStatus.Duplicated,
            MonitorResolver.ClassifyOpen(new[] { Mon.Make("p", 1, primary: true, duplicated: true), Mon.Make("second", 1, duplicated: true) }, "second"));
    }

    [Fact]
    public void ToGeometry_UsesPhysicalPixelsAndGuardsBadValues()
    {
        var g = MonitorResolver.ToGeometry(Mon.Make("x", 2, w: 3840, h: 2160, dpi: 1.5, hz: 120));
        Assert.Equal(new OutputGeometry(3840, 2160, 1.5, 120), g);
        Assert.Equal(2560, g.WidthDip, 3);
        var bad = MonitorResolver.ToGeometry(Mon.Make("x", 2, w: 0, h: 0, dpi: 0, hz: 0));
        Assert.Equal(new OutputGeometry(1, 1, 1.0, 60.0), bad);
    }
}

public class DisplayComposerTests
{
    private static GdiMonitor Gdi(string name, bool primary, int x = 0) =>
        new(name, new PixelRect(x, 0, 1920, 1080), new PixelRect(x, 0, 1920, 1040), 1.25, primary);

    [Theory]
    [InlineData(@"\\.\DISPLAY1", 1)]
    [InlineData(@"\\.\DISPLAY12", 12)]
    [InlineData(@"\\.\display3", 3)]
    [InlineData(@"\\.\DISPLAYV1", 0)]
    [InlineData("", 0)]
    public void ParseDisplayNumber_ReadsTheGdiName(string name, int expected) =>
        Assert.Equal(expected, DisplayComposer.ParseDisplayNumber(name));

    [Fact]
    public void ExtendedDisplays_AreNotDuplicated_AndUseTargetPathAsStableId()
    {
        var result = DisplayComposer.Compose(
            [Gdi(@"\\.\DISPLAY1", true), Gdi(@"\\.\DISPLAY2", false, 1920)],
            [new DisplayPath(@"\\.\DISPLAY2", "target-b", "LG 55UH5", 59.94, false), new DisplayPath(@"\\.\DISPLAY1", "target-a", "", 60, true)]);

        Assert.Equal(2, result.Count);
        Assert.Equal("target-a", result[0].StableId);
        Assert.Equal("Built-in display", result[0].FriendlyName);
        Assert.True(result[0].IsPrimary);
        Assert.False(result[0].IsDuplicated);
        Assert.Equal("target-b", result[1].StableId);
        Assert.Equal(2, result[1].DisplayNumber);
        Assert.Equal("Display 2 · LG 55UH5", result[1].Label);
        Assert.Equal(59.94, result[1].RefreshHz, 2);
        Assert.Equal(1.25, result[1].DpiScale);
    }

    [Fact]
    public void TwoTargetsOnOneSource_AreBothMarkedDuplicated()
    {
        var result = DisplayComposer.Compose(
            [Gdi(@"\\.\DISPLAY1", true)],
            [new DisplayPath(@"\\.\DISPLAY1", "target-a", "Panel", 60, true), new DisplayPath(@"\\.\DISPLAY1", "target-b", "TV", 60, false)]);

        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.True(m.IsDuplicated));
        Assert.Equal(OutputDisplayStatus.Duplicated, MonitorResolver.Classify(result, null));
    }

    [Fact]
    public void MissingDisplayConfig_FallsBackToTheGdiName()
    {
        var result = DisplayComposer.Compose([Gdi(@"\\.\DISPLAY2", false, 1920)], []);
        var m = Assert.Single(result);
        Assert.Equal(@"\\.\DISPLAY2", m.StableId);
        Assert.Equal(60.0, m.RefreshHz);
        Assert.Equal("Display 2", m.Label);
    }

    [Fact]
    public void ZeroRefreshRate_FallsBackToSixtyHertz()
    {
        var m = Assert.Single(DisplayComposer.Compose([Gdi(@"\\.\DISPLAY1", true)], [new DisplayPath(@"\\.\DISPLAY1", "t", "X", 0, false)]));
        Assert.Equal(60.0, m.RefreshHz);
    }
}

public class MonitorServiceTests
{
    private sealed class NullLog : IAppLog
    {
        public bool DiagnosticsEnabled => false;
        public string LogDirectory => "";
        public void Info(string area, string message) { }
        public void Warn(string area, string message) { }
        public void Error(string area, string message, Exception? exception = null) { }
        public void Trace(string area, string message) { }
    }

    [Fact]
    public void DisplayChangeBurst_RaisesMonitorsChangedOnceAfterTheDebounce()
    {
        var calls = 0;
        IReadOnlyList<MonitorInfo> list = [Mon.Make("p", 1, primary: true)];
        using var service = new MonitorService(new NullLog(), () => { calls++; return list; }, TimeSpan.FromMilliseconds(80), listenToSystemEvents: false);
        var raised = 0;
        using var signal = new ManualResetEventSlim();
        service.MonitorsChanged += (_, _) => { Interlocked.Increment(ref raised); signal.Set(); };

        Assert.Single(service.GetMonitors());
        Assert.Single(service.GetMonitors());
        Assert.Equal(1, calls);                       // cached

        list = [Mon.Make("p", 1, primary: true), Mon.Make("s", 2)];
        for (var i = 0; i < 5; i++)
        {
            service.OnDisplaySettingsChanged(null, EventArgs.Empty);
            Thread.Sleep(10);
        }

        Assert.True(signal.Wait(2000));
        Thread.Sleep(250);
        Assert.Equal(1, raised);
        Assert.Equal(2, service.GetMonitors().Count);
        Assert.Equal("s", service.ResolveOutputMonitor(null)?.StableId);
    }

    [Fact]
    public void EnumeratorFailure_ReturnsAnEmptyList()
    {
        using var service = new MonitorService(new NullLog(), () => throw new InvalidOperationException("boom"), TimeSpan.FromMilliseconds(50), listenToSystemEvents: false);
        Assert.Empty(service.GetMonitors());
        Assert.Null(service.ResolveOutputMonitor(null));
    }

    [Fact]
    public void RealEnumeration_ReportsAtLeastOneSaneDisplay()
    {
        // Reads this machine's own display configuration only (no network, no scanning).
        using var service = new MonitorService(new NullLog());
        var monitors = service.GetMonitors();
        Assert.NotEmpty(monitors);
        Assert.Contains(monitors, m => m.IsPrimary);
        Assert.All(monitors, m =>
        {
            Assert.True(m.BoundsPx.Width > 0 && m.BoundsPx.Height > 0);
            Assert.InRange(m.DpiScale, 1.0, 6.0);
            Assert.True(m.RefreshHz >= 24);
            Assert.False(string.IsNullOrEmpty(m.StableId));
            Assert.True(m.DisplayNumber >= 1);
        });
    }
}
