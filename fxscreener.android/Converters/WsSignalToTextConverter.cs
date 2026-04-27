using System.Globalization;
using fxscreener.android.Models;

namespace fxscreener.android.Converters
{
    public class WsSignalToTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is WsSignal signal)
            {
                return signal.Text;
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}