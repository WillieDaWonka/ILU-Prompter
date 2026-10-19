using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IluPrompter.App.Editing;
using IluPrompter.App.ViewModels;
using IluPrompter.Documents;

namespace IluPrompter.App.Views;

public partial class EditorPanel : UserControl
{
    private EditorToolbarViewModel? _toolbar;

    public EditorPanel() => InitializeComponent();

    public EditorController? Controller { get; private set; }

    /// <summary>Create the controller for this editor and wire the toolbar and the LIVE edge to the live view model.</summary>
    public EditorController Attach(EditorServices services, EditorToolbarViewModel toolbar, LiveViewModel live)
    {
        _toolbar = toolbar;
        DataContext = toolbar;
        var controller = new EditorController(Editor, toolbar, services);
        Controller = controller;

        void ApplyZoom(double z)
        {
            Zoom.ScaleX = z;
            Zoom.ScaleY = z;
        }

        ApplyZoom(controller.Zoom / 100.0);
        controller.ZoomChanged += (_, z) => ApplyZoom(z);
        controller.ReadingLineMoved += (_, rect) => PlaceReadingLine(rect);

        void SyncLive()
        {
            toolbar.IsLive = live.IsLive;
            var display = live.OutputChip.Subject.StartsWith("Display", StringComparison.Ordinal) ? live.OutputChip.Subject : "the output";
            toolbar.LiveCaption = "Changes show on " + display + " as you type";
        }

        SyncLive();
        live.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LiveViewModel.IsLive) or nameof(LiveViewModel.OutputChip) or null or "") SyncLive();
        };
        return controller;
    }

    /// <summary>Wide shows Find and zoom in the toolbar; the other tiers fold them into the overflow menu.</summary>
    public void ApplyTier(bool isWide)
    {
        if (_toolbar is not null) _toolbar.IsWide = isWide;
    }

    private void PlaceReadingLine(Rect? rect)
    {
        if (rect is not { } r)
        {
            Band.Visibility = Visibility.Collapsed;
            GutterMark.Visibility = Visibility.Collapsed;
            return;
        }

        var top = Editor.TranslatePoint(new Point(0, r.Top), ZoomHost).Y;
        Canvas.SetLeft(Band, 0);
        Canvas.SetTop(Band, top);
        Band.Width = Math.Max(0, ZoomHost.ActualWidth);
        Band.Height = Math.Max(1, r.Height);
        Band.Visibility = Visibility.Visible;

        Canvas.SetLeft(GutterMark, 6);
        Canvas.SetTop(GutterMark, top + Math.Max(0, (r.Height - 16) / 2));
        GutterMark.Visibility = Visibility.Visible;
    }

    // ===== toolbar =====

    private void OnUndo(object sender, RoutedEventArgs e) => Controller?.Undo();

    private void OnRedo(object sender, RoutedEventArgs e) => Controller?.Redo();

    private void OnBold(object sender, RoutedEventArgs e) => Controller?.ToggleBold();

    private void OnItalic(object sender, RoutedEventArgs e) => Controller?.ToggleItalic();

    private void OnUnderline(object sender, RoutedEventArgs e) => Controller?.ToggleUnderline();

    private void OnStrike(object sender, RoutedEventArgs e) => Controller?.ToggleStrikethrough();

    private void OnClear(object sender, RoutedEventArgs e) => Controller?.ClearFormatting();

    private void OnInsertSection(object sender, RoutedEventArgs e) => Controller?.InsertSectionMarker();

    private void OnSize(object sender, RoutedEventArgs e)
    {
        SizeToggle.IsChecked = false;
        if (sender is FrameworkElement { Tag: string name } && Enum.TryParse<ScriptSize>(name, out var size)) Controller?.ApplySize(size);
    }

    private void OnInk(object sender, RoutedEventArgs e)
    {
        InkToggle.IsChecked = false;
        if (sender is FrameworkElement { Tag: string token }) Controller?.ApplyInk(token);
    }

    private void OnHighlight(object sender, RoutedEventArgs e)
    {
        HighlightToggle.IsChecked = false;
        if (sender is FrameworkElement { Tag: string token }) Controller?.ApplyHighlight(token);
    }

    private void OnFind(object sender, RoutedEventArgs e) => Controller?.OpenFind();

    private void OnZoom(object sender, RoutedEventArgs e)
    {
        ZoomToggle.IsChecked = false;
        if (sender is FrameworkElement { Tag: int percent }) Controller?.SetZoom(percent);
    }

    private void OnOverflow(object sender, RoutedEventArgs e)
    {
        OverflowMenu.PlacementTarget = OverflowButton;
        OverflowMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        OverflowMenu.IsOpen = true;
    }

    private void OnZoomIn(object sender, RoutedEventArgs e) => Controller?.ZoomBy(+1);

    private void OnZoomOut(object sender, RoutedEventArgs e) => Controller?.ZoomBy(-1);

    private void OnZoomReset(object sender, RoutedEventArgs e) => Controller?.SetZoom(100);

    // ===== gutter =====

    private void OnGutterMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || Controller is null) return;
        Controller.JumpPromptToEditorPoint(e.GetPosition(Editor));
        e.Handled = true;
    }

    // ===== find bar =====

    private void OnFindVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            FindBox.Focus();
            FindBox.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnFindKeyDown(object sender, KeyEventArgs e)
    {
        if (Controller is null) return;
        switch (e.Key)
        {
            case Key.Enter:
            case Key.F3:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) Controller.FindPrevious();
                else Controller.FindNext();
                e.Handled = true;
                break;
            case Key.Escape:
                Controller.CloseFind();
                e.Handled = true;
                break;
        }
    }

    private void OnFindNext(object sender, RoutedEventArgs e) => Controller?.FindNext();

    private void OnFindPrevious(object sender, RoutedEventArgs e) => Controller?.FindPrevious();

    private void OnFindClose(object sender, RoutedEventArgs e) => Controller?.CloseFind();
}
