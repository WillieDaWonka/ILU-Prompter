using System.Globalization;
using System.Windows;
using System.Windows.Data;
using IluPrompter.App.Logic;

namespace IluPrompter.App.Controls;

/// <summary>true shows, false collapses (ConverterParameter "invert" flips it).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var on = value is true;
        if (parameter is string s && s == "invert") on = !on;
        return on ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>A non-empty string shows, empty collapses.</summary>
public sealed class TextToVisibilityConverter : IValueConverter
{
    public static readonly TextToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Maps a LayoutTier to a Visibility: ConverterParameter "wide", "notnarrow" or "narrow".</summary>
public sealed class TierToVisibilityConverter : IValueConverter
{
    public static readonly TierToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not LayoutTier tier) return Visibility.Collapsed;
        var show = (parameter as string) switch
        {
            "wide" => tier == LayoutTier.Wide,
            "notwide" => tier != LayoutTier.Wide,
            "narrow" => tier == LayoutTier.Narrow,
            "notnarrow" => tier != LayoutTier.Narrow,
            _ => true,
        };
        return show ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Maps a LayoutTier to the live column width in DIPs.</summary>
public sealed class TierToLiveWidthConverter : IValueConverter
{
    public static readonly TierToLiveWidthConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is LayoutTier tier ? LayoutTiers.LiveColumnWidth(tier) : 720.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Maps a LayoutTier to the sections column width: 240 px panel at Wide, 44 px rail otherwise.</summary>
public sealed class TierToSectionsWidthConverter : IValueConverter
{
    public static readonly TierToSectionsWidthConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is LayoutTier.Wide ? 240.0 : 44.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Equality of a bound value with the ConverterParameter, as a boolean (for chip styles etc.).</summary>
public sealed class EqualsConverter : IValueConverter
{
    public static readonly EqualsConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Equals(value?.ToString(), parameter?.ToString());

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? parameter : Binding.DoNothing;
}

/// <summary>Boolean negation (a toggle that must not be clickable while its own pop-up is open).</summary>
public sealed class NotConverter : IValueConverter
{
    public static readonly NotConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
