using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SyncFaction.Core.Services.FactionFiles;

namespace SyncFaction.Converters;

public class EmptyStringShowCollapseConverter : IValueConverter
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

/// <summary>
/// Converts true/false to visible/hidden. Parameter is bool when object should be visible
/// </summary>
public class BoolShowHideConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool.TryParse(parameter as string ?? string.Empty, out var visible);
        var boolValue = value as bool?;
        if (boolValue == visible)
        {
            return Visibility.Visible;
        }

        return Visibility.Hidden;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // not much sense here
        return string.Empty;
    }
}

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

/// <summary>
/// Converts category enum to human-readable name
/// </summary>
public class CategoryHumanReadableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // TODO better naming?
        var category = value as Category?;
        return category switch
        {
            Category.Artwork => "Artwork",
            Category.MapsStandalone => "Standalone Maps",
            Category.MapsPatches => "Patched Maps",
            Category.MapPacks => "Map Packs",
            Category.ModsClassic => "Mods for Classic Edition",
            Category.ModsRemaster => "Mods for Re-Mars-tered",
            Category.ModsScriptLoader => "ScriptLoader Mods",
            Category.ModsStandalone => "Standalone Mods",
            Category.Patches => "Patches",
            Category.Tools => "Tools",
            Category.Local => "Local",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // not much sense here
        return string.Empty;
    }
}
