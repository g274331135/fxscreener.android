using fxscreener.android.Models;
using System.Windows.Input;

namespace fxscreener.android.ViewModels;

public class ChartViewModel : BindableObject
{
    private ChartData _chartData = new();
    public ChartData ChartData
    {
        get => _chartData;
        set { _chartData = value; OnPropertyChanged(); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    private string _instrumentName = string.Empty;
    public string InstrumentName
    {
        get => _instrumentName;
        set
        {
            _instrumentName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Title)); // обновляем Title
        }
    }

    private string _period = string.Empty;
    public string Period
    {
        get => _period;
        set
        {
            _period = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Title)); // обновляем Title
        }
    }

    // Составной заголовок для отображения
    public string Title => $"{InstrumentName} {Period}";

    public ICommand CloseCommand { get; }

    public ChartViewModel()
    {
        CloseCommand = new Command(async () => await Close());
    }

    public async Task LoadData(string symbol, string period, List<Bar> bars, List<double> wpr5Values, List<double> wpr21Values)
    {
        System.Diagnostics.Debug.WriteLine($"LoadData called on ViewModel hash: {this.GetHashCode()}");

        IsLoading = true;

        try
        {
            InstrumentName = symbol;
            Period = period;

            // Здесь преобразуем полученные бары и индикаторы в ChartData
            // Для WPR нужно создать список Bar-подобных объектов (можно просто хранить значения)
            // Упростим: создадим список с временем и значением
            var wpr5Bars = new List<Bar>();
            var wpr21Bars = new List<Bar>();
            for (int i = 0; i < bars.Count; i++)
            {
                wpr5Bars.Add(new Bar
                {
                    Time = bars[i].Time,
                    Close = wpr5Values[i],
                    Open = wpr5Values[i],
                    High = wpr5Values[i],
                    Low = wpr5Values[i]
                });
                wpr21Bars.Add(new Bar
                {
                    Time = bars[i].Time,
                    Close = wpr21Values[i],
                    Open = wpr21Values[i],
                    High = wpr21Values[i],
                    Low = wpr21Values[i]
                });
            }

            ChartData = new ChartData
            {
                Bars = bars,
                Wpr5 = wpr5Bars,
                Wpr21 = wpr21Bars,
                VisibleStartIndex = 0,
                VisibleEndIndex = Math.Min(bars.Count - 1, 49)
            };
        }
        finally
        {
            IsLoading = false;
        }

        System.Diagnostics.Debug.WriteLine($"ChartData assigned: Bars={ChartData.Bars.Count}, Wpr5={ChartData.Wpr5.Count}, Wpr21={ChartData.Wpr21.Count}");
    }

    private async Task Close()
    {
        await Shell.Current.GoToAsync("..");
    }
}