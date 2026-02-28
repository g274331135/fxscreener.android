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

    #region Первая строка (W5...)

    public int? W5_20 { get; set; }      // номер бара
    public bool U_W5d { get; set; }       // true = "+"
    public int? W5_80 { get; set; }
    public bool D_W5u { get; set; }       // true = "+"

    #endregion

    #region Вторая строка (W21...)

    public int? W21_20 { get; set; }
    public bool U_W21d { get; set; }
    public int? W21_80 { get; set; }
    public bool D_W21u { get; set; }

    #endregion

    #region Для верстки (объединение ячеек)

    /// <summary>
    /// Флаг: это первая строка пары (для Name/Period объединены)
    /// </summary>
    public bool IsFirstRow => true; // В коллекции будем добавлять по две строки

    /// <summary>
    /// Флаг: это вторая строка пары
    /// </summary>
    public bool IsSecondRow => false; // Переопределяется в DisplayRow

    #endregion
}

/// <summary>
/// Вспомогательный класс для отображения в CollectionView
/// (одна физическая строка грида)
/// </summary>
public class DisplayRow
{
    public string? Name { get; set; }           // только у первой строки
    public string? Period { get; set; }         // только у первой строки
    public string? C5 { get; set; }             // только у первой строки
    public string? F2 { get; set; }             // только у первой строки

    public int? Col1 { get; set; }              // W5-20 или W21-20
    public string? Col2 { get; set; }           // U-W5d или U-W21d ("+" или "")
    public int? Col3 { get; set; }              // W5-80 или W21-80
    public string? Col4 { get; set; }           // D-W5u или D-W21u ("+" или "")

    public bool IsFirstRow { get; set; }        // true для первой строки пары
    public bool IsSecondRow { get; set; }       // true для второй строки пары
    public string RowColor => IsFirstRow ? "LightGray" : "White"; // для читаемости
}