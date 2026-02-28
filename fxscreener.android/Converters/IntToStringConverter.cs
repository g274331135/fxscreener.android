using System.Globalization;

namespace fxscreener.android.Converters;

/// <summary>
/// Конвертирует int? в строку (пусто если null)
/// </summary>
public class IntToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue.ToString();

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}