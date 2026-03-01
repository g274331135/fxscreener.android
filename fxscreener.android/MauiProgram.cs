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
            });

        // Регистрация сервисов (Singleton - один экземпляр на всё приложение)
        builder.Services.AddSingleton<IMt5ApiService, Mt5ApiService>();
        builder.Services.AddSingleton<IIndicatorCalculator, IndicatorCalculator>();
        builder.Services.AddSingleton<ITimeAggregationService, TimeAggregationService>();

        // Регистрация ViewModels
        builder.Services.AddTransient<ScannerViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        // Временно комментируем, пока не создали эти ViewModels
        //builder.Services.AddTransient<InstrumentsViewModel>();

        // Регистрация Views
        builder.Services.AddTransient<ScannerPage>();
        builder.Services.AddTransient<SettingsPage>();
        // Временно комментируем
        // builder.Services.AddTransient<InstrumentsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}