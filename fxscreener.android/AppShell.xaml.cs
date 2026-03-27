using fxscreener.android.Views;

namespace fxscreener.android;

public partial class AppShell : Shell
{
    private static AppShell? _current;
    public static AppShell? Current => _current;

    public AppShell()
    {
        InitializeComponent();
        _current = this;

        FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.SetNavBarIsVisible(this, false);

        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("instruments", typeof(InstrumentsPage));
        Routing.RegisterRoute("scanner", typeof(ScannerPage));
        Routing.RegisterRoute("chart", typeof(ChartPage));
    }

    public async Task SafeGoToAsync(string route)
    {
        try
        {
            await Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            // Повторная попытка через 100мс
            await Task.Delay(100);
            await Current.GoToAsync(route);
        }
    }
}