using fxscreener.android.Models;

namespace fxscreener.android.Services;

/// <summary>
/// Реализация сервиса для агрегации временных рядов к целевому часовому поясу
/// </summary>
public class TimeAggregationService : ITimeAggregationService
{
    #region Основные методы

    public List<Bar> AggregateToTargetZone(List<Bar> bars, int targetOffsetHours)
    {
        if (bars == null || bars.Count == 0)
            return new List<Bar>();

        // Просто сдвигаем время каждого бара
        return bars.Select(b => b.WithTimeZone(targetOffsetHours)).ToList();
    }

    public List<Bar> AggregateToTargetZone(List<BarData> barsData, int targetOffsetHours)
    {
        if (barsData == null || barsData.Count == 0)
            return new List<Bar>();

        return barsData.Select(data => Bar.FromBarData(data))
               .Select(b => b.WithTimeZone(targetOffsetHours))
               .ToList();
    }

    #endregion

    #region Построение текущего бара из минутных данных

    public Bar BuildCurrentBarFromMinutes(
        List<Bar> minuteBars,
        int timeframeMinutes,
        int targetOffsetHours)
    {
        if (minuteBars == null || minuteBars.Count == 0)
            throw new ArgumentException("Minute bars cannot be empty");

        // Сортируем по времени (на случай если пришли не по порядку)
        var sorted = minuteBars.OrderBy(b => b.Time).ToList();

        // Время открытия периода - это время первой минуты, округлённое вниз до timeframeMinutes
        var firstMinuteTime = sorted.First().Time;
        var periodStart = FloorToTimeframe(firstMinuteTime, timeframeMinutes);

        // Агрегируем минутные бары
        double open = sorted.First().Open;
        double high = sorted.Max(b => b.High);
        double low = sorted.Min(b => b.Low);
        double close = sorted.Last().Close;
        long volume = sorted.Sum(b => b.Volume);
        int ticks = sorted.Sum(b => b.Ticks);

        return new Bar
        {
            Time = periodStart,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            Ticks = ticks
        };
    }

    /// <summary>
    /// Округлить время вниз до начала периода
    /// </summary>
    private DateTime FloorToTimeframe(DateTime time, int timeframeMinutes)
    {
        long ticks = time.Ticks;
        long periodTicks = TimeSpan.FromMinutes(timeframeMinutes).Ticks;

        // Целочисленное деление вниз
        long resultTicks = (ticks / periodTicks) * periodTicks;

        return new DateTime(resultTicks, time.Kind);
    }

    #endregion

    #region Режимы обновления

    public bool IsBuildingMode(DateTime currentTime, int timeframeMinutes)
    {
        var minutesToClose = GetMinutesToNextBar(currentTime, timeframeMinutes);
        return minutesToClose <= 5 && minutesToClose > 0;
    }

    public DateTime GetNextBarCloseTime(DateTime currentTime, int timeframeMinutes)
    {
        // Находим начало текущего периода
        var periodStart = FloorToTimeframe(currentTime, timeframeMinutes);

        // Добавляем длительность периода
        return periodStart.AddMinutes(timeframeMinutes);
    }

    /// <summary>
    /// Получить количество минут до закрытия текущего бара
    /// </summary>
    private int GetMinutesToNextBar(DateTime currentTime, int timeframeMinutes)
    {
        var nextClose = GetNextBarCloseTime(currentTime, timeframeMinutes);
        var diff = nextClose - currentTime;

        // Округляем вверх до целых минут
        return (int)Math.Ceiling(diff.TotalMinutes);
    }

    #endregion

    #region Вспомогательные методы для индексации

    /// <summary>
    /// Определить, какой индекс имеет бар (0 = последний полный)
    /// </summary>
    public int GetBarIndex(DateTime barTime, DateTime currentTime, int timeframeMinutes)
    {
        var barStart = FloorToTimeframe(barTime, timeframeMinutes);
        var currentBarStart = FloorToTimeframe(currentTime, timeframeMinutes);

        var diff = (int)((currentBarStart - barStart).TotalMinutes / timeframeMinutes);

        return diff; // 0 = текущий бар, 1 = предыдущий и т.д.
    }

    #endregion
}