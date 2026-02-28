using fxscreener.android.Services; // Для BarData

namespace fxscreener.android.Models;

/// <summary>
/// Модель свечи (бара) с методами для анализа
/// </summary>
public class Bar
{
    #region Основные поля (из API)

    /// <summary>
    /// Время открытия свечи (в часовом поясе брокера)
    /// </summary>
    public DateTime Time { get; set; }

    /// <summary>
    /// Цена открытия
    /// </summary>
    public double Open { get; set; }

    /// <summary>
    /// Максимальная цена
    /// </summary>
    public double High { get; set; }

    /// <summary>
    /// Минимальная цена
    /// </summary>
    public double Low { get; set; }

    /// <summary>
    /// Цена закрытия
    /// </summary>
    public double Close { get; set; }

    /// <summary>
    /// Объём (тики)
    /// </summary>
    public long Volume { get; set; }

    /// <summary>
    /// Количество тиков
    /// </summary>
    public int Ticks { get; set; }

    #endregion

    #region Производные поля (вычисляемые)

    /// <summary>
    /// Свеча бычья (закрытие выше открытия)
    /// </summary>
    public bool IsBullish => Close > Open;

    /// <summary>
    /// Свеча медвежья (закрытие ниже открытия)
    /// </summary>
    public bool IsBearish => Close < Open;

    /// <summary>
    /// Свеча плоская (закрытие равно открытию)
    /// </summary>
    public bool IsFlat => Math.Abs(Close - Open) < 0.000001;

    /// <summary>
    /// Размах свечи (High - Low)
    /// </summary>
    public double Range => High - Low;

    /// <summary>
    /// Тело свечи (|Close - Open|)
    /// </summary>
    public double Body => Math.Abs(Close - Open);

    /// <summary>
    /// Верхняя тень (High - max(Open, Close))
    /// </summary>
    public double UpperShadow => High - Math.Max(Open, Close);

    /// <summary>
    /// Нижняя тень (min(Open, Close) - Low)
    /// </summary>
    public double LowerShadow => Math.Min(Open, Close) - Low;

    /// <summary>
    /// Отношение тела к размаху (0..1)
    /// </summary>
    public double BodyToRangeRatio => Range > 0 ? Body / Range : 0;

    /// <summary>
    /// Является ли свеча доджи (тело мало)
    /// </summary>
    public bool IsDoji => Body < Range * 0.1;

    #endregion

    #region Конструкторы и фабрики

    public Bar() { }

    /// <summary>
    /// Создать бар из данных API
    /// </summary>
    public static Bar FromBarData(BarData data)
    {
        return new Bar
        {
            Time = data.time,
            Open = data.open,
            High = data.high,
            Low = data.low,
            Close = data.close,
            Volume = data.volume,
            Ticks = data.ticks
        };
    }

    /// <summary>
    /// Создать копию бара
    /// </summary>
    public Bar Clone()
    {
        return new Bar
        {
            Time = Time,
            Open = Open,
            High = High,
            Low = Low,
            Close = Close,
            Volume = Volume,
            Ticks = Ticks
        };
    }

    #endregion

    #region Методы для работы со временем

    /// <summary>
    /// Скорректировать время свечи под целевой часовой пояс
    /// </summary>
    public Bar WithTimeZone(int targetOffsetHours)
    {
        var result = Clone();
        result.Time = Time.AddHours(targetOffsetHours);
        return result;
    }

    /// <summary>
    /// Проверить, принадлежит ли момент времени этой свече
    /// (для заданного таймфрейма в минутах)
    /// </summary>
    public bool ContainsTime(DateTime moment, int timeframeMinutes)
    {
        var barEnd = Time.AddMinutes(timeframeMinutes);
        return moment >= Time && moment < barEnd;
    }

    #endregion

    #region Вспомогательные методы

    public override string ToString()
    {
        return $"{Time:yyyy-MM-dd HH:mm} O:{Open:F5} H:{High:F5} L:{Low:F5} C:{Close:F5} V:{Volume}";
    }

    /// <summary>
    /// Получить направление свечи для отображения
    /// </summary>
    public string GetDirectionSymbol()
    {
        if (IsBullish) return "↑";
        if (IsBearish) return "↓";
        return "→";
    }

    /// <summary>
    /// Получить цвет свечи (для UI)
    /// </summary>
    public Color GetColor()
    {
        if (IsBullish) return Colors.Green;
        if (IsBearish) return Colors.Red;
        return Colors.Gray;
    }

    #endregion
}

/// <summary>
/// Расширения для работы со списками баров
/// </summary>
public static class BarExtensions
{
    /// <summary>
    /// Получить максимальный High за период
    /// </summary>
    public static double HighestHigh(this List<Bar> bars, int startIndex, int count)
    {
        if (bars.Count == 0) return 0;

        double max = double.MinValue;
        for (int i = startIndex; i < startIndex + count && i < bars.Count; i++)
        {
            if (bars[i].High > max)
                max = bars[i].High;
        }
        return max;
    }

    /// <summary>
    /// Получить минимальный Low за период
    /// </summary>
    public static double LowestLow(this List<Bar> bars, int startIndex, int count)
    {
        if (bars.Count == 0) return 0;

        double min = double.MaxValue;
        for (int i = startIndex; i < startIndex + count && i < bars.Count; i++)
        {
            if (bars[i].Low < min)
                min = bars[i].Low;
        }
        return min;
    }

    /// <summary>
    /// Найти фрактал вверх (паттерн из 5 свечей)
    /// </summary>
    public static int? FindFractalUp(this List<Bar> bars, int currentIndex, int lookback = 15)
    {
        int start = Math.Max(0, currentIndex - lookback);
        int end = Math.Min(bars.Count - 3, currentIndex); // Нужно место для 2 свечей справа

        for (int i = start; i <= end; i++)
        {
            // Проверяем паттерн фрактала вверх:
            // i-2: High < High[i]
            // i-1: High < High[i]
            // i:   центральная свеча
            // i+1: High < High[i]
            // i+2: High < High[i]
            if (i >= 2 && i + 2 < bars.Count)
            {
                if (bars[i - 2].High < bars[i].High &&
                    bars[i - 1].High < bars[i].High &&
                    bars[i + 1].High < bars[i].High &&
                    bars[i + 2].High < bars[i].High)
                {
                    return i;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Найти фрактал вниз (паттерн из 5 свечей)
    /// </summary>
    public static int? FindFractalDown(this List<Bar> bars, int currentIndex, int lookback = 15)
    {
        int start = Math.Max(0, currentIndex - lookback);
        int end = Math.Min(bars.Count - 3, currentIndex);

        for (int i = start; i <= end; i++)
        {
            if (i >= 2 && i + 2 < bars.Count)
            {
                if (bars[i - 2].Low > bars[i].Low &&
                    bars[i - 1].Low > bars[i].Low &&
                    bars[i + 1].Low > bars[i].Low &&
                    bars[i + 2].Low > bars[i].Low)
                {
                    return i;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Найти ближайший фрактал (любой) в пределах lookback баров
    /// </summary>
    public static int? FindNearestFractal(this List<Bar> bars, int currentIndex, int lookback = 15)
    {
        var up = bars.FindFractalUp(currentIndex, lookback);
        var down = bars.FindFractalDown(currentIndex, lookback);

        if (up == null && down == null) return null;
        if (up == null) return down;
        if (down == null) return up;

        // Возвращаем ближайший к currentIndex
        return Math.Abs(up.Value - currentIndex) < Math.Abs(down.Value - currentIndex) ? up : down;
    }

    /// <summary>
    /// Рассчитать WPR (Williams Percent Range) для указанного индекса
    /// </summary>
    public static double CalculateWPR(this List<Bar> bars, int index, int period)
    {
        if (bars.Count < index + 1 || index - period + 1 < 0)
            return 0;

        int startIndex = Math.Max(0, index - period + 1);
        int actualPeriod = index - startIndex + 1;

        double highestHigh = bars.HighestHigh(startIndex, actualPeriod);
        double lowestLow = bars.LowestLow(startIndex, actualPeriod);

        if (Math.Abs(highestHigh - lowestLow) < 0.000001)
            return 0;

        return -100 * (highestHigh - bars[index].Close) / (highestHigh - lowestLow);
    }
}