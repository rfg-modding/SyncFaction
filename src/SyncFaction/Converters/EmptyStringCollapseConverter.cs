using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SyncFaction.Converters;

public class EmptyStringCollapseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            null or "" => Visibility.Collapsed,
            string or _ => Visibility.Visible
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        // not much sense here
        string.Empty;
}