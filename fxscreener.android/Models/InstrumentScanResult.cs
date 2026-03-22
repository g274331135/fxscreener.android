namespace fxscreener.android.Models;

/// <summary>
/// Результат сканирования для одного инструмента (занимает 2 строки в гриде)
/// </summary>
public class InstrumentScanResult
{
    #region Основные поля (первые 4 колонки)

    public string Name { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string C5 { get; set; } = string.Empty;      // "выше" / "ниже"
    public string F2 { get; set; } = string.Empty;      // "выше" / "ниже"

    #endregion

    public WprSignal? W5e { get; set; }   // Сигнал для WPR(5)
    public WprSignal? W21e { get; set; }  // Сигнал для WPR(21)

    public UdSignal? UD5 { get; set; }   // Сигнал для UD5
    public UdSignal? UD21 { get; set; }  // Сигнал для UD21
}

public class UdSignal
{
    public UdSignalType SignalType { get; set; }
}

public enum UdSignalType
{
    None,           // Нет сигнала
    Bullish,        // Бычий разворот (светло-зелёный)
    Bearish         // Медвежий разворот (светло-красный)
}

/// <summary>
/// Сигнал от WPR индикатора
/// </summary>
public class WprSignal
{
    public int BarNumber { get; set; }      // Номер бара (0..4)
    public WprSignalType SignalType { get; set; }  // Тип сигнала
    public double WprValue { get; set; }    // Значение WPR для отладки
}

/// <summary>
/// Тип сигнала WPR
/// </summary>
public enum WprSignalType
{
    None,           // Нет сигнала
    AboveMinus20,   // Выше -20 (бледно-красный)
    StrongAboveMinus5, // Выше -5 (красный)
    BelowMinus80,   // Ниже -80 (бледно-зелёный)
    StrongBelowMinus95 // Ниже -95 (зелёный)
}