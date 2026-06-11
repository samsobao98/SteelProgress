using MauiIcons.Core;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Pages;

public partial class ExercisePage : ContentPage
{
    private readonly ExerciseViewModel _vm;

    public ExercisePage(ExerciseViewModel vm)
    {
        InitializeComponent();
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
            ExerciseList.Opacity = 1;
            FabButton.Opacity = 1;
            FabButton.Scale = 1;
            return;
        }

        ExerciseList.Opacity = 0;
        FabButton.Opacity = 0;
        FabButton.Scale = 0;

        await ExerciseList.FadeToAsync(1, 320, Easing.CubicOut);

        await Task.WhenAll(
            FabButton.FadeToAsync(1, 260, Easing.CubicOut),
            FabButton.ScaleToAsync(1.0, 260, Easing.SpringOut));
        FabButton.Scale = 1;
    }

    // ── FAB: abre overlay en modo añadir ──────────────────────────────────

    private void OnFabTapped(object? sender, TappedEventArgs e)
    {
        _vm.ClearForm();
        _ = ShowOverlayAsync();
    }

    // ── Item de la lista: abre overlay en modo edición ───────────────────

    private void OnExerciseTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is Exercise exercise)
        {
            _vm.SelectedExercise = exercise;
            _ = ShowOverlayAsync();
        }
    }

    // ── Cierre del overlay ────────────────────────────────────────────────

    private void OnBackdropTapped(object? sender, TappedEventArgs e)
        => _ = HideOverlayAsync();

    private void OnCloseSheetTapped(object? sender, TappedEventArgs e)
        => _ = HideOverlayAsync();

    // ── Guardar (añadir o actualizar) ─────────────────────────────────────

    private async void OnSaveTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.IsEditMode)
            await _vm.UpdateExerciseCommand.ExecuteAsync(null);
        else
            await _vm.AddExerciseCommand.ExecuteAsync(null);

        // VM llama ClearForm() en éxito → Name queda vacío
        if (string.IsNullOrWhiteSpace(_vm.Name))
            await HideOverlayAsync();
    }

    // ── Eliminar ──────────────────────────────────────────────────────────

    private async void OnDeleteTapped(object? sender, TappedEventArgs e)
    {
        await _vm.DeleteExerciseCommand.ExecuteAsync(null);

        // VM llama ClearForm() en éxito → SelectedExercise queda null
        if (_vm.SelectedExercise is null)
            await HideOverlayAsync();
    }

    // ── Animaciones overlay ───────────────────────────────────────────────

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
