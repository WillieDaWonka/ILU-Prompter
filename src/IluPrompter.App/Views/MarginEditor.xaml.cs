using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using IluPrompter.App.ViewModels;

namespace IluPrompter.App.Views;

public partial class MarginEditor : UserControl
{
    private const double DiagramWidth = 150;
    private const double DiagramHeight = 84;
    private MarginEditorViewModel? _model;

    public MarginEditor()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (_model is not null) _model.PropertyChanged -= OnModelChanged;
            _model = e.NewValue as MarginEditorViewModel;
            if (_model is not null) _model.PropertyChanged += OnModelChanged;
            UpdateBands();
        };
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MarginEditorViewModel.Fractions) or null or "") UpdateBands();
    }

    private void UpdateBands()
    {
        if (_model is null) return;
        var f = _model.Fractions;
        var left = f.Left * DiagramWidth;
        var right = f.Right * DiagramWidth;
        var top = f.Top * DiagramHeight;
        var bottom = f.Bottom * DiagramHeight;

        Place(BandLeft, 0, 0, left, DiagramHeight);
        Place(BandRight, DiagramWidth - right, 0, right, DiagramHeight);
        Place(BandTop, left, 0, Math.Max(0, DiagramWidth - left - right), top);
        Place(BandBottom, left, DiagramHeight - bottom, Math.Max(0, DiagramWidth - left - right), bottom);
    }

    private static void Place(Rectangle band, double x, double y, double width, double height)
    {
        Canvas.SetLeft(band, x);
        Canvas.SetTop(band, y);
        band.Width = Math.Max(0, width);
        band.Height = Math.Max(0, height);
    }
}
