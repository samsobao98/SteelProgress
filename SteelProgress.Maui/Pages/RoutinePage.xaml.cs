using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class RoutinePage : ContentPage
{
    public RoutinePage(RoutineViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void ShowSection(int section)
    {
        SectionRutinas.IsVisible    = section == 0;
        SectionDias.IsVisible       = section == 1;
        SectionEjercicios.IsVisible = section == 2;

        FormRutinas.IsVisible    = section == 0;
        FormDias.IsVisible       = section == 1;
        FormEjercicios.IsVisible = section == 2;

        var active       = Color.FromArgb("#89B4FA");
        var inactive     = Color.FromArgb("#45475A");
        var textActive   = Color.FromArgb("#1E1E2E");
        var textInactive = Color.FromArgb("#CDD6F4");

        BtnRutinas.BackgroundColor    = section == 0 ? active : inactive;
        BtnRutinas.TextColor          = section == 0 ? textActive : textInactive;
        BtnDias.BackgroundColor       = section == 1 ? active : inactive;
        BtnDias.TextColor             = section == 1 ? textActive : textInactive;
        BtnEjercicios.BackgroundColor = section == 2 ? active : inactive;
        BtnEjercicios.TextColor       = section == 2 ? textActive : textInactive;
    }

    private void OnRutinasClicked(object? sender, EventArgs e)    => ShowSection(0);
    private void OnDiasClicked(object? sender, EventArgs e)       => ShowSection(1);
    private void OnEjerciciosClicked(object? sender, EventArgs e) => ShowSection(2);
}
