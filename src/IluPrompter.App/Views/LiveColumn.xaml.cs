using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IluPrompter.App.Logic;
using IluPrompter.App.ViewModels;
using IluPrompter.Rendering;

namespace IluPrompter.App.Views;

public partial class LiveColumn : UserControl
{
    public LiveColumn()
    {
        InitializeComponent();
        LayoutUpdated += OnLayoutUpdated;
    }

    /// <summary>The live panel (D7): its 32 px bar is what the flyout lines up with.</summary>
    public LivePanel PanelView => PanelArea;

    /// <summary>The live preview; the shell attaches it to the input bridge and to PreviewServices.</summary>
    public PrompterPreview PreviewControl => Preview;

    private LiveViewModel? Model => DataContext as LiveViewModel;

    private void OnSlower(object sender, RoutedEventArgs e) => Model?.StepSpeed(-1, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));

    private void OnFaster(object sender, RoutedEventArgs e) => Model?.StepSpeed(+1, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));

    /// <summary>
    /// Fits the live panel to the height left below the readouts (DESIGN.md "Where the body goes"). The height is measured with
    /// no give-way applied (what hiding the transport captions and moving the preview caption freed is added back) and with the
    /// LIVE caption's room always reserved, so it depends on the window alone and never on LIVE, hiding or the panel itself.
    /// Only changes are passed on, so a layout pass that changes nothing costs a few additions.
    /// </summary>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (Model is not { } live || ColumnGrid.ActualHeight <= 0) return;
        var free = ColumnGrid.ActualHeight;
        for (var row = 0; row < 6; row++) free -= ColumnGrid.RowDefinitions[row].ActualHeight;
        var captionsByTier = live.Tier == LayoutTier.Wide;
        var panel = live.Panel;
        if (panel.HideTransportCaptions && captionsByTier) free += LivePanelFit.TransportCaptionsHeight;
        if (panel.MovePreviewCaption) free += LivePanelFit.PreviewCaptionHeight;
        panel.UpdateFit(free, captionsByTier);
    }

    /// <summary>Keyboard focus to Play (the live column is reachable without the mouse; Play is first in the order).</summary>
    public void FocusPlay() => PlayButton.Focus();
}
