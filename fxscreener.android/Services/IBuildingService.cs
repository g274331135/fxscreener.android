using fxscreener.android.Models;

namespace fxscreener.android.Services;

/// <summary>
/// Интерфейс сервиса достройки баров
/// </summary>
public interface IBuildingService
{
    /// <summary>
    /// Определить, нужно ли достраивать бар для указанного периода
    /// </summary>
    bool ShouldBuild(DateTime currentTime, DateTime lastClosedBarTime, int timeframeMinutes);

    /// <summary>
    /// Получить время начала текущего бара
    /// </summary>
    DateTime GetCurrentBarStart(DateTime lastClosedBarTime, int timeframeMinutes);

    /// <summary>
    /// Получить время до закрытия текущего бара (в минутах)
    /// </summary>
    double GetMinutesToClose(DateTime currentTime, DateTime lastClosedBarTime, int timeframeMinutes);

    /// <summary>
    /// Построить текущий бар для указанного периода из M1 данных
    /// </summary>
    Task<Bar?> BuildCurrentBarAsync(
        string symbol,
        DateTime lastClosedBarTime,
        int timeframeMinutes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Построить все уровни для символа (для кэширования)
    /// </summary>
    Task<Dictionary<int, List<Bar>>> BuildAllLevelsForSymbolAsync(
        string symbol,
        DateTime earliestStart,
        DateTime now,
        CancellationToken cancellationToken = default);
}