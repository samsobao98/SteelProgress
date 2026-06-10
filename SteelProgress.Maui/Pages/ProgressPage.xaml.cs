using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

[QueryProperty(nameof(ExerciseId), "exerciseId")]
public partial class ProgressPage : ContentPage
{
    private readonly ProgressViewModel _vm;
    private int _exerciseId;

    public int ExerciseId
    {
        get => _exerciseId;
        set => _exerciseId = value;
    }

    public ProgressPage(ProgressViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();
        if (_exerciseId > 0)
            _vm.SelectExerciseById(_exerciseId);
    }
}
