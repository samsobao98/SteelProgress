using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class HistoryPage : ContentPage
{
    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
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
}
