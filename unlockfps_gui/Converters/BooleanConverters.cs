using Avalonia.Data.Converters;

namespace UnlockFps.Gui.Converters;

public static class BooleanConverters
{
    public static readonly IValueConverter Not =
        new DelegateConverter((x, _, _, _) => !(bool)x, (x, _, _, _) => !(bool)x);
}