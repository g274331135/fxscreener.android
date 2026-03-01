using fxscreener.android.Models;
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

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private void OnSwitchToggled(object sender, ToggledEventArgs e)
    {
        if (sender is Switch switchControl && switchControl.BindingContext is InstrumentParams instrument)
        {
            _viewModel?.ToggleActiveCommand?.Execute(instrument);
        }
    }
}