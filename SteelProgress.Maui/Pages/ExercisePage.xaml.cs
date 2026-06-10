using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class ExercisePage : ContentPage
{
    private readonly ExerciseViewModel _vm;

    public ExercisePage(ExerciseViewModel vm)
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
