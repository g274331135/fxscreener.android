using fxscreender.android.Services;
using fxscreener.android.Models;

namespace fxscreener.android.Services;

/// <summary>
/// Реализация сервиса достройки баров
/// </summary>
public class BuildingService : IBuildingService
{
    private readonly IM1CacheService _m1Cache;
    private readonly IBarBuilderService _barBuilder;
    private readonly BuildSettings _buildSettings;

    public BuildingService(
        IM1CacheService m1Cache,
        IBarBuilderService barBuilder,
        BuildSettings buildSettings)
    {
        _m1Cache = m1Cache;
        _barBuilder = barBuilder;
        _buildSettings = buildSettings;
    }

    /// <summary>
    /// Определить, нужно ли достраивать бар
    /// </summary>
    public bool ShouldBuild(DateTime currentTime, DateTime lastClosedBarTime, int timeframeMinutes)
    {
        var minutesToClose = GetMinutesToClose(currentTime, lastClosedBarTime, timeframeMinutes);
        return minutesToClose <= _buildSettings.BuildTimeMinutes && minutesToClose > 0;
    }

    /// <summary>
    /// Получить время начала текущего бара
    /// </summary>
    public DateTime GetCurrentBarStart(DateTime lastClosedBarTime, int timeframeMinutes)
    {
        return lastClosedBarTime.AddMinutes(timeframeMinutes);
    }

    /// <summary>
    /// Получить время до закрытия текущего бара (в минутах)
    /// </summary>
    public double GetMinutesToClose(DateTime currentTime, DateTime lastClosedBarTime, int timeframeMinutes)
    {
        var currentBarStart = GetCurrentBarStart(lastClosedBarTime, timeframeMinutes);
        var currentBarEnd = currentBarStart.AddMinutes(timeframeMinutes);
        return (currentBarEnd - currentTime).TotalMinutes;
    }

    /// <summary>
    /// Построить текущий бар для указанного периода
    /// </summary>
    public async Task<Bar?> BuildCurrentBarAsync(
        string symbol,
        DateTime lastClosedBarTime,
        int timeframeMinutes,
        CancellationToken cancellationToken = default)
    {
        var currentBarStart = GetCurrentBarStart(lastClosedBarTime, timeframeMinutes);
        var now = DateTime.UtcNow;

        // Загружаем M1 с начала текущего бара до now
        var m1Bars = await _m1Cache.GetOrLoadM1BarsAsync(
            symbol,
            currentBarStart,
            now,
            cancellationToken);

        if (m1Bars == null || m1Bars.Count == 0)
        {
            // Нет данных — возвращаем бар с ценой последнего закрытого
            return new Bar
            {
                Time = currentBarStart,
                Open = 0,
                High = 0,
                Low = 0,
                Close = 0,
                Volume = 0,
                Ticks = 0
            };
        }

        // Строим бар из M1
        var bar = _barBuilder.BuildBarFromM1(m1Bars, currentBarStart, timeframeMinutes);

        // Если бар пустой (нет данных за период), используем последнюю цену
        if (bar.Open == 0 && bar.Close == 0)
        {
            // В реальности нужно получить цену последнего закрытого бара
            // Пока оставляем как есть
        }

        return bar;
    }

    /// <summary>
    /// Построить все уровни для символа (M5, M15, H1, H6, D1, W1)
    /// </summary>
    public async Task<Dictionary<int, List<Bar>>> BuildAllLevelsForSymbolAsync(
        string symbol,
        DateTime earliestStart,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        // Загружаем M1 с самого раннего from до now
        var m1Bars = await _m1Cache.GetOrLoadM1BarsAsync(
            symbol,
            earliestStart,
            now,
            cancellationToken);

        if (m1Bars == null || m1Bars.Count == 0)
        {
            return new Dictionary<int, List<Bar>>();
        }

        // Строим все уровни из M1
        return _barBuilder.BuildAllLevelsFromM1(m1Bars, earliestStart, now);
    }
}