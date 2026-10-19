using System.Windows.Input;

namespace IluPrompter.App.Views;

/// <summary>
/// Window-level routed commands with their gestures (DESIGN.md "Keyboard shortcuts", Global group). Routed commands,
/// not InputBindings, because InputBindings cannot bind to the view model. F5, F8 and F11 are not here: the input
/// router owns them, so they work in both key modes.
/// </summary>
public static class AppCommands
{
    public static readonly RoutedUICommand NewScript = Make("New script", nameof(NewScript), Key.N, ModifierKeys.Control);

    public static readonly RoutedUICommand OpenScript = Make("Open script", nameof(OpenScript), Key.O, ModifierKeys.Control);

    public static readonly RoutedUICommand SaveScript = Make("Save script", nameof(SaveScript), Key.S, ModifierKeys.Control);

    public static readonly RoutedUICommand SaveScriptAs = Make("Save script as", nameof(SaveScriptAs), Key.S, ModifierKeys.Control | ModifierKeys.Shift);

    public static readonly RoutedUICommand ShowSettings = Make("Settings", nameof(ShowSettings), Key.OemComma, ModifierKeys.Control);

    public static readonly RoutedUICommand ShowShortcuts = Make("Shortcut list", nameof(ShowShortcuts), Key.OemQuestion, ModifierKeys.Control);

    public static readonly RoutedUICommand GoToScript = Make("Go to the script", nameof(GoToScript), Key.F2, ModifierKeys.None);

    /// <summary>F9: show or hide the live panel. Global, like Ctrl+, : works while typing.</summary>
    public static readonly RoutedUICommand ToggleLivePanel = Make("Show or hide the live panel", nameof(ToggleLivePanel), Key.F9, ModifierKeys.None);

    /// <summary>Ctrl+F9: open the live panel's other tab, showing the panel if it is hidden.</summary>
    public static readonly RoutedUICommand OtherLivePanelTab = Make("Open the live panel's other tab", nameof(OtherLivePanelTab), Key.F9, ModifierKeys.Control);

    private static RoutedUICommand Make(string text, string name, Key key, ModifierKeys modifiers)
    {
        var gestures = new InputGestureCollection();
        gestures.Add(new KeyGesture(key, modifiers));
        return new RoutedUICommand(text, name, typeof(AppCommands), gestures);
    }
}
