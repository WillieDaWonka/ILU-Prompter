using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IluPrompter.App.Controls;

/// <summary>
/// A small numeric input: Up/Down keys step by <see cref="Step"/> (Shift: <see cref="ShiftStep"/>), the buttons do the
/// same, typed text is committed on Enter or when focus leaves. Values clamp to Minimum..Maximum. No mouse-wheel editing.
/// </summary>
public partial class NumberBox : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(NumberBox),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(NumberBox), new PropertyMetadata(0.0, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(NumberBox), new PropertyMetadata(100.0, OnRangeChanged));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(double), typeof(NumberBox), new PropertyMetadata(1.0));

    public static readonly DependencyProperty ShiftStepProperty = DependencyProperty.Register(
        nameof(ShiftStep), typeof(double), typeof(NumberBox), new PropertyMetadata(8.0));

    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(
        nameof(Decimals), typeof(int), typeof(NumberBox), new PropertyMetadata(0, (d, _) => ((NumberBox)d).ShowValue()));

    private bool _ready;

    public NumberBox()
    {
        InitializeComponent();
        ShowValue();
        // Clamp only once every binding (Minimum, Maximum, Value) has been applied, so a late Maximum can't squash the value.
        Loaded += (_, _) =>
        {
            _ready = true;
            CoerceValue(ValueProperty);
        };
    }

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }

    public double ShiftStep { get => (double)GetValue(ShiftStepProperty); set => SetValue(ShiftStepProperty, value); }

    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    public static string Format(double value, int decimals) =>
        value.ToString("F" + Math.Max(0, decimals).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>Parse typed text; accepts "," as the decimal mark. Null if it isn't a number.</summary>
    public static double? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var cleaned = text.Trim().Replace(',', '.').Replace("−", "-");
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && !double.IsNaN(v) && !double.IsInfinity(v) ? v : null;
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var box = (NumberBox)d;
        var v = (double)baseValue;
        if (!box._ready) return double.IsNaN(v) ? 0.0 : v;
        if (double.IsNaN(v)) v = box.Minimum;
        v = Math.Min(box.Maximum, Math.Max(box.Minimum, v));
        return Math.Round(v, Math.Max(0, box.Decimals));
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((NumberBox)d).ShowValue();

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => d.CoerceValue(ValueProperty);

    private void ShowValue()
    {
        if (Box is null) return;
        var text = Format(Value, Decimals);
        if (Box.Text != text) Box.Text = text;
    }

    private void Commit()
    {
        if (Parse(Box.Text) is { } parsed) Value = parsed;
        ShowValue();
    }

    private void Nudge(int direction, bool shift)
    {
        Commit();
        Value += direction * (shift ? ShiftStep : Step);
        ShowValue();
        Box.SelectAll();
    }

    private void OnBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                Nudge(+1, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
                e.Handled = true;
                break;
            case Key.Down:
                Nudge(-1, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
                e.Handled = true;
                break;
            case Key.Enter:
                Commit();
                Box.SelectAll();
                e.Handled = true;
                break;
            case Key.Escape:
                ShowValue();
                break;
        }
    }

    private void OnBoxGotFocus(object sender, KeyboardFocusChangedEventArgs e) => Box.SelectAll();

    private void OnBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e) => Commit();

    private void OnUp(object sender, RoutedEventArgs e) => Nudge(+1, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));

    private void OnDown(object sender, RoutedEventArgs e) => Nudge(-1, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
}
