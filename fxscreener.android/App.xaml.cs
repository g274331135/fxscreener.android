using fxscreener.android.Views;

namespace fxscreener.android;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;

        // Подписываемся на события жизненного цикла
        Current!.Sleep += OnSleep;
        Current!.Resume += OnResume;
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

    private void OnSleep(object? sender, AppLifecycleState e)
    {
        // Приложение свернули - можно освободить некоторые ресурсы
        System.Diagnostics.Debug.WriteLine("App sleeping");
    }

    private void OnResume(object? sender, AppLifecycleState e)
    {
        // Приложение развернули - проверяем соединение
        System.Diagnostics.Debug.WriteLine("App resuming");

        // Здесь можно вызвать проверку подключения через сервис
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var apiService = _serviceProvider.GetService<IMt5ApiService>();
            if (apiService != null && apiService.IsConnected)
            {
                await apiService.CheckConnectAsync();
            }
        });
    }

    protected override void OnStart()
    {
        // При запуске - загружаем настройки
        System.Diagnostics.Debug.WriteLine("App starting");
    }

    protected override void OnStop()
    {
        // При полном закрытии - отключаемся от API
        System.Diagnostics.Debug.WriteLine("App stopping");

        var apiService = _serviceProvider.GetService<IMt5ApiService>();
        if (apiService != null)
        {
            Task.Run(async () => await apiService.DisconnectAsync());
        }
    }
}