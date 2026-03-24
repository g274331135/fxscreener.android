
using fxscreener.android.Models;
using fxscreener.android.Services;

namespace fxscreender.android.Services;

/// <summary>
/// Реализация сервиса построения баров из M1
/// </summary>
public class BarBuilderService : IBarBuilderService
{
    /// <summary>
    /// Построить бар для указанного таймфрейма
    /// </summary>
    public Bar BuildBarFromM1(
        List<PriceHistoryBar> m1Bars,
        DateTime targetBarStart,
        int timeframeMinutes)
    {
        var targetBarEnd = targetBarStart.AddMinutes(timeframeMinutes);

        // Фильтруем M1 бары, попадающие в интервал
        var relevantBars = m1Bars
            .Where(b => b.Time >= targetBarStart && b.Time < targetBarEnd)
            .ToList();

        if (relevantBars.Count == 0)
        {
            // Нет данных — возвращаем бар с ценой последнего известного
            return new Bar
            {
                Time = targetBarStart,
                Open = 0,
                High = 0,
                Low = 0,
                Close = 0,
                Volume = 0,
                Ticks = 0
            };
        }

        var open = relevantBars.First().OpenPrice;
        var close = relevantBars.Last().ClosePrice;
        var high = relevantBars.Max(b => b.HighPrice);
        var low = relevantBars.Min(b => b.LowPrice);
        var volume = relevantBars.Sum(b => b.Volume);
        var ticks = relevantBars.Sum(b => b.TickVolume);

        return new Bar
        {
            Time = targetBarStart,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = (long)volume,
            Ticks = (int)ticks
        };
    }

    /// <summary>
    /// Построить все уровни из M1 (M5, M15, H1, H6, D1, W1)
    /// </summary>
    public Dictionary<int, List<Bar>> BuildAllLevelsFromM1(
        List<PriceHistoryBar> m1Bars,
        DateTime from,
        DateTime to)
    {
        var result = new Dictionary<int, List<Bar>>();

        // Таймфреймы в минутах
        var timeframes = new[] { 5, 15, 60, 360, 1440, 10080 }; // M5, M15, H1, H6, D1, W1

        foreach (var tf in timeframes)
        {
            var bars = BuildBarsForTimeframe(m1Bars, from, to, tf);
            result[tf] = bars;
        }

        return result;
    }

    private List<Bar> BuildBarsForTimeframe(
        List<PriceHistoryBar> m1Bars,
        DateTime from,
        DateTime to,
        int timeframeMinutes)
    {
        var bars = new List<Bar>();

        // Находим начало первого бара
        var firstBarStart = FloorToTimeframe(from, timeframeMinutes);

        // Проходим по всем барам в интервале
        for (var barStart = firstBarStart; barStart < to; barStart = barStart.AddMinutes(timeframeMinutes))
        {
            var bar = BuildBarFromM1(m1Bars, barStart, timeframeMinutes);

            // Добавляем только если бар имеет данные (цена не 0)
            if (bar.Open != 0 || bar.Close != 0)
            {
                bars.Add(bar);
            }
        }

        return bars;
    }

    private DateTime FloorToTimeframe(DateTime time, int timeframeMinutes)
    {
        long ticks = time.Ticks;
        long periodTicks = TimeSpan.FromMinutes(timeframeMinutes).Ticks;
        long resultTicks = (ticks / periodTicks) * periodTicks;
        return new DateTime(resultTicks, time.Kind);
    }
}