using SteelProgress.Maui.Services;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

[QueryProperty(nameof(ExerciseId), "exerciseId")]
public partial class ProgressPage : ContentPage
{
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

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
        if (_coveredByPopup) { _coveredByPopup = false; return; } // vuelve de una ventana propia, no de otra pantalla
        await _vm.RefreshAsync();
        if (_exerciseId > 0)
            _vm.SelectExerciseById(_exerciseId);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (AppDialogs.IsShowing) _coveredByPopup = true; // solo se abrió una ventana encima
    }

}
