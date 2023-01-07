using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Xml;

namespace SyncFaction.Converters;

public class XmlNodeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var node = value as XmlNode;
        if (node is null)
        {
            return "null";
        }

        using var ms = new MemoryStream();
        using (var tw = new XmlTextWriter(ms, Encoding.UTF8))
        {
            tw.Formatting = Formatting.Indented;
            node.WriteContentTo(tw);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // not much sense here
        return null;
    }
}
