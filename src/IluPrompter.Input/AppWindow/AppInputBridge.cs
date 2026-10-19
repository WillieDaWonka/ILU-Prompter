using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using IluPrompter.Core;

namespace IluPrompter.Input;

/// <summary>
/// In-window input only (contract §2.12): WPF key events at window level and mouse wheel/buttons over the
/// preview, converted to Core <see cref="InputEvent"/>s and submitted on the UI thread. No hooks, no Raw
/// Input, no global capture.
/// </summary>
public sealed class AppInputBridge : IAppInputBridge
{
    private static readonly InputSourceId KeyboardApp = new(ControllerKind.Keyboard, AppSources.App);
    private static readonly InputSourceId KeyboardText = new(ControllerKind.Keyboard, AppSources.TextInput);
    private static readonly InputSourceId MouseApp = new(ControllerKind.MouseWheel, AppSources.App);

    private readonly IInputSink _sink;

    // Keys / mouse buttons whose press was handled; their release is swallowed too so a handled Space
    // does not leak a KeyUp into a focused control. Touched on the UI thread only.
    private readonly HashSet<Key> _swallowedKeys = new();
    private readonly HashSet<MouseButton> _swallowedButtons = new();
    private readonly HashSet<Window> _windows = new();
    private readonly HashSet<UIElement> _wheelAreas = new();

    public AppInputBridge(IInputSink sink) => _sink = sink;

    public void AttachWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!_windows.Add(window))
        {
            return;
        }

        window.PreviewKeyDown += OnPreviewKeyDown;
        window.PreviewKeyUp += OnPreviewKeyUp;
        window.Deactivated += OnWindowDeactivated;
    }

    public void DetachWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!_windows.Remove(window))
        {
            return;
        }

        window.PreviewKeyDown -= OnPreviewKeyDown;
        window.PreviewKeyUp -= OnPreviewKeyUp;
        window.Deactivated -= OnWindowDeactivated;
        _swallowedKeys.Clear();
    }

    public void AttachWheelArea(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!_wheelAreas.Add(element))
        {
            return;
        }

        element.PreviewMouseWheel += OnPreviewMouseWheel;
        element.PreviewMouseDown += OnPreviewMouseDown;
        element.PreviewMouseUp += OnPreviewMouseUp;
    }

    public void DetachWheelArea(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!_wheelAreas.Remove(element))
        {
            return;
        }

        element.PreviewMouseWheel -= OnPreviewMouseWheel;
        element.PreviewMouseDown -= OnPreviewMouseDown;
        element.PreviewMouseUp -= OnPreviewMouseUp;
        _swallowedButtons.Clear();
    }

    // ---- pure event construction (unit-tested) ------------------------------------------------------

    /// <summary>Wheel notches for a WPF <c>MouseWheelEventArgs.Delta</c>: Delta / 120.0, never rounded
    /// (high-resolution wheels send fractions of a notch; the router accumulates them).</summary>
    internal static double WheelNotches(int delta) => delta / 120.0;

    internal static RelativeInput CreateWheelEvent(int delta, long timestamp) =>
        new(MouseApp, ControlIds.Wheel, timestamp, WheelNotches(delta));

    /// <summary>Maps a WPF mouse button to its canonical control id; null for buttons the app does not forward.</summary>
    internal static string? MouseButtonControlId(MouseButton button) => button switch
    {
        MouseButton.Middle => ControlIds.MiddleButton,
        MouseButton.XButton1 => ControlIds.XButton1,
        MouseButton.XButton2 => ControlIds.XButton2,
        _ => null,
    };

    internal static ButtonInput? CreateMouseButtonEvent(MouseButton button, bool isPressed, long timestamp) =>
        MouseButtonControlId(button) is { } id ? new ButtonInput(MouseApp, id, timestamp, isPressed) : null;

    internal static ButtonInput CreateKeyEvent(string chord, bool inTextInput, bool isPressed, bool isRepeat, long timestamp) =>
        new(inTextInput ? KeyboardText : KeyboardApp, chord, timestamp, isPressed, isRepeat);

    // ---- WPF handlers -------------------------------------------------------------------------------

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = KeyChordFormatter.Resolve(e.Key, e.SystemKey);
        if (!KeyChordFormatter.TryFormat(key, e.KeyboardDevice.Modifiers, out var chord))
        {
            return;
        }

        var input = CreateKeyEvent(chord, TypingContext.IsTextInputFocused(), isPressed: true, e.IsRepeat, Stopwatch.GetTimestamp());
        if (_sink.Submit(input))
        {
            e.Handled = true;
            _swallowedKeys.Add(key);
        }
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        var key = KeyChordFormatter.Resolve(e.Key, e.SystemKey);
        if (_swallowedKeys.Remove(key))
        {
            e.Handled = true;
        }

        if (!KeyChordFormatter.TryFormat(key, e.KeyboardDevice.Modifiers, out var chord))
        {
            return;
        }

        // The release edge goes to the sink as well (the router acts on presses only).
        _sink.Submit(CreateKeyEvent(chord, TypingContext.IsTextInputFocused(), isPressed: false, isRepeat: false, Stopwatch.GetTimestamp()));
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => _swallowedKeys.Clear();

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0)
        {
            return;
        }

        if (_sink.Submit(CreateWheelEvent(e.Delta, Stopwatch.GetTimestamp())))
        {
            e.Handled = true;
        }
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (CreateMouseButtonEvent(e.ChangedButton, true, Stopwatch.GetTimestamp()) is not { } input)
        {
            return;
        }

        if (_sink.Submit(input))
        {
            e.Handled = true;
            _swallowedButtons.Add(e.ChangedButton);
        }
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (CreateMouseButtonEvent(e.ChangedButton, false, Stopwatch.GetTimestamp()) is not { } input)
        {
            return;
        }

        _sink.Submit(input);
        if (_swallowedButtons.Remove(e.ChangedButton))
        {
            e.Handled = true;
        }
    }
}
