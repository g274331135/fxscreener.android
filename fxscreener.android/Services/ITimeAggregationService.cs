using fxscreener.android.Models;

namespace fxscreener.android.Services;

/// <summary>
/// Интерфейс сервиса для агрегации временных рядов к целевому часовому поясу
/// </summary>
public interface ITimeAggregationService
{
    /// <summary>
    /// Привести список баров к целевому часовому поясу
    /// </summary>
    /// <param name="bars">Исходные бары (время брокера)</param>
    /// <param name="targetOffsetHours">Целевой сдвиг (например, 3 для UTC+3)</param>
    /// <returns>Новый список баров с временем, сдвинутым на targetOffsetHours</returns>
    List<Bar> AggregateToTargetZone(List<Bar> bars, int targetOffsetHours);

    /// <summary>
    /// Привести список баров к целевому часовому поясу (из BarData)
    /// </summary>
    List<Bar> AggregateToTargetZone(List<BarData> barsData, int targetOffsetHours);

    /// <summary>
    /// Построить текущий (незакрытый) бар из минутных данных
    /// </summary>
    /// <param name="minuteBars">Минутные бары с начала периода</param>
    /// <param name="timeframeMinutes">Таймфрейм целевого бара (60 для H1)</param>
    /// <param name="targetOffsetHours">Целевой часовой пояс</param>
    /// <returns>Построенный бар (незакрытый) с временем открытия периода</returns>
    Bar BuildCurrentBarFromMinutes(List<Bar> minuteBars, int timeframeMinutes, int targetOffsetHours);

    /// <summary>
    /// Проверить, нужно ли достраивать текущий бар (осталось меньше 5 минут)
    /// </summary>
    /// <param name="currentTime">Текущее время в целевом поясе</param>
    /// <param name="timeframeMinutes">Таймфрейм</param>
    /// <returns>true если до закрытия не более 5 минут</returns>
    bool IsBuildingMode(DateTime currentTime, int timeframeMinutes);

    /// <summary>
    /// Получить время закрытия текущего бара
    /// </summary>
    DateTime GetNextBarCloseTime(DateTime currentTime, int timeframeMinutes);
}