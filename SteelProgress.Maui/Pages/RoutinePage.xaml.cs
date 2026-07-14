using MauiIcons.Core;
using Microsoft.Extensions.DependencyInjection;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Pages;

public partial class RoutinePage : ContentPage
{
    private readonly RoutineViewModel _vm;
    private readonly IServiceProvider _services;

    public RoutinePage(RoutineViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        _ = new MauiIcon();
        _services = services;
        BindingContext = _vm = vm;
        _vm.RoutineCreated += () => _ = HideOverlayAsync();
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
            RoutineList.Opacity = 1;
            return;
        }

        RoutineList.Opacity = 0;
        // El FAB no se anima: animar Scale/Opacity sobre un elemento con Shadow
        // deja la sombra rasterizada borrosa en Android (el detalle lo hace igual).
        await RoutineList.FadeToAsync(1, 320, Easing.CubicOut);
    }

    // ── Navegación al detalle ──────────────────────────────────────────────

    private async void OnRoutineTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not RoutineSummary summary)
            return;

        var page = _services.GetRequiredService<RoutineDetailPage>();
        await page.LoadAsync(summary.Routine);
        await Navigation.PushAsync(page);
    }

    // ── FAB: abre el sheet de creación ─────────────────────────────────────

    private void OnFabTapped(object? sender, TappedEventArgs e)
    {
        _vm.ClearForm();
        _ = ShowOverlayAsync();
    }

    private void OnBackdropTapped(object? sender, TappedEventArgs e)
        => _ = HideOverlayAsync();

    private void OnCloseSheetTapped(object? sender, TappedEventArgs e)
        => _ = HideOverlayAsync();

    private async void OnCreateTapped(object? sender, TappedEventArgs e)
    {
        NameEntry.Unfocus();
        await _vm.CreateRoutineCommand.ExecuteAsync(null);
        // El sheet se cierra desde el evento RoutineCreated en caso de éxito.
    }

    // ── Animaciones del overlay ────────────────────────────────────────────

    private async Task ShowOverlayAsync()
    {
        BottomSheet.TranslationY = 700;
        BottomSheet.IsVisible = true;
        OverlayBackdrop.Opacity = 0;
        OverlayBackdrop.IsVisible = true;

        if (IsReduceMotionEnabled())
        {
            BottomSheet.TranslationY = 0;
            OverlayBackdrop.Opacity = 0.6;
            return;
        }

        await Task.WhenAll(
            BottomSheet.TranslateToAsync(0, 0, 320, Easing.CubicOut),
            OverlayBackdrop.FadeToAsync(0.6, 280, Easing.CubicOut));
    }

    private async Task HideOverlayAsync()
    {
        if (IsReduceMotionEnabled())
        {
            BottomSheet.IsVisible = false;
            OverlayBackdrop.IsVisible = false;
            return;
        }

        await Task.WhenAll(
            BottomSheet.TranslateToAsync(0, 700, 260, Easing.CubicIn),
            OverlayBackdrop.FadeToAsync(0, 220, Easing.CubicIn));

        BottomSheet.IsVisible = false;
        OverlayBackdrop.IsVisible = false;
    }

    private static bool IsReduceMotionEnabled()
    {
#if IOS
        return UIKit.UIAccessibility.IsReduceMotionEnabled;
#else
        return false;
#endif
    }
}
