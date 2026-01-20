using System.Globalization;
using System.Windows.Data;
using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Presentation.Converters;

public class HotkeyDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is HotkeyDefinition hotkey)
        {
            return $"({hotkey.GetDisplayString()})";
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
