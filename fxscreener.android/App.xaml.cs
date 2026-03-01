using fxscreener.android.Models;
using fxscreener.android.Services;
using fxscreener.android.Views;

namespace fxscreener.android;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Создаём NavigationPage с правильными цветами
        var navigationPage = new NavigationPage();

        // Устанавливаем цвета для NavigationPage (не для Window)
        navigationPage.BarBackgroundColor = Color.FromArgb("#512BD4");
        navigationPage.BarTextColor = Colors.White;

        // Асинхронно проверяем настройки и устанавливаем главную страницу
        Task.Run(async () =>
        {
            var settings = await ApiSettings.LoadAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (settings == null || string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    // Настроек нет - показываем страницу настроек
                    var settingsPage = _serviceProvider.GetRequiredService<SettingsPage>();
                    navigationPage.PushAsync(settingsPage);
                }
                else
                {
                    // Настройки есть - показываем главный сканер
                    var scannerPage = _serviceProvider.GetRequiredService<ScannerPage>();
                    navigationPage.PushAsync(scannerPage);
                }
            });
        });

        // Window оборачивает NavigationPage
        return new Window(navigationPage);
    }

    protected override void OnStart()
    {
        System.Diagnostics.Debug.WriteLine("App starting");
    }

    protected override void OnSleep()
    {
        System.Diagnostics.Debug.WriteLine("App sleeping");
    }

    protected override void OnResume()
    {
        System.Diagnostics.Debug.WriteLine("App resuming");

        // При возвращении в приложение можно проверить соединение
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var apiService = _serviceProvider.GetService<IMt5ApiService>();
                if (apiService != null && apiService.IsConnected)
                {
                    await apiService.CheckConnectAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Resume check error: {ex.Message}");
            }
        });
    }
}