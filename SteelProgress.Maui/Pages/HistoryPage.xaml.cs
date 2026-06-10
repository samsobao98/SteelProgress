using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _vm;

    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();
    }

    private void ShowSection(int section)
    {
        SectionSesiones.IsVisible = section == 0;
        SectionDetalle.IsVisible  = section == 1;

        var active       = Color.FromArgb("#89B4FA");
        var inactive     = Color.FromArgb("#45475A");
        var textActive   = Color.FromArgb("#1E1E2E");
        var textInactive = Color.FromArgb("#CDD6F4");

        BtnSesiones.BackgroundColor = section == 0 ? active : inactive;
        BtnSesiones.TextColor       = section == 0 ? textActive : textInactive;
        BtnDetalle.BackgroundColor  = section == 1 ? active : inactive;
        BtnDetalle.TextColor        = section == 1 ? textActive : textInactive;
    }

    private void OnSesionesClicked(object? sender, EventArgs e) => ShowSection(0);
    private void OnDetalleClicked(object? sender, EventArgs e)  => ShowSection(1);

    private async void OnVerProgresoClicked(object? sender, EventArgs e)
    {
        if (_vm.SelectedExerciseSummary is null)
            return;
        await Shell.Current.GoToAsync($"ProgressPage?exerciseId={_vm.SelectedExerciseSummary.ExerciseId}");
    }
}
