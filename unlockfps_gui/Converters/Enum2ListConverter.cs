using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls.Shapes;
using Avalonia.Data.Converters;

namespace UnlockFps.Gui.Converters;

internal sealed class Enum2ListConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is Type t && t.IsSubclassOf(typeof(Enum)))
            return GetTypeList(t);
        if (value is Enum)
            return GetTypeList(value.GetType());
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static object GetTypeList(Type t)
    {
        var list = Enum.GetValues(t).Cast<Enum>().ToList();
        return list;
    }
}