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
            // TODO rename cats
            Category.Artwork => "Artwork",
            Category.ModsExperimentalMaps => "Mods - Experimental Map Edits",
            Category.ModsClassic => "Mods - ModManager (Classic)",
            Category.ModsGeneral => "Mods - General (Re-Mars-tered)",
            Category.ModsScriptLoader => "Mods - Script Loader (Re-Mars-tered)",
            Category.ModsClassicStandalone => "Mods - Standalone (Classic)",
            Category.PatchesClassic => "Software - Patches (Classic)",
            Category.Tools => "Software - Tools",
            Category.Local => "Local Folders",
            Category.Dev => "Development (CDN only)",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        // not much sense here
        string.Empty;
}
