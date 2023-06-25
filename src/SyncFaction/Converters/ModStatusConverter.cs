using System;
using System.Globalization;
using System.Windows.Data;
using SyncFaction.Core.Models.FactionFiles;

namespace SyncFaction.Converters;

/// <summary>
/// Converts mod status to unicode icons
/// </summary>
public class ModStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value as OnlineModStatus?;
        return status switch
        {
            OnlineModStatus.None => string.Empty,
            OnlineModStatus.Ready => "✓",
            OnlineModStatus.InProgress => "⭮",
            OnlineModStatus.Failed => "⨯",
            _ => string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        // not much sense here
        string.Empty;
}
