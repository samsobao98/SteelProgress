using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using MauiIcons.Core;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Maui.Services;

namespace SteelProgress.Maui.Pages;

public partial class HomePage : ContentPage
{
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

    private readonly HomeViewModel _vm;
    private readonly ClientContextService _clientContext;

    // Por constructor: Inicio es la primera pestaña y en su primer OnAppearing aún no hay Handler
    // del que sacar los servicios (le pasaba lo mismo a Entreno)
    public HomePage(HomeViewModel vm, ClientContextService clientContext)
    {
        InitializeComponent();
        // Workaround for MAUI bug with URL-style xmlns (dotnet/maui#7503)
        _ = new MauiIcon();
        BindingContext = _vm = vm;
        _clientContext = clientContext;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    // Al entrar o salir de un cliente, Inicio pasa a mostrar sus datos
    private void OnClientChanged() => MainThread.BeginInvokeOnMainThread(async () => await _vm.RefreshAsync());

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_coveredByPopup) { _coveredByPopup = false; return; } // vuelve de una ventana propia, no de otra pantalla
        _clientContext.Changed -= OnClientChanged;
        _clientContext.Changed += OnClientChanged;

        var first = !_vm.IsLoaded;
        try { await _vm.RefreshAsync(); }
        finally
        {
            // La primera vez aparece con un fundido (también si la carga falla: nunca se queda en blanco)
            if (first)
            {
                PageBody.TranslationY = 10;
                await Task.WhenAll(PageBody.FadeToAsync(1, 260, Easing.CubicOut), PageBody.TranslateToAsync(0, 0, 260, Easing.CubicOut));
            }
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (AppDialogs.IsShowing) { _coveredByPopup = true; return; } // solo se abrió una ventana encima
        _clientContext.Changed -= OnClientChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.Week)) BuildWeek();
        else if (e.PropertyName is nameof(HomeViewModel.TodayExercises) or nameof(HomeViewModel.TodayMoreText)) BuildTodayChips();
    }

    // Tira de la semana: letra del día y un círculo (✓ si se entrenó; contorno el de hoy)
    private void BuildWeek()
    {
        WeekStrip.Children.Clear();
        var col = 0;
        foreach (var day in _vm.Week)
        {
            var circle = new Border
            {
                HeightRequest = 34,
                WidthRequest = 34,
                HorizontalOptions = LayoutOptions.Center,
                StrokeShape = new RoundRectangle { CornerRadius = 17 },
                BackgroundColor = day.Done ? Color.FromArgb("#262B40") : Color.FromArgb("#1B1E28"),
                Stroke = day.IsToday ? Color.FromArgb("#B3B7EC") : Colors.Transparent,
                StrokeThickness = day.IsToday ? 2 : 0,
                Opacity = day.IsFuture ? 0.55 : 1,
                Content = new Label
                {
                    Text = day.Done ? "✓" : "",
                    FontFamily = "SpaceGroteskMedium",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#B3B7EC"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            var cell = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = day.Letter,
                        FontFamily = "SpaceGroteskMedium",
                        FontSize = 11,
                        TextColor = day.IsToday ? Color.FromArgb("#E8EAF2") : Color.FromArgb("#8A91A8"),
                        HorizontalOptions = LayoutOptions.Center
                    },
                    circle
                }
            };
            SemanticProperties.SetDescription(cell, $"{day.Date:dddd}{(day.Done ? ", entrenado" : "")}");
            WeekStrip.Add(cell, col++, 0);
        }
    }

    private void BuildTodayChips()
    {
        TodayChips.Children.Clear();
        foreach (var name in _vm.TodayExercises)
            TodayChips.Children.Add(Chip(name, "#A0A7BD", "SpaceGroteskRegular"));
        if (_vm.HasTodayMore)
            TodayChips.Children.Add(Chip(_vm.TodayMoreText, "#8A91A8", "SpaceGroteskMedium"));
    }

    private static Border Chip(string text, string color, string font) => new()
    {
        BackgroundColor = Color.FromArgb("#1B1E28"),
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 12 },
        Padding = new Thickness(10, 5),
        Margin = new Thickness(0, 0, 6, 6),
        Content = new Label { Text = text, FontFamily = font, FontSize = 12, TextColor = Color.FromArgb(color), LineBreakMode = LineBreakMode.TailTruncation, MaximumWidthRequest = 220 }
    };

    private static async Task PressAsync(VisualElement? element)
    {
        if (element is null) return;
        await element.ScaleToAsync(0.97, 70, Easing.CubicIn);
        await element.ScaleToAsync(1.0, 120, Easing.SpringOut);
    }

    private async void OnProfileTapped(object? sender, TappedEventArgs e)
    {
        await PressAsync(ProfileButton);
        await Shell.Current.GoToAsync("ProfilePage");
    }

    // ▶ Empezar: Entreno crea la sesión al aparecer (WorkoutLaunch)
    private async void OnStartTapped(object? sender, TappedEventArgs e)
    {
        await PressAsync(StartButton);
        if (!_vm.RequestStartToday()) return;
        await Shell.Current.GoToAsync("//WorkoutPage");
    }

    // «Elegir otro entreno» y «Continuar»: la pantalla de Entreno, con el día que toca ya propuesto
    private async void OnOtherWorkoutTapped(object? sender, TappedEventArgs e)
    {
        await PressAsync(sender as VisualElement);
        await Shell.Current.GoToAsync("//WorkoutPage");
    }

    private async void OnRoutinesTapped(object? sender, TappedEventArgs e)
    {
        await PressAsync(sender as VisualElement);
        // Sin apartado Rutinas (vista simple de entrenador), a Perfil para cambiar la vista
        await Shell.Current.GoToAsync(_vm.CanEditRoutines ? "//RoutinePage" : "ProfilePage");
    }

    // Inicio de entrenador: tocar un cliente lo activa e Inicio pasa a mostrar sus datos (OnClientChanged)
    private void OnCoachClientTapped(object? sender, Domain.Entities.Client client) => _vm.OpenClient(client);

    private async void OnGoToClientsTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//ClientsPage");

    private async void OnHistoryTapped(object? sender, TappedEventArgs e)
    {
        ProgressPage.Request(ProgressSection.History);
        await Shell.Current.GoToAsync("//ProgressPage");
    }

    private async void OnSessionTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not RecentSessionItem item) return;
        await PressAsync(sender as VisualElement);
        ProgressPage.Request(ProgressSection.History, item.SessionId);
        await Shell.Current.GoToAsync("//ProgressPage");
    }
}
