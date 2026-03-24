using fxscreener.android.Models;
using fxscreener.android.Services;

namespace fxscreender.android.Services;

/// <summary>
/// Интерфейс сервиса построения баров из M1
/// </summary>
public interface IBarBuilderService
{
    /// <summary>
    /// Построить бар для указанного таймфрейма из M1 баров
    /// </summary>
    Bar BuildBarFromM1(
        List<PriceHistoryBar> m1Bars,
        DateTime targetBarStart,
        int timeframeMinutes);

    /// <summary>
    /// Построить все недостающие уровни из M1 для периода достройки
    /// </summary>
    Dictionary<int, List<Bar>> BuildAllLevelsFromM1(
        List<PriceHistoryBar> m1Bars,
        DateTime from,
        DateTime to);
}