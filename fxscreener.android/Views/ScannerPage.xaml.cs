using fxscreener.android.Models;
using fxscreener.android.ViewModels;
using fxscreener.android.Views;
using Microsoft.Extensions.DependencyInjection;

namespace fxscreener.android.Views;

public partial class ScannerPage : ContentPage
{
    private readonly ScannerViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;

    public ScannerPage(ScannerViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
    }

    private async void OnMenuButtonClicked(object sender, EventArgs e)
    {
        var action = await DisplayActionSheet(
            "Меню",
            "Отмена",
            null,
            "Настройки подключения",
            "Управление инструментами");

        if (action == "Настройки подключения")
        {
            await Shell.Current.GoToAsync("settings");
        }
        else if (action == "Управление инструментами")
        {
            await Shell.Current.GoToAsync("instruments");
        }
    }

    // Обработчик двойного касания
    private async void OnInstrumentTapped(object sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is DisplayRow row && row.IsFirstRow)
        {
            var instrument = _viewModel.GetInstrumentByName(row.Name, row.Period);
            if (instrument == null) return;

            var chartData = _viewModel.GetChartData(row.Name, row.Period);
            if (chartData == null) return;

            var (bars, wpr5, wpr21) = chartData.Value;

            var chartVM = _serviceProvider.GetRequiredService<ChartViewModel>();
            await chartVM.LoadData(instrument.Symbol, instrument.Period, bars, wpr5, wpr21);
            await Shell.Current.GoToAsync("chart");
        }
    }
}