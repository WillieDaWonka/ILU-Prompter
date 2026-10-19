using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IluPrompter.Theme;

/// <summary>
/// Margin for the speed slider's fill: runs from the centre of a track that is centred on zero to the thumb.
/// Inputs: value, minimum, maximum, control width (the 20 px thumb sits inside 10 px end margins).
/// </summary>
public sealed class CentreFillConverter : IMultiValueConverter
{
    public static readonly CentreFillConverter Instance = new();

    private const double EndMargin = 10.0;

    public static Thickness Compute(double value, double minimum, double maximum, double width)
    {
        if (width <= 2 * EndMargin || maximum <= minimum) return new Thickness(0);
        var usable = width - (2 * EndMargin);
        var t = Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);
        var zero = Math.Clamp((0 - minimum) / (maximum - minimum), 0, 1);
        var a = EndMargin + (Math.Min(t, zero) * usable);
        var b = EndMargin + (Math.Max(t, zero) * usable);
        return new Thickness(a, 0, Math.Max(0, width - b), 0);
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4 || values.Any(v => v is not double d || double.IsNaN(d))) return new Thickness(0);
        return Compute((double)values[0], (double)values[1], (double)values[2], (double)values[3]);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
