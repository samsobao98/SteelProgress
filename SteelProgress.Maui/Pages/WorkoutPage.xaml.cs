using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class WorkoutPage : ContentPage
{
    private readonly WorkoutViewModel _vm;

    public WorkoutPage(WorkoutViewModel vm)
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
