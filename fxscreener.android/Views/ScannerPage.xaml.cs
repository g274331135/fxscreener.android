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
}