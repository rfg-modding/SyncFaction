using System;
using System.Globalization;
using System.Windows.Data;

namespace SyncFaction.Converters;

public class EmptyStringDisabledConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            null or "" => false,
            string or _ => true
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        // not much sense here
        string.Empty;
}