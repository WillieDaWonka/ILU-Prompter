using System.Windows;
using System.Windows.Controls;
using IluPrompter.App.ViewModels;

namespace IluPrompter.App.Views.Pages;

public partial class ControllerPage : UserControl
{
    public ControllerPage() => InitializeComponent();

    private void OnRefresh(object sender, RoutedEventArgs e) => (DataContext as ControllerPageViewModel)?.RefreshDevices();
}
