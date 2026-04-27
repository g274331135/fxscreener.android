using System.Globalization;
using fxscreener.android.Models;

namespace fxscreener.android.Converters
{
    public class WsSignalBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is WsSignal signal)
            {
                return signal.Signal switch
                {
                    SignalType.Bullish => Color.FromArgb("#CCFFCC"),   // Светло-зелёный фон для бычьего
                    SignalType.Bearish => Color.FromArgb("#FFCCCC"),   // Светло-красный фон для медвежьего
                    _ => Colors.Transparent
                };
            }
            return Colors.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}