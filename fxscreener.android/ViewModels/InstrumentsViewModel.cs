using System.Collections.ObjectModel;
using System.Windows.Input;
using fxscreener.android.Models;
using fxscreener.android.Services;
using fxscreener.android.Views;

namespace fxscreener.android.ViewModels;

public class InstrumentsViewModel : BindableObject
{
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
        ToggleActiveCommand = new Command<InstrumentParams>(async (instrument) => await ToggleActiveAsync(instrument));
        RefreshFromApiCommand = new Command(async () => await RefreshSelectedFromApiAsync());
        BackCommand = new Command(async () => await GoBackAsync());

        // Загружаем при старте
        Task.Run(LoadInstrumentsAsync);
    }

    #region Загрузка

    private async Task LoadInstrumentsAsync()
    {
        IsLoading = true;
        StatusMessage = "Загрузка списка инструментов...";

        try
        {
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

        NewSymbol = symbol.ToUpper();
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
            newInstrument.IsActive = true;

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

        _storage.Remove(instrument);
        await _storage.SaveAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Instruments.Remove(instrument);
        });

        StatusMessage = $"✅ {instrument.DisplayName} удалён";
        StatusColor = Colors.Green;
    }

    #endregion

    #region Активация/деактивация

    private async Task ToggleActiveAsync(InstrumentParams instrument)
    {
        instrument.IsActive = !instrument.IsActive;
        _storage.AddOrUpdate(instrument);
        await _storage.SaveAsync();

        // Обновляем отображение
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var index = Instruments.IndexOf(instrument);
            if (index >= 0)
            {
                Instruments.RemoveAt(index);
                Instruments.Insert(index, instrument);
            }
        });

        StatusMessage = $"{instrument.Symbol} {(instrument.IsActive ? "активирован" : "деактивирован")}";
        StatusColor = Colors.Gray;
    }

    #endregion

    #region Обновление из API

    private async Task RefreshSelectedFromApiAsync()
    {
        var selected = Instruments.Where(i => i.IsActive).ToList();
        if (!selected.Any())
        {
            StatusMessage = "Нет активных инструментов для обновления";
            return;
        }

        IsLoading = true;
        StatusMessage = "Обновление параметров из API...";

        try
        {
            foreach (var instrument in selected)
            {
                var freshParams = await _apiService.GetSymbolParamsAsync(instrument.Symbol);
                if (freshParams != null)
                {
                    // Обновляем поля, но сохраняем период и активность
                    instrument.TickSize = freshParams.tickSize;
                    instrument.TickValue = freshParams.tickValue;
                    instrument.Digits = freshParams.digits;
                    instrument.Point = freshParams.point;
                    instrument.Spread = freshParams.spread;
                    instrument.ContractSize = freshParams.contractSize;
                    instrument.SwapLong = freshParams.swapLong;
                    instrument.SwapShort = freshParams.swapShort;
                    instrument.Swap3Day = freshParams.swap3Day;
                    instrument.LastUpdated = DateTime.UtcNow;

                    _storage.AddOrUpdate(instrument);
                }
            }

            await _storage.SaveAsync();
            StatusMessage = $"✅ Обновлено {selected.Count} инструментов";
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