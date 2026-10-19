namespace IluPrompter.App.Logic;

/// <summary>One line of the shortcut list.</summary>
public sealed record ShortcutEntry(string Keys, string Action, string? Note = null);

public sealed record ShortcutGroup(string Title, string Caption, IReadOnlyList<ShortcutEntry> Entries);

/// <summary>
/// The keyboard map from DESIGN.md "Keyboard shortcuts", read-only in Settings › Shortcuts. The Global and Transport
/// groups mirror the defaults the input router applies (<c>DefaultBindings</c>); the Script group is the editor's.
/// </summary>
public static class ShortcutCatalog
{
    public static IReadOnlyList<ShortcutGroup> Groups { get; } =
    [
        new ShortcutGroup("Everywhere", "These work while you type and while the keys are with the transport.",
        [
            new("F5", "Play or pause"),
            new("F8", "Blank output on or off"),
            new("F11", "Show or hide the output"),
            new("Esc", "In the script: leave it and hand the keys to the transport. In transport: pause"),
            new("F9", "Show or hide the live panel (Controller, Output layout)"),
            new("Ctrl+F9", "Open the live panel's other tab"),
            new("Ctrl+,", "Settings"),
            new("Ctrl+/", "Shortcut list"),
            new("Ctrl+N", "New script"),
            new("Ctrl+O", "Open script"),
            new("Ctrl+S", "Save script"),
            new("Ctrl+Shift+S", "Save script as"),
            new("F2", "Go to the script (keys type)"),
        ]),
        new ShortcutGroup("Transport keys", "When the keys are with the transport (status bar: Transport keys).",
        [
            new("Space", "Play or pause"),
            new("Down / Up", "Display scrolling: speed up or down by 0.5 (Shift: 0.1)", "Line scrolling: next or previous line"),
            new("Right / Left", "Next or previous line"),
            new("Page Down / Page Up", "Next or previous paragraph"),
            new("Ctrl+Down / Ctrl+Up", "Next or previous section"),
            new("Home", "Jump to start and pause"),
            new("B", "Blank output on or off"),
            new("M", "Switch between Display scrolling and Line scrolling"),
            new("0", "Speed back to the start speed"),
        ]),
        new ShortcutGroup("Script keys", "While you type in the script.",
        [
            new("Ctrl+B / Ctrl+I / Ctrl+U", "Bold, italic, underline"),
            new("Ctrl+Shift+X", "Strikethrough"),
            new("Ctrl+Shift+H", "Highlight with the last highlight"),
            new("Ctrl+] / Ctrl+[", "Larger or smaller text, one step"),
            new("Ctrl+Space", "Clear formatting"),
            new("Ctrl+Shift+Enter", "Insert section marker"),
            new("Ctrl+F / F3", "Find, find next"),
            new("Ctrl+Z / Ctrl+Y", "Undo, redo"),
            new("Ctrl+= / Ctrl+−", "Editor zoom (the output is unaffected)"),
        ]),
    ];

    public const string ClickerNote =
        "Presenter clickers send Page Down / Page Up and Right / Left, so a clicker works as a controller with no setup.";
}
