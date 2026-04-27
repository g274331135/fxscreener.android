using fxscreener.android.Models;

namespace fxscreener.android.Services;

public interface IIndicatorCalculator
{
    /// <summary>
    /// Рассчитывает все индикаторы для инструмента
    /// </summary>
    InstrumentScanResult CalculateForInstrument(string symbol, string period, List<Bar> bars);

    /// <summary>
    /// Получает список значений WPR для всех баров
    /// </summary>
    List<double> GetWprValues(List<Bar> bars, int period);

    /// <summary>
    /// Получает сигнал WPR (поиск среди последних 5 баров)
    /// </summary>
    WprSignal? GetWprSignal(List<Bar> bars, int period);

    /// <summary>
    /// Рассчитывает WPR для конкретного бара
    /// </summary>
    double CalculateWPR(List<Bar> bars, int index, int period);

    /// <summary>
    /// Получает сигнал UD (бычий/медвежий на основе WPR)
    /// </summary>
    UdSignal? GetUdSignal(List<Bar> bars, int period);

    /// <summary>
    /// Рассчитывает трёхбаровый разворотный паттерн Ws на основе WPR
    /// </summary>
    WsSignal CalculateWs(List<double> bars, int wprPeriod);

    /// <summary>
    /// Поиск ближайшего фрактала
    /// </summary>
    int? FindNearestFractal(List<Bar> bars, int startIndex, int lookback = 15);

    /// <summary>
    /// Значение C5 (сравнение с 5 баров назад)
    /// </summary>
    string GetC5Value(List<Bar> bars);

    /// <summary>
    /// Значение F2 (сравнение с ближайшим фракталом)
    /// </summary>
    string GetF2Value(List<Bar> bars);
}