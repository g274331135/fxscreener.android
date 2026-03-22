using fxscreener.android.Models;

namespace fxscreener.android.Services;

public class IndicatorCalculator : IIndicatorCalculator
{
    #region Основной метод

    public InstrumentScanResult CalculateForInstrument(string symbol, string period, List<Bar> bars)
    {
        if (bars == null || bars.Count < 21)
            return new InstrumentScanResult
            {
                Name = symbol,
                Period = period
            };

        var result = new InstrumentScanResult
        {
            Name = symbol,
            Period = period,

            // C5 и F2
            C5 = GetC5Value(bars),
            F2 = GetF2Value(bars),

            // W5e и W21e
            W5e = GetWprSignal(bars, 5),
            W21e = GetWprSignal(bars, 21),

            // UD5 и UD21
            UD5 = GetUdSignal(bars, 5),
            UD21 = GetUdSignal(bars, 21)
        };

        return result;
    }

    #endregion

    #region WPR сигнал (новая логика)

    public WprSignal? GetWprSignal(List<Bar> bars, int period)
    {
        // Ищем среди последних 5 баров (индексы 0..4)
        for (int i = 0; i <= 4; i++)
        {
            if (i >= bars.Count) break;

            var wpr = CalculateWPR(bars, i, period);

            // Проверяем условие выше -20
            if (wpr > -20)
            {
                var signalType = wpr > -5
                    ? WprSignalType.StrongAboveMinus5
                    : WprSignalType.AboveMinus20;

                return new WprSignal
                {
                    BarNumber = i,
                    SignalType = signalType,
                    WprValue = wpr
                };
            }

            // Проверяем условие ниже -80
            if (wpr < -80)
            {
                var signalType = wpr < -95
                    ? WprSignalType.StrongBelowMinus95
                    : WprSignalType.BelowMinus80;

                return new WprSignal
                {
                    BarNumber = i,
                    SignalType = signalType,
                    WprValue = wpr
                };
            }
        }

        return null;
    }

    #endregion

    #region WPR (Williams Percent Range)

    public double CalculateWPR(List<Bar> bars, int index, int period)
    {
        return bars.CalculateWPR(index, period);
    }

    #endregion

    public UdSignal? GetUdSignal(List<Bar> bars, int period)
    {
        if (bars.Count < 2) return null;

        var currentWpr = CalculateWPR(bars, 0, period);
        var prevWpr = CalculateWPR(bars, 1, period);

        // Бычий сигнал (светло-зелёный)
        // Условия: WPR(5) на текущем баре МЕНЬШЕ предыдущего 
        //          И WPR(5) на предыдущем баре был ВЫШЕ -20
        //          И закрытие текущего бара ВЫШЕ открытия
        if (currentWpr < prevWpr && prevWpr > -20 && bars[0].IsBullish)
        {
            return new UdSignal { SignalType = UdSignalType.Bullish };
        }

        // Медвежий сигнал (светло-красный)
        // Условия: WPR(5) на текущем баре БОЛЬШЕ предыдущего
        //          И WPR(5) на предыдущем баре был НИЖЕ -80
        //          И закрытие текущего бара НИЖЕ открытия
        if (currentWpr > prevWpr && prevWpr < -80 && bars[0].IsBearish)
        {
            return new UdSignal { SignalType = UdSignalType.Bearish };
        }

        return null;
    }

    #region Фракталы (F2)

    public int? FindNearestFractal(List<Bar> bars, int startIndex, int lookback = 15)
    {
        return bars.FindNearestFractal(startIndex, lookback);
    }

    #endregion

    #region Колонка C5

    public string GetC5Value(List<Bar> bars)
    {
        if (bars.Count < 6) return string.Empty;

        // bars[0] — последний бар (текущий)
        // bars[5] — бар 5 периодов назад
        var currentClose = bars[0].Close;
        var bar5Close = bars[5].Close;

        return currentClose > bar5Close ? "выше" : "ниже";
    }

    #endregion

    #region Колонка F2

    public string GetF2Value(List<Bar> bars)
    {
        var fractalIndex = FindNearestFractal(bars, 0, 15);

        if (!fractalIndex.HasValue)
            return string.Empty;

        var currentClose = bars[0].Close;
        var fractalBar = bars[fractalIndex.Value];

        bool isFractalUp = IsFractalUp(bars, fractalIndex.Value);

        if (isFractalUp)
        {
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
}