using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IluPrompter.App.ViewModels;

namespace IluPrompter.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        // Esc closes the window, unless it is closing an open drop-down.
        if (Keyboard.FocusedElement is ComboBox { IsDropDownOpen: true }) return;
        Close();
        e.Handled = true;
    }
}
