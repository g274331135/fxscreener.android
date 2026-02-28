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
        Task.Run(async () => await LoadInstrumentsAsync());

        // Команды
        RefreshCommand = new Command(async () => await ForceRefreshAsync());

        // Запускаем таймер обновления
        StartUpdateTimer();
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
            var activeInstruments = storage.GetActiveInstruments();

            if (activeInstruments.Count == 0)
            {
                StatusMessage = "Нет активных инструментов";
                return;
            }

            StatusMessage = "Обновление данных...";

            // Группируем по периодам для массовой загрузки
            var groups = activeInstruments
                .GroupBy(x => x.Period)
                .ToList();

            var allResults = new List<InstrumentScanResult>();
            var now = DateTime.UtcNow.AddHours(_utcOffset);

            foreach (var group in groups)
            {
                var period = group.Key;
                var symbols = group.Select(x => x.Symbol).ToList();
                var timeframeMinutes = Mt5ApiService.ConvertPeriodToMinutes(period);

                // Определяем режим (достройка или обычный)
                var isBuilding = _timeAggregationService.IsBuildingMode(now, timeframeMinutes);

                List<Bar>? barsForGroup = null;

                if (isBuilding)
                {
                    // Режим достройки: нужны минутные данные
                    StatusMessage = $"Достройка {period}...";
                    barsForGroup = await GetBuildingBarsAsync(symbols, period, timeframeMinutes);
                }
                else
                {
                    // Обычный режим: грузим последние 50 баров
                    StatusMessage = $"Загрузка {period}...";
                    barsForGroup = await GetHistoricalBarsAsync(symbols, period, timeframeMinutes);
                }

                // Для каждого символа рассчитываем индикаторы
                foreach (var symbol in symbols)
                {
                    // Находим бары для этого символа (в реальности нужно маппить по символу)
                    // Здесь упрощённо - в production нужно словарь
                    var symbolBars = barsForGroup; // В реальности фильтровать по символу

                    if (symbolBars != null && symbolBars.Count >= 21)
                    {
                        var result = _indicatorCalculator.CalculateForInstrument(
                            symbol, period, symbolBars);
                        allResults.Add(result);
                    }
                }
            }

            // После получения allResults, преобразуем в DisplayRows
            var displayRows = new ObservableCollection<DisplayRow>();
            foreach (var result in allResults.OrderBy(r => r.Name))
            {
                // Первая строка
                displayRows.Add(new DisplayRow
                {
                    Name = result.Name,
                    Period = result.Period,
                    C5 = result.C5,
                    F2 = result.F2,
                    Col1 = result.W5_20,
                    Col2 = result.U_W5d ? "+" : "",
                    Col3 = result.W5_80,
                    Col4 = result.D_W5u ? "+" : "",
                    IsFirstRow = true,
                    IsSecondRow = false
                });

                // Вторая строка
                displayRows.Add(new DisplayRow
                {
                    Name = null, // пусто
                    Period = null,
                    C5 = null,
                    F2 = null,
                    Col1 = result.W21_20,
                    Col2 = result.U_W21d ? "+" : "",
                    Col3 = result.W21_80,
                    Col4 = result.D_W21u ? "+" : "",
                    IsFirstRow = false,
                    IsSecondRow = true
                });
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DisplayRows.Clear();
                foreach (var row in displayRows)
                    DisplayRows.Add(row);
            });

            // Обновляем грид
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ScanResults.Clear();
                foreach (var result in allResults.OrderBy(r => r.Name))
                {
                    ScanResults.Add(result);
                }

                LastUpdateTime = DateTime.Now;
                StatusMessage = $"Обновлено: {ScanResults.Count} инструментов";
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Получить бары для обычного режима (последние 50 закрытых)
    /// </summary>
    private async Task<List<Bar>> GetHistoricalBarsAsync(
        List<string> symbols,
        string period,
        int timeframeMinutes)
    {
        var request = new PriceHistoryManyRequest
        {
            symbolsPeriods = symbols.Select(s => new SymbolPeriodRequest
            {
                symbol = s,
                timeframe = timeframeMinutes,
                barsCount = 50
            }).ToList()
        };

        var response = await _apiService.GetPriceHistoryManyAsync(request);

        if (response?.data == null || response.data.Count == 0)
            return new List<Bar>();

        // Агрегируем все бары (упрощённо)
        var allBars = new List<Bar>();
        foreach (var symbolData in response.data)
        {
            var bars = _timeAggregationService.AggregateToTargetZone(
                symbolData.bars, _utcOffset);
            allBars.AddRange(bars);
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
        // Загружаем минутные данные (последние 60 минут)
        var minuteRequest = new PriceHistoryManyRequest
        {
            symbolsPeriods = symbols.Select(s => new SymbolPeriodRequest
            {
                symbol = s,
                timeframe = 1, // M1
                barsCount = 60
            }).ToList()
        };

        var minuteResponse = await _apiService.GetPriceHistoryManyAsync(minuteRequest);

        if (minuteResponse?.data == null || minuteResponse.data.Count == 0)
            return new List<Bar>();

        // Для каждого символа строим текущий бар
        var resultBars = new List<Bar>();
        var now = DateTime.UtcNow.AddHours(_utcOffset);

        foreach (var symbolData in minuteResponse.data)
        {
            var minuteBars = _timeAggregationService.AggregateToTargetZone(
                symbolData.bars, _utcOffset);

            // Строим текущий незакрытый бар
            var currentBar = _timeAggregationService.BuildCurrentBarFromMinutes(
                minuteBars, timeframeMinutes, _utcOffset);

            // Добавляем несколько последних закрытых баров для контекста
            // В реальности нужно загрузить и их, но для простоты пока так
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

    #region Освобождение ресурсов

    public void Cleanup()
    {
        _updateTimer?.Dispose();
    }

    #endregion
}