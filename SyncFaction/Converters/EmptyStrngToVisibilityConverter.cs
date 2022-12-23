using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SyncFaction.Converters;

public class EmptyStringShowCollapseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            null or "" => Visibility.Visible,
            string or _ => Visibility.Collapsed,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // not much sense here
        return string.Empty;
    }
}

public class BoolShowHideConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            null or true => Visibility.Visible,
            false or _ => Visibility.Hidden,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // not much sense here
        return string.Empty;
    }
}

/// <summary>
/// Converts bool to ✓ or empty string
/// </summary>
public class BoolCheckEmptyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            true => "✓",
            false or null or _ => string.Empty,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // not much sense here
        return string.Empty;
    }
}
