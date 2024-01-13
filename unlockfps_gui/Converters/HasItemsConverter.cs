using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Data.Converters;

namespace UnlockFps.Gui.Converters;

internal sealed class HasItemsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            var moveNext = enumerator.MoveNext();
            if (enumerator is IDisposable disposable)
            {
                disposable.Dispose();
            }

            return moveNext;
        }

        if (value is int i)
        {
            return i > 0;
        }

        return AvaloniaProperty.UnsetValue;
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