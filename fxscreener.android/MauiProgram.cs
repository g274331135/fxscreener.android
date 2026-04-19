using fxscreener.android.Models;
using fxscreener.android.Services;
using fxscreener.android.ViewModels;
using fxscreener.android.Views;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace fxscreener.android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });

        builder.Services.AddSingleton<INavigation>(sp =>
        {
            return Application.Current?.MainPage?.Navigation ?? throw new InvalidOperationException("Navigation not available");
        });

        // Сервисы
        builder.Services.AddSingleton<IMt5ApiService, Mt5ApiService>();
        builder.Services.AddSingleton<IIndicatorCalculator, IndicatorCalculator>();
        builder.Services.AddSingleton<ITimeAggregationService, TimeAggregationService>();
        builder.Services.AddSingleton<IParallelLoaderService, ParallelLoaderService>();

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