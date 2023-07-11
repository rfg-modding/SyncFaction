using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows.Data;
using SyncFaction.Models;

namespace SyncFaction.Converters;

/// <summary>
/// Converts tab enum value to IsSelected=true
/// </summary>
[SuppressMessage("Performance", "CA1806:Do not ignore method results", Justification = "Parsing has fallback value")]
public class TabSelectedByTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        Enum.TryParse(parameter as string ?? string.Empty, out Tab selected);
        var enumValue = value as Tab?;
        if (enumValue == selected)
        {
            return true;
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        // not much sense here
        string.Empty;
}
