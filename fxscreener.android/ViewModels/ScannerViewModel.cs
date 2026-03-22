using System.Collections.ObjectModel;
using System.Windows.Input;
using fxscreener.android.Models;
using fxscreener.android.Services;

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
    private int _utcOffset = 3; // По умолчанию Москва

    private string _currentOperationId = string.Empty;

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

        // Проверяем подключение при старте
        Task.Run(async () =>
        {
            if (!_apiService.IsConnected)
            {
                var settings = await ApiSettings.LoadAsync();
                if (settings != null)
                {
                    await _apiService.ConnectAsync(settings);
                }
            }

            // Загружаем инструменты
            await LoadInstrumentsAsync();

            // Запускаем таймер обновления
            StartUpdateTimer();
        });

        RefreshCommand = new Command(async () => await ForceRefreshAsync());
    }

    #endregion

    #region Свойства для привязки

    private ObservableCollection<InstrumentScanResult> _scanResults = new();

    /// <summary>
    /// Результаты сканирования для отображения в гриде
    /// </summary>
    public ObservableCollection<InstrumentScanResult> ScanResults
    {
        get => _scanResults;
        set
        {
            _scanResults = value;
            OnPropertyChanged();
        }
    }

    private ObservableCollection<DisplayRow> _displayRows = new();

    /// <summary>
    /// 
    /// </summary>
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
            // Загружаем настройки чтобы получить OperationId
            var settings = await ApiSettings.LoadAsync();

            var loaded = await InstrumentsStorage.LoadAsync();
            // Копируем инструменты в наше поле (но нам нужен доступ к ним)
            // Для простоты будем использовать _storage в методах обновления
            // В реальном проекте лучше сделать отдельное свойство
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
        // Проверяем каждую минуту
        _updateTimer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    private async void OnTimerTick(object? state)
    {
        await UpdateAllInstrumentsAsync();
    }

    #endregion

    #region Основной метод обновления

    /// <summary>
    /// Обновить все активные инструменты
    /// </summary>
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

            // Группируем по периодам
            var groups = allInstruments
                .GroupBy(x => x.Period)
                .ToList();

            var allResults = new List<InstrumentScanResult>();
            var nowUtc = DateTime.UtcNow;
            var nowLocal = nowUtc.AddHours(_utcOffset);

            foreach (var group in groups)
            {
                var period = group.Key;
                var instrumentsInGroup = group.ToList();
                var symbols = instrumentsInGroup.Select(x => x.Symbol).ToList();
                var timeframeMinutes = Mt5ApiService.ConvertPeriodToMinutes(period);

                // Определяем режим
                var isBuilding = _timeAggregationService.IsBuildingMode(nowLocal, timeframeMinutes);

                List<PriceHistoryItem>? historyItems = null;

                try
                {
                    if (isBuilding)
                    {
                        StatusMessage = $"Достройка {period}...";
                        // Используем метод GetBuildingBarsAsync (который теперь будет возвращать сырые данные)
                        historyItems = await GetBuildingHistoryAsync(symbols, timeframeMinutes);
                    }
                    else
                    {
                        StatusMessage = $"Загрузка {period}...";
                        // Используем метод GetHistoricalBarsAsync (который теперь будет возвращать сырые данные)
                        historyItems = await GetHistoricalHistoryAsync(symbols, timeframeMinutes);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading {period}: {ex.Message}");
                    continue;
                }

                if (historyItems == null || historyItems.Count == 0)
                    continue;

                // Обрабатываем каждый инструмент
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

                    var result = _indicatorCalculator.CalculateForInstrument(
                        instrument.Symbol, period, bars);
                    allResults.Add(result);
                }
            }

            // Формируем DisplayRows...
            BuildDisplayRows(allResults);
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
    /// Возвращает цвета для WPR сигнала
    /// </summary>
    private (Color? Background, Color? Text) GetWprColor(WprSignal? signal)
    {
        if (signal == null)
            return (null, null);

        switch (signal.SignalType)
        {
            case WprSignalType.AboveMinus20:
                return (Color.FromArgb("#FFCCCC"), Color.FromArgb("#990000"));
            case WprSignalType.StrongAboveMinus5:
                return (Color.FromArgb("#FF6666"), Color.FromArgb("#CC0000"));
            case WprSignalType.BelowMinus80:
                return (Color.FromArgb("#CCFFCC"), Color.FromArgb("#006600"));
            case WprSignalType.StrongBelowMinus95:
                return (Color.FromArgb("#66CC66"), Color.FromArgb("#003300"));
            default:
                return (null, null);
        }
    }

    /// <summary>
    /// Загружает исторические данные (обычный режим) с запасом на выходные
    /// </summary>
    private async Task<List<PriceHistoryItem>?> GetHistoricalHistoryAsync(
        List<string> symbols,
        int timeframeMinutes)
    {
        var now = DateTime.UtcNow;
        var neededBarsCount = 50;
        var multiplier = GetSafetyMultiplier(timeframeMinutes);
        var requestedBarsCount = neededBarsCount * multiplier;
        var from = now.AddMinutes(-timeframeMinutes * requestedBarsCount);

        System.Diagnostics.Debug.WriteLine($"Loading history: from={from:yyyy-MM-dd HH:mm}, to={now:yyyy-MM-dd HH:mm}, requested={requestedBarsCount} bars");

        var response = await _apiService.GetPriceHistoryManyAsync(
            symbols,  // больше не передаём operationId
            from,
            now,
            timeframeMinutes);

        if (response == null || response.Count == 0)
            return null;

        // Оставляем только последние 50 баров для каждого символа
        foreach (var item in response)
        {
            if (item.Bars != null && item.Bars.Count > neededBarsCount)
            {
                item.Bars = item.Bars.TakeLast(neededBarsCount).ToList();
            }
            System.Diagnostics.Debug.WriteLine($"Symbol {item.Symbol}: {item.Bars?.Count ?? 0} bars after trim");
        }

        return response;
    }

    /// <summary>
    /// Загружает данные для достройки текущего бара (минутные данные)
    /// </summary>
    private async Task<List<PriceHistoryItem>?> GetBuildingHistoryAsync(
        List<string> symbols,
        int timeframeMinutes)
    {
        var now = DateTime.UtcNow;
        var periodStart = _timeAggregationService.FloorToTimeframe(now.AddHours(_utcOffset), timeframeMinutes).ToUniversalTime();

        System.Diagnostics.Debug.WriteLine($"Building mode: from={periodStart:yyyy-MM-dd HH:mm}, to={now:yyyy-MM-dd HH:mm}");

        var response = await _apiService.GetPriceHistoryManyAsync(
            symbols,
            periodStart,
            now,
            1); // M1

        return response;
    }

    /// <summary>
    /// Формирует DisplayRows из результатов расчёта
    /// </summary>
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

                displayRows.Add(new DisplayRow
                {
                    Name = result.Name,
                    Period = result.Period,
                    C5 = result.C5,
                    F2 = result.F2,
                    WprDisplay = w5eText,
                    WprColor = w5eColor.Item1,
                    WprTextColor = w5eColor.Item2,
                    IsFirstRow = true,
                    IsSecondRow = false,
                    PairColor = pairColor
                });

                // W21e
                var w21eColor = GetWprColor(result.W21e);
                var w21eText = result.W21e?.BarNumber.ToString() ?? "";

                displayRows.Add(new DisplayRow
                {
                    Name = null,
                    Period = null,
                    C5 = null,
                    F2 = null,
                    WprDisplay = w21eText,
                    WprColor = w21eColor.Item1,
                    WprTextColor = w21eColor.Item2,
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

    /// <summary>
    /// Принудительное обновление (по кнопке)
    /// </summary>
    private async Task ForceRefreshAsync()
    {
        await UpdateAllInstrumentsAsync();
    }

    #endregion

    /// <summary>
    /// Возвращает множитель для запаса баров в зависимости от таймфрейма
    /// </summary>
    private int GetSafetyMultiplier(int timeframeMinutes)
    {
        return timeframeMinutes switch
        {
            <= 5 => 2,    // M1, M5 — рынок почти всегда открыт, запас 2x
            <= 60 => 5,   // M15, M30, H1 — покрывает выходные
            <= 240 => 7,  // H4 — больше запас на случай длинных выходных
            _ => 10       // D1, W1 — максимальный запас
        };
    }

    #region Освобождение ресурсов

    public void Cleanup()
    {
        _updateTimer?.Dispose();
    }

    #endregion
}