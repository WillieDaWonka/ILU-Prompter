using System.Windows;
using System.Windows.Input;
using IluPrompter.App.Services;

namespace IluPrompter.App.Views;

public partial class AppDialog : Window
{
    private readonly DialogRequest _request;
    private readonly bool _hasPrompt;

    public AppDialog(DialogRequest request, string? promptText = null)
    {
        _request = request;
        InitializeComponent();
        Title = request.Title;
        TitleText.Text = request.Title;
        MessageText.Text = request.Message;
        MessageText.Visibility = request.Message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        Primary.Content = request.PrimaryLabel;
        if (request.SecondaryLabel is { } secondary)
        {
            Secondary.Content = secondary;
            Secondary.Visibility = Visibility.Visible;
        }

        if (request.TertiaryLabel is { } tertiary)
        {
            Tertiary.Content = tertiary;
            Tertiary.Visibility = Visibility.Visible;
        }

        if (promptText is not null)
        {
            _hasPrompt = true;
            PromptBox.Text = promptText;
            PromptBox.Visibility = Visibility.Visible;
        }

        // The safe choice is the default: Enter presses it, and the focus ring starts on it.
        if (request.PrimaryIsDefault || request.SecondaryLabel is null) Primary.IsDefault = true;
        else Secondary.IsDefault = true;
    }

    public DialogChoice Choice { get; private set; } = DialogChoice.Cancelled;

    public string PromptText => PromptBox.Text;

    private void OnContentRendered(object? sender, EventArgs e)
    {
        if (_hasPrompt)
        {
            PromptBox.Focus();
            PromptBox.SelectAll();
        }
        else if (_request.PrimaryIsDefault || _request.SecondaryLabel is null) Primary.Focus();
        else Secondary.Focus();
    }

    private void Finish(DialogChoice choice)
    {
        Choice = choice;
        DialogResult = choice != DialogChoice.Cancelled;
    }

    private void OnPrimary(object sender, RoutedEventArgs e) => Finish(DialogChoice.Primary);

    private void OnSecondary(object sender, RoutedEventArgs e) => Finish(DialogChoice.Secondary);

    private void OnTertiary(object sender, RoutedEventArgs e) => Finish(DialogChoice.Tertiary);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Finish(DialogChoice.Cancelled);
        e.Handled = true;
    }

    private void OnPromptKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Finish(DialogChoice.Primary);
        e.Handled = true;
    }
}
