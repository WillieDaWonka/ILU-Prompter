using System.Windows;
using System.Windows.Controls;

namespace IluPrompter.App.Views;

public partial class LivePanel : UserControl
{
    public LivePanel() => InitializeComponent();

    /// <summary>The 32 px tab bar. The flyout's bottom edge is level with its bottom.</summary>
    public FrameworkElement TabBar => Bar;
}
