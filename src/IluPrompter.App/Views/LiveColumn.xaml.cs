using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IluPrompter.App.ViewModels;
using IluPrompter.Rendering;

namespace IluPrompter.App.Views;

public partial class LiveColumn : UserControl
{
    public LiveColumn() => InitializeComponent();

    /// <summary>The live preview; the shell attaches it to the input bridge and to PreviewServices.</summary>
    public PrompterPreview PreviewControl => Preview;

    private LiveViewModel? Model => DataContext as LiveViewModel;

    private void OnSlower(object sender, RoutedEventArgs e) => Model?.StepSpeed(-1, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));

    private void OnFaster(object sender, RoutedEventArgs e) => Model?.StepSpeed(+1, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));

    /// <summary>Keyboard focus to Play (the live column is reachable without the mouse; Play is first in the order).</summary>
    public void FocusPlay() => PlayButton.Focus();
}
