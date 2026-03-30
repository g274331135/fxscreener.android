using fxscreener.android.ViewModels;

namespace fxscreener.android.Views;

public partial class InstrumentsPage : ContentPage
{
    private readonly AppShell _shell;
    private readonly InstrumentsViewModel _viewModel;

    public InstrumentsPage(InstrumentsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _shell = AppShell.Current ?? throw new InvalidOperationException("AppShell not available");
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await _shell.SafeGoToAsync(".."); // или "//scanner"
    }
}