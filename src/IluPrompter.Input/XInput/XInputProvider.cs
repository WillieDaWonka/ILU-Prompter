using System.Diagnostics;
using IluPrompter.Core;
using Vortice.XInput;
using CoreBatteryLevel = IluPrompter.Core.BatteryLevel;
using Xi = Vortice.XInput.XInput;

namespace IluPrompter.Input;

/// <summary>
/// Xbox controller through XInput 1.4 (in-box, polling, works while the app is not focused).
/// Thread "ILU XInput" polls with <c>Thread.Sleep(8)</c> (no timeBeginPeriod). An empty slot is probed at most
/// once per second. Never touches WPF objects.
/// </summary>
public sealed class XInputProvider : IScrollInputProvider
{
    private const string Area = "input.xinput";
    private const int PollSleepMs = 8;
    private static readonly long ProbeIntervalTicks = Stopwatch.Frequency;           // 1 s
    private static readonly long BatteryIntervalTicks = Stopwatch.Frequency * 30;    // 30 s

    private readonly IAppLog _log;
    private readonly object _gate = new();

    private Thread? _thread;
    private volatile bool _stop;
    private volatile IInputSink? _sink;
    private volatile int _requestedIndex = -1;
    private volatile bool _reselect;

    public XInputProvider(IAppLog log) => _log = log;

    public ControllerKind Kind => ControllerKind.Xbox;

    public bool IsRunning
    {
        get
        {
            var t = _thread;
            return t is not null && t.IsAlive && !_stop;
        }
    }

    public IReadOnlyList<InputDeviceInfo> EnumerateDevices()
    {
        var list = new List<InputDeviceInfo>();
        try
        {
            for (var i = 0; i < XInputNormaliser.SlotCount; i++)
            {
                if (Xi.GetState((uint)i, out _))
                {
                    list.Add(new InputDeviceInfo(Kind, XInputNormaliser.DeviceId(i), XInputNormaliser.DisplayName(i), true));
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warn(Area, "Could not enumerate controllers: " + ex.Message);
        }

        return list;
    }

    public void Start(IInputSink sink, string? deviceId)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (_gate)
        {
            _sink = sink;
            _requestedIndex = XInputNormaliser.ParseDeviceId(deviceId);
            if (_thread is { IsAlive: true } && !_stop)
            {
                _reselect = true;       // switch device: the poll loop picks it up
                return;
            }

            _stop = false;
            _reselect = false;
            var thread = new Thread(PollLoop) { Name = "ILU XInput", IsBackground = true };
            _thread = thread;
            thread.Start();
        }
    }

    public void Stop()
    {
        Thread? thread;
        lock (_gate)
        {
            thread = _thread;
            _thread = null;
            _stop = true;
        }

        if (thread is null)
        {
            return;
        }

        if (!thread.Join(TimeSpan.FromSeconds(1)))
        {
            _log.Warn(Area, "XInput thread did not stop within 1 s.");
        }
    }

    public void Dispose() => Stop();

    // ---- poll thread ---------------------------------------------------------------------------------

    private void PollLoop()
    {
        var nextProbe = new long[XInputNormaliser.SlotCount];
        var axes = new double[XInputNormaliser.AxisIds.Length];
        var lastAxes = new double[XInputNormaliser.AxisIds.Length];
        var edges = new ButtonEdge[XInputNormaliser.MaxEdges];

        var active = -1;
        var lastPacket = -1;
        var lastButtons = GamepadButtons.None;
        var lastBattery = CoreBatteryLevel.Unknown;
        long nextBattery = 0;
        var reportedNothing = false;

        try
        {
            while (!_stop)
            {
                var sink = _sink;
                if (sink is null)
                {
                    break;
                }

                var now = Stopwatch.GetTimestamp();

                if (_reselect)
                {
                    _reselect = false;
                    if (active >= 0)
                    {
                        ZeroAxes(sink, active, lastAxes);   // do not leave the old pad's trigger "held"
                        active = -1;
                    }

                    Array.Clear(nextProbe);
                    reportedNothing = false;
                }

                if (active >= 0)
                {
                    if (Xi.GetState((uint)active, out var state))
                    {
                        if (state.PacketNumber != lastPacket)
                        {
                            lastPacket = state.PacketNumber;
                            Process(sink, active, state.Gamepad, axes, lastAxes, edges, ref lastButtons, forceAxes: false);
                        }

                        if (now >= nextBattery)
                        {
                            nextBattery = now + BatteryIntervalTicks;
                            var level = ReadBattery(active);
                            if (level != lastBattery)
                            {
                                lastBattery = level;
                                ReportConnected(sink, active, level);
                            }
                        }
                    }
                    else
                    {
                        sink.ReportStatus(new DeviceStatus(
                            new InputSourceId(Kind, XInputNormaliser.DeviceId(active)),
                            XInputNormaliser.DisplayName(active),
                            DeviceConnection.Disconnected,
                            Detail: "Controller disconnected"));
                        nextProbe[active] = now + ProbeIntervalTicks;
                        Array.Clear(lastAxes);
                        active = -1;
                        reportedNothing = true;     // Disconnected already told the UI
                    }
                }
                else
                {
                    var found = Probe(now, nextProbe, out var index, out var probed, out var probedState);
                    if (found)
                    {
                        active = index;
                        lastPacket = probedState.PacketNumber;
                        lastButtons = probedState.Gamepad.Buttons;      // baseline: buttons already held do not fire
                        lastBattery = ReadBattery(active);
                        nextBattery = now + BatteryIntervalTicks;
                        reportedNothing = false;
                        ReportConnected(sink, active, lastBattery);
                        Array.Clear(lastAxes);
                        Process(sink, active, probedState.Gamepad, axes, lastAxes, edges, ref lastButtons, forceAxes: true);
                    }
                    else if (probed && !reportedNothing)
                    {
                        reportedNothing = true;
                        var shown = _requestedIndex >= 0 ? _requestedIndex : 0;
                        sink.ReportStatus(new DeviceStatus(
                            new InputSourceId(Kind, XInputNormaliser.DeviceId(shown)),
                            _requestedIndex >= 0 ? XInputNormaliser.DisplayName(shown) : "Xbox controller",
                            DeviceConnection.NotFound,
                            Detail: "No controller found. Connect one or wake it with the Xbox button"));
                    }
                }

                Thread.Sleep(PollSleepMs);
            }
        }
        catch (DllNotFoundException ex)
        {
            _log.Error(Area, "XInput is not available on this machine.", ex);
            _sink?.ReportStatus(new DeviceStatus(
                new InputSourceId(Kind, XInputNormaliser.DeviceId(0)), "Xbox controller", DeviceConnection.NotFound,
                Detail: "XInput is not available on this machine"));
        }
        catch (Exception ex)
        {
            _log.Error(Area, "XInput polling failed.", ex);
            _sink?.ReportStatus(new DeviceStatus(
                new InputSourceId(Kind, XInputNormaliser.DeviceId(Math.Max(active, 0))), "Xbox controller",
                DeviceConnection.NotFound, Detail: "XInput error: " + ex.Message));
        }
        finally
        {
            try
            {
                if (active >= 0 && _sink is { } sink)
                {
                    ZeroAxes(sink, active, lastAxes);
                }
            }
            catch (Exception ex)
            {
                _log.Warn(Area, "Zeroing axes on stop: " + ex.Message);
            }
        }
    }

    /// <summary>Probes the wanted slot(s) that are due. found = a pad answered; probed = at least one slot was asked.</summary>
    private bool Probe(long now, long[] nextProbe, out int index, out bool probed, out State state)
    {
        index = -1;
        probed = false;
        state = default;
        var requested = _requestedIndex;
        var from = requested >= 0 ? requested : 0;
        var to = requested >= 0 ? requested : XInputNormaliser.SlotCount - 1;
        for (var i = from; i <= to; i++)
        {
            if (now < nextProbe[i])
            {
                continue;
            }

            probed = true;
            nextProbe[i] = now + ProbeIntervalTicks;
            if (Xi.GetState((uint)i, out state))
            {
                index = i;
                return true;
            }
        }

        return false;
    }

    private void Process(
        IInputSink sink, int index, in Gamepad pad, double[] axes, double[] lastAxes, ButtonEdge[] edges,
        ref GamepadButtons lastButtons, bool forceAxes)
    {
        var source = new InputSourceId(Kind, XInputNormaliser.DeviceId(index));
        var ts = Stopwatch.GetTimestamp();

        XInputNormaliser.ReadAxes(pad, axes);
        for (var a = 0; a < axes.Length; a++)
        {
            if (forceAxes || XInputNormaliser.AxisChanged(lastAxes[a], axes[a]))
            {
                lastAxes[a] = axes[a];
                sink.Submit(new AxisInput(source, XInputNormaliser.AxisIds[a], ts, axes[a]));
            }
        }

        var count = XInputNormaliser.DiffButtons(lastButtons, pad.Buttons, edges);
        lastButtons = pad.Buttons;
        for (var e = 0; e < count; e++)
        {
            sink.Submit(new ButtonInput(source, edges[e].ControlId, ts, edges[e].IsPressed));
        }
    }

    private static void ZeroAxes(IInputSink sink, int index, double[] lastAxes)
    {
        var source = new InputSourceId(ControllerKind.Xbox, XInputNormaliser.DeviceId(index));
        var ts = Stopwatch.GetTimestamp();
        for (var a = 0; a < lastAxes.Length; a++)
        {
            if (lastAxes[a] != 0.0)
            {
                lastAxes[a] = 0.0;
                sink.Submit(new AxisInput(source, XInputNormaliser.AxisIds[a], ts, 0.0));
            }
        }
    }

    private CoreBatteryLevel ReadBattery(int index)
    {
        try
        {
            return Xi.GetBatteryInformation((uint)index, BatteryDeviceType.Gamepad, out var info)
                ? XInputNormaliser.Battery(info.BatteryType, info.BatteryLevel)
                : CoreBatteryLevel.Unknown;
        }
        catch (Exception ex)
        {
            _log.Warn(Area, "Battery query failed: " + ex.Message);
            return CoreBatteryLevel.Unknown;
        }
    }

    private void ReportConnected(IInputSink sink, int index, CoreBatteryLevel battery) =>
        sink.ReportStatus(new DeviceStatus(
            new InputSourceId(Kind, XInputNormaliser.DeviceId(index)),
            XInputNormaliser.DisplayName(index),
            DeviceConnection.Connected,
            battery));
}
