using MauiIcons.Core;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        // Workaround for MAUI bug with URL-style xmlns (dotnet/maui#7503)
        _ = new MauiIcon();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();
        await RunEntryAnimationsAsync();
    }

    private async Task RunEntryAnimationsAsync()
    {
        if (IsReduceMotionEnabled())
        {
            HeaderSection.Opacity = 1;
            StatHeroCard.Opacity = 1; StatHeroCard.Scale = 1;
            StatRow.Opacity = 1;     StatRow.TranslationX = 0;
            CtaSection.Opacity = 1;  CtaSection.Scale = 1;
            SessionsSection.Opacity = 1;
            return;
        }

        // Reset — each section has a different starting state (impeccable: vary per element)
        HeaderSection.Opacity = 0;  HeaderSection.TranslationY = -16;
        StatHeroCard.Opacity = 0;   StatHeroCard.Scale = 0.95;
        StatRow.Opacity = 0;        StatRow.TranslationX = -20;
        CtaSection.Opacity = 0;     CtaSection.Scale = 0.97;
        SessionsSection.Opacity = 0;

        // 1. Header: slide desde arriba
        await Task.WhenAll(
            HeaderSection.FadeToAsync(1, 260, Easing.CubicOut),
            HeaderSection.TranslateToAsync(0, 0, 260, Easing.CubicOut));

        // 2. Hero stat: escala (distinto al header — sin Y)
        await Task.WhenAll(
            StatHeroCard.FadeToAsync(1, 300, Easing.CubicOut),
            StatHeroCard.ScaleToAsync(1, 300, Easing.CubicOut));

        // 3. Fila de stats: deslizamiento horizontal (distinto eje)
        await Task.WhenAll(
            StatRow.FadeToAsync(1, 280, Easing.CubicOut),
            StatRow.TranslateToAsync(0, 0, 280, Easing.CubicOut));

        // 4. CTA: escala suave (como el hero pero más rápido)
        await Task.WhenAll(
            CtaSection.FadeToAsync(1, 240, Easing.CubicOut),
            CtaSection.ScaleToAsync(1, 240, Easing.CubicOut));

        // 5. Sesiones: fade puro, sin movimiento — calma al final de la secuencia
        await SessionsSection.FadeToAsync(1, 300, Easing.CubicOut);
    }

    private static bool IsReduceMotionEnabled()
    {
#if IOS
        return UIKit.UIAccessibility.IsReduceMotionEnabled;
#else
        return false;
#endif
    }

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        await SettingsButton.ScaleToAsync(0.92, 70, Easing.CubicIn);
        await SettingsButton.ScaleToAsync(1.0, 120, Easing.SpringOut);
        await Shell.Current.GoToAsync("SettingsPage");
    }

    private async void OnWorkoutCtaTapped(object? sender, TappedEventArgs e)
    {
        await CtaSection.ScaleToAsync(0.97, 80, Easing.CubicIn);
        await CtaSection.ScaleToAsync(1.0, 80, Easing.CubicOut);
        await Shell.Current.GoToAsync("//WorkoutPage");
    }
}
