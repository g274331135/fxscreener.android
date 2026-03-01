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
        // Определяем, какую страницу показать при запуске
        ContentPage startPage;

        // Синхронно проверяем настройки (или асинхронно с ожиданием)
        var settings = Task.Run(async () => await ApiSettings.LoadAsync()).GetAwaiter().GetResult();

        if (settings == null || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            // Настроек нет - показываем страницу настроек
            startPage = _serviceProvider.GetRequiredService<SettingsPage>();
        }
        else
        {
            // Настройки есть - показываем главный сканер
            startPage = _serviceProvider.GetRequiredService<ScannerPage>();
        }

        // Создаём Window с обычной страницей (без NavigationPage)
        return new Window(startPage);
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