using SteelProgress.Maui.Services;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class HistoryPage : ContentPage
{
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

    private readonly HistoryViewModel _vm;

    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    private Core.Services.ClientContextService? _clientContext;

    // Al entrar o salir de un cliente, la pantalla vuelve a cargar sus datos
    private void OnClientChanged() => MainThread.BeginInvokeOnMainThread(async () => await _vm.RefreshAsync());

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (AppDialogs.IsShowing) { _coveredByPopup = true; return; } // solo se abrió una ventana encima
        if (_clientContext is not null) _clientContext.Changed -= OnClientChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_coveredByPopup) { _coveredByPopup = false; return; } // vuelve de una ventana propia, no de otra pantalla
        _clientContext ??= Handler?.MauiContext?.Services.GetService<Core.Services.ClientContextService>();
        if (_clientContext is not null)
        {
            _clientContext.Changed -= OnClientChanged;
            _clientContext.Changed += OnClientChanged;
        }
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
