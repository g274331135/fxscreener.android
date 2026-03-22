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
            if (settings != null)
            {
                _currentOperationId = settings.OperationId;
            }

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
            // 1. Загружаем актуальный список инструментов
            var storage = await InstrumentsStorage.LoadAsync();
            var allInstruments = storage.GetAllInstruments();

            if (allInstruments.Count == 0)
            {
                StatusMessage = "Нет инструментов для сканирования";
                MainThread.BeginInvokeOnMainThread(() => DisplayRows.Clear());
                return;
            }

            StatusMessage = "Обновление данных...";

            // 2. Группируем по периодам для массовой загрузки
            var groups = allInstruments
                .GroupBy(x => x.Period)
                .ToList();

            var allResults = new List<InstrumentScanResult>();
            var nowUtc = DateTime.UtcNow; // Для запросов к API всегда UTC
            var nowLocal = nowUtc.AddHours(_utcOffset); // Для логики времени

            foreach (var group in groups)
            {
                var period = group.Key;
                var instrumentsInGroup = group.ToList(); // Все инструменты этой группы
                var symbols = instrumentsInGroup.Select(x => x.Symbol).ToList();
                var timeframeMinutes = Mt5ApiService.ConvertPeriodToMinutes(period);

                // 3. Определяем режим (достройка или обычный)
                var isBuilding = _timeAggregationService.IsBuildingMode(nowLocal, timeframeMinutes);

                // 4. Загружаем данные для ВСЕХ символов группы
                List<PriceHistoryItem>? historyItems = null;

                try
                {
                    if (isBuilding)
                    {
                        StatusMessage = $"Достройка {period}...";
                        var periodStart = _timeAggregationService.FloorToTimeframe(nowLocal, timeframeMinutes).ToUniversalTime();

                        var response = await _apiService.GetPriceHistoryManyAsync(
                            _currentOperationId,
                            symbols,
                            periodStart,
                            nowUtc,
                            1); // Минутные данные

                        historyItems = response?.ToList();
                    }
                    else
                    {
                        StatusMessage = $"Загрузка {period}...";
                        var from = nowUtc.AddMinutes(-timeframeMinutes * 50); // Последние 50 баров

                        var response = await _apiService.GetPriceHistoryManyAsync(
                            _currentOperationId,
                            symbols,
                            from,
                            nowUtc,
                            timeframeMinutes);

                        historyItems = response?.ToList();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading {period}: {ex.Message}");
                    continue;
                }

                if (historyItems == null || historyItems.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"No data received for {period}");
                    continue;
                }

                // 5. Для КАЖДОГО инструмента в группе находим его бары и рассчитываем индикаторы
                foreach (var instrument in instrumentsInGroup)
                {
                    // Находим элемент истории, соответствующий символу
                    var itemForSymbol = historyItems.FirstOrDefault(h => h.Symbol == instrument.Symbol);

                    if (itemForSymbol?.Bars == null || itemForSymbol.Bars.Count < 21)
                    {
                        System.Diagnostics.Debug.WriteLine($"Not enough bars for {instrument.Symbol} ({itemForSymbol?.Bars.Count ?? 0})");
                        continue;
                    }

                    // Конвертируем бары API в наш формат Bar и корректируем время
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

                    // Рассчитываем индикаторы
                    var result = _indicatorCalculator.CalculateForInstrument(
                        instrument.Symbol,
                        period,
                        bars);

                    allResults.Add(result);
                }
            }

            // 6. Преобразуем результаты в DisplayRows (две строки на инструмент)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var displayRows = new ObservableCollection<DisplayRow>();
                var toolIndex = 0; // Счётчик инструментов для чередования цветов

                foreach (var result in allResults.OrderBy(r => r.Name))
                {
                    // Определяем цвет пары: чётный индекс — белый, нечётный — светло-серый
                    var pairColor = (toolIndex % 2 == 0) ? "White" : "#F8F8F8";

                    // --- Первая строка (W5e) ---
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

                    // --- Вторая строка (W21e) ---
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

                    toolIndex++; // Переходим к следующему инструменту
                }

                DisplayRows.Clear();
                foreach (var row in displayRows)
                    DisplayRows.Add(row);

                LastUpdateTime = DateTime.Now;
                StatusMessage = $"Обновлено: {allResults.Count} инструментов";
            });
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

    // Вспомогательный метод для определения цветов
    private (Color? Background, Color? Text) GetWprColor(WprSignal? signal)
    {
        if (signal == null)
            return (null, null);

        switch (signal.SignalType)
        {
            case WprSignalType.AboveMinus20:
                return (Color.FromArgb("#FFCCCC"), Color.FromArgb("#990000")); // бледно-красный
            case WprSignalType.StrongAboveMinus5:
                return (Color.FromArgb("#FF6666"), Color.FromArgb("#CC0000")); // красный
            case WprSignalType.BelowMinus80:
                return (Color.FromArgb("#CCFFCC"), Color.FromArgb("#006600")); // бледно-зелёный
            case WprSignalType.StrongBelowMinus95:
                return (Color.FromArgb("#66CC66"), Color.FromArgb("#003300")); // зелёный
            default:
                return (null, null);
        }
    }

    /// <summary>
    /// Получить бары для обычного режима
    /// </summary>
    private async Task<List<Bar>> GetHistoricalBarsAsync(
        List<string> symbols,
        string period,
        int timeframeMinutes)
    {
        var now = DateTime.UtcNow;
        var neededBarsCount = 50; // Нам нужно 50 баров

        // Получаем множитель запаса
        var multiplier = GetSafetyMultiplier(timeframeMinutes);
        var requestedBarsCount = neededBarsCount * multiplier;

        // Рассчитываем from с запасом
        var from = now.AddMinutes(-timeframeMinutes * requestedBarsCount);

        System.Diagnostics.Debug.WriteLine($"Loading {period} for {string.Join(",", symbols)}: " +
            $"from={from:yyyy-MM-dd HH:mm}, to={now:yyyy-MM-dd HH:mm}, " +
            $"requested={requestedBarsCount} bars (multiplier={multiplier})");

        var response = await _apiService.GetPriceHistoryManyAsync(
            _currentOperationId,
            symbols,
            from,
            now,
            timeframeMinutes);

        if (response == null || response.Count == 0)
            return new List<Bar>();

        var allBars = new List<Bar>();

        foreach (var item in response)
        {
            if (item.Bars == null || item.Bars.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"No bars received for {item.Symbol}");
                continue;
            }

            System.Diagnostics.Debug.WriteLine($"Received {item.Bars.Count} bars for {item.Symbol}");

            // Берём последние 50 баров (или сколько есть, если меньше)
            var lastBars = item.Bars
                .TakeLast(neededBarsCount)
                .Select(b => new Bar
                {
                    Time = b.Time.AddHours(_utcOffset),
                    Open = b.OpenPrice,
                    High = b.HighPrice,
                    Low = b.LowPrice,
                    Close = b.ClosePrice,
                    Volume = b.Volume,
                    Ticks = (int)b.TickVolume
                })
                .ToList();

            System.Diagnostics.Debug.WriteLine($"After TakeLast({neededBarsCount}): {lastBars.Count} bars for {item.Symbol}");

            allBars.AddRange(lastBars);
        }

        return allBars;
    }

    /// <summary>
    /// Получить бары для режима достройки (минутные данные + агрегация)
    /// </summary>
    private async Task<List<Bar>> GetBuildingBarsAsync(
        List<string> symbols,
        string period,
        int timeframeMinutes)
    {
        var now = DateTime.UtcNow;
        var periodStart = _timeAggregationService.FloorToTimeframe(now.AddHours(_utcOffset), timeframeMinutes).ToUniversalTime();

        var response = await _apiService.GetPriceHistoryManyAsync(
            _currentOperationId,
            symbols,
            periodStart,
            now,
            1); // M1

        if (response == null || response.Count == 0)
            return new List<Bar>();

        var resultBars = new List<Bar>();

        foreach (var item in response)
        {
            var minuteBars = item.Bars.Select(b => new Bar
            {
                Time = b.Time.AddHours(_utcOffset),
                Open = b.OpenPrice,
                High = b.HighPrice,
                Low = b.LowPrice,
                Close = b.ClosePrice,
                Volume = b.Volume,
                Ticks = (int)b.TickVolume
            }).ToList();

            var currentBar = _timeAggregationService.BuildCurrentBarFromMinutes(
                minuteBars, timeframeMinutes, _utcOffset);

            resultBars.Add(currentBar);
        }

        return resultBars;
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