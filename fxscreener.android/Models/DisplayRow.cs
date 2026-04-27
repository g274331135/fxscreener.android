namespace fxscreener.android.Models;

/// <summary>
/// Вспомогательный класс для отображения в CollectionView
/// (одна физическая строка грида)
/// </summary>
public class DisplayRow
{
    // Первая строка (Name/Period/C5/F2)
    public string? Name { get; set; }
    public string? Period { get; set; }
    public string? C5 { get; set; }
    public string? F2 { get; set; }

    // Данные для W5e (первая строка) и W21e (вторая строка)
    public string? WprDisplay { get; set; }      // Текст для отображения (номер бара)
    public Color? WprTextColor { get; set; }     // Цвет текста

    // Для UD5/UD21
    public Color? UdBackgroundColor { get; set; }
    public string? UdDisplay { get; set; }  // Символ ▲ или ▼

    // Для Ws5/Ws21
    public Color? WsBackgroundColor { get; set; }
    public string? WsDisplay { get; set; }  // Символ ▲ или ▼

    // Флаги для вёрстки
    public bool IsFirstRow { get; set; }
    public bool IsSecondRow { get; set; }

    // Цвет для пары строк (инструмента)
    public string PairColorKey { get; set; } = "RowEvenColor";

    public Color PairColor
    {
        get
        {
            var color = Application.Current?.Resources[PairColorKey] as Color;
            return color ?? Colors.White;
        }
    }
}