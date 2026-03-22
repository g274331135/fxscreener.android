using System.Collections.ObjectModel;
using System.Windows.Input;
using fxscreener.android.Models;
using fxscreener.android.Services;
using fxscreener.android.Views;

namespace fxscreener.android.ViewModels;

public class InstrumentsViewModel : BindableObject
{
    private string _currentOperationId = string.Empty;
    private readonly IMt5ApiService _apiService;
    private readonly IServiceProvider _serviceProvider;
    private InstrumentsStorage _storage;

    #region Свойства

    private ObservableCollection<InstrumentParams> _instruments = new();
    public ObservableCollection<InstrumentParams> Instruments
    {
        get => _instruments;
        set
        {
            _instruments = value;
            OnPropertyChanged();
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotLoading));
        }
    }

    public bool IsNotLoading => !IsLoading;

    private string _statusMessage = "Готов";
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    private Color _statusColor = Colors.Gray;
    public Color StatusColor
    {
        get => _statusColor;
        set
        {
            _statusColor = value;
            OnPropertyChanged();
        }
    }

    // Для диалога добавления
    private string _newSymbol = string.Empty;
    public string NewSymbol
    {
        get => _newSymbol;
        set
        {
            _newSymbol = value;
            OnPropertyChanged();
        }
    }

    private string _selectedPeriod = "H1";
    public string SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            _selectedPeriod = value;
            OnPropertyChanged();
        }
    }

    // Список доступных периодов
    public List<string> AvailablePeriods { get; } = new()
    {
        "M1", "M5", "M15", "M30", "H1", "H4", "H6", "D1", "W1", "MN1"
    };

    #endregion

    #region Команды

    public ICommand LoadInstrumentsCommand { get; }
    public ICommand AddInstrumentCommand { get; }
    public ICommand DeleteInstrumentCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public ICommand RefreshFromApiCommand { get; }
    public ICommand BackCommand { get; }

    #endregion

    public InstrumentsViewModel(IMt5ApiService apiService, IServiceProvider serviceProvider)
    {
        _apiService = apiService;
        _serviceProvider = serviceProvider;

        LoadInstrumentsCommand = new Command(async () => await LoadInstrumentsAsync());
        AddInstrumentCommand = new Command(async () => await ShowAddDialogAsync());
        DeleteInstrumentCommand = new Command<InstrumentParams>(async (instrument) => await DeleteInstrumentAsync(instrument));
        RefreshFromApiCommand = new Command(async () => await RefreshAllFromApiAsync());
        BackCommand = new Command(async () => await GoBackAsync());

        // Загружаем при старте
        Task.Run(LoadInstrumentsAsync);
    }

    public async Task OnAppearing()
    {
        await LoadInstrumentsAsync();

        // Проверяем подключение к API
        if (!_apiService.IsConnected)
        {
            var settings = await ApiSettings.LoadAsync();
            if (settings != null)
            {
                await _apiService.ConnectAsync(settings);
            }
        }
    }

    #region Загрузка

    private async Task LoadInstrumentsAsync()
    {
        IsLoading = true;
        StatusMessage = "Загрузка списка инструментов...";

        try
        {
            // Загружаем настройки API для OperationId
            var settings = await ApiSettings.LoadAsync();

            _storage = await InstrumentsStorage.LoadAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Instruments.Clear();
                foreach (var instrument in _storage.GetAllInstruments().OrderBy(i => i.Symbol))
                {
                    Instruments.Add(instrument);
                }

                StatusMessage = $"Загружено {Instruments.Count} инструментов";
                StatusColor = Colors.Green;
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            StatusColor = Colors.Red;
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Добавление инструмента

    private async Task ShowAddDialogAsync()
    {
        // Простой диалог ввода
        var symbol = await Application.Current?.MainPage?.DisplayPromptAsync(
            "Новый инструмент",
            "Введите тикер (например, EURUSD):",
            "Далее",
            "Отмена")!;

        if (string.IsNullOrWhiteSpace(symbol))
            return;

        // Выбор периода
        var period = await Application.Current?.MainPage?.DisplayActionSheet(
            "Выберите период",
            "Отмена",
            null,
            AvailablePeriods.ToArray())!;

        if (period == "Отмена" || period == null)
            return;

        NewSymbol = symbol;
        SelectedPeriod = period;

        await AddInstrumentAsync();
    }

    private async Task AddInstrumentAsync()
    {
        IsLoading = true;
        StatusMessage = $"Проверка {NewSymbol}...";

        try
        {
            // Проверяем через API
            var symbolParams = await _apiService.GetSymbolParamsAsync(NewSymbol);

            if (symbolParams == null)
            {
                StatusMessage = $"❌ Инструмент {NewSymbol} не найден";
                StatusColor = Colors.Red;
                return;
            }

            // Создаём новый инструмент
            var newInstrument = InstrumentParams.FromSymbolParams(symbolParams, SelectedPeriod);

            // Сохраняем
            _storage.AddOrUpdate(newInstrument);
            await _storage.SaveAsync();

            // Добавляем в список
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Instruments.Add(newInstrument);
                // Сортируем
                var sorted = Instruments.OrderBy(i => i.Symbol).ToList();
                Instruments.Clear();
                foreach (var item in sorted)
                    Instruments.Add(item);
            });

            StatusMessage = $"✅ {newInstrument.Symbol} добавлен";
            StatusColor = Colors.Green;
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Ошибка: {ex.Message}";
            StatusColor = Colors.Red;
        }
        finally
        {
            IsLoading = false;
            NewSymbol = string.Empty;
        }
    }

    #endregion

    #region Удаление

    private async Task DeleteInstrumentAsync(InstrumentParams instrument)
    {
        var confirm = await Application.Current?.MainPage?.DisplayAlert(
            "Подтверждение",
            $"Удалить {instrument.DisplayName}?",
            "Да",
            "Нет")!;

        if (!confirm) return;

        try
        {
            _storage.Remove(instrument);
            await _storage.SaveAsync();

            // Безопасное удаление из ObservableCollection
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Находим инструмент в коллекции
                var itemToRemove = Instruments.FirstOrDefault(i => i.Key == instrument.Key);
                if (itemToRemove != null)
                {
                    Instruments.Remove(itemToRemove);
                }

                StatusMessage = $"✅ {instrument.DisplayName} удалён";
                StatusColor = Colors.Green;
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Ошибка: {ex.Message}";
            StatusColor = Colors.Red;
        }
    }

    #endregion

    #region Обновление из API

    private async Task RefreshAllFromApiAsync()
    {
        if (!Instruments.Any())
        {
            StatusMessage = "Нет инструментов для обновления";
            StatusColor = Colors.Orange;
            return;
        }

        IsLoading = true;
        StatusMessage = "Проверка подключения к API...";

        try
        {
            // Проверяем подключение
            if (!_apiService.IsConnected)
            {
                var settings = await ApiSettings.LoadAsync();
                if (settings == null)
                {
                    StatusMessage = "❌ Нет настроек API";
                    StatusColor = Colors.Red;
                    return;
                }

                var connected = await _apiService.ConnectAsync(settings);
                if (!connected)
                {
                    StatusMessage = "❌ Не удалось подключиться к API";
                    StatusColor = Colors.Red;
                    return;
                }
            }

            StatusMessage = "Обновление параметров из API...";

            foreach (var instrument in Instruments.ToList())
            {
                var freshParams = await _apiService.GetSymbolParamsAsync(instrument.Symbol);
                if (freshParams != null)
                {
                    instrument.TickSize = freshParams.SymbolInfo.TickSize;
                    instrument.TickValue = freshParams.SymbolInfo.TickValue;
                    instrument.Digits = freshParams.SymbolInfo.Digits;
                    instrument.Point = freshParams.SymbolInfo.Point;
                    instrument.Spread = freshParams.SymbolInfo.Spread;
                    instrument.ContractSize = freshParams.SymbolInfo.ContractSize;
                    instrument.SwapLong = freshParams.SymbolGroup.SwapLong;
                    instrument.SwapShort = freshParams.SymbolGroup.SwapShort;
                    instrument.ThreeDaysSwap = freshParams.SymbolGroup.ThreeDaysSwap;
                    instrument.LastUpdated = DateTime.UtcNow;

                    _storage.AddOrUpdate(instrument);
                }

                // Небольшая задержка, чтобы не забивать API
                await Task.Delay(100);
            }

            await _storage.SaveAsync();

            // Обновляем список
            await LoadInstrumentsAsync();

            StatusMessage = $"✅ Обновлено {Instruments.Count} инструментов";
            StatusColor = Colors.Green;
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Ошибка: {ex.Message}";
            StatusColor = Colors.Red;
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Навигация

    private async Task GoBackAsync()
    {
        await Application.Current?.MainPage?.Navigation.PopModalAsync()!;
    }

    #endregion
}

public static class CommandExtensions
{
    public static async Task ExecuteAsync(this ICommand command, object parameter = null)
    {
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
        await Task.CompletedTask;
    }
}