using fxscreener.android.Models;

namespace fxscreener.android.Services;

/// <summary>
/// Интерфейс сервиса кэширования M1 баров
/// </summary>
public interface IM1CacheService
{
    /// <summary>
    /// Получить M1 бары из кэша или загрузить из API
    /// </summary>
    Task<List<PriceHistoryBar>> GetOrLoadM1BarsAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Очистить устаревший кэш
    /// </summary>
    void Cleanup();
}