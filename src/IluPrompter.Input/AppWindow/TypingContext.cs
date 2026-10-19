using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace IluPrompter.Input;

/// <summary>Decides whether keyboard focus is in a text input (so only Global key bindings may act).</summary>
internal static class TypingContext
{
    /// <summary>True when <paramref name="focused"/> is a TextBoxBase (TextBox, RichTextBox), a PasswordBox
    /// or an editable ComboBox. The caret-bearing part of an editable ComboBox is a TextBox, so it is
    /// covered either way.</summary>
    public static bool IsTextInput(object? focused) => focused switch
    {
        TextBoxBase => true,
        PasswordBox => true,
        ComboBox { IsEditable: true } => true,
        _ => false,
    };

    /// <summary>Reads the live keyboard focus. UI thread only.</summary>
    public static bool IsTextInputFocused() => IsTextInput(Keyboard.FocusedElement);
}
