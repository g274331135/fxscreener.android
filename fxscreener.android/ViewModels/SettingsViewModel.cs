using System.Windows.Input;
using fxscreener.android.Models;
using fxscreener.android.Services;
using fxscreener.android.Views;

namespace fxscreener.android.ViewModels;

public class SettingsViewModel : BindableObject
{
    private readonly IMt5ApiService _apiService;
    private readonly IServiceProvider _serviceProvider;

    #region Свойства

    private string _login = string.Empty;
    public string Login
    {
        get => _login;
        set { _login = value; OnPropertyChanged(); }
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set { _password = value; OnPropertyChanged(); }
    }

    private string _host = "mt5full2.mtapi.io";
    public string Host
    {
        get => _host;
        set { _host = value; OnPropertyChanged(); }
    }

    private string _port = "443";
    public string Port
    {
        get => _port;
        set { _port = value; OnPropertyChanged(); }
    }

    private string _apiKey = string.Empty;
    public string ApiKey
    {
        get => _apiKey;
        set { _apiKey = value; OnPropertyChanged(); }
    }

    private string _operationId = string.Empty;
    public string OperationId
    {
        get => _operationId;
        set { _operationId = value; OnPropertyChanged(); }
    }

    private string _utcOffset = "3";
    public string UtcOffset
    {
        get => _utcOffset;
        set { _utcOffset = value; OnPropertyChanged(); }
    }

    private string _statusMessage = "Введите данные для подключения";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    private Color _statusColor = Colors.Gray;
    public Color StatusColor
    {
        get => _statusColor;
        set { _statusColor = value; OnPropertyChanged(); }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotBusy)); }
    }

    public bool IsNotBusy => !IsBusy;

    #endregion

    #region Команды

    public ICommand TestConnectionCommand { get; }
    public ICommand SaveAndContinueCommand { get; }

    #endregion

    public SettingsViewModel(IMt5ApiService apiService, IServiceProvider serviceProvider)
    {
        _apiService = apiService;
        _serviceProvider = serviceProvider;

        TestConnectionCommand = new Command(async () => await TestConnectionAsync());
        SaveAndContinueCommand = new Command(async () => await SaveAndContinueAsync());

        // Загружаем сохранённые настройки при открытии
        Task.Run(LoadSavedSettingsAsync);
    }

    private async Task LoadSavedSettingsAsync()
    {
        try
        {
            var settings = await ApiSettings.LoadAsync();
            if (settings != null)
            {
                Login = settings.Login;
                Password = settings.Password;
                Host = settings.Host;
                Port = settings.Port.ToString();
                ApiKey = settings.ApiKey;
                OperationId = settings.OperationId;
                UtcOffset = settings.UtcOffset.ToString();

                StatusMessage = "Настройки загружены";
                StatusColor = Colors.Green;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            StatusColor = Colors.Red;
        }
    }

    private async Task TestConnectionAsync()
    {
        if (!ValidateInputs()) return;

        IsBusy = true;
        StatusMessage = "Подключение...";
        StatusColor = Colors.Gray;

        try
        {
            var settings = CreateSettingsFromInputs();

            var success = await _apiService.ConnectAsync(settings);

            if (success)
            {
                StatusMessage = "✅ Подключение успешно!";
                StatusColor = Colors.Green;

                // Отключаемся, чтобы не занимать сессию
                await _apiService.DisconnectAsync();
            }
            else
            {
                StatusMessage = "❌ Ошибка подключения";
                StatusColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ {ex.Message}";
            StatusColor = Colors.Red;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAndContinueAsync()
    {
        if (!ValidateInputs()) return;

        IsBusy = true;
        StatusMessage = "Сохранение...";

        try
        {
            var settings = CreateSettingsFromInputs();
            await settings.SaveAsync();

            StatusMessage = "✅ Настройки сохранены!";
            StatusColor = Colors.Green;

            // Переходим на главный экран
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var scannerPage = _serviceProvider.GetRequiredService<ScannerPage>();
                await Application.Current?.MainPage?.Navigation.PushAsync(scannerPage)!;

                // Удаляем страницу настроек из стека, чтобы нельзя было вернуться назад
                var currentPage = Application.Current?.MainPage?.Navigation.NavigationStack.FirstOrDefault();
                if (currentPage is SettingsPage)
                {
                    Application.Current.MainPage.Navigation.RemovePage(currentPage);
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Ошибка сохранения: {ex.Message}";
            StatusColor = Colors.Red;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(Login) ||
            string.IsNullOrWhiteSpace(Password) ||
            string.IsNullOrWhiteSpace(Host) ||
            !int.TryParse(Port, out _) ||
            string.IsNullOrWhiteSpace(ApiKey) ||
            string.IsNullOrWhiteSpace(OperationId) ||
            !int.TryParse(UtcOffset, out _))
        {
            StatusMessage = "❌ Заполните все поля корректно";
            StatusColor = Colors.Red;
            return false;
        }

        return true;
    }

    private ApiSettings CreateSettingsFromInputs()
    {
        return new ApiSettings
        {
            Login = Login.Trim(),
            Password = Password,
            Host = Host.Trim(),
            Port = int.Parse(Port),
            ApiKey = ApiKey.Trim(),
            OperationId = OperationId.Trim(),
            UtcOffset = int.Parse(UtcOffset)
        };
    }
}