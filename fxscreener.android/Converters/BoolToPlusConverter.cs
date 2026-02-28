using System.Globalization;

namespace fxscreener.android.Converters;

/// <summary>
/// Конвертирует bool в "+" или пустую строку
/// </summary>
public class BoolToPlusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            if (parameter?.ToString() == "color")
            {
                return boolValue ? Colors.Green : Colors.Gray;
            }
            return boolValue ? "+" : string.Empty;
        }

        if (parameter?.ToString() == "color")
            return Colors.Gray;

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}