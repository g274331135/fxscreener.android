using fxscreener.android.Models;
using fxscreener.android.ViewModels;
using fxscreener.android.Views;

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
            // Используем Shell для навигации
            await Shell.Current.GoToAsync("settings");
        }
        else if (action == "Управление инструментами")
        {
            await Shell.Current.GoToAsync("instruments");
        }
    }

    private async void OnInstrumentTapped(object sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is DisplayRow row && row.IsFirstRow)
        {
            // Найти инструмент по row.Name и row.Period
            var instrument = _viewModel.GetInstrumentByName(row.Name, row.Period);
            if (instrument != null)
            {
                // Получить бары и WPR значения (нужно будет добавить в ViewModel)
                var bars = _viewModel.GetBarsForInstrument(instrument);
                var wpr5 = _viewModel.GetWpr5ForInstrument(instrument);
                var wpr21 = _viewModel.GetWpr21ForInstrument(instrument);

                var chartVM = _serviceProvider.GetRequiredService<ChartViewModel>();
                await chartVM.LoadData(instrument.Symbol, instrument.Period, bars, wpr5, wpr21);
                await Shell.Current.GoToAsync("chart");
            }
        }
    }
}