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
            // Загружаем актуальный список инструментов - теперь ВСЕ инструменты
            var storage = await InstrumentsStorage.LoadAsync();
            var allInstruments = storage.GetAllInstruments(); // вместо GetActiveInstruments()

            if (allInstruments.Count == 0)
            {
                StatusMessage = "Нет инструментов для сканирования";
                return;
            }

            StatusMessage = "Обновление данных...";

            // Группируем по периодам для массовой загрузки
            var groups = allInstruments
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
                // ВАЖНО: нужно сопоставить бары с символами
                // Это упрощённая версия - в реальности нужно маппить по символам
                if (barsForGroup != null)
                {
                    // Здесь должна быть логика распределения баров по символам
                    // Пока для примера просто создаём результат для первого символа
                    if (symbols.Count > 0 && barsForGroup.Count >= 21)
                    {
                        var result = _indicatorCalculator.CalculateForInstrument(
                            symbols[0], period, barsForGroup);
                        allResults.Add(result);
                    }
                }
            }

            // Обновляем грид
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Здесь нужно преобразовать allResults в DisplayRows
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
                        Name = null,
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
        var now = DateTime.UtcNow;
        var from = now.AddMinutes(-timeframeMinutes * 50);

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
            foreach (var bar in item.Bars)
            {
                allBars.Add(new Bar
                {
                    Time = bar.Time.AddHours(_utcOffset),
                    Open = bar.OpenPrice,
                    High = bar.HighPrice,
                    Low = bar.LowPrice,
                    Close = bar.ClosePrice,
                    Volume = bar.Volume,
                    Ticks = (int)bar.TickVolume
                });
            }
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

    #region Освобождение ресурсов

    public void Cleanup()
    {
        _updateTimer?.Dispose();
    }

    #endregion
}