using fxscreener.android.Models;
using fxscreener.android.Services;

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
        // Создаём Shell
        var shell = _serviceProvider.GetRequiredService<AppShell>();

        // Асинхронно проверяем настройки и устанавливаем начальную страницу
        Task.Run(async () =>
        {
            var settings = await ApiSettings.LoadAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (settings == null || string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    // Настроек нет - показываем страницу настроек
                    shell.GoToAsync("//settings");
                }
                else
                {
                    // Настройки есть - показываем главный сканер
                    shell.GoToAsync("//scanner");
                }
            });
        });

        return new Window(shell);
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