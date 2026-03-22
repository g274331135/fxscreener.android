using fxscreener.android.Models;

namespace fxscreener.android.Services;

public interface IIndicatorCalculator
{
    /// <summary>
    /// Рассчитать все колонки для одного инструмента
    /// </summary>
    InstrumentScanResult CalculateForInstrument(string symbol, string period, List<Bar> bars);

    /// <summary>
    /// Рассчитать WPR для указанного индекса
    /// </summary>
    double CalculateWPR(List<Bar> bars, int index, int period);

    /// <summary>
    /// Найти ближайший фрактал (для F2)
    /// </summary>
    int? FindNearestFractal(List<Bar> bars, int startIndex, int lookback = 15);

    /// <summary>
    /// Получить значение для колонки C5 (выше/ниже)
    /// </summary>
    string GetC5Value(List<Bar> bars);

    /// <summary>
    /// Получить значение для колонки F2 (выше/ниже)
    /// </summary>
    string GetF2Value(List<Bar> bars);

    /// <summary>
    /// Получить сигнал WPR для указанного периода
    /// </summary>
    WprSignal? GetWprSignal(List<Bar> bars, int period);
}