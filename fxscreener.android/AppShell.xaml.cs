using fxscreener.android.Views;
using Microsoft.Extensions.DependencyInjection;

namespace fxscreener.android;

public partial class AppShell : Shell
{
    private readonly IServiceProvider _serviceProvider;

    public AppShell(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;

        // Отключаем визуальные элементы Shell (шапка, меню)
        FlyoutBehavior = FlyoutBehavior.Disabled;
        Shell.SetNavBarIsVisible(this, false);

        // Регистрируем маршруты с передачей параметров (если понадобятся)
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("instruments", typeof(InstrumentsPage));
        Routing.RegisterRoute("scanner", typeof(ScannerPage));
    }

    // Вспомогательный метод для навигации
    public async Task GoToAsync(string route)
    {
        await Current.GoToAsync(route);
    }

    // Метод для возврата
    public async Task GoBackAsync()
    {
        await Current.GoToAsync("..");
    }
}