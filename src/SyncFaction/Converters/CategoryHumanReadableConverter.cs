using System;
using System.Globalization;
using System.Windows.Data;
using SyncFaction.Core.Models.FactionFiles;

namespace SyncFaction.Converters;

/// <summary>
/// Converts category enum to human-readable name
/// </summary>
public class CategoryHumanReadableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var category = value as Category?;
        return category switch
        {
            Category.Artwork => "Artwork",
            Category.MapsStandalone => "Standalone Maps",
            Category.ModsClassic => "Mods for Classic Edition",
            Category.ModsRemaster => "Mods for Re-Mars-tered",
            Category.ModsScriptLoader => "ScriptLoader Mods",
            Category.ModsStandalone => "Standalone Mods",
            Category.Patches => "Patches",
            Category.Tools => "Tools",
            Category.Local => "Local Folders",
            Category.Dev => "Development (CDN only)",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        // not much sense here
        string.Empty;
}
