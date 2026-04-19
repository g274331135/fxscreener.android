using fxscreener.android.Models;

namespace fxscreener.android.Services;

public interface IParallelLoaderService
{
    /// <summary>
    /// Параллельно загрузить историю для нескольких инструментов
    /// </summary>
    /// <returns>Словарь, где ключ = "Symbol_Period", значение = список баров</returns>
    Task<Dictionary<string, List<Bar>>> LoadHistoryParallelAsync(
        List<InstrumentParams> instruments,
        int maxParallelism = 3,
        CancellationToken cancellationToken = default);
}