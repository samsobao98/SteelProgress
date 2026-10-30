using System.ComponentModel;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Maui.Services;

namespace SteelProgress.Maui.Pages;

// Se abre con el id del cliente: GoToAsync("ClientLimitationsPage", { ["clientId"] = id })
public partial class ClientLimitationsPage : ContentPage, IQueryAttributable
{
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

    private readonly ClientLimitationsViewModel _vm;
    private int? _clientId;

    public ClientLimitationsPage(ClientLimitationsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnViewModelChanged;
        BuildChoices();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("clientId", out var value) && value is int id)
            _clientId = id;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_coveredByPopup) { _coveredByPopup = false; return; } // vuelve de una ventana propia, no de otra pantalla
        if (_clientId is int id) await _vm.LoadAsync(id);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (AppDialogs.IsShowing) _coveredByPopup = true; // solo se abrió una ventana encima
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_vm.IsEditing) return base.OnBackButtonPressed();
        _vm.CancelEditCommand.Execute(null);
        return true;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("..");

    // Marcar la casilla registra el consentimiento con su fecha (desmarcar no hace nada: se retira abajo)
    private async void OnConsentChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (e.Value) await _vm.GiveConsentAsync();
    }

    // El texto de la casilla también la marca
    private void OnConsentTextTapped(object? sender, TappedEventArgs e) =>
        _vm.ConsentChecked = !_vm.ConsentChecked;

    private async void OnItemOptionsTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not LimitationItem item) return;

        const string edit = "Editar", delete = "Eliminar";
        var toggle = item.Limitation.ResolvedAt is null ? "Marcar como superada" : "Vuelve a estar vigente";
        var choice = await AppDialogs.ChooseAsync(item.Title, [edit, toggle], destructive: delete);

        if (choice == edit) _vm.StartEdit(item);
        else if (choice == toggle) await _vm.ToggleResolvedAsync(item);
        else if (choice == delete) await _vm.DeleteAsync(item);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ClientLimitationsViewModel.EditZone):
            case nameof(ClientLimitationsViewModel.EditSide):
            case nameof(ClientLimitationsViewModel.EditSource):
                UpdateChoices();
                break;
            case nameof(ClientLimitationsViewModel.ShowResolved):
                ResolvedChevron.Rotation = _vm.ShowResolved ? 180 : 0;
                break;
        }
    }

    // ── Zona, lado y de quién son las pautas: botones que se marcan al tocarlos ──

    private void BuildChoices()
    {
        foreach (var zone in LimitationText.Zones)
        {
            var chip = Choice(zone, 36, 18, () => _vm.EditZone = zone);
            chip.Padding = new Thickness(14, 0);
            chip.Margin = new Thickness(0, 0, 8, 8);
            ZoneRow.Children.Add(chip);
        }
        for (var i = 0; i < LimitationText.Sides.Count; i++)
        {
            var (side, label) = LimitationText.Sides[i];
            var chip = Choice(label, 40, 10, () => _vm.PickSide(side));
            Grid.SetColumn(chip, i);
            SideRow.Children.Add(chip);
        }
        for (var i = 0; i < LimitationText.Sources.Count; i++)
        {
            var (source, label) = LimitationText.Sources[i];
            var chip = Choice(label, 40, 10, () => _vm.EditSource = source);
            Grid.SetColumn(chip, i);
            SourceRow.Children.Add(chip);
        }
        UpdateChoices();
    }

    private static Border Choice(string label, double height, double radius, Action pick)
    {
        var chip = new Border
        {
            HeightRequest = height,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = radius },
            StrokeThickness = 1,
            ClassId = label,
            Content = new Label { Text = label, FontSize = 14, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => pick();
        chip.GestureRecognizers.Add(tap);
        return chip;
    }

    private void UpdateChoices()
    {
        foreach (var chip in ZoneRow.Children.OfType<Border>())
            Paint(chip, chip.ClassId == _vm.EditZone);
        foreach (var chip in SideRow.Children.OfType<Border>())
            Paint(chip, LimitationText.Sides.Any(s => s.Label == chip.ClassId && s.Side == _vm.EditSide));
        foreach (var chip in SourceRow.Children.OfType<Border>())
            Paint(chip, LimitationText.Sources.Any(s => s.Label == chip.ClassId && s.Source == _vm.EditSource));
    }

    private static void Paint(Border chip, bool selected)
    {
        var res = Application.Current!.Resources;
        var accent = (Color)res["SpSteel1"];
        chip.BackgroundColor = selected ? accent : (Color)res["SpBg"];
        chip.Stroke = selected ? accent : (Color)res["SpLine"];
        ((Label)chip.Content!).TextColor = selected ? (Color)res["SpOnAccent"] : (Color)res["SpText2"];
    }
}
