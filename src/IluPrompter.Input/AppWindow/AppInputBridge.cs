using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using IluPrompter.Core;

namespace IluPrompter.Input;

/// <summary>STUB (stream B replaces the body). Keyboard only, no typing-context detection, no wheel.</summary>
public sealed class AppInputBridge : IAppInputBridge
{
    private static readonly InputSourceId Source = new(ControllerKind.Keyboard, AppSources.App);
    private readonly IInputSink _sink;

    public AppInputBridge(IInputSink sink) => _sink = sink;

    public void AttachWindow(Window window) => window.PreviewKeyDown += OnPreviewKeyDown;

    public void DetachWindow(Window window) => window.PreviewKeyDown -= OnPreviewKeyDown;

    public void AttachWheelArea(UIElement element)
    {
    }

    public void DetachWheelArea(UIElement element)
    {
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var m = Keyboard.Modifiers;
        var chord = ControlIds.KeyChord(m.HasFlag(ModifierKeys.Control), m.HasFlag(ModifierKeys.Alt), m.HasFlag(ModifierKeys.Shift), key.ToString());
        e.Handled = _sink.Submit(new ButtonInput(Source, chord, Stopwatch.GetTimestamp(), true, e.IsRepeat));
    }
}
