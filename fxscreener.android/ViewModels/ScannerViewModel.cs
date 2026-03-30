using fxscreener.android.Models;
using fxscreener.android.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace fxscreener.android.ViewModels;

public class ScannerViewModel : BindableObject
{
    #region Поля и зависимости

    private readonly IMt5ApiService _apiService;
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly ITimeAggregationService _timeAggregationService;
    private readonly InstrumentsStorage _storage;

    private Timer? _updateTimer;
    private bool _isLoading;
    private string _statusMessage = "Готов";
    private DateTime _lastUpdateTime;
    private int _utcOffset = 3;

    // Кэш для истории (по символу и периоду)
    private readonly Dictionary<string, List<Bar>> _historyCache = new();

    private Dictionary<string, (List<Bar> bars, List<double> wpr5, List<double> wpr21)> _chartDataCache = new();

    #endregion

    #region Конструктор

    public ScannerViewModel(
        IMt5ApiService apiService,
        IIndicatorCalculator indicatorCalculator,
        ITimeAggregationService timeAggregationService)
    {
        _apiService = apiService;
        _indicatorCalculator = indicatorCalculator;
        _timeAggregationService = timeAggregationService;

        // Загружаем сохранённые инструменты
        Task.Run(LoadInstrumentsAsync);

        // Команды
        RefreshCommand = new Command(async () => await ForceRefreshAsync());

        // Запускаем таймер обновления
        StartUpdateTimer();
    }

    #endregion

    #region Свойства для привязки

    private ObservableCollection<DisplayRow> _displayRows = new();
    public ObservableCollection<DisplayRow> DisplayRows
    {
        get => _displayRows;
        set
        {
            _displayRows = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public DateTime LastUpdateTime
    {
        get => _lastUpdateTime;
        set
        {
            _lastUpdateTime = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LastUpdateTimeFormatted));
        }
    }

    public string LastUpdateTimeFormatted => LastUpdateTime.ToString("HH:mm:ss");

    #endregion

    #region Команды

    public ICommand RefreshCommand { get; }

    #endregion

    #region Загрузка инструментов

    private async Task LoadInstrumentsAsync()
    {
        try
        {
            var loaded = await InstrumentsStorage.LoadAsync();
            // Сохраняем в поле (в реальности нужно хранить список)
            // Пока оставляем как есть
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки инструментов: {ex.Message}";
        }
    }

    #endregion

    #region Таймер обновления

    private void StartUpdateTimer()
    {
        _updateTimer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    private async void OnTimerTick(object? state)
    {
        await UpdateAllInstrumentsAsync();
    }

    #endregion

    #region Основной метод обновления

    private async Task UpdateAllInstrumentsAsync()
    {
        if (IsLoading) return;

        IsLoading = true;

        try
        {
            // Загружаем актуальный список инструментов
            var storage = await InstrumentsStorage.LoadAsync();
            var allInstruments = storage.GetAllInstruments();

            if (allInstruments.Count == 0)
            {
                StatusMessage = "Нет инструментов для сканирования";
                MainThread.BeginInvokeOnMainThread(() => DisplayRows.Clear());
                return;
            }

            StatusMessage = "Обновление данных...";

            // Группируем по периодам для массовой загрузки истории
            var groups = allInstruments
                .GroupBy(x => x.Period)
                .ToList();

            var allResults = new List<InstrumentScanResult>();
            var nowUtc = DateTime.UtcNow;
            var nowLocal = nowUtc.AddHours(_utcOffset);

            // 1. Загружаем историю для всех групп (50 баров)
            foreach (var group in groups)
            {
                var period = group.Key;
                var instrumentsInGroup = group.ToList();
                var symbols = instrumentsInGroup.Select(x => x.Symbol).ToList();
                var timeframeMinutes = Mt5ApiService.ConvertPeriodToMinutes(period);

                // Загружаем историю с расширением периода
                var historyItems = await LoadHistoryWithExpansionAsync(symbols, timeframeMinutes);

                if (historyItems == null || historyItems.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"No history data for {period}");
                    continue;
                }

                // Для каждого инструмента в группе находим его бары и рассчитываем индикаторы
                foreach (var instrument in instrumentsInGroup)
                {
                    var itemForSymbol = historyItems.FirstOrDefault(h => h.Symbol == instrument.Symbol);
                    if (itemForSymbol?.Bars == null || itemForSymbol.Bars.Count < 21)
                        continue;

                    // Конвертируем бары
                    var bars = itemForSymbol.Bars.Select(b => new Bar
                    {
                        Time = b.Time,
                        Open = b.OpenPrice,
                        High = b.HighPrice,
                        Low = b.LowPrice,
                        Close = b.ClosePrice,
                        Volume = b.Volume,
                        Ticks = (int)b.TickVolume
                    }).ToList();

                    var reversedBars = bars.Reverse<Bar>().ToList();

                    // получаем WPR значения и сохраняем в кэш
                    var wpr5Values = _indicatorCalculator.GetWprValues(reversedBars, 5);
                    var wpr21Values = _indicatorCalculator.GetWprValues(reversedBars, 21);

                    // Рассчитываем индикаторы для грида
                    var result = _indicatorCalculator.CalculateForInstrument(
                        instrument.Symbol, period, reversedBars);

                    allResults.Add(result);
                }
            }

            // 3. Формируем DisplayRows
            BuildDisplayRows(allResults);

            LastUpdateTime = DateTime.Now;
            StatusMessage = $"Обновлено: {allResults.Count} инструментов";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Update error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Загрузить исторические бары для списка символов с расширением периода до получения 50 баров
    /// </summary>
    private async Task<List<PriceHistoryItem>?> LoadHistoryWithExpansionAsync(
        List<string> symbols,
        int timeframeMinutes)
    {
        var now = DateTime.Now.AddHours(3);
        var barsNeeded = 50;
        var attempts = 0;

        // Начальный период: от now - (timeframeMinutes * barsNeeded)
        var from = now.AddMinutes(-timeframeMinutes * barsNeeded);

        while (attempts < 3)
        {
            var response = await _apiService.GetPriceHistoryManyAsync(
                symbols,
                from,
                now,
                timeframeMinutes);

            if (response != null && response.Count > 0)
            {
                // Проверяем, хватает ли баров для каждого символа
                bool allHaveEnough = true;
                foreach (var item in response)
                {
                    if (item.Bars == null || item.Bars.Count < barsNeeded)
                    {
                        allHaveEnough = false;
                        break;
                    }
                }

                if (allHaveEnough)
                {
                    // Берём последние 50 баров для каждого символа
                    foreach (var item in response)
                    {
                        if (item.Bars != null && item.Bars.Count > barsNeeded)
                        {
                            item.Bars = item.Bars.TakeLast(barsNeeded).ToList();
                        }
                    }
                    return response;
                }
            }

            // Не хватило баров — расширяем период
            attempts++;
            from = from.AddMinutes(-timeframeMinutes * barsNeeded);
            System.Diagnostics.Debug.WriteLine($"Expanding history: attempt {attempts}, new from={from}");
        }

        // После всех попыток возвращаем то, что есть
        var finalResponse = await _apiService.GetPriceHistoryManyAsync(
            symbols,
            from,
            now,
            timeframeMinutes);

        return finalResponse;
    }

    #endregion

    #region Формирование DisplayRows

    private void BuildDisplayRows(List<InstrumentScanResult> allResults)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var displayRows = new ObservableCollection<DisplayRow>();
            var toolIndex = 0;

            foreach (var result in allResults.OrderBy(r => r.Name))
            {
                var pairColor = (toolIndex % 2 == 0) ? "White" : "#F8F8F8";

                // W5e
                var w5eColor = GetWprColor(result.W5e);
                var w5eText = result.W5e?.BarNumber.ToString() ?? "";

                // UD5
                var ud5Color = GetUdColor(result.UD5);
                var ud5Display = GetUdDisplay(result.UD5);

                displayRows.Add(new DisplayRow
                {
                    Name = result.Name,
                    Period = result.Period,
                    C5 = result.C5,
                    F2 = result.F2,
                    WprDisplay = w5eText,
                    WprBackgroundColor = w5eColor.Background,
                    WprTextColor = w5eColor.Text,
                    UdBackgroundColor = ud5Color,
                    UdDisplay = ud5Display,
                    IsFirstRow = true,
                    IsSecondRow = false,
                    PairColor = pairColor
                });

                // W21e
                var w21eColor = GetWprColor(result.W21e);
                var w21eText = result.W21e?.BarNumber.ToString() ?? "";

                // UD21
                var ud21Color = GetUdColor(result.UD21);
                var ud21Display = GetUdDisplay(result.UD21);

                displayRows.Add(new DisplayRow
                {
                    Name = null,
                    Period = null,
                    C5 = null,
                    F2 = null,
                    WprDisplay = w21eText,
                    WprBackgroundColor = w21eColor.Background,
                    WprTextColor = w21eColor.Text,
                    UdBackgroundColor = ud21Color,
                    UdDisplay = ud21Display,
                    IsFirstRow = false,
                    IsSecondRow = true,
                    PairColor = pairColor
                });

                toolIndex++;
            }

            DisplayRows.Clear();
            foreach (var row in displayRows)
                DisplayRows.Add(row);

            LastUpdateTime = DateTime.Now;
            StatusMessage = $"Обновлено: {allResults.Count} инструментов";
        });
    }

    private (Color? Background, Color? Text) GetWprColor(WprSignal? signal)
    {
        if (signal == null) return (null, null);

        return signal.SignalType switch
        {
            WprSignalType.AboveMinus20 => (Color.FromArgb("#FFCCCC"), Color.FromArgb("#990000")),
            WprSignalType.StrongAboveMinus5 => (Color.FromArgb("#FF6666"), Color.FromArgb("#CC0000")),
            WprSignalType.BelowMinus80 => (Color.FromArgb("#CCFFCC"), Color.FromArgb("#006600")),
            WprSignalType.StrongBelowMinus95 => (Color.FromArgb("#66CC66"), Color.FromArgb("#003300")),
            _ => (null, null)
        };
    }

    private Color? GetUdColor(UdSignal? signal)
    {
        if (signal == null) return null;

        return signal.SignalType switch
        {
            UdSignalType.Bullish => Color.FromArgb("#CCFFCC"),
            UdSignalType.Bearish => Color.FromArgb("#FFCCCC"),
            _ => null
        };
    }

    private string GetUdDisplay(UdSignal? signal)
    {
        if (signal == null) return "";

        return signal.SignalType switch
        {
            UdSignalType.Bullish => "▲",
            UdSignalType.Bearish => "▼",
            _ => ""
        };
    }

    #endregion

    #region Вспомогательные методы

    public (List<Bar> bars, List<double> wpr5, List<double> wpr21)? GetChartData(string symbol, string period)
    {
        var key = $"{symbol}_{period}";
        if (_chartDataCache.TryGetValue(key, out var data))
            return data;
        return null;
    }

    public async Task<InstrumentParams?> GetInstrumentByName(string symbol, string period)
    {
        try
        {
            var storage = await InstrumentsStorage.LoadAsync();
            return storage.Get(symbol, period);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetInstrumentByName error: {ex.Message}");
            return null;
        }
    }

    private async Task ForceRefreshAsync()
    {
        await UpdateAllInstrumentsAsync();
    }

    public void Cleanup()
    {
        _updateTimer?.Dispose();
    }

    #endregion
}