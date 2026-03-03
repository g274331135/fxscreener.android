using fxscreener.android.Models;
using fxscreener.android.Services;
using fxscreener.android.Views;

namespace fxscreener.android;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMt5ApiService _apiService;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _apiService = serviceProvider.GetRequiredService<IMt5ApiService>();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Создаём Shell
        var shell = _serviceProvider.GetRequiredService<AppShell>();

        // Асинхронно проверяем настройки и подключаемся
        Task.Run(async () => await InitializeAppAsync(shell));

        return new Window(shell);
    }

    private async Task InitializeAppAsync(AppShell shell)
    {
        try
        {
            // 1. Загружаем сохранённые настройки
            var settings = await ApiSettings.LoadAsync();

            // 2. Если настроек нет — сразу в настройки
            if (settings == null || string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    shell.GoToAsync("//settings");
                });
                return;
            }

            // 3. Пробуем подключиться
            var connected = await _apiService.ConnectAsync(settings);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (connected)
                {
                    // Всё ок — на главный экран
                    shell.GoToAsync("//scanner");
                }
                else
                {
                    // Не удалось подключиться — показываем настройки с сообщением
                    shell.GoToAsync("//settings");

                    // Показываем предупреждение через небольшой таймаут
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Task.Delay(500);
                        await shell.CurrentPage?.DisplayAlert(
                            "Ошибка подключения",
                            "Не удалось подключиться к API. Проверьте настройки.",
                            "OK")!;
                    });
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Init error: {ex.Message}");

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                shell.GoToAsync("//settings");
            });
        }
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

        // При возвращении проверяем соединение
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_apiService.IsConnected)
            {
                await _apiService.CheckConnectAsync();
            }
        });
    }
}