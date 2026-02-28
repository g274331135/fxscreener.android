using fxscreener.android.Models;

namespace fxscreener.android.Services;

/// <summary>
/// Реализация расчёта индикаторов для сканера
/// </summary>
public class IndicatorCalculator : IIndicatorCalculator
{
    #region Основной метод

    public InstrumentScanResult CalculateForInstrument(string symbol, string period, List<Bar> bars)
    {
        if (bars == null || bars.Count < 21) // минимум для WPR(21)
            return new InstrumentScanResult
            {
                Name = symbol,
                Period = period,
                // Остальные поля останутся пустыми/default
            };

        var result = new InstrumentScanResult
        {
            Name = symbol,
            Period = period,

            // C5 и F2
            C5 = GetC5Value(bars),
            F2 = GetF2Value(bars),

            // W5 серия
            W5_20 = FindBarWhereWprAboveMinus20(bars, 5),
            U_W5d = CheckU_Wxxd(bars, 5),
            W5_80 = FindBarWhereWprBelowMinus80(bars, 5),
            D_W5u = CheckD_Wxxu(bars, 5),

            // W21 серия
            W21_20 = FindBarWhereWprAboveMinus20(bars, 21),
            U_W21d = CheckU_Wxxd(bars, 21),
            W21_80 = FindBarWhereWprBelowMinus80(bars, 21),
            D_W21u = CheckD_Wxxu(bars, 21)
        };

        return result;
    }

    #endregion

    #region WPR (Williams Percent Range)

    public double CalculateWPR(List<Bar> bars, int index, int period)
    {
        // Используем метод расширения из Bar.cs
        return bars.CalculateWPR(index, period);
    }

    #endregion

    #region Фракталы (F2)

    public int? FindNearestFractal(List<Bar> bars, int startIndex, int lookback = 15)
    {
        return bars.FindNearestFractal(startIndex, lookback);
    }

    #endregion

    #region Колонка C5 (закрытие vs 5 баров назад)

    public string GetC5Value(List<Bar> bars)
    {
        if (bars.Count < 6) return string.Empty;

        var currentClose = bars[0].Close;
        var bar5Close = bars[5].Close;

        return currentClose > bar5Close ? "выше" : "ниже";
    }

    #endregion

    #region Колонка F2 (ближайший фрактал)

    public string GetF2Value(List<Bar> bars)
    {
        var fractalIndex = FindNearestFractal(bars, 0, 15);

        if (!fractalIndex.HasValue)
            return string.Empty;

        var currentClose = bars[0].Close;
        var fractalBar = bars[fractalIndex.Value];

        // Определяем, какой это фрактал (вверх или вниз)
        bool isFractalUp = IsFractalUp(bars, fractalIndex.Value);

        if (isFractalUp)
        {
            // Для фрактала вверх: если текущая цена выше фрактала - сигнал?
            // Уточните логику: что значит "выше/ниже фрактала"?
            // Пока реализуем как сравнение с ценой фрактала
            return currentClose > fractalBar.High ? "выше" : "ниже";
        }
        else
        {
            return currentClose < fractalBar.Low ? "ниже" : "выше";
        }
    }

    private bool IsFractalUp(List<Bar> bars, int index)
    {
        if (index < 2 || index + 2 >= bars.Count)
            return false;

        return bars[index - 2].High < bars[index].High &&
               bars[index - 1].High < bars[index].High &&
               bars[index + 1].High < bars[index].High &&
               bars[index + 2].High < bars[index].High;
    }

    #endregion

    #region Поиск баров с условиями WPR

    public int? FindBarWhereWprAboveMinus20(List<Bar> bars, int period)
    {
        // Ищем среди последних 5 баров (индексы 0..4)
        for (int i = 0; i <= 4; i++)
        {
            if (i >= bars.Count) break;

            var wpr = CalculateWPR(bars, i, period);
            if (wpr > -20)
                return i; // 0 = текущий бар
        }

        return null;
    }

    public int? FindBarWhereWprBelowMinus80(List<Bar> bars, int period)
    {
        for (int i = 0; i <= 4; i++)
        {
            if (i >= bars.Count) break;

            var wpr = CalculateWPR(bars, i, period);
            if (wpr < -80)
                return i;
        }

        return null;
    }

    #endregion

    #region Условия U-Wxxd и D-Wxxu

    public bool CheckU_Wxxd(List<Bar> bars, int period)
    {
        if (bars.Count < 2) return false;

        // Текущий бар (индекс 0)
        var currentBar = bars[0];
        if (!currentBar.IsBullish) return false; // закрытие должно быть выше открытия

        var currentWpr = CalculateWPR(bars, 0, period);
        var prevWpr = CalculateWPR(bars, 1, period);

        return currentWpr < prevWpr;
    }

    public bool CheckD_Wxxu(List<Bar> bars, int period)
    {
        if (bars.Count < 2) return false;

        var currentBar = bars[0];
        if (!currentBar.IsBearish) return false; // закрытие должно быть ниже открытия

        var currentWpr = CalculateWPR(bars, 0, period);
        var prevWpr = CalculateWPR(bars, 1, period);

        return currentWpr > prevWpr;
    }

    #endregion
}