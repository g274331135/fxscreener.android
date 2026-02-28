using fxscreener.android.Models;

namespace fxscreener.android.Services;

/// <summary>
/// Интерфейс для расчёта индикаторов и колонок грида
/// </summary>
public interface IIndicatorCalculator
{
    /// <summary>
    /// Рассчитать все колонки для одного инструмента
    /// </summary>
    /// <param name="symbol">Тикер</param>
    /// <param name="period">Период (H1, H4 и т.д.)</param>
    /// <param name="bars">Список баров (индекс 0 = текущий/последний)</param>
    /// <returns>Результат для отображения в гриде</returns>
    InstrumentScanResult CalculateForInstrument(string symbol, string period, List<Bar> bars);

    /// <summary>
    /// Рассчитать WPR для указанного индекса
    /// </summary>
    double CalculateWPR(List<Bar> bars, int index, int period);

    /// <summary>
    /// Найти ближайший фрактал (для F2)
    /// </summary>
    /// <returns>Индекс бара с фракталом или null</returns>
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
    /// Найти номер бара, где WPR > -20 (не более 5 баров назад)
    /// </summary>
    int? FindBarWhereWprAboveMinus20(List<Bar> bars, int period);

    /// <summary>
    /// Найти номер бара, где WPR < -80 (не более 5 баров назад)
    /// </summary>
    int? FindBarWhereWprBelowMinus80(List<Bar> bars, int period);

    /// <summary>
    /// Проверить условие U-W5d / U-W21d (закрытие выше открытия И WPR < предыдущего WPR)
    /// </summary>
    bool CheckU_Wxxd(List<Bar> bars, int period);

    /// <summary>
    /// Проверить условие D-W5u / D-W21u (закрытие ниже открытия И WPR > предыдущего WPR)
    /// </summary>
    bool CheckD_Wxxu(List<Bar> bars, int period);
}