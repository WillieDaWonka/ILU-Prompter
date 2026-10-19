using System.Windows.Controls;
using System.Windows.Input;

namespace IluPrompter.App.Controls;

/// <summary>
/// A live panel tab or its hide toggle (DESIGN.md "Live panel (D7)", deviation 18). A plain button that answers Enter and
/// the mouse only. Space, the arrows, Home, Page Up / Page Down, Ctrl+Up / Ctrl+Down, B, M and 0 keep their transport
/// meanings with focus on it, so it never uses the WPF TabControl (arrows, Home / End, Ctrl+Tab) and ignores Space,
/// which would otherwise press a button.
/// </summary>
public class PanelTabButton : Button
{
    /// <summary>The only key a tab answers.</summary>
    public static bool HandlesKey(Key key) => key == Key.Enter;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!HandlesKey(e.Key)) return;   // not handled: it carries on to the transport keys
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (!HandlesKey(e.Key)) return;
        base.OnKeyUp(e);
    }
}
