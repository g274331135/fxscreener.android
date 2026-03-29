using System.Collections.ObjectModel;
using System.Windows.Input;
using fxscreener.android.Models;
using DevExpress.Maui.Charts;

namespace fxscreener.android.ViewModels;

public class ChartViewModel : BindableObject
{
    private ObservableCollection<CandleData> _candleData = new();
    public ObservableCollection<CandleData> CandleData
    {
        get => _candleData;
        set { _candleData = value; OnPropertyChanged(); }
    }

    private ObservableCollection<IndicatorData> _wpr5Data = new();
    public ObservableCollection<IndicatorData> Wpr5Data
    {
        get => _wpr5Data;
        set { _wpr5Data = value; OnPropertyChanged(); }
    }

    private ObservableCollection<IndicatorData> _wpr21Data = new();
    public ObservableCollection<IndicatorData> Wpr21Data
    {
        get => _wpr21Data;
        set { _wpr21Data = value; OnPropertyChanged(); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public ICommand CloseCommand { get; }

    public ChartViewModel()
    {
        CloseCommand = new Command(async () => await Close());
    }

    public async Task LoadData(string symbol, string period, List<Bar> bars, List<double> wpr5Values, List<double> wpr21Values)
    {
        IsLoading = true;

        try
        {
            var candles = new ObservableCollection<CandleData>();
            var wpr5 = new ObservableCollection<IndicatorData>();
            var wpr21 = new ObservableCollection<IndicatorData>();

            for (int i = 0; i < bars.Count; i++)
            {
                candles.Add(new CandleData
                {
                    Date = bars[i].Time,
                    Open = bars[i].Open,
                    High = bars[i].High,
                    Low = bars[i].Low,
                    Close = bars[i].Close
                });

                wpr5.Add(new IndicatorData { Date = bars[i].Time, Value = wpr5Values[i] });
                wpr21.Add(new IndicatorData { Date = bars[i].Time, Value = wpr21Values[i] });
            }

            CandleData = candles;
            Wpr5Data = wpr5;
            Wpr21Data = wpr21;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task Close()
    {
        await Shell.Current.GoToAsync("..");
    }
}