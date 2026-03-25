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
    private readonly IBuildingService _buildingService;
    private readonly BuildSettings _buildSettings;
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
        ITimeAggregationService timeAggregationService,
        IBuildingService buildingService,
        BuildSettings buildSettings)
    {
        _apiService = apiService;
        _indicatorCalculator = indicatorCalculator;
        _timeAggregationService = timeAggregationService;
        _buildingService = buildingService;
        _buildSettings = buildSettings;

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
                        Time = b.Time.AddHours(_utcOffset),
                        Open = b.OpenPrice,
                        High = b.HighPrice,
                        Low = b.LowPrice,
                        Close = b.ClosePrice,
                        Volume = b.Volume,
                        Ticks = (int)b.TickVolume
                    }).ToList();

                    // ✅ НОВЫЙ КОД: получаем WPR значения и сохраняем в кэш
                    var wpr5Values = _indicatorCalculator.GetWprValues(bars, 5);
                    var wpr21Values = _indicatorCalculator.GetWprValues(bars, 21);

                    var cacheKey = $"{instrument.Symbol}_{instrument.Period}";
                    _chartDataCache[cacheKey] = (bars, wpr5Values, wpr21Values);

                    // Рассчитываем индикаторы для грида
                    var result = _indicatorCalculator.CalculateForInstrument(
                        instrument.Symbol, period, bars);

                    allResults.Add(result);
                }
            }

            // 2. Обрабатываем достройку для инструментов, у которых осталось < BuildTimeMinutes
            foreach (var instrument in allInstruments)
            {
                var timeframeMinutes = Mt5ApiService.ConvertPeriodToMinutes(instrument.Period);
                var cacheKey = $"{instrument.Symbol}_{instrument.Period}";

                if (_historyCache.TryGetValue(cacheKey, out var historyBars) && historyBars.Count > 0)
                {
                    var lastClosedBar = historyBars.First(); // bars[0] — последний закрытый бар
                    var shouldBuild = _buildingService.ShouldBuild(nowLocal, lastClosedBar.Time, timeframeMinutes);

                    if (shouldBuild)
                    {
                        // Достраиваем текущий бар
                        var currentBar = await _buildingService.BuildCurrentBarAsync(
                            instrument.Symbol,
                            lastClosedBar.Time,
                            timeframeMinutes);

                        if (currentBar != null && currentBar.Open != 0)
                        {
                            // Создаём копию списка баров с текущим баром на первом месте
                            var barsWithCurrent = new List<Bar> { currentBar };
                            barsWithCurrent.AddRange(historyBars);

                            // Пересчитываем индикаторы с учётом текущего бара
                            var result = _indicatorCalculator.CalculateForInstrument(
                                instrument.Symbol, instrument.Period, barsWithCurrent);

                            // Обновляем результат в allResults (заменяем)
                            var existing = allResults.FirstOrDefault(r => r.Name == instrument.Symbol && r.Period == instrument.Period);
                            if (existing != null)
                            {
                                allResults.Remove(existing);
                                allResults.Add(result);
                            }
                        }
                    }
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
        var now = DateTime.UtcNow;
        var barsNeeded = 50;
        var attempts = 0;

        // Начальный период: от now - (timeframeMinutes * barsNeeded)
        var from = now.AddMinutes(-timeframeMinutes * barsNeeded);

        while (attempts < _buildSettings.MaxHistoryAttempts)
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

    #region Обработчики
    private async void OnInstrumentTapped(object sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is DisplayRow row && row.IsFirstRow)
        {
            var instrument = _viewModel.GetInstrumentByName(row.Name, row.Period); // этот метод нужно добавить в ViewModel
            if (instrument == null) return;

            var chartData = _viewModel.GetChartData(row.Name, row.Period);
            if (chartData == null) return;

            var (bars, wpr5, wpr21) = chartData.Value;

            var chartVM = _serviceProvider.GetRequiredService<ChartViewModel>();
            await chartVM.LoadData(instrument.Symbol, instrument.Period, bars, wpr5, wpr21);
            await Shell.Current.GoToAsync("chart");
        }
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

    public InstrumentParams? GetInstrumentByName(string symbol, string period)
    {
        // Загружаем инструменты синхронно (или используйте _storage, если он уже загружен)
        var storage = InstrumentsStorage.LoadAsync().GetAwaiter().GetResult();
        return storage.Get(symbol, period);
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