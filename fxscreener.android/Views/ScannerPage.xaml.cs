using fxscreener.android.ViewModels;

namespace fxscreener.android.Views;

public partial class ScannerPage : ContentPage
{
    private ScannerViewModel? _viewModel;

    public ScannerPage()
    {
        InitializeComponent();

        // Подписываемся на появление/исчезновение страницы
        Appearing += OnAppearing;
        Disappearing += OnDisappearing;
    }

    private void OnAppearing(object? sender, EventArgs e)
    {
        _viewModel = BindingContext as ScannerViewModel;
    }

    private void OnDisappearing(object? sender, EventArgs e)
    {
        // Очищаем ресурсы при уходе со страницы
        _viewModel?.Cleanup();
    }
}