using fxscreener.android.Services;
using fxscreener.android.ViewModels;
using fxscreener.android.Views;
using Microsoft.Extensions.Logging;

namespace fxscreener.android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

        // Сервисы
        builder.Services.AddSingleton<IMt5ApiService, Mt5ApiService>();
        builder.Services.AddSingleton<IIndicatorCalculator, IndicatorCalculator>();
        builder.Services.AddSingleton<ITimeAggregationService, TimeAggregationService>();

        // ViewModels
        builder.Services.AddTransient<ScannerViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<InstrumentsViewModel>();

        // Views
        builder.Services.AddTransient<ScannerPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<InstrumentsPage>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}