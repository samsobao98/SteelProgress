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

    // Cerrar sin guardar: ClearForm borra los vídeos elegidos para un ejercicio nuevo que no se guardó
    private void OnBackdropTapped(object? sender, TappedEventArgs e)
        => _ = CancelOverlayAsync();

    private void OnCloseSheetTapped(object? sender, TappedEventArgs e)
        => _ = CancelOverlayAsync();

    private async Task CancelOverlayAsync()
    {
        await HideOverlayAsync();
        _vm.ClearForm();
    }

    // ── Vídeos e imágenes ─────────────────────────────────────────────────

    private async void OnAddMediaClicked(object? sender, EventArgs e)
    {
        const string photo = "Foto o GIF de la galería";
        const string video = "Vídeo de la galería";
        const string record = "Grabar vídeo";

        var options = MediaPicker.Default.IsCaptureSupported ? new[] { photo, video, record } : new[] { photo, video };
        var choice = await DisplayActionSheetAsync("Añadir vídeo o imagen", "Cancelar", null, options);

        try
        {
            // De la galería se pueden elegir varios a la vez
            IEnumerable<FileResult?> files = choice switch
            {
                photo => await MediaPicker.Default.PickPhotosAsync() ?? [],
                video => await MediaPicker.Default.PickVideosAsync() ?? [],
                record => await MediaPicker.Default.CaptureVideoAsync() is { } recorded ? [recorded] : [],
                _ => []
            };

            foreach (var file in files)
            {
                if (file is null) continue;
                await using var stream = await file.OpenReadAsync();
                await _vm.AddMediaAsync(stream, file.FileName);
            }
        }
        catch (PermissionException)
        {
            await DisplayAlertAsync("Permiso necesario", "Da permiso a SteelProgress para acceder a tus fotos y vídeos.", "OK");
        }
    }

    private async void OnRemoveMediaTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ExerciseMedia media)
            await _vm.RemoveMediaCommand.ExecuteAsync(media);
    }

    // Por ahora el vídeo se abre con el reproductor del móvil (reproductor propio: decisión D1 pendiente)
    private async void OnMediaTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ExerciseMedia media) return;
        if (_vm.GetMediaPath(media) is not string path || !File.Exists(path)) return;

        if (sender is VisualElement element)
        {
            await element.ScaleToAsync(0.95, 70, Easing.CubicIn);
            await element.ScaleToAsync(1.0, 120, Easing.SpringOut);
        }
        await Launcher.Default.OpenAsync(new OpenFileRequest(media.Kind == MediaKind.Video ? "Vídeo" : "Imagen", new ReadOnlyFile(path)));
    }

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
