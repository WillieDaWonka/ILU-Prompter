using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IluPrompter.App.Logic;
using IluPrompter.App.Services;
using IluPrompter.Core;
using IluPrompter.Theme;

namespace IluPrompter.App.ViewModels;

/// <summary>The Settings pages (also the targets of "click a chip").</summary>
public enum SettingsPageId
{
    Controller = 0,
    Output,
    Text,
    Scrolling,
    Shortcuts,
    About,
}

/// <summary>
/// The live column (DESIGN.md D1 to D7): chips, preview frame state, transport, speed, mode, Blank output and the
/// readouts. Reads <see cref="IScrollEngine.Status"/> from a 10 Hz poll (<see cref="Poll"/>); output and device
/// changes arrive as events from other threads and are marshalled with <see cref="IUiThread"/>.
/// </summary>
public sealed partial class LiveViewModel : ObservableObject
{
    private readonly IScrollEngine _engine;
    private readonly IOutputSurface _output;
    private readonly IInputRouter _router;
    private readonly ISettingsService _settings;
    private readonly IMonitorService _monitors;
    private readonly OutputController _outputController;
    private readonly IUiThread _ui;
    private readonly Dictionary<ControllerKind, DeviceStatus> _deviceStatus = new();
    private EngineStatus? _lastStatus;
    private bool _applying;
    private double _speed;
    private bool _isBlank;
    private LayoutTier _tier = LayoutTier.Wide;

    [ObservableProperty]
    private ChipModel _outputChip;

    [ObservableProperty]
    private ChipModel _controllerChip;

    [ObservableProperty]
    private bool _isLive;

    [ObservableProperty]
    private string _previewCaption = "";

    [ObservableProperty]
    private string _liveNote = "";

    [ObservableProperty]
    private double _previewAspect = 16.0 / 9.0;

    [ObservableProperty]
    private bool _wheelHintApplies;

    [ObservableProperty]
    private string _playState = "Paused";

    [ObservableProperty]
    private string _playLabel = "Play";

    [ObservableProperty]
    private string _playIcon = IluIcons.Play;

    [ObservableProperty]
    private string _playCaption = "Play";

    [ObservableProperty]
    private bool _canPlay = true;

    [ObservableProperty]
    private bool _canStepBack;

    [ObservableProperty]
    private bool _canStepForward = true;

    [ObservableProperty]
    private string _speedText = "0.0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WpmDisplay))]
    private string _wpmText = "";

    [ObservableProperty]
    private string _speedTooltip = "";

    [ObservableProperty]
    private bool _isLineScrolling;

    [ObservableProperty]
    private string _stepCaption = "Manual";

    [ObservableProperty]
    private string _stepValue = "";

    [ObservableProperty]
    private string _lineText = "Line 0 / 0";

    [ObservableProperty]
    private string _sectionText = "Section 0 / 0";

    [ObservableProperty]
    private string _timeText = ReadoutFormat.NoValue;

    [ObservableProperty]
    private string _timeCaption = "left";

    public LiveViewModel(
        IScrollEngine engine,
        IOutputSurface output,
        IInputRouter router,
        ISettingsService settings,
        IMonitorService monitors,
        OutputController outputController,
        IUiThread ui,
        OutputLayoutViewModel layout)
    {
        _engine = engine;
        _output = output;
        _router = router;
        _settings = settings;
        _monitors = monitors;
        _outputController = outputController;
        _ui = ui;
        Layout = layout;

        _outputChip = ChipMapper.Output(OutputState.Initial, new OutputChipContext(false, false, false, null));
        _controllerChip = ChipMapper.Controller(settings.Current.Input.SelectedController, null, false);

        foreach (var s in router.DeviceStatuses) _deviceStatus[s.Source.Kind] = s;

        output.StateChanged += (_, _) => _ui.Post(RefreshOutput);
        router.DeviceStatusChanged += (_, status) =>
        {
            _ui.Post(() =>
            {
                _deviceStatus[status.Source.Kind] = status;
                RefreshChips();
            });
        };
        monitors.MonitorsChanged += (_, _) => _ui.Post(RefreshOutput);
        outputController.Changed += (_, _) => _ui.Post(RefreshChips);
        settings.Changed += (_, e) =>
        {
            if (!ReferenceEquals(e.Old.Input, e.New.Input) || !ReferenceEquals(e.Old.Output, e.New.Output))
                _ui.Post(RefreshOutput);
        };

        RefreshOutput();
        Poll();
    }

    public OutputLayoutViewModel Layout { get; }

    /// <summary>Raised when a chip is clicked: the matching Settings page should open.</summary>
    public event EventHandler<SettingsPageId>? SettingsPageRequested;

    // ===== tier-dependent presentation =====

    public LayoutTier Tier
    {
        get => _tier;
        set
        {
            if (_tier == value) return;
            _tier = value;
            OnPropertyChanged(string.Empty);
        }
    }

    public bool ShowTransportCaptions => _tier == LayoutTier.Wide;

    public bool ShowWpm => _tier == LayoutTier.Wide;

    /// <summary>"≈ 140 wpm" under the speed at Wide; below Wide it moves into the readout tooltip.</summary>
    public string WpmDisplay => ShowWpm ? WpmText : "";

    /// <summary>D7 sits in the live column at Wide only; at Compact and Narrow it lives in Settings › Output.</summary>
    public bool ShowOutputLayout => _tier == LayoutTier.Wide;

    public bool ChipsInLiveColumn => _tier != LayoutTier.Narrow;

    /// <summary>The D6 readouts (line and section counts) move into the status bar at Narrow.</summary>
    public bool ShowReadoutsRow => _tier != LayoutTier.Narrow;

    /// <summary>Play: 96 x 52 at Wide, 72 at Compact; 56 at Narrow so the transport row fits 440 px.</summary>
    public double PlayWidth => _tier switch { LayoutTier.Wide => 96, LayoutTier.Compact => 72, _ => 56 };

    /// <summary>Play stands 12 px from its neighbours (8 at Narrow, where the row must fit 422 px).</summary>
    public System.Windows.Thickness PlayMargin => new(_tier == LayoutTier.Narrow ? 8 : 12, 0, _tier == LayoutTier.Narrow ? 8 : 12, 0);

    /// <summary>Panel padding is 12; at Narrow it is 8 so the transport row (8 targets, 8 px apart) fits.</summary>
    public System.Windows.Thickness ColumnPadding => new(_tier == LayoutTier.Narrow ? 8 : 12);

    /// <summary>At Narrow the time remaining joins the speed row, because the readouts row is gone.</summary>
    public bool TimeInSpeedRow => _tier == LayoutTier.Narrow;

    public string DisplayModeLabel => _tier == LayoutTier.Wide ? "Display scrolling" : "Display";

    public string LineModeLabel => _tier == LayoutTier.Wide ? "Line scrolling" : "Line";

    public string BlankLabel => _tier == LayoutTier.Wide ? "■ Blank output" : "■ Blank";

    // ===== two-way inputs =====

    /// <summary>The set speed, -10 to +10. Setting it from the slider posts <see cref="EngineCommand.SetSpeed"/>.</summary>
    public double Speed
    {
        get => _speed;
        set
        {
            var v = Math.Round(Math.Clamp(double.IsNaN(value) ? 0 : value, _settings.Current.Scroll.MinSpeed, _settings.Current.Scroll.MaxSpeed), 1, MidpointRounding.AwayFromZero);
            if (Math.Abs(_speed - v) < 0.0001) return;
            _speed = v;
            OnPropertyChanged();
            SpeedText = ReadoutFormat.Speed(v);
            if (!_applying) _engine.Post(EngineCommand.SetSpeed(v));
        }
    }

    public bool IsDisplayScrolling
    {
        get => !IsLineScrolling;
        set
        {
            if (!value || _applying || !IsLineScrolling) return;
            _engine.Post(EngineCommand.SetMode(ScrollMode.Display));
        }
    }

    public bool IsLineScrollingSelected
    {
        get => IsLineScrolling;
        set
        {
            if (!value || _applying || IsLineScrolling) return;
            _engine.Post(EngineCommand.SetMode(ScrollMode.Line));
        }
    }

    public bool IsBlank
    {
        get => _isBlank;
        set
        {
            if (_isBlank == value) return;
            _isBlank = value;
            OnPropertyChanged();
            if (!_applying) _outputController.SetBlank(value);
        }
    }

    partial void OnIsLineScrollingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDisplayScrolling));
        OnPropertyChanged(nameof(IsLineScrollingSelected));
    }

    // ===== commands =====

    [RelayCommand]
    private void Run(PrompterCommand command) => _router.Execute(command);

    /// <summary>The − and + buttons beside the speed track: 0.5, or 0.1 with Shift.</summary>
    public void StepSpeed(int direction, bool fine)
    {
        var command = (direction, fine) switch
        {
            (> 0, false) => PrompterCommand.SpeedUp,
            (> 0, true) => PrompterCommand.SpeedUpFine,
            (_, false) => PrompterCommand.SpeedDown,
            _ => PrompterCommand.SpeedDownFine,
        };
        _router.Execute(command);
    }

    [RelayCommand]
    private void OpenOutputSettings() => SettingsPageRequested?.Invoke(this, SettingsPageId.Output);

    [RelayCommand]
    private void OpenControllerSettings() => SettingsPageRequested?.Invoke(this, SettingsPageId.Controller);

    // ===== refresh =====

    /// <summary>Called at 10 Hz on the UI thread.</summary>
    public void Poll()
    {
        var status = _engine.Status;
        if (ReferenceEquals(status, _lastStatus)) return;
        _lastStatus = status;
        Apply(status);
    }

    private void Apply(EngineStatus s)
    {
        _applying = true;
        try
        {
            var atEnd = s.Transport == TransportState.AtEnd || s.IsAtEnd;
            var playing = s.Transport == TransportState.Playing;

            PlayState = atEnd ? "AtEnd" : playing ? "Playing" : "Paused";
            PlayLabel = playing ? "Pause" : "Play";
            PlayIcon = playing ? IluIcons.Pause : IluIcons.Play;
            PlayCaption = atEnd ? "At end" : playing ? "Pause" : "Play";
            CanPlay = !atEnd;
            CanStepBack = !s.IsAtStart;
            CanStepForward = !s.IsAtEnd;

            Speed = s.SetSpeed;
            IsLineScrolling = s.Mode == ScrollMode.Line;
            WpmText = ReadoutFormat.Wpm(s.WordsPerMinute);
            SpeedTooltip = (playing ? "SCROLLING" : atEnd ? "AT END" : "PAUSED") + " at " + ReadoutFormat.Speed(s.SetSpeed)
                + (WpmText.Length > 0 ? " · " + WpmText : "");

            if (s.Mode == ScrollMode.Line)
            {
                var auto = playing && s.StepIntervalSeconds > 0;
                StepCaption = auto ? "Step" : "Manual";
                StepValue = auto ? ReadoutFormat.StepDelay(s.StepIntervalSeconds) : "";
            }

            LineText = ReadoutFormat.Line(s);
            SectionText = ReadoutFormat.Section(s);
            if (atEnd)
            {
                TimeText = ReadoutFormat.Duration(0);
                TimeCaption = "At end";
            }
            else
            {
                TimeText = ReadoutFormat.Duration(s.SecondsRemaining);
                TimeCaption = playing ? "left" : "at " + ReadoutFormat.Speed(s.SetSpeed);
            }
        }
        finally
        {
            _applying = false;
        }
    }

    private void RefreshOutput()
    {
        var state = _output.State;
        _applying = true;
        try
        {
            IsBlank = state.IsBlank;
        }
        finally
        {
            _applying = false;
        }

        IsLive = state.IsLive;
        var g = state.Geometry;
        PreviewAspect = g.WidthPx > 0 && g.HeightPx > 0 ? (double)g.WidthPx / g.HeightPx : 16.0 / 9.0;
        var hz = g.RefreshHz > 0 ? " · " + g.RefreshHz.ToString("0.#", CultureInfo.InvariantCulture) + " Hz" : "";
        var monitor = state.Monitor is { } m
            ? $" · Display {m.DisplayNumber}" + (m.FriendlyName.Length > 0 ? $" ({m.FriendlyName})" : "")
            : "";
        PreviewCaption = $"Output · {g.WidthPx} × {g.HeightPx}{hz}{monitor}";
        LiveNote = state.IsLive ? "Changes show on " + (state.Monitor is { } mm ? $"Display {mm.DisplayNumber}" : "the output") + " now" : "";

        var input = _settings.Current.Input;
        WheelHintApplies = input.SelectedController == ControllerKind.MouseWheel && input.Wheel.DedicatedDeviceId is null;
        RefreshChips();
    }

    private void RefreshChips()
    {
        var state = _output.State;
        var monitor = _monitors.ResolveOutputMonitor(_settings.Current.Output.MonitorStableId);
        var duplicated = _monitors.GetMonitors().Any(x => x.IsDuplicated);
        var name = monitor is null ? null : $"Display {monitor.DisplayNumber}";
        OutputChip = ChipMapper.Output(state, new OutputChipContext(_outputController.UserPressedShow, monitor is not null, duplicated, name));

        var selected = _settings.Current.Input.SelectedController;
        _deviceStatus.TryGetValue(selected, out var status);
        ControllerChip = ChipMapper.Controller(selected, status, state.IsLive);
    }

    /// <summary>The latest report for a controller kind (Settings cards use it).</summary>
    public DeviceStatus? StatusFor(ControllerKind kind) => _deviceStatus.TryGetValue(kind, out var s) ? s : null;

    /// <summary>Clicking the output chip opens Settings › Output; the controller chip opens Settings › Controller.</summary>
    [RelayCommand]
    private void ChipClicked(string which) =>
        SettingsPageRequested?.Invoke(this, which == "controller" ? SettingsPageId.Controller : SettingsPageId.Output);
}
