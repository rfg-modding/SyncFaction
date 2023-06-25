using System;
using System.Globalization;
using System.Windows.Data;

namespace SyncFaction.Converters;

/// <summary>
/// For json tree conversion
/// </summary>
public sealed class MethodToValueConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var methodName = parameter as string;
        if (value == null || methodName == null)
        {
            return null;
        }

        var methodInfo = value.GetType().GetMethod(methodName, Type.EmptyTypes);
        if (methodInfo == null)
        {
            return null;
        }

        return methodInfo.Invoke(value, Array.Empty<object>());
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException(GetType().Name + " can only be used for one way conversion.");
}
