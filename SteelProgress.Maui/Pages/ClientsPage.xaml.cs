using SteelProgress.Maui.Services;
using System.ComponentModel;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class ClientsPage : ContentPage
{
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

    private readonly ClientsViewModel _vm;

    private readonly Core.Services.ViewOptionsService _viewOptions;

    public ClientsPage(ClientsViewModel vm, Core.Services.ViewOptionsService viewOptions)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _viewOptions = viewOptions;
        _vm.PropertyChanged += OnViewModelChanged;
        BuildColorRow();
        BuildDayRow();
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
        const string edit = "Editar", limitations = "Limitaciones", delete = "Eliminar cliente";
        // En la vista simple de entrenador no hay apartado Rutinas: sus rutinas se usan desde Entreno
        string[] options = _viewOptions.Current.ShowRoutines
            ? [train, routines, history, limitations, edit]
            : [train, history, limitations, edit];
        var choice = await AppDialogs.ChooseAsync(name, options, destructive: delete);

        if (choice == edit) { _vm.StartEdit(item.Client); return; }
        if (choice == limitations)
        {
            await Shell.Current.GoToAsync(nameof(ClientLimitationsPage), new Dictionary<string, object> { ["clientId"] = item.Client.Id });
            return;
        }
        if (choice == delete) { await _vm.DeleteAsync(item.Client); return; }

        if (choice == history) ProgressPage.Request(ProgressSection.History);
        var route = choice == train ? "//WorkoutPage" : choice == routines ? "//RoutinePage" : choice == history ? "//ProgressPage" : null;
        if (route is null) return;

        // Desde aquí, esas pantallas muestran y guardan los datos del cliente
        _vm.Open(item.Client);
        await Shell.Current.GoToAsync(route);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClientsViewModel.EditColor))
            UpdateColorSelection();
        else if (e.PropertyName == nameof(ClientsViewModel.EditTrainingDays))
            UpdateDaySelection();
    }

    private void OnParqLabelTapped(object? sender, TappedEventArgs e) => _vm.EditHasParq = !_vm.EditHasParq;

    // Un botón redondo por día, de lunes a domingo; se marcan y desmarcan tocándolos
    private void BuildDayRow()
    {
        foreach (var (day, letter) in Core.Services.TrainingDays.Week)
        {
            var chip = new Border
            {
                WidthRequest = 38,
                HeightRequest = 38,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 19 },
                StrokeThickness = 1,
                ClassId = day.ToString(),
                Content = new Label { Text = letter, FontSize = 14, FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
            };
            SemanticProperties.SetDescription(chip, Core.Services.TrainingDays.Name(day));
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => _vm.ToggleTrainingDay(day);
            chip.GestureRecognizers.Add(tap);
            DayRow.Children.Add(chip);
        }
        UpdateDaySelection();
    }

    private void UpdateDaySelection()
    {
        var accent = (Color)Application.Current!.Resources["SpSteel1"];
        foreach (var chip in DayRow.Children.OfType<Border>())
        {
            var selected = (_vm.EditTrainingDays & Core.Services.TrainingDays.Bit(Enum.Parse<DayOfWeek>(chip.ClassId))) != 0;
            chip.BackgroundColor = selected ? accent : (Color)Application.Current.Resources["SpBg"];
            chip.Stroke = selected ? accent : (Color)Application.Current.Resources["SpLine"];
            ((Label)chip.Content!).TextColor = selected ? (Color)Application.Current.Resources["SpOnAccent"] : (Color)Application.Current.Resources["SpText2"];
        }
        DayHint.Text = _vm.EditTrainingDays == 0
            ? "Sin marcar, el Inicio avisa a los 7 días sin sesiones."
            : "El Inicio le marca «Toca hoy» esos días y avisa si se salta dos seguidos.";
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
