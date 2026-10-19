using System.Windows;
using IluPrompter.App.Views;

namespace IluPrompter.App.Services;

public enum DialogChoice
{
    Cancelled = 0,
    Primary,
    Secondary,
    Tertiary,
}

/// <summary>
/// An app dialog in the house skin (never the native white MessageBox). One primary (verb + object) and, if needed,
/// a secondary and a tertiary. <see cref="PrimaryIsDefault"/> false makes the secondary the default button
/// (used for "Close the output?": the safe choice, "Keep output", is the default).
/// </summary>
public sealed record DialogRequest(
    string Title,
    string Message,
    string PrimaryLabel,
    string? SecondaryLabel = null,
    string? TertiaryLabel = null,
    bool PrimaryIsDefault = true,
    bool PrimaryIsRisky = false);

public interface IDialogService
{
    DialogChoice Ask(DialogRequest request);

    /// <summary>One-button notice (an error or a result). The button reads "Close" unless given.</summary>
    void Inform(string title, string message, string buttonLabel = "Close");

    /// <summary>A single-line text prompt. Returns null if cancelled.</summary>
    string? Prompt(string title, string message, string initialText, string primaryLabel);
}

public sealed class DialogService : IDialogService
{
    public DialogChoice Ask(DialogRequest request)
    {
        var dialog = new AppDialog(request) { Owner = ActiveWindow() };
        dialog.ShowDialog();
        return dialog.Choice;
    }

    public void Inform(string title, string message, string buttonLabel = "Close") =>
        Ask(new DialogRequest(title, message, buttonLabel));

    public string? Prompt(string title, string message, string initialText, string primaryLabel)
    {
        var dialog = new AppDialog(new DialogRequest(title, message, primaryLabel, "Cancel"), promptText: initialText)
        {
            Owner = ActiveWindow(),
        };
        dialog.ShowDialog();
        return dialog.Choice == DialogChoice.Primary ? dialog.PromptText : null;
    }

    private static Window? ActiveWindow()
    {
        var app = Application.Current;
        if (app is null) return null;
        foreach (Window window in app.Windows)
            if (window.IsActive) return window;
        return app.MainWindow is { IsLoaded: true } main ? main : null;
    }
}
