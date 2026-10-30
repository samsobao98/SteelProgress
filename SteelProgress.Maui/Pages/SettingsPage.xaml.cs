using SteelProgress.Maui.Services;
using System.ComponentModel;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class SettingsPage : ContentPage
{
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnViewModelChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_coveredByPopup) { _coveredByPopup = false; return; } // vuelve de una ventana propia, no de otra pantalla
        await _vm.LoadAsync();
        UpdateModeSelector();

        PageContent.Opacity = 0;
        PageContent.TranslationY = 14;
        await Task.WhenAll(
            PageContent.FadeToAsync(1, 260, Easing.CubicOut),
            PageContent.TranslateToAsync(0, 0, 260, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (AppDialogs.IsShowing) _coveredByPopup = true; // solo se abrió una ventana encima
    }


    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.IsDetailed) or nameof(SettingsViewModel.IsCoach))
            UpdateModeSelector();
    }

    // El seleccionado lleva el degradado acero; el otro queda transparente
    private void UpdateModeSelector()
    {
        Paint(PersonalOption, !_vm.IsCoach);
        Paint(CoachOption, _vm.IsCoach);
        Paint(SimpleOption, !_vm.IsDetailed);
        Paint(DetailedOption, _vm.IsDetailed);
    }

    private static void Paint(Border option, bool selected)
    {
        option.Background = selected ? Resource<Brush>("SpSteelBrush") : Brush.Transparent;
        if (option.Content is Label label)
            label.TextColor = Resource<Color>(selected ? "SpOnAccent" : "SpText2");
    }

    // Busca también en los diccionarios combinados (Tokens.xaml)
    private static T Resource<T>(string key) =>
        Application.Current!.Resources.TryGetValue(key, out var value) ? (T)value : throw new KeyNotFoundException(key);

    // Respuesta al tocar: se encoge un poco y rebota
    private async void OnOptionTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not VisualElement element) return;
        await element.ScaleToAsync(0.96, 70, Easing.CubicIn);
        await element.ScaleToAsync(1.0, 120, Easing.SpringOut);
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
