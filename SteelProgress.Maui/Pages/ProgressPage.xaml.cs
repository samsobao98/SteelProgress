using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class ProgressPage : ContentPage
{
    private readonly ProgressViewModel _vm;

    public ProgressPage(ProgressViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();
    }
}
