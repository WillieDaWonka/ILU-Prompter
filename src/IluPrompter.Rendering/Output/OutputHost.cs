using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering;

/// <summary>
/// IOutputSurface implementation: owns the output STA thread, its Dispatcher and the OutputWindow.
/// STUB (stream C replaces the body; keep the type name and constructor signature): no thread, no window.
/// </summary>
public sealed class OutputHost : IOutputSurface
{
    private readonly IMonitorService _monitors;
    private OutputState _state;

    public OutputHost(
        IScrollEngine engine,
        IPromptDocumentService documents,
        ISettingsService settings,
        IMonitorService monitors,
        IOutputDocumentFactory documentFactory,
        ILayoutGeometryBuilder geometryBuilder,
        IAppLog log)
    {
        _monitors = monitors;
        _state = OutputState.Initial with { Geometry = settings.Current.Output.LastGeometry };
    }

    public OutputState State => Volatile.Read(ref _state);

    public event EventHandler<OutputState>? StateChanged;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task ShowAsync()
    {
        var monitor = _monitors.ResolveOutputMonitor(null);
        Set(State with { IsOpen = monitor is not null, Monitor = monitor, Display = monitor is null ? OutputDisplayStatus.NoSecondDisplay : OutputDisplayStatus.Ready });
        return Task.CompletedTask;
    }

    public Task HideAsync()
    {
        Set(State with { IsOpen = false });
        return Task.CompletedTask;
    }

    public void SetBlank(bool blank) => Set(State with { IsBlank = blank });

    public Task ShutdownAsync() => Task.CompletedTask;

    private void Set(OutputState s)
    {
        Volatile.Write(ref _state, s);
        StateChanged?.Invoke(this, s);
    }
}
