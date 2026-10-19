using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IluPrompter.App.Logic;
using IluPrompter.App.ViewModels;
using IluPrompter.App.Views.Drawings;

namespace IluPrompter.App.Views;

/// <summary>
/// The Controller tab's body: the controller drawing and the info column (DESIGN.md "Controller drawing"). It only sets
/// properties whose value changed, on the 10 Hz poll's property notifications. It starts no timer, storyboard or
/// <c>CompositionTarget.Rendering</c> subscription (ARCHITECTURE §3 rule 10), and skips the work while it is not visible.
/// </summary>
public partial class ControllerPanelView : UserControl
{
    public static readonly DependencyProperty ControlColumnProperty = DependencyProperty.Register(
        nameof(ControlColumn), typeof(GridLength), typeof(ControllerPanelView), new PropertyMetadata(new GridLength(88)));

    public static readonly DependencyProperty ValueColumnProperty = DependencyProperty.Register(
        nameof(ValueColumn), typeof(GridLength), typeof(ControllerPanelView), new PropertyMetadata(new GridLength(64)));

    /// <summary>True in the flyout, where a bound control rests on <c>pressed</c> instead of <c>raised</c>.</summary>
    public static readonly DependencyProperty RaisedGroundProperty = DependencyProperty.Register(
        nameof(RaisedGround), typeof(bool), typeof(ControllerPanelView), new PropertyMetadata(false, (d, _) => ((ControllerPanelView)d).Refresh(force: true)));

    private readonly Dictionary<ControllerDrawingKind, IControllerDrawing> _drawings = new();
    private LivePanelViewModel? _panel;
    private ControllerDrawingKind _shown = ControllerDrawingKind.None;
    private bool? _appliedSmall;

    public ControllerPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += (_, _) => Refresh(force: true);
    }

    /// <summary>Width of the control-name column in a row: 88 in the large body, 60 in the small.</summary>
    public GridLength ControlColumn
    {
        get => (GridLength)GetValue(ControlColumnProperty);
        set => SetValue(ControlColumnProperty, value);
    }

    /// <summary>Width of the value column in a row: 64 in the large body, 44 in the small.</summary>
    public GridLength ValueColumn
    {
        get => (GridLength)GetValue(ValueColumnProperty);
        set => SetValue(ValueColumnProperty, value);
    }

    public bool RaisedGround
    {
        get => (bool)GetValue(RaisedGroundProperty);
        set => SetValue(RaisedGroundProperty, value);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_panel is not null)
        {
            _panel.PropertyChanged -= OnPanelChanged;
            _panel.Controller.PropertyChanged -= OnControllerChanged;
        }

        _panel = e.NewValue as LivePanelViewModel;
        if (_panel is not null)
        {
            _panel.PropertyChanged += OnPanelChanged;
            _panel.Controller.PropertyChanged += OnControllerChanged;
        }

        Refresh(force: true);
    }

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(LivePanelViewModel.IsSmall)) Refresh(force: false);
    }

    private void OnControllerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ControllerPanelViewModel.Model)) Refresh(force: false);
    }

    private void Refresh(bool force)
    {
        if (_panel is null || !IsVisible) return;
        ApplySize(_panel.IsSmall, force);
        ApplyModel(_panel.Controller.Model);
    }

    /// <summary>Large: drawing 320 x 160 at (0,4), info from x 336. Small: 240 x 120 at (0,0), info from x 252, three rows, no detail line.</summary>
    private void ApplySize(bool small, bool force)
    {
        if (!force && _appliedSmall == small) return;
        _appliedSmall = small;
        DrawingColumn.Width = new GridLength(small ? 240 : 320);
        GapColumn.Width = new GridLength(small ? 12 : 16);
        DrawingBox.Width = small ? 240 : 320;
        DrawingBox.Height = small ? 120 : 160;
        DrawingBox.Margin = new Thickness(0, small ? 0 : 4, 0, 0);
        DetailRow.Height = new GridLength(small ? 0 : 18);
        RowsRow.Height = new GridLength(small ? 66 : 88);
        ControlColumn = new GridLength(small ? 60 : 88);
        ValueColumn = new GridLength(small ? 44 : 64);
        MissingDetail.Visibility = small ? Visibility.Collapsed : Visibility.Visible;   // the keyboard sentence stays in the chip's tooltip
        if (_drawings.TryGetValue(ControllerDrawingKind.Xbox, out var xbox)) ((XboxDrawing)xbox).Small = small;
    }

    private void ApplyModel(ControllerPanelModel model)
    {
        var notChosen = model.State == ControllerPanelState.NotChosen;
        NotChosenPanel.Visibility = notChosen ? Visibility.Visible : Visibility.Collapsed;
        Main.Visibility = notChosen ? Visibility.Collapsed : Visibility.Visible;
        if (notChosen) return;

        var missing = model.State is ControllerPanelState.NotFound or ControllerPanelState.Lost;
        RowsList.Visibility = model.State == ControllerPanelState.InUse ? Visibility.Visible : Visibility.Collapsed;
        NoInputPanel.Visibility = model.State == ControllerPanelState.NoInputYet ? Visibility.Visible : Visibility.Collapsed;
        MissingPanel.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
        DetailText.Visibility = missing ? Visibility.Collapsed : Visibility.Visible;

        ShowDrawing(model.Frame);
        ApplyBattery(model.Battery);
    }

    private void ShowDrawing(ControllerFrame frame)
    {
        if (frame.Kind == ControllerDrawingKind.None)
        {
            DrawingBox.Visibility = Visibility.Collapsed;
            return;
        }

        DrawingBox.Visibility = Visibility.Visible;
        if (!_drawings.TryGetValue(frame.Kind, out var drawing))
        {
            drawing = frame.Kind switch
            {
                ControllerDrawingKind.Mouse => new MouseDrawing(),
                ControllerDrawingKind.Xbox => new XboxDrawing { Small = _appliedSmall == true },
                ControllerDrawingKind.ShuttlePro => new ShuttleProDrawing(),
                _ => new ShuttleXpressDrawing(),
            };
            _drawings[frame.Kind] = drawing;
        }

        if (_shown != frame.Kind)
        {
            DrawingHost.Content = drawing;
            _shown = frame.Kind;
        }

        drawing.Apply(frame, RaisedGround);
    }

    /// <summary>
    /// A 20 x 10 outline with a 2 x 4 nub and three 4 x 5 segments: Full 3, Medium 2, Low 1 in <c>attention</c>, Empty 0 with
    /// the outline in <c>attention</c>. Wired: no glyph, the word. Unknown: nothing.
    /// </summary>
    private void ApplyBattery(BatteryGlyph battery)
    {
        BatteryPanel.Visibility = battery == BatteryGlyph.None ? Visibility.Collapsed : Visibility.Visible;
        if (battery == BatteryGlyph.None) return;

        var attention = (Brush)FindResource("Ilu.Brush.Attention");
        var text = (Brush)FindResource("Ilu.Brush.Text");
        var muted = (Brush)FindResource("Ilu.Brush.TextMuted");
        var segments = battery switch { BatteryGlyph.Full => 3, BatteryGlyph.Medium => 2, BatteryGlyph.Low => 1, _ => 0 };
        BatteryIcon.Visibility = battery == BatteryGlyph.Wired ? Visibility.Collapsed : Visibility.Visible;
        BatteryOutline.Stroke = battery == BatteryGlyph.Empty ? attention : muted;
        var all = new[] { BatterySegment1, BatterySegment2, BatterySegment3 };
        for (var i = 0; i < all.Length; i++)
        {
            all[i].Visibility = i < segments ? Visibility.Visible : Visibility.Collapsed;
            all[i].Fill = battery == BatteryGlyph.Low ? attention : text;
        }

        BatteryWord.Foreground = battery is BatteryGlyph.Low or BatteryGlyph.Empty ? attention : muted;
    }
}
