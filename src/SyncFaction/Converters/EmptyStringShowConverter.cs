using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SyncFaction.Converters;

public class EmptyStringShowConverter : IValueConverter
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

public class EmptyStringCollapseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            null or "" => Visibility.Collapsed,
            string or _ => Visibility.Visible,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // not much sense here
        return string.Empty;
    }
}
