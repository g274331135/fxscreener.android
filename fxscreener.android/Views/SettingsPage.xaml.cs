using fxscreener.android.Models;
using fxscreener.android.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace fxscreener.android.Views;

public partial class SettingsPage : ContentPage
{
    private readonly IMt5ApiService _apiService;

    public SettingsPage(IMt5ApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;

        // Загружаем сохранённые настройки при открытии
        LoadSettingsAsync();

        // Подписываемся на события
        TestConnectionButton.Clicked += OnTestConnectionClicked;
        SaveSettingsButton.Clicked += OnSaveSettingsClicked;
    }

    private async void LoadSettingsAsync()
    {
        try
        {
            var settings = await ApiSettings.LoadAsync();
            if (settings != null)
            {
                LoginEntry.Text = settings.Login;
                PasswordEntry.Text = settings.Password; // Зашифрован, но ок
                HostEntry.Text = settings.Host;
                PortEntry.Text = settings.Port.ToString();
                ApiKeyEntry.Text = settings.ApiKey;
                OperationIdEntry.Text = settings.OperationId;
                UtcOffsetEntry.Text = settings.UtcOffset.ToString();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось загрузить настройки: {ex.Message}", "OK");
        }
    }

    private async void OnTestConnectionClicked(object? sender, EventArgs e)
    {
        if (!ValidateInputs())
            return;

        TestConnectionButton.IsEnabled = false;
        TestConnectionButton.Text = "Проверка...";
        StatusLabel.Text = "Подключение...";

        try
        {
            var settings = CreateSettingsFromInputs();

            // Пробуем подключиться
            var success = await _apiService.ConnectAsync(settings);

            if (success)
            {
                StatusLabel.TextColor = Colors.Green;
                StatusLabel.Text = "? Подключение успешно!";

                // Отключаемся, чтобы не занимать сессию
                await _apiService.DisconnectAsync();
            }
            else
            {
                StatusLabel.TextColor = Colors.Red;
                StatusLabel.Text = "? Ошибка подключения";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.TextColor = Colors.Red;
            StatusLabel.Text = $"? {ex.Message}";
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
            TestConnectionButton.Text = "Проверить подключение";
        }
    }

    private async void OnSaveSettingsClicked(object? sender, EventArgs e)
    {
        if (!ValidateInputs())
            return;

        try
        {
            var settings = CreateSettingsFromInputs();
            await settings.SaveAsync();

            StatusLabel.TextColor = Colors.Green;
            StatusLabel.Text = "? Настройки сохранены!";

            // Можно вернуться на главный экран
            await Task.Delay(1000);
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            StatusLabel.TextColor = Colors.Red;
            StatusLabel.Text = $"? Ошибка сохранения: {ex.Message}";
        }
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(LoginEntry.Text) ||
            string.IsNullOrWhiteSpace(PasswordEntry.Text) ||
            string.IsNullOrWhiteSpace(HostEntry.Text) ||
            string.IsNullOrWhiteSpace(PortEntry.Text) ||
            string.IsNullOrWhiteSpace(ApiKeyEntry.Text) ||
            string.IsNullOrWhiteSpace(OperationIdEntry.Text) ||
            string.IsNullOrWhiteSpace(UtcOffsetEntry.Text))
        {
            StatusLabel.TextColor = Colors.Red;
            StatusLabel.Text = "? Заполните все поля";
            return false;
        }

        return true;
    }

    private ApiSettings CreateSettingsFromInputs()
    {
        return new ApiSettings
        {
            Login = LoginEntry.Text?.Trim() ?? string.Empty,
            Password = PasswordEntry.Text?.Trim() ?? string.Empty,
            Host = HostEntry.Text?.Trim() ?? "mt5full2.mtapi.io",
            Port = int.TryParse(PortEntry.Text, out var port) ? port : 443,
            ApiKey = ApiKeyEntry.Text?.Trim() ?? string.Empty,
            OperationId = OperationIdEntry.Text?.Trim() ?? string.Empty,
            UtcOffset = int.TryParse(UtcOffsetEntry.Text, out var offset) ? offset : 3
        };
    }
}