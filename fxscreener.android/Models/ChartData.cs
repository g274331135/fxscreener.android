namespace fxscreener.android.Models;

public class ChartData
{
    public List<Bar> Bars { get; set; } = new();
    public List<Bar> Wpr5 { get; set; } = new();  // предварительно рассчитанные значения WPR(5)
    public List<Bar> Wpr21 { get; set; } = new(); // WPR(21)

    // Диапазон отображаемых баров (индексы)
    public int VisibleStartIndex { get; set; } = 0;
    public int VisibleEndIndex { get; set; } = 49; // для 50 баров

    // Выбранный бар (для крестика-прицела)
    public int SelectedIndex { get; set; } = -1;
}