using IluPrompter.Core;

namespace IluPrompter.Input;

// STUBS (stream B replaces each class with a real implementation in its own file:
// RawInput/RawMouseWheelProvider.cs, XInput/XInputProvider.cs, Shuttle/ContourShuttleProvider.cs,
// and deletes this file). Keep the class names and constructor signatures.

public sealed class RawMouseWheelProvider : IScrollInputProvider
{
    public RawMouseWheelProvider(IAppLog log) { }
    public ControllerKind Kind => ControllerKind.MouseWheel;
    public bool IsRunning => false;
    public IReadOnlyList<InputDeviceInfo> EnumerateDevices() => Array.Empty<InputDeviceInfo>();
    public void Start(IInputSink sink, string? deviceId) { }
    public void Stop() { }
    public void Dispose() { }
}

public sealed class XInputProvider : IScrollInputProvider
{
    public XInputProvider(IAppLog log) { }
    public ControllerKind Kind => ControllerKind.Xbox;
    public bool IsRunning => false;
    public IReadOnlyList<InputDeviceInfo> EnumerateDevices() => Array.Empty<InputDeviceInfo>();
    public void Start(IInputSink sink, string? deviceId) =>
        sink.ReportStatus(new DeviceStatus(new InputSourceId(Kind, "xinput:0"), "Xbox", DeviceConnection.NotFound));
    public void Stop() { }
    public void Dispose() { }
}

public sealed class ContourShuttleProvider : IScrollInputProvider
{
    public ContourShuttleProvider(IAppLog log) { }
    public ControllerKind Kind => ControllerKind.JogShuttle;
    public bool IsRunning => false;
    public IReadOnlyList<InputDeviceInfo> EnumerateDevices() => Array.Empty<InputDeviceInfo>();
    public void Start(IInputSink sink, string? deviceId) =>
        sink.ReportStatus(new DeviceStatus(new InputSourceId(Kind, ""), "Shuttle", DeviceConnection.NotFound));
    public void Stop() { }
    public void Dispose() { }
}
