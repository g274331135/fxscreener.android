using fxscreener.android.ViewModels;

namespace fxscreener.android.Views;

public partial class SettingsPage : ContentPage
{
    private readonly AppShell _shell;

    public SettingsPage(SettingsViewModel viewModel)
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