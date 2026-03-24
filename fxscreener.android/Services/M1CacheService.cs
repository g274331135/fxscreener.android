using fxscreener.android.Models;
using fxscreener.android.Services;

namespace fxscreender.android.Services;

/// <summary>
/// Реализация сервиса кэширования M1 баров
/// </summary>
public class M1CacheService : IM1CacheService
{
    private readonly IMt5ApiService _apiService;
    private readonly BuildSettings _buildSettings;

    /// <summary>
    /// Структура элемента кэша
    /// </summary>
    private class CacheItem
    {
        public List<PriceHistoryBar> Bars { get; set; } = new();
        public DateTime ExpiresAt { get; set; }
    }

    private readonly Dictionary<string, CacheItem> _cache = new();
    private readonly object _lock = new();

    public M1CacheService(IMt5ApiService apiService, BuildSettings buildSettings)
    {
        _apiService = apiService;
        _buildSettings = buildSettings;
    }

    public async Task<List<PriceHistoryBar>> GetOrLoadM1BarsAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        // Нормализуем время до минут (убираем секунды)
        from = new DateTime(from.Year, from.Month, from.Day, from.Hour, from.Minute, 0, DateTimeKind.Utc);
        to = new DateTime(to.Year, to.Month, to.Day, to.Hour, to.Minute, 0, DateTimeKind.Utc);

        var cacheKey = $"M1_{symbol}_{from:yyyyMMddHHmm}_{to:yyyyMMddHHmm}";

        // Проверяем кэш
        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            {
                System.Diagnostics.Debug.WriteLine($"[Cache] Hit for {cacheKey}");
                return cached.Bars;
            }
        }

        System.Diagnostics.Debug.WriteLine($"[Cache] Miss for {cacheKey}, loading from API...");

        // Загружаем из API
        var response = await _apiService.GetPriceHistoryManyAsync(
            new List<string> { symbol },
            from,
            to,
            1, // M1
            cancellationToken);

        var bars = response?.FirstOrDefault()?.Bars ?? new List<PriceHistoryBar>();

        // Сохраняем в кэш
        lock (_lock)
        {
            _cache[cacheKey] = new CacheItem
            {
                Bars = bars,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_buildSettings.BuildTimeMinutes)
            };
        }

        return bars;
    }

    public void Cleanup()
    {
        lock (_lock)
        {
            var expiredKeys = _cache
                .Where(kv => kv.Value.ExpiresAt <= DateTime.UtcNow)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.Remove(key);
                System.Diagnostics.Debug.WriteLine($"[Cache] Removed expired: {key}");
            }
        }
    }
}