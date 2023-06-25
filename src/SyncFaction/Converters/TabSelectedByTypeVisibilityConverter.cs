using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SyncFaction.Converters;

/// <summary>
/// Converts tab enum value to Visibility=Visible
/// </summary>
[SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "Parsing has fallback value")]
public class TabSelectedByTypeVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        Enum.TryParse(parameter as string ?? string.Empty, out Tab selected);
        var enumValue = value as Tab?;
        if (enumValue == selected)
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        // not much sense here
        string.Empty;
}
