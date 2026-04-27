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
    private readonly IParallelLoaderService _parallelLoader;

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
        IParallelLoaderService parallelLoader)
    {
        _apiService = apiService;
        _indicatorCalculator = indicatorCalculator;
        _timeAggregationService = timeAggregationService;
        _parallelLoader = parallelLoader;

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
            var storage = await InstrumentsStorage.LoadAsync();
            var allInstruments = storage.GetAllInstruments();

            if (allInstruments.Count == 0)
            {
                StatusMessage = "Нет инструментов для сканирования";
                MainThread.BeginInvokeOnMainThread(() => DisplayRows.Clear());
                return;
            }

            StatusMessage = "Обновление данных (параллельная загрузка)...";

            // 🔥 ПАРАЛЛЕЛЬНАЯ ЗАГРУЗКА — возвращает словарь
            var barsDictionary = await _parallelLoader.LoadHistoryParallelAsync(
                allInstruments,
                maxParallelism: 5);

            if (barsDictionary == null || barsDictionary.Count == 0)
            {
                StatusMessage = "Не удалось загрузить данные";
                return;
            }

            var allResults = new List<InstrumentScanResult>();

            // Обрабатываем каждый инструмент
            foreach (var instrument in allInstruments)
            {
                var key = $"{instrument.Symbol}_{instrument.Period}";

                if (!barsDictionary.TryGetValue(key, out var bars))
                {
                    System.Diagnostics.Debug.WriteLine($"[Update] No data for {key}");
                    continue;
                }

                if (bars.Count < 21)
                {
                    System.Diagnostics.Debug.WriteLine($"[Update] Not enough bars for {key}: {bars.Count}");
                    continue;
                }

                var reversedBars = bars.Reverse<Bar>().ToList();

                var lastBar = reversedBars[0];
                var timeframeMinutes = Mt5ApiService.ConvertPeriodToMinutes(instrument.Period);
                var currentTime = DateTime.UtcNow.AddHours(_utcOffset);

                if (!IsBarClosed(lastBar, timeframeMinutes, currentTime))
                {
                    // Удаляем незакрытый бар
                    reversedBars.RemoveAt(0);
                    bars.RemoveAt(bars.Count - 1);
                    //System.Diagnostics.Debug.WriteLine($"[Scanner] Removed unclosed bar for {item.Symbol} at {lastBar.Time}");
                }

                // Получаем WPR значения для графика
                var wpr5Values = _indicatorCalculator.GetWprValues(reversedBars, 5);
                var wpr21Values = _indicatorCalculator.GetWprValues(reversedBars, 21);

                // Сохраняем в кэш для графика
                _chartDataCache[key] = (bars, wpr5Values, wpr21Values);

                // Рассчитываем индикаторы для грида
                var result = _indicatorCalculator.CalculateForInstrument(
                    instrument.Symbol, instrument.Period, reversedBars);

                result.Ws5Signal = _indicatorCalculator.CalculateWs(bars, 5);
                result.Ws21Signal = _indicatorCalculator.CalculateWs(bars, 21);

                allResults.Add(result);
            }

            // Формируем DisplayRows
            BuildDisplayRows(allResults);

            LastUpdateTime = DateTime.Now;
            StatusMessage = $"Обновлено: {allResults.Count} инструментов (параллельно)";
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
    /// Проверяет, является ли бар полностью закрытым
    /// </summary>
    /// <param name="bar">Бар для проверки</param>
    /// <param name="timeframeMinutes">Таймфрейм в минутах</param>
    /// <param name="currentTime">Текущее время</param>
    /// <returns>True если бар закрыт, иначе False</returns>
    private bool IsBarClosed(Bar bar, int timeframeMinutes, DateTime currentTime)
    {
        // Время начала следующего бара = время текущего бара + таймфрейм
        var nextBarStartTime = bar.Time.AddMinutes(timeframeMinutes);

        // Если время начала следующего бара больше чем текущее время + 5 минут,
        // значит текущий бар ещё не закрыт
        return nextBarStartTime <= currentTime.AddMinutes(5);
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
                // Используем ключи динамических цветов
                var pairColorKey = (toolIndex % 2 == 0) ? "RowEvenColor" : "RowOddColor";

                // W5e
                var w5eText = result.W5e?.BarNumber.ToString() ?? "";
                var w5eColor = GetWprTextColor(result.W5e);
                var ud5Color = GetUdColor(result.UD5);
                var ud5Display = GetUdDisplay(result.UD5);

                displayRows.Add(new DisplayRow
                {
                    Name = result.Name,
                    Period = result.Period,
                    C5 = result.C5,
                    F2 = result.F2,
                    WprDisplay = w5eText,
                    WprTextColor = w5eColor,
                    UdBackgroundColor = ud5Color,
                    UdDisplay = ud5Display,
                    IsFirstRow = true,
                    IsSecondRow = false,
                    PairColorKey = pairColorKey,
                    Ws5Signal = result.Ws5Signal
                });

                // W21e
                var w21eText = result.W21e?.BarNumber.ToString() ?? "";
                var w21eColor = GetWprTextColor(result.W21e);
                var ud21Color = GetUdColor(result.UD21);
                var ud21Display = GetUdDisplay(result.UD21);

                displayRows.Add(new DisplayRow
                {
                    Name = null,
                    Period = null,
                    C5 = null,
                    F2 = null,
                    WprDisplay = w21eText,
                    WprTextColor = w21eColor,
                    UdBackgroundColor = ud21Color,
                    UdDisplay = ud21Display,
                    IsFirstRow = false,
                    IsSecondRow = true,
                    PairColorKey = pairColorKey,
                    Ws21Signal = result.Ws21Signal
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

    private Color? GetWprTextColor(WprSignal? signal)
    {
        if (signal == null) return Colors.Gray;

        return signal.SignalType switch
        {
            WprSignalType.AboveMinus20 => Color.FromArgb("#FFB74D"),   // светло-оранжевый
            WprSignalType.StrongAboveMinus5 => Color.FromArgb("#FF5252"), // ярко-красный
            WprSignalType.BelowMinus80 => Color.FromArgb("#81C784"),   // светло-зелёный
            WprSignalType.StrongBelowMinus95 => Color.FromArgb("#4CAF50"), // зелёный
            _ => Colors.Gray
        };
    }

    private Color? GetUdColor(UdSignal? signal)
    {
        if (signal == null) return null;

        return signal.SignalType switch
        {
            SignalType.Bullish => Color.FromArgb("#CCFFCC"),
            SignalType.Bearish => Color.FromArgb("#FFCCCC"),
            _ => null
        };
    }

    private string GetUdDisplay(UdSignal? signal)
    {
        if (signal == null) return "";

        return signal.SignalType switch
        {
            SignalType.Bullish => "▲",
            SignalType.Bearish => "▼",
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