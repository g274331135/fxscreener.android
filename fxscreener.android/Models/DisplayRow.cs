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
    public Color? WprColor { get; set; }         // Цвет фона ячейки
    public Color? WprTextColor { get; set; }     // Цвет текста

    // Флаги для верстки
    public bool IsFirstRow { get; set; }
    public bool IsSecondRow { get; set; }

    // Цвет для пары строк (инструмента)
    public string PairColor { get; set; } = "White";

    // Для совместимости с существующим XAML
    public string RowColor => PairColor;
}