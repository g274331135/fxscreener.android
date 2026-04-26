using fxscreener.android.Services;
using fxscreener.android.Views;

namespace fxscreener.android;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMt5ApiService _apiService;
    private AppShell? _shell;
    private bool _isInitialized = false;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _apiService = serviceProvider.GetRequiredService<IMt5ApiService>();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        if (_shell == null)
        {
            _shell = _serviceProvider.GetRequiredService<AppShell>();
        }

        var window = new Window(_shell);
        window.Activated += OnWindowActivated;

        if (!_isInitialized)
        {
            _isInitialized = true;
            Task.Run(async () => await InitializeAppAsync());
        }

        return window;
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("Window activated");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (_shell?.CurrentPage != null)
                {
                    // Восстанавливаем BindingContext
                    var page = _shell.CurrentPage;
                    var context = page.BindingContext;
                    page.BindingContext = null;
                    page.BindingContext = context;

                    // Принудительная перерисовка
                    page.ForceLayout();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WindowActivated error: {ex.Message}");
            }
        });
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
            try
            {
                // Обновляем текущую страницу
                if (_shell?.CurrentPage != null)
                {
                    var page = _shell.CurrentPage;
                    var context = page.BindingContext;
                    page.BindingContext = null;
                    page.BindingContext = context;
                    page.ForceLayout();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Resume error: {ex.Message}");
            }
        });
    }
}