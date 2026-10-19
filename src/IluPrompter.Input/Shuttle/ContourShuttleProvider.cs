using System.Diagnostics;
using HidSharp;
using IluPrompter.Core;

namespace IluPrompter.Input;

/// <summary>
/// Contour ShuttleXpress / ShuttlePRO read as a plain HID device through HidSharp (reads continue while the
/// app is not focused). Thread "ILU Shuttle" blocks in <c>HidStream.Read</c>; <c>DeviceList.Local.Changed</c>
/// wakes it to reconnect. Never touches WPF objects.
/// </summary>
public sealed class ContourShuttleProvider : IScrollInputProvider
{
    private const string Area = "input.shuttle";
    private const string OpenFailedDetail = "Close the Contour ShuttlePro software, then reconnect";

    private readonly IAppLog _log;
    private readonly object _gate = new();
    private readonly object _streamGate = new();
    private readonly AutoResetEvent _wake = new(false);

    private Thread? _thread;
    private volatile bool _stop;
    private volatile bool _reselect;
    private volatile IInputSink? _sink;
    private volatile string? _wantedPath;
    private HidStream? _stream;

    public ContourShuttleProvider(IAppLog log) => _log = log;

    public ControllerKind Kind => ControllerKind.JogShuttle;

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
        try
        {
            return FindDevices()
                .Select(f => new InputDeviceInfo(Kind, f.Device.DevicePath, f.Model.Name, true))
                .ToList();
        }
        catch (Exception ex)
        {
            _log.Warn(Area, "Could not enumerate HID devices: " + ex.Message);
            return Array.Empty<InputDeviceInfo>();
        }
    }

    public void Start(IInputSink sink, string? deviceId)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (_gate)
        {
            _sink = sink;
            _wantedPath = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId;
            if (_thread is { IsAlive: true } && !_stop)
            {
                _reselect = true;       // switch device: drop the current stream, the loop re-selects
                CloseStream();
                _wake.Set();
                return;
            }

            _stop = false;
            _reselect = false;
            var thread = new Thread(Run) { Name = "ILU Shuttle", IsBackground = true };
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

        CloseStream();      // unblocks a pending Read
        _wake.Set();
        if (!thread.Join(TimeSpan.FromSeconds(2)))
        {
            _log.Warn(Area, "Shuttle thread did not stop within 2 s.");
        }
    }

    public void Dispose()
    {
        Stop();
        _wake.Dispose();
    }

    // ---- device thread -------------------------------------------------------------------------------

    private void Run()
    {
        EventHandler<DeviceListChangedEventArgs> onChanged = (_, _) => _wake.Set();
        string? lastReportKey = null;

        void Report(IInputSink sink, DeviceConnection state, string path, string name, string? detail)
        {
            var key = state + "|" + path + "|" + detail;
            if (key == lastReportKey)
            {
                return;
            }

            lastReportKey = key;
            sink.ReportStatus(new DeviceStatus(new InputSourceId(Kind, path), name, state, Detail: detail));
        }

        try
        {
            DeviceList.Local.Changed += onChanged;
            var edges = new ButtonEdge[ShuttleReportTracker.MaxEdges];

            while (!_stop)
            {
                var sink = _sink;
                if (sink is null)
                {
                    break;
                }

                _reselect = false;
                var wanted = _wantedPath;
                var found = SelectDevice(wanted);

                if (found is null)
                {
                    Report(sink, DeviceConnection.NotFound, wanted ?? string.Empty, "Contour ShuttleXpress / PRO",
                        "Not found. Plug it in and close the Contour ShuttlePro software");
                    _wake.WaitOne(2000);
                    continue;
                }

                var (device, model) = found.Value;
                var path = device.DevicePath;
                if (!device.TryOpen(out var stream))
                {
                    Report(sink, DeviceConnection.NotFound, path, model.Name, OpenFailedDetail);
                    _wake.WaitOne(2000);
                    continue;
                }

                try
                {
                    stream.ReadTimeout = Timeout.Infinite;
                    lock (_streamGate)
                    {
                        _stream = stream;
                    }

                    Report(sink, DeviceConnection.Connected, path, model.Name, null);
                    ReadLoop(sink, stream, device, path, new ShuttleReportTracker(model), edges);
                }
                catch (Exception ex) when (!_stop)
                {
                    _log.Warn(Area, "Shuttle read ended: " + ex.Message);
                }
                finally
                {
                    lock (_streamGate)
                    {
                        _stream = null;
                    }

                    try
                    {
                        stream.Dispose();
                    }
                    catch
                    {
                        // already closed by Stop / unplug
                    }
                }

                if (_stop)
                {
                    break;
                }

                if (_reselect)
                {
                    // Deliberate switch: leave nothing held on the old unit, no Disconnected fail-safe.
                    sink.Submit(new AxisInput(new InputSourceId(Kind, path), ControlIds.ShuttleRing, Stopwatch.GetTimestamp(), 0.0));
                    lastReportKey = null;
                    continue;
                }

                Report(sink, DeviceConnection.Disconnected, path, model.Name, "Shuttle disconnected");
                _wake.WaitOne(1000);
            }
        }
        catch (Exception ex)
        {
            _log.Error(Area, "Shuttle thread failed.", ex);
            _sink?.ReportStatus(new DeviceStatus(
                new InputSourceId(Kind, _wantedPath ?? string.Empty), "Contour ShuttleXpress / PRO",
                DeviceConnection.NotFound, Detail: "HID error: " + ex.Message));
        }
        finally
        {
            try
            {
                DeviceList.Local.Changed -= onChanged;
            }
            catch (Exception ex)
            {
                _log.Warn(Area, "Unsubscribe: " + ex.Message);
            }
        }
    }

    private void ReadLoop(IInputSink sink, HidStream stream, HidDevice device, string path, ShuttleReportTracker tracker, ButtonEdge[] edges)
    {
        var source = new InputSourceId(Kind, path);
        var buffer = new byte[Math.Max(device.GetMaxInputReportLength(), ContourShuttleReportParser.ReportLength + 1)];
        var warnedFormat = false;

        while (!_stop && !_reselect)
        {
            var read = stream.Read(buffer);
            if (read <= 0)
            {
                return;
            }

            if (!ContourShuttleReportParser.TryParse(buffer.AsSpan(0, read), out var report))
            {
                if (!warnedFormat)
                {
                    warnedFormat = true;
                    _log.Warn(Area, $"Unrecognised Contour report ({read} bytes, first byte 0x{buffer[0]:X2}); ignoring.");
                }

                continue;
            }

            var ts = Stopwatch.GetTimestamp();
            var changes = tracker.Update(report, edges);
            if (changes.RingChanged)
            {
                sink.Submit(new AxisInput(source, ControlIds.ShuttleRing, ts, changes.Ring));
            }

            if (changes.JogDelta != 0)
            {
                sink.Submit(new RelativeInput(source, ControlIds.JogWheel, ts, changes.JogDelta));
            }

            for (var i = 0; i < changes.ButtonEdgeCount; i++)
            {
                sink.Submit(new ButtonInput(source, edges[i].ControlId, ts, edges[i].IsPressed));
            }
        }
    }

    private void CloseStream()
    {
        HidStream? stream;
        lock (_streamGate)
        {
            stream = _stream;
        }

        try
        {
            stream?.Dispose();
        }
        catch
        {
            // best effort: this only exists to unblock Read
        }
    }

    private (HidDevice Device, ContourModel Model)? SelectDevice(string? wantedPath)
    {
        foreach (var f in FindDevices())
        {
            if (wantedPath is null)
            {
                return f;
            }

            if (string.Equals(f.Device.DevicePath, wantedPath, StringComparison.OrdinalIgnoreCase))
            {
                return f;
            }
        }

        // A specific path that is not attached is "not found" (do not silently use another unit).
        return null;
    }

    private static IEnumerable<(HidDevice Device, ContourModel Model)> FindDevices()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in DeviceList.Local.GetHidDevices())
        {
            if (ContourModels.TryGet(d.VendorID, d.ProductID, out var model) && seen.Add(d.DevicePath))
            {
                yield return (d, model);
            }
        }
    }
}
