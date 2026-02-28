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
        // Получаем главную страницу через DI
        var scannerPage = _serviceProvider.GetRequiredService<ScannerPage>();

        return new Window(new NavigationPage(scannerPage)
        {
            BarBackgroundColor = Color.FromArgb("#512BD4"),
            BarTextColor = Colors.White
        });
    }

    // В .NET MAUI жизненный цикл обрабатывается через Window, а не через Application
    // Поэтому эти методы не нужны или реализуются иначе

    // Если нужно обрабатывать свёртывание/разворачивание, делаем так:
    protected override void OnStart()
    {
        // Приложение запускается
        System.Diagnostics.Debug.WriteLine("App starting");

        // Проверяем подключение при старте
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var apiService = _serviceProvider.GetService<IMt5ApiService>();
                var settings = await Models.ApiSettings.LoadAsync();

                if (settings != null && apiService != null)
                {
                    await apiService.ConnectAsync(settings);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-connect error: {ex.Message}");
            }
        });
    }

    protected override void OnSleep()
    {
        // Приложение свёрнуто
        System.Diagnostics.Debug.WriteLine("App sleeping");
    }

    protected override void OnResume()
    {
        // Приложение развёрнуто
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

    // OnStop не нужен - используем OnSleep
}