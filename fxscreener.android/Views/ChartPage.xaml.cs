using fxscreener.android.ViewModels;

namespace fxscreener.android.Views;

public partial class ChartPage : ContentPage
{
    private readonly ChartViewModel _viewModel;

    public ChartPage(ChartViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }
}