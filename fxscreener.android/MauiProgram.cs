using fxscreender.android.Services;
using fxscreener.android.Models;
using fxscreener.android.Services;
using fxscreener.android.ViewModels;
using fxscreener.android.Views;
using Microsoft.Extensions.Logging;
using DevExpress.Maui;

namespace fxscreener.android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseDevExpress()
            .UseDevExpressCharts()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

        builder.Services.AddSingleton<INavigation>(sp =>
        {
            return Shell.Current?.Navigation ?? throw new InvalidOperationException("Navigation not available");
        });

        // Сервисы
        builder.Services.AddSingleton<IMt5ApiService, Mt5ApiService>();
        builder.Services.AddSingleton<IIndicatorCalculator, IndicatorCalculator>();
        builder.Services.AddSingleton<ITimeAggregationService, TimeAggregationService>();
        builder.Services.AddSingleton<BuildSettings>(BuildSettings.LoadSynchronous());
        builder.Services.AddSingleton<IM1CacheService, M1CacheService>();
        builder.Services.AddSingleton<IBarBuilderService, BarBuilderService>();
        builder.Services.AddSingleton<IBuildingService, BuildingService>();

        // ViewModels
        builder.Services.AddSingleton<ChartViewModel>();
        builder.Services.AddTransient<ScannerViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<InstrumentsViewModel>();

        // Views
        builder.Services.AddTransient<ChartPage>();
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