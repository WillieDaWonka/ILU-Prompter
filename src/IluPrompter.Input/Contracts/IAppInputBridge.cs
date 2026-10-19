using System.Windows;

namespace IluPrompter.Input;

/// <summary>
/// In-window input only (no hooks, no global keyboard capture). Converts WPF keyboard and preview-wheel
/// events into Core InputEvents and submits them to IInputSink on the UI thread.
/// </summary>
public interface IAppInputBridge
{
    /// <summary>Handle PreviewKeyDown/PreviewKeyUp at window level. Each key becomes a ButtonInput with
    /// Source (Keyboard, "app") and ControlId = ControlIds.KeyChord(...); e.Handled = sink.Submit(...).
    /// Ignores pure modifier keys and the Windows key. When keyboard focus is in a TextBoxBase, PasswordBox
    /// or editable ComboBox, the event is submitted as Typing context (only Global bindings apply) even if
    /// IKeyModeState.Mode is Transport.</summary>
    void AttachWindow(Window window);

    void DetachWindow(Window window);

    /// <summary>Mouse wheel and buttons over this element (the preview) become input from (MouseWheel, "app"):
    /// RelativeInput Wheel (Delta / 120) and ButtonInput Middle/XButton1/XButton2. e.Handled = sink.Submit(...).</summary>
    void AttachWheelArea(UIElement element);

    void DetachWheelArea(UIElement element);
}
