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
            UD21 = GetUdSignal(bars, 21),
        };

        return result;
    }

    #endregion

    public List<double> GetWprValues(List<Bar> bars, int period)
    {
        var result = new List<double>();

        for (int i = 0; i < bars.Count; i++)
        {
            var wpr = CalculateWPR(bars, i, period);
            result.Add(wpr);
        }

        result.Reverse();
        return result;
    }

    #region WPR сигнал

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

    #region UD сигнал

    public UdSignal? GetUdSignal(List<Bar> bars, int period)
    {
        if (bars.Count < 2) return null;

        var currentWpr = CalculateWPR(bars, 0, period);
        var prevWpr = CalculateWPR(bars, 1, period);

        // Бычий сигнал (светло-зелёный)
        if (currentWpr < prevWpr && prevWpr > -20 && bars[0].IsBullish)
        {
            return new UdSignal { SignalType = SignalType.Bullish };
        }

        // Медвежий сигнал (светло-красный)
        if (currentWpr > prevWpr && prevWpr < -80 && bars[0].IsBearish)
        {
            return new UdSignal { SignalType = SignalType.Bearish };
        }

        return null;
    }

    #endregion

    #region Ws сигнал (трёхбаровый разворотный паттерн)

    public WsSignal CalculateWs(List<double> wpr, int wprPeriod)
    {
        var signal = new WsSignal();

        // Нужно минимум 3 бара для паттерна + достаточно истории для расчёта WPR
        if (wpr == null || wpr.Count < 3)
            return signal;

        try
        {
            // Проверка на МЕДВЕЖИЙ сигнал ▼
            if (wpr[2] > -20 &&                      // Bar(2) выше -20
                wpr[1] < wpr[2] &&                      // Bar(1) ниже Bar(2)
                wpr[0] > wpr[1] &&                      // Bar(0) выше Bar(1)
                wpr[0] > -80 && wpr[1] > -80 && wpr[2] > -80) // Все бары не ниже -80
            {
                // Дополнительное условие: величина движения
                double move1 = wpr[2] - wpr[1];  // Падение от 2 к 1
                double move2 = wpr[0] - wpr[1];  // Рост от 1 к 0

                if (move2 < move1)  // Второе движение меньше первого
                {
                    signal.Signal = SignalType.Bearish;
                    return signal;
                }
            }

            // Проверка на БЫЧИЙ сигнал ▲
            if (wpr[2] < -80 &&                      // Bar(2) ниже -80
                wpr[1] > wpr[2] &&                      // Bar(1) выше Bar(2)
                wpr[0] < wpr[1] &&                      // Bar(0) ниже Bar(1)
                wpr[0] < -20 && wpr[1] < -20 && wpr[2] < -20) // Все бары не выше -20
            {
                // Дополнительное условие: величина движения
                double move1 = wpr[1] - wpr[2];  // Рост от 2 к 1
                double move2 = wpr[1] - wpr[0];  // Падение от 1 к 0

                if (move2 < move1)  // Второе движение меньше первого
                {
                    signal.Signal = SignalType.Bullish;
                    return signal;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error calculating Ws for period {wprPeriod}: {ex.Message}");
        }

        return signal;
    }

    #endregion

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