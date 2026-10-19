using System.Diagnostics;
using System.Windows.Interop;
using System.Windows.Threading;
using IluPrompter.Core;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;

namespace IluPrompter.Input;

/// <summary>
/// A dedicated prompter mouse read through Raw Input (works while the app is not focused). Only mouse
/// (usage page 1, usage 2) is registered; the keyboard is never registered. Only the device the user
/// selected is read; everything from other mice is dropped.
/// </summary>
/// <remarks>
/// Threading: one STA thread "ILU RawInput" owns a never-shown HwndSource (message-only by default) and
/// runs a Dispatcher. Every field below marked "provider thread" is touched only there; Start/Stop
/// marshal onto it. Events go to the sink straight from the window procedure.
/// Set the environment variable ILU_RAWINPUT_TOOLWINDOW=1 to use a zero-size, never-shown top-level tool
/// window instead of a message-only window (the contract's fallback if Phase-0 S4b shows no WM_INPUT
/// while unfocused).
/// </remarks>
public sealed class RawMouseWheelProvider : IScrollInputProvider
{
    private const string Area = "input.rawmouse";
    private const int WM_INPUT = 0x00FF;
    private const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
    private const int GIDC_ARRIVAL = 1;
    private const int GIDC_REMOVAL = 2;
    private static readonly IntPtr HWND_MESSAGE = new(-3);
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private readonly IAppLog _log;
    private readonly object _gate = new();

    private Thread? _thread;
    private Dispatcher? _dispatcher;
    private volatile bool _running;

    // Provider thread only.
    private HwndSource? _source;
    private IInputSink? _sink;
    private string? _selectedPath;
    private IntPtr _selectedHandle;
    private string _selectedName = "Mouse";
    private readonly ButtonEdge[] _edges = new ButtonEdge[RawMouseNormaliser.MaxEdges];

    public RawMouseWheelProvider(IAppLog log) => _log = log;

    public ControllerKind Kind => ControllerKind.MouseWheel;

    public bool IsRunning => _running;

    public IReadOnlyList<InputDeviceInfo> EnumerateDevices()
    {
        try
        {
            var list = new List<InputDeviceInfo>();
            foreach (var d in RawInputDevice.GetDevices())
            {
                if (d.DeviceType != RawInputDeviceType.Mouse || string.IsNullOrEmpty(d.DevicePath))
                {
                    continue;
                }

                list.Add(new InputDeviceInfo(
                    ControllerKind.MouseWheel,
                    d.DevicePath,
                    RawMouseNormaliser.DisplayName(d.ProductName, d.ManufacturerName, d.VendorId, d.ProductId),
                    d.IsConnected));
            }

            return list;
        }
        catch (Exception ex)
        {
            _log.Warn(Area, "Could not enumerate mice: " + ex.Message);
            return Array.Empty<InputDeviceInfo>();
        }
    }

    public void Start(IInputSink sink, string? deviceId)
    {
        ArgumentNullException.ThrowIfNull(sink);
        try
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                // A "first available mouse" would be the operator's own mouse and would scroll the
                // prompter from any window. A dedicated device must be chosen explicitly.
                Stop();
                sink.ReportStatus(new DeviceStatus(
                    new InputSourceId(Kind, string.Empty), "Dedicated mouse", DeviceConnection.NotFound,
                    Detail: "Choose the prompter mouse in Settings"));
                return;
            }

            lock (_gate)
            {
                if (_thread is not null && _dispatcher is not null)
                {
                    var path = deviceId;
                    _dispatcher.BeginInvoke(() => SelectDevice(sink, path));
                    return;
                }

                using var ready = new ManualResetEventSlim(false);
                var thread = new Thread(() => ThreadMain(sink, deviceId, ready))
                {
                    Name = "ILU RawInput",
                    IsBackground = true,
                };
                thread.SetApartmentState(ApartmentState.STA);
                _thread = thread;
                thread.Start();
                if (!ready.Wait(TimeSpan.FromSeconds(5)))
                {
                    _log.Warn(Area, "Raw Input thread did not signal ready within 5 s.");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(Area, "Start failed.", ex);
            sink.ReportStatus(new DeviceStatus(
                new InputSourceId(Kind, deviceId ?? string.Empty), "Dedicated mouse", DeviceConnection.NotFound,
                Detail: "Raw Input could not start: " + ex.Message));
        }
    }

    public void Stop()
    {
        Thread? thread;
        Dispatcher? dispatcher;
        lock (_gate)
        {
            thread = _thread;
            dispatcher = _dispatcher;
            _thread = null;
            _dispatcher = null;
        }

        _running = false;
        if (thread is null)
        {
            return;
        }

        try
        {
            dispatcher?.BeginInvokeShutdown(DispatcherPriority.Normal);
            if (!thread.Join(TimeSpan.FromSeconds(2)))
            {
                _log.Warn(Area, "Raw Input thread did not stop within 2 s.");
            }
        }
        catch (Exception ex)
        {
            _log.Warn(Area, "Stop: " + ex.Message);
        }
    }

    public void Dispose() => Stop();

    // ---- provider thread -----------------------------------------------------------------------------

    private void ThreadMain(IInputSink sink, string deviceId, ManualResetEventSlim ready)
    {
        try
        {
            _sink = sink;
            _dispatcher = Dispatcher.CurrentDispatcher;

            var toolWindow = Environment.GetEnvironmentVariable("ILU_RAWINPUT_TOOLWINDOW") == "1";
            var parameters = new HwndSourceParameters("ILU RawInput", 0, 0)
            {
                PositionX = 0,
                PositionY = 0,
                ParentWindow = toolWindow ? IntPtr.Zero : HWND_MESSAGE,
                WindowStyle = toolWindow ? WS_POPUP : 0,
                ExtendedWindowStyle = toolWindow ? WS_EX_TOOLWINDOW : 0,
            };
            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);

            // Mouse only. InputSink = keep receiving while another window is in front;
            // DevNotify = WM_INPUT_DEVICE_CHANGE for plug/unplug.
            RawInputDevice.RegisterDevice(
                HidUsageAndPage.Mouse,
                RawInputDeviceFlags.InputSink | RawInputDeviceFlags.DevNotify,
                _source.Handle);

            _running = true;
            SelectDevice(sink, deviceId);
            ready.Set();
            Dispatcher.Run();
        }
        catch (Exception ex)
        {
            _log.Error(Area, "Raw Input thread failed.", ex);
            Report(DeviceConnection.NotFound, deviceId, "Raw Input failed: " + ex.Message);
            lock (_gate)
            {
                if (ReferenceEquals(_thread, Thread.CurrentThread))
                {
                    _thread = null;
                    _dispatcher = null;
                }
            }

            ready.Set();
        }
        finally
        {
            _running = false;
            try
            {
                RawInputDevice.UnregisterDevice(HidUsageAndPage.Mouse);
            }
            catch (Exception ex)
            {
                _log.Warn(Area, "Unregister: " + ex.Message);
            }

            try
            {
                _source?.RemoveHook(WndProc);
                _source?.Dispose();
            }
            catch (Exception ex)
            {
                _log.Warn(Area, "Dispose window: " + ex.Message);
            }

            _source = null;
            _sink = null;
            _selectedHandle = IntPtr.Zero;
        }
    }

    private void SelectDevice(IInputSink sink, string path)
    {
        _sink = sink;
        _selectedPath = path;
        _selectedHandle = IntPtr.Zero;
        try
        {
            foreach (var d in RawInputDevice.GetDevices())
            {
                if (d.DeviceType == RawInputDeviceType.Mouse && RawMouseNormaliser.SamePath(d.DevicePath, path))
                {
                    _selectedHandle = RawInputDeviceHandle.GetRawValue(d.Handle);
                    _selectedName = RawMouseNormaliser.DisplayName(d.ProductName, d.ManufacturerName, d.VendorId, d.ProductId);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Warn(Area, "Device lookup failed: " + ex.Message);
        }

        if (_selectedHandle != IntPtr.Zero)
        {
            Report(DeviceConnection.Connected, path, null);
        }
        else
        {
            Report(DeviceConnection.NotFound, path, "Mouse not found. Plug it in or choose another in Settings");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // handled stays false so DefWindowProc runs (required for WM_INPUT cleanup).
        try
        {
            if (msg == WM_INPUT)
            {
                OnRawInput(lParam);
            }
            else if (msg == WM_INPUT_DEVICE_CHANGE)
            {
                OnDeviceChange(wParam.ToInt32(), lParam);
            }
        }
        catch (Exception ex)
        {
            _log.Warn(Area, "Message handling: " + ex.Message);
        }

        return IntPtr.Zero;
    }

    private void OnRawInput(IntPtr lParam)
    {
        var selected = _selectedHandle;
        var sink = _sink;
        if (selected == IntPtr.Zero || sink is null)
        {
            return;
        }

        // Read the small fixed header first and drop other devices' records before any parsing.
        var raw = (RawInputHandle)lParam;
        var header = User32.GetRawInputDataHeader(raw);
        if (header.Type != RawInputDeviceType.Mouse || RawInputDeviceHandle.GetRawValue(header.DeviceHandle) != selected)
        {
            return;
        }

        var mouse = User32.GetRawInputMouseData(raw, out _);
        var source = new InputSourceId(Kind, _selectedPath!);
        var now = Stopwatch.GetTimestamp();
        var flags = mouse.Buttons;

        if (RawMouseNormaliser.HasVerticalWheel(flags))
        {
            sink.Submit(new RelativeInput(source, ControlIds.Wheel, now, RawMouseNormaliser.WheelNotches(mouse.ButtonData)));
        }

        var edges = _edges;
        var count = RawMouseNormaliser.ButtonEdges(flags, edges);
        for (var i = 0; i < count; i++)
        {
            sink.Submit(new ButtonInput(source, edges[i].ControlId, now, edges[i].IsPressed));
        }
    }

    private void OnDeviceChange(int action, IntPtr deviceHandle)
    {
        if (_selectedPath is null)
        {
            return;
        }

        if (action == GIDC_REMOVAL)
        {
            if (_selectedHandle != IntPtr.Zero && deviceHandle == _selectedHandle)
            {
                _selectedHandle = IntPtr.Zero;
                Report(DeviceConnection.Disconnected, _selectedPath, "Mouse unplugged");
            }
        }
        else if (action == GIDC_ARRIVAL && _selectedHandle == IntPtr.Zero && _sink is { } sink)
        {
            // Re-resolve by path; reports Connected when it is the selected mouse.
            SelectDevice(sink, _selectedPath);
        }
    }

    private void Report(DeviceConnection state, string path, string? detail) =>
        _sink?.ReportStatus(new DeviceStatus(new InputSourceId(Kind, path), _selectedName, state, Detail: detail));
}
