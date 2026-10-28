using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Maui.Controls;
using SteelProgress.Maui.Services;

namespace SteelProgress.Maui.Pages;

/// <summary>Pestaña Progreso (absorbe Historial): resumen, evolución por ejercicio e historial de sesiones.</summary>
public partial class ProgressPage : ContentPage
{
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

    private readonly ProgressViewModel _vm;
    private readonly GoalRingDrawable _ring = new();
    private readonly ConsistencyDrawable _calendar = new();
    private ClientContextService? _clientContext;

    // Lo que piden otras pantallas antes de cambiar a esta pestaña (Inicio, Clientes)
    private static (ProgressSection Section, int? SessionId)? _request;

    /// <summary>Abre Progreso en una sección (y, en el historial, una sesión). Llamar antes de navegar.</summary>
    public static void Request(ProgressSection section, int? sessionId = null) => _request = (section, sessionId);

    public ProgressPage(ProgressViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        GoalRing.Drawable = _ring;
        CalendarView.Drawable = _calendar;
        BuildLegend();
        _vm.PropertyChanged += OnVmPropertyChanged;
        SizeChanged += (_, _) => FitDetail();
        DetailHeader.SizeChanged += (_, _) => FitDetail();
    }

    // El panel crece con la sesión hasta el 85 % de la pantalla; a partir de ahí se desplaza la lista
    private void FitDetail()
    {
        if (Height <= 0) return;
        var padding = DetailSheet.Padding.VerticalThickness + DetailScroll.Margin.VerticalThickness;
        DetailScroll.MaximumHeightRequest = Math.Max(160, Height * 0.85 - DetailHeader.Height - padding);
    }

    private void OnClientChanged() => MainThread.BeginInvokeOnMainThread(async () => await _vm.RefreshAsync());

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_coveredByPopup) { _coveredByPopup = false; return; } // vuelve de una ventana propia, no de otra pantalla
        _clientContext ??= Handler?.MauiContext?.Services.GetService<ClientContextService>();
        if (_clientContext is not null)
        {
            _clientContext.Changed -= OnClientChanged;
            _clientContext.Changed += OnClientChanged;
        }

        await _vm.RefreshAsync();

        if (_request is { } request)
        {
            _request = null;
            _vm.Section = request.Section;
            if (request.SessionId is int id) _vm.OpenSessionById(id);
            else _vm.CloseSessionCommand.Execute(null);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (AppDialogs.IsShowing) { _coveredByPopup = true; return; } // solo se abrió una ventana encima
        if (_clientContext is not null) _clientContext.Changed -= OnClientChanged;
    }

    // Atrás cierra el detalle de una sesión antes de salir de la pantalla
    protected override bool OnBackButtonPressed()
    {
        if (_vm.HasSessionDetail)
        {
            _vm.CloseSessionCommand.Execute(null);
            return true;
        }
        return base.OnBackButtonPressed();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ProgressViewModel.WeekProgress):
                _ring.Progress = _vm.WeekProgress;
                GoalRing.Invalidate();
                break;
            case nameof(ProgressViewModel.Calendar):
                _calendar.Days = _vm.Calendar;
                CalendarView.Invalidate();
                break;
            case nameof(ProgressViewModel.SessionDetail):
                _ = _vm.HasSessionDetail ? ShowDetailAsync() : HideDetailAsync();
                break;
            case nameof(ProgressViewModel.Section):
                _ = Scroller.ScrollToAsync(0, 0, false);
                break;
        }
    }

    // «menos ■■■■■ más»
    private void BuildLegend()
    {
        CalendarLegend.Children.Add(LegendText("menos"));
        foreach (var color in ConsistencyDrawable.Levels)
            CalendarLegend.Children.Add(new Border
            {
                WidthRequest = 10,
                HeightRequest = 10,
                BackgroundColor = color,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 2 },
                VerticalOptions = LayoutOptions.Center
            });
        CalendarLegend.Children.Add(LegendText("más"));
    }

    private static Label LegendText(string text) => new()
    {
        Text = text,
        FontFamily = "OpenSansRegular",
        FontSize = 10,
        TextColor = Color.FromArgb("#6C7086"),
        VerticalOptions = LayoutOptions.Center,
        Margin = new Thickness(3, 0)
    };

    private static async Task PressAsync(VisualElement? element)
    {
        if (element is null) return;
        await element.ScaleToAsync(0.97, 70, Easing.CubicIn);
        await element.ScaleToAsync(1.0, 120, Easing.SpringOut);
    }

    private async void OnExerciseTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ExerciseProgressItem item) return;
        await PressAsync(sender as VisualElement);
        _vm.SelectExerciseCommand.Execute(item);
        if (_vm.HasSelectedExercise)
            await Scroller.ScrollToAsync(0, 0, true);
    }

    private async void OnSessionTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not RecentSessionItem item) return;
        await PressAsync(sender as VisualElement);
        _vm.OpenSessionCommand.Execute(item);
    }

    private void OnDetailCloseTapped(object? sender, TappedEventArgs e) => _vm.CloseSessionCommand.Execute(null);

    // «Evolución ›» en el detalle: cierra la sesión y abre la gráfica de ese ejercicio
    private void OnDetailExerciseTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not SessionExerciseDetail exercise) return;
        _vm.CloseSessionCommand.Execute(null);
        _vm.SelectExerciseById(exercise.ExerciseId);
    }

    private async Task ShowDetailAsync()
    {
        DetailSheet.BindingContext = _vm.SessionDetail;
        FitDetail();
        DetailLayer.IsVisible = true;
        DetailSheet.TranslationY = 400;
        await Task.WhenAll(
            DetailBackdrop.FadeToAsync(0.6, 200, Easing.CubicOut),
            DetailSheet.TranslateToAsync(0, 0, 260, Easing.CubicOut));
    }

    private async Task HideDetailAsync()
    {
        if (!DetailLayer.IsVisible) return;
        await Task.WhenAll(
            DetailBackdrop.FadeToAsync(0, 160, Easing.CubicIn),
            DetailSheet.TranslateToAsync(0, 400, 200, Easing.CubicIn));
        if (!_vm.HasSessionDetail) DetailLayer.IsVisible = false;
    }
}
