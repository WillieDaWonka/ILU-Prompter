using IluPrompter.Core;
using IluPrompter.Rendering;

namespace IluPrompter.Rendering.Tests;

/// <summary>
/// The OutputHost state machine with the real thread and Dispatcher but no native window (Headless), so no test ever
/// puts a window on a display.
/// </summary>
public sealed class OutputHostTests : IAsyncLifetime
{
    private static readonly MonitorInfo Primary = Mon.Make("primary", 1, primary: true);
    private static readonly MonitorInfo Second = Mon.Make("second", 2, w: 3840, h: 2160, dpi: 1.5, hz: 120);
    private static readonly MonitorInfo Third = Mon.Make("third", 3);

    private readonly FakeEngine _engine = new();
    private readonly PromptDocumentService _documents = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeMonitors _monitors;
    private readonly RecordingLog _log = new();
    private readonly OutputHost _host;
    private readonly List<OutputState> _states = new();
    private readonly List<int> _stateThreads = new();

    public OutputHostTests()
    {
        _monitors = new FakeMonitors(Primary, Second);
        _host = new OutputHost(_engine, _documents, _settings, _monitors, new FakeDocumentFactory(), new FakeGeometryBuilder(), _log,
            new OutputHostOptions(Headless: true));
        _host.StateChanged += (_, s) =>
        {
            lock (_states)
            {
                _states.Add(s);
                _stateThreads.Add(Environment.CurrentManagedThreadId);
            }
        };
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _host.ShutdownAsync();

    private bool WaitForState(Func<OutputState, bool> predicate) => Wait.For(() => predicate(_host.State));

    [Fact]
    public void BeforeInitialize_StateIsInitialWithTheLastKnownGeometry()
    {
        var settings = new FakeSettings(PrompterSettings.Default with { Output = new OutputSettings { LastWidthPx = 3840, LastHeightPx = 2160, LastDpiScale = 1.5 } });
        var host = new OutputHost(_engine, _documents, settings, _monitors, new FakeDocumentFactory(), new FakeGeometryBuilder(), _log, new OutputHostOptions(Headless: true));
        Assert.False(host.State.IsOpen);
        Assert.Equal(new OutputGeometry(3840, 2160, 1.5, 60), host.State.Geometry);
        Assert.False(host.State.IsLive);
    }

    [Fact]
    public async Task Initialize_RunsAnStaDispatcherThreadNamedIluOutput_AndReportsTheDisplayWithoutOpening()
    {
        await _host.InitializeAsync();
        await _host.InitializeAsync();    // idempotent

        var dispatcher = _host.OutputDispatcher!;
        Assert.NotEqual(Thread.CurrentThread, dispatcher.Thread);
        Assert.Equal("ILU Output", dispatcher.Thread.Name);
        Assert.Equal(ApartmentState.STA, dispatcher.Thread.GetApartmentState());
        Assert.True(dispatcher.Thread.IsBackground);

        Assert.False(_host.State.IsOpen);
        Assert.Equal(OutputDisplayStatus.Ready, _host.State.Display);
        Assert.Equal("second", _host.State.Monitor?.StableId);
        Assert.Equal(new OutputGeometry(3840, 2160, 1.5, 120), _host.State.Geometry);
    }

    [Fact]
    public async Task Initialize_WithOnlyThePrimaryDisplay_ReportsNoSecondDisplay()
    {
        _monitors.Set(Primary);
        await _host.InitializeAsync();
        Assert.Equal(OutputDisplayStatus.NoSecondDisplay, _host.State.Display);
        Assert.Null(_host.State.Monitor);
    }

    [Fact]
    public async Task Show_OpensOnTheSecondDisplay_AndPersistsItAndItsGeometry()
    {
        await _host.ShowAsync();

        var s = _host.State;
        Assert.True(s.IsOpen);
        Assert.True(s.IsLive);
        Assert.Equal(OutputDisplayStatus.Ready, s.Display);
        Assert.Equal("second", s.Monitor?.StableId);
        Assert.Equal(new OutputGeometry(3840, 2160, 1.5, 120), s.Geometry);
        Assert.Equal(2560, s.Geometry.WidthDip, 6);

        var saved = _settings.Current.Output;
        Assert.Equal("second", saved.MonitorStableId);
        Assert.Equal(3840, saved.LastWidthPx);
        Assert.Equal(2160, saved.LastHeightPx);
        Assert.Equal(1.5, saved.LastDpiScale);
        Assert.Equal(120, saved.LastRefreshHz);
    }

    [Fact]
    public async Task Show_KeepsAnExplicitlySavedDisplay_AndNeverTouchesIt()
    {
        _monitors.Set(Primary, Second, Third);
        _settings.Update(s => s with { Output = s.Output with { MonitorStableId = "third" } });

        await _host.ShowAsync();

        Assert.Equal("third", _host.State.Monitor?.StableId);
        Assert.Equal("third", _settings.Current.Output.MonitorStableId);
        Assert.Equal(1920, _host.State.Geometry.WidthPx);
    }

    [Fact]
    public async Task Show_WithOnlyThePrimary_StaysClosed_AndNeverOpensOnTheOperatorDisplay()
    {
        _monitors.Set(Primary);
        await _host.ShowAsync();
        Assert.False(_host.State.IsOpen);
        Assert.False(_host.State.IsLive);
        Assert.Equal(OutputDisplayStatus.NoSecondDisplay, _host.State.Display);
        Assert.Null(_host.State.Monitor);
        Assert.Null(_settings.Current.Output.MonitorStableId);
    }

    [Fact]
    public async Task Show_WhileWindowsMirrors_ReportsDuplicated()
    {
        _monitors.Set(Mon.Make("panelA", 1, primary: true, duplicated: true), Mon.Make("panelB", 1, duplicated: true));
        await _host.ShowAsync();
        Assert.False(_host.State.IsOpen);
        Assert.Equal(OutputDisplayStatus.Duplicated, _host.State.Display);
    }

    [Fact]
    public async Task DisplayVanishesWhileOpen_WindowHidden_StateMissing_StillOpen()
    {
        await _host.ShowAsync();
        _monitors.Set(Primary);

        Assert.True(WaitForState(s => s.Display == OutputDisplayStatus.Missing));
        Assert.True(_host.State.IsOpen);          // stays open: it comes back by itself
        Assert.False(_host.State.IsLive);
        Assert.Null(_host.State.Monitor);
    }

    [Fact]
    public async Task DisplayReturns_TheSameOutputReappears()
    {
        await _host.ShowAsync();
        _monitors.Set(Primary);
        Assert.True(WaitForState(s => s.Display == OutputDisplayStatus.Missing));

        _monitors.Set(Primary, Second);

        Assert.True(WaitForState(s => s.Display == OutputDisplayStatus.Ready));
        Assert.True(_host.State.IsOpen);
        Assert.True(_host.State.IsLive);
        Assert.Equal("second", _host.State.Monitor?.StableId);
    }

    [Fact]
    public async Task ChosenDisplayVanishes_NeverMovesToAnotherDisplay_NorToThePrimary()
    {
        _monitors.Set(Primary, Second, Third);
        _settings.Update(s => s with { Output = s.Output with { MonitorStableId = "second" } });
        await _host.ShowAsync();
        Assert.Equal("second", _host.State.Monitor?.StableId);

        _monitors.Set(Primary, Third);          // "second" is unplugged, "third" is still there

        Assert.True(WaitForState(s => s.Display == OutputDisplayStatus.Missing));
        Assert.Null(_host.State.Monitor);
        Assert.Equal("second", _settings.Current.Output.MonitorStableId);
    }

    [Fact]
    public async Task WindowsStartsMirroringWhileOpen_ReportsDuplicated_ThenRecovers()
    {
        await _host.ShowAsync();
        _monitors.Set(Mon.Make("primary", 1, primary: true, duplicated: true), Mon.Make("second", 1, duplicated: true));
        Assert.True(WaitForState(s => s.Display == OutputDisplayStatus.Duplicated));
        Assert.True(_host.State.IsOpen);
        Assert.False(_host.State.IsLive);

        _monitors.Set(Primary, Second);
        Assert.True(WaitForState(s => s.Display == OutputDisplayStatus.Ready && s.IsOpen));
    }

    [Fact]
    public async Task DisplayResolutionChangesWhileOpen_GeometryFollows_AndIsPersisted()
    {
        await _host.ShowAsync();
        _monitors.Set(Primary, Mon.Make("second", 2, w: 1920, h: 1080, dpi: 1.0, hz: 60));

        Assert.True(WaitForState(s => s.Geometry.WidthPx == 1920));
        Assert.Equal(1.0, _host.State.Geometry.DpiScale);
        Assert.True(Wait.For(() => _settings.Current.Output.LastWidthPx == 1920));
    }

    [Fact]
    public async Task ChoosingAnotherDisplayInSettingsWhileOpen_MovesTheOutput()
    {
        _monitors.Set(Primary, Second, Third);
        await _host.ShowAsync();
        Assert.Equal("second", _host.State.Monitor?.StableId);

        _settings.Update(s => s with { Output = s.Output with { MonitorStableId = "third" } });

        Assert.True(WaitForState(s => s.Monitor?.StableId == "third"));
        Assert.True(_host.State.IsOpen);
    }

    [Fact]
    public async Task Hide_ClosesTheOutput_AndTheDisplayStaysAvailable()
    {
        await _host.ShowAsync();
        await _host.HideAsync();

        Assert.False(_host.State.IsOpen);
        Assert.Equal(OutputDisplayStatus.Ready, _host.State.Display);
        await _host.ShowAsync();
        Assert.True(_host.State.IsOpen);
    }

    [Fact]
    public async Task Hide_WhenMissing_ClosesIt_AndStopsWaiting()
    {
        await _host.ShowAsync();
        _monitors.Set(Primary);
        Assert.True(WaitForState(s => s.Display == OutputDisplayStatus.Missing));

        await _host.HideAsync();
        Assert.False(_host.State.IsOpen);
        Assert.Equal(OutputDisplayStatus.NoSecondDisplay, _host.State.Display);

        _monitors.Set(Primary, Second);   // plugging it back must not reopen a closed output
        Assert.True(WaitForState(s => s.Display == OutputDisplayStatus.Ready));
        Assert.False(_host.State.IsOpen);
    }

    [Fact]
    public async Task MonitorChangesWhileClosed_UpdateTheDisplayStatusAndGeometry()
    {
        await _host.InitializeAsync();
        _monitors.Set(Primary);
        Assert.True(WaitForState(s => s.Display == OutputDisplayStatus.NoSecondDisplay));

        _monitors.Set(Primary, Mon.Make("second", 2, w: 2560, h: 1440));
        Assert.True(WaitForState(s => s.Display == OutputDisplayStatus.Ready));
        Assert.Equal(2560, _host.State.Geometry.WidthPx);
        Assert.False(_host.State.IsOpen);
    }

    [Fact]
    public async Task SetBlank_ChangesLiveness_ImmediatelyAndFromAnyThread()
    {
        await _host.ShowAsync();
        Assert.True(_host.State.IsLive);

        _host.SetBlank(true);
        Assert.True(_host.State.IsBlank);          // synchronous: a fast B-B toggle reads the right value
        Assert.True(_host.State.IsOpen);
        Assert.False(_host.State.IsLive);

        await Task.Run(() => _host.SetBlank(false));
        Assert.False(_host.State.IsBlank);
        Assert.True(_host.State.IsLive);
    }

    [Fact]
    public void SetBlank_BeforeInitialize_JustRemembersIt()
    {
        _host.SetBlank(true);
        Assert.True(_host.State.IsBlank);
        Assert.False(_host.State.IsLive);
    }

    [Fact]
    public async Task StateChanged_IsRaisedOnTheOutputThread_InOrder_WithoutDuplicates()
    {
        await _host.ShowAsync();
        _host.SetBlank(true);
        _host.SetBlank(true);        // no change: no event
        _host.SetBlank(false);
        await _host.HideAsync();

        Assert.True(Wait.For(() => { lock (_states) { return _states.Count > 0 && !_states[^1].IsOpen; } }));
        List<OutputState> states;
        List<int> threads;
        lock (_states) { states = _states.ToList(); threads = _stateThreads.ToList(); }

        var outputThread = _host.OutputDispatcher!.Thread.ManagedThreadId;
        Assert.All(threads, t => Assert.Equal(outputThread, t));
        for (var i = 1; i < states.Count; i++) Assert.NotEqual(states[i - 1], states[i]);
        Assert.Contains(states, s => s.IsOpen && s.IsLive);
        Assert.Contains(states, s => s.IsBlank);
    }

    [Fact]
    public async Task Show_BuildsTheOutputSurfaceAndGivesTheEngineAGeometryAtTheDisplaysSize()
    {
        await _host.ShowAsync();
        Assert.True(Wait.For(() => _engine.LayoutCount > 0));
        var (geometry, viewport) = _engine.Layouts[^1];
        Assert.NotNull(geometry);
        // 3840 x 2160 at 1.5x = 2560 x 1440 DIP: text area width = 2560 - 2 x 160 / 1920 x 2560 (margins are fractions).
        var area = TypographyMath.ComputeTextArea(_host.State.Geometry, _settings.Current.Margins, _settings.Current.Text, _settings.Current.Output);
        Assert.Equal(area.ToViewport(), viewport);
        Assert.Equal(area.BaseLineHeight, geometry.BaseLineHeight, 6);
    }

    [Fact]
    public async Task Shutdown_StopsTheThread_AndCanBeCalledTwice()
    {
        await _host.ShowAsync();
        var thread = _host.OutputDispatcher!.Thread;

        await _host.ShutdownAsync();
        Assert.True(thread.Join(2000));
        Assert.False(_host.State.IsOpen);

        await _host.ShutdownAsync();
    }

    [Fact]
    public async Task Shutdown_WithoutInitialize_IsHarmless() => await _host.ShutdownAsync();

    [Fact]
    public async Task CanBeRestartedAfterShutdown()
    {
        await _host.ShowAsync();
        await _host.ShutdownAsync();
        await _host.ShowAsync();
        Assert.True(_host.State.IsOpen);
    }

    [Fact]
    public async Task DevFlag_ReportsReadyOnAOneDisplayMachine_WithoutPersistingAnything()
    {
        _monitors.Set(Primary);
        var host = new OutputHost(_engine, _documents, _settings, _monitors, new FakeDocumentFactory(), new FakeGeometryBuilder(), _log,
            new OutputHostOptions(DevOutputOnPrimary: true, Headless: true));
        try
        {
            await host.InitializeAsync();
            Assert.Equal(OutputDisplayStatus.Ready, host.State.Display);
            Assert.Equal(new OutputGeometry(960, 540, 1.0, 60), host.State.Geometry);

            await host.ShowAsync();
            Assert.True(host.State.IsOpen);
            Assert.Equal("primary", host.State.Monitor?.StableId);
            Assert.Null(_settings.Current.Output.MonitorStableId);
            Assert.Equal(1920, _settings.Current.Output.LastWidthPx);     // the dev window never overwrites real settings
            Assert.Contains(_log.Lines, l => l.Contains("--dev-output-on-primary"));
        }
        finally
        {
            await host.ShutdownAsync();
        }
    }

    [Fact]
    public void DevFlag_IsOnlyParsedFromTheCommandLine_NeverTheDefault()
    {
        Assert.False(new OutputHostOptions().DevOutputOnPrimary);
        Assert.False(new OutputHostOptions().Headless);
        Assert.Equal("--dev-output-on-primary", OutputHostOptions.DevFlag);
    }
}
