using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SyncFaction.Converters;

/// <summary>
/// Converts true/false to visible/hidden. Parameter is bool when object should be visible
/// </summary>
public class BoolShowHideConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool.TryParse(parameter as string ?? string.Empty, out var visible);
        var boolValue = value as bool?;
        if (boolValue == visible)
        {
            return Visibility.Visible;
        }

        return Visibility.Hidden;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // not much sense here
        return string.Empty;
    }
}
