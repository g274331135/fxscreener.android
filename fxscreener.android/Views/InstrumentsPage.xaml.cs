using fxscreener.android.ViewModels;

namespace fxscreener.android.Views;

public partial class InstrumentsPage : ContentPage
{
    private readonly InstrumentsViewModel _viewModel;

    public InstrumentsPage(InstrumentsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearing();
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}