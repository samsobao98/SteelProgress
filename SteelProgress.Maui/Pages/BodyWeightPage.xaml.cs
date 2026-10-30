using SteelProgress.Core.ViewModels;
using SteelProgress.Maui.Services;

namespace SteelProgress.Maui.Pages;

// Desde Progreso: GoToAsync("BodyWeightPage"), o con { ["add"] = true } para abrir directamente «Apuntar peso»
public partial class BodyWeightPage : ContentPage, IQueryAttributable
{
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

    private readonly BodyWeightViewModel _vm;
    private bool _startEntry;

    public BodyWeightPage(BodyWeightViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _startEntry = query.TryGetValue("add", out var add) && add is true;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_coveredByPopup) { _coveredByPopup = false; return; } // vuelve de una ventana propia, no de otra pantalla
        await _vm.LoadAsync();
        if (_startEntry)
        {
            _startEntry = false;
            _vm.StartEntryCommand.Execute(null);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (AppDialogs.IsShowing) _coveredByPopup = true; // solo se abrió una ventana encima
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_vm.IsEntering) return base.OnBackButtonPressed();
        _vm.CancelEntryCommand.Execute(null);
        return true;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
