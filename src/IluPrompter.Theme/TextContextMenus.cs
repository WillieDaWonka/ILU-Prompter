using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace IluPrompter.Theme;

/// <summary>
/// WPF's built-in text box context menu uses its own (light) default style. This replaces it with a normal
/// <see cref="ContextMenu"/> that the theme skins (DESIGN.md: no white anywhere).
/// </summary>
public static class TextContextMenus
{
    private static bool _registered;

    /// <summary>Call once from App startup. Gives every TextBoxBase without its own menu a dark Cut/Copy/Paste menu.</summary>
    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        EventManager.RegisterClassHandler(typeof(TextBoxBase), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBoxBase box && box.ReadLocalValue(FrameworkElement.ContextMenuProperty) == DependencyProperty.UnsetValue)
            box.ContextMenu = Create(box);
    }

    public static ContextMenu Create(TextBoxBase target)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Cut", ApplicationCommands.Cut, target, "Ctrl+X"));
        menu.Items.Add(Item("Copy", ApplicationCommands.Copy, target, "Ctrl+C"));
        menu.Items.Add(Item("Paste", ApplicationCommands.Paste, target, "Ctrl+V"));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Select all", ApplicationCommands.SelectAll, target, "Ctrl+A"));
        return menu;
    }

    private static MenuItem Item(string header, ICommand command, IInputElement target, string gesture) =>
        new() { Header = header, Command = command, CommandTarget = target, InputGestureText = gesture };
}
