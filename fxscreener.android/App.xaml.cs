using fxscreener.android.Services;
using fxscreener.android.Views;

namespace fxscreener.android;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMt5ApiService _apiService;
    private AppShell? _shell;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _apiService = serviceProvider.GetRequiredService<IMt5ApiService>();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _shell = _serviceProvider.GetRequiredService<AppShell>();

        Task.Run(async () => await InitializeAppAsync());

        return new Window(_shell);
    }

    private async Task InitializeAppAsync()
    {
        try
        {
            var settings = await Models.ApiSettings.LoadAsync();

            if (settings == null || string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                await MainThread.InvokeOnMainThreadAsync(() => _shell?.GoToAsync("//settings"));
                return;
            }

            var connected = await _apiService.ConnectAsync(settings);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (connected)
                    _shell?.GoToAsync("//scanner");
                else
                    _shell?.GoToAsync("//settings");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Init error: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(() => _shell?.GoToAsync("//settings"));
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

        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Обновляем текущую страницу
            _shell?.CurrentPage?.ForceLayout();
        });
    }

    // Метод для доступа к Shell из других классов
    public AppShell? GetShell() => _shell;
}