using SteelProgress.Maui.Services;
using System.ComponentModel;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class ClientsPage : ContentPage
{
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

    private readonly ClientsViewModel _vm;

    public ClientsPage(ClientsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnViewModelChanged;
        BuildColorRow();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_coveredByPopup) { _coveredByPopup = false; return; } // vuelve de una ventana propia, no de otra pantalla
        await _vm.RefreshAsync();

        ClientList.Opacity = 0;
        ClientList.TranslationY = 12;
        await Task.WhenAll(ClientList.FadeToAsync(1, 260, Easing.CubicOut), ClientList.TranslateToAsync(0, 0, 260, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (AppDialogs.IsShowing) _coveredByPopup = true; // solo se abrió una ventana encima
    }


    // Al tocar un cliente: qué hacer con él
    private async void OnClientTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ClientItem item) return;
        if (sender is VisualElement element)
        {
            await element.ScaleToAsync(0.97, 70, Easing.CubicIn);
            await element.ScaleToAsync(1.0, 120, Easing.SpringOut);
        }

        var name = item.Client.Name;
        string routines = $"Rutinas de {name}", train = $"Entrenar con {name}", history = $"Historial de {name}";
        const string edit = "Editar", delete = "Eliminar cliente";
        var choice = await AppDialogs.ChooseAsync(name, [train, routines, history, edit], destructive: delete);

        if (choice == edit) { _vm.StartEdit(item.Client); return; }
        if (choice == delete) { await _vm.DeleteAsync(item.Client); return; }

        var route = choice == train ? "//WorkoutPage" : choice == routines ? "//RoutinePage" : choice == history ? "//HistoryPage" : null;
        if (route is null) return;

        // Desde aquí, esas pantallas muestran y guardan los datos del cliente
        _vm.Open(item.Client);
        await Shell.Current.GoToAsync(route);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClientsViewModel.EditColor))
            UpdateColorSelection();
    }

    private void BuildColorRow()
    {
        foreach (var hex in _vm.AvailableColors)
        {
            var swatch = new Border
            {
                WidthRequest = 30,
                HeightRequest = 30,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 15 },
                BackgroundColor = Color.FromArgb(hex),
                StrokeThickness = 0,
                ClassId = hex
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => _vm.EditColor = hex;
            swatch.GestureRecognizers.Add(tap);
            ColorRow.Children.Add(swatch);
        }
        UpdateColorSelection();
    }

    private void UpdateColorSelection()
    {
        foreach (var child in ColorRow.Children.OfType<Border>())
        {
            var selected = child.ClassId == _vm.EditColor;
            child.StrokeThickness = selected ? 3 : 0;
            child.Stroke = selected ? Colors.White : null;
            child.Scale = selected ? 1.1 : 1.0;
        }
    }
}
