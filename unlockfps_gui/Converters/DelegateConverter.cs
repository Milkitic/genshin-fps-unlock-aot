using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace UnlockFps.Gui.Converters;

public class DelegateConverter : IValueConverter
{
    public delegate object? ConvertDelegate(object? value, Type targetType, object? parameter, CultureInfo culture);

    public delegate object? ConvertBackDelegate(object? value, Type targetType, object? parameter,
        CultureInfo culture);

    private readonly ConvertDelegate _convert;
    private readonly ConvertBackDelegate? _convertBack;

    public DelegateConverter(ConvertDelegate convert, ConvertBackDelegate? convertBack = null)
    {
        _convert = convert ?? throw new ArgumentNullException(nameof(convert));
        _convertBack = convertBack;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return _convert(value, targetType, parameter, culture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (_convertBack == null)
            throw new NotImplementedException($"ConvertBack() of {GetType().Name} is not implemented.");
        return _convertBack(value, targetType, parameter, culture);
    }
}