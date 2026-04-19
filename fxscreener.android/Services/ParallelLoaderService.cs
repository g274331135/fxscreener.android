using fxscreener.android.Models;
using System.Collections.Concurrent;

namespace fxscreener.android.Services;

public class ParallelLoaderService : IParallelLoaderService
{
    private readonly IMt5ApiService _apiService;
    private readonly int _utcOffset;

    public ParallelLoaderService(IMt5ApiService apiService)
    {
        _apiService = apiService;
        _utcOffset = 3;
    }

    public async Task<Dictionary<string, List<Bar>>> LoadHistoryParallelAsync(
        List<InstrumentParams> instruments,
        int maxParallelism = 3,
        CancellationToken cancellationToken = default)
    {
        if (instruments == null || instruments.Count == 0)
            return new Dictionary<string, List<Bar>>();

        // Группируем по периодам для оптимизации
        var groups = instruments
            .GroupBy(x => x.Period)
            .ToList();

        // Используем ConcurrentDictionary для потокобезопасного хранения
        var results = new ConcurrentDictionary<string, List<Bar>>();

        // Семафор для ограничения количества параллельных запросов
        using var semaphore = new SemaphoreSlim(maxParallelism);
        var tasks = new List<Task>();

        foreach (var group in groups)
        {
            var period = group.Key;
            var instrumentsInGroup = group.ToList();
            var symbols = instrumentsInGroup.Select(x => x.Symbol).ToList();
            var timeframeMinutes = Mt5ApiService.ConvertPeriodToMinutes(period);

            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[Parallel] Loading {period} for {symbols.Count} symbols...");

                    var historyItems = await LoadHistoryForSymbolsAsync(symbols, timeframeMinutes, cancellationToken);

                    // Сохраняем в ConcurrentDictionary с ключом "Symbol_Period"
                    foreach (var item in historyItems)
                    {
                        var key = $"{item.Symbol}_{period}";
                        var bars = item.Bars.Select(b => new Bar
                        {
                            Time = b.Time,
                            Open = b.OpenPrice,
                            High = b.HighPrice,
                            Low = b.LowPrice,
                            Close = b.ClosePrice,
                            Volume = b.Volume,
                            Ticks = (int)b.TickVolume
                        }).ToList();

                        results[key] = bars;
                        System.Diagnostics.Debug.WriteLine($"[Parallel] Stored {key} with {bars.Count} bars");
                    }

                    System.Diagnostics.Debug.WriteLine($"[Parallel] Completed {period} for {symbols.Count} symbols");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Parallel] Error loading {period}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);

        return new Dictionary<string, List<Bar>>(results);
    }

    private async Task<List<PriceHistoryItem>> LoadHistoryForSymbolsAsync(
        List<string> symbols,
        int timeframeMinutes,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.AddHours(_utcOffset);
        var barsNeeded = 50;
        var maxAttempts = 3;

        // Начальный период: от now - (timeframeMinutes * barsNeeded * 2) для запаса
        var from = now.AddMinutes(-timeframeMinutes * barsNeeded * 2);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var response = await _apiService.GetPriceHistoryManyAsync(
                symbols,
                from,
                now,
                timeframeMinutes,
                cancellationToken);

            if (response != null && response.Count > 0)
            {
                // Проверяем, хватает ли баров для каждого символа
                bool allHaveEnough = true;
                foreach (var item in response)
                {
                    if (item.Bars == null || item.Bars.Count < barsNeeded)
                    {
                        allHaveEnough = false;
                        System.Diagnostics.Debug.WriteLine($"[Parallel] {item.Symbol} has only {item.Bars?.Count ?? 0} bars, need {barsNeeded}");
                        break;
                    }
                }

                if (allHaveEnough)
                {
                    // Оставляем последние 50 баров
                    foreach (var item in response)
                    {
                        if (item.Bars != null && item.Bars.Count > barsNeeded)
                        {
                            item.Bars = item.Bars.TakeLast(barsNeeded).ToList();
                        }
                    }
                    return response;
                }
            }

            // Расширяем период
            from = from.AddMinutes(-timeframeMinutes * barsNeeded);
            System.Diagnostics.Debug.WriteLine($"[Parallel] Expanding period for attempt {attempt + 1}, new from={from}");
        }

        // Последняя попытка
        var finalResponse = await _apiService.GetPriceHistoryManyAsync(
            symbols,
            from,
            now,
            timeframeMinutes,
            cancellationToken);

        return finalResponse ?? new List<PriceHistoryItem>();
    }
}