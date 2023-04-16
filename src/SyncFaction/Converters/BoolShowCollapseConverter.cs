using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SyncFaction.Converters;

public class BoolShowCollapseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            true => Visibility.Visible,
            _ => Visibility.Collapsed,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // not much sense here
        return string.Empty;
    }
}