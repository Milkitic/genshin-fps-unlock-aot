using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace UnlockFps.Gui.Converters;

internal sealed class FullPath2NameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            return System.IO.Path.GetFileName(s);
        }

        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}