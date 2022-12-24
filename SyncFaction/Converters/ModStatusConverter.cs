using System;
using System.Globalization;
using System.Windows.Data;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Converters;

/// <summary>
/// Converts mod status to unicode icons
/// </summary>
public class ModStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value as ModStatus?;
        return status switch
        {
            ModStatus.None => string.Empty,
            ModStatus.Ready => "✓",
            ModStatus.InProgress => "⭮",
            ModStatus.Failed => "⨯",
            _ => string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // not much sense here
        return string.Empty;
    }
}