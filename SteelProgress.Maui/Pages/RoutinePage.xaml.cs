using MauiIcons.Core;
using Microsoft.Extensions.DependencyInjection;
using SteelProgress.Core.Services;
using SteelProgress.Core.Sharing;
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

    private ClientContextService? _clientContext;

    // Al entrar o salir de un cliente, la pantalla vuelve a cargar sus datos
    private void OnClientChanged() => MainThread.BeginInvokeOnMainThread(async () => await _vm.RefreshAsync());

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_clientContext is not null) _clientContext.Changed -= OnClientChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _clientContext ??= _services.GetService<ClientContextService>();
        if (_clientContext is not null)
        {
            _clientContext.Changed -= OnClientChanged;
            _clientContext.Changed += OnClientChanged;
        }
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

    // ── Importar (.steel) ──────────────────────────────────────────────────

    private async void OnImportTapped(object? sender, TappedEventArgs e)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Elige un archivo .steel" });
        if (file is null) return;

        if (!file.FileName.EndsWith(SteelPackageFormat.Extension, StringComparison.OrdinalIgnoreCase))
        {
            NotificationService.Error("Elige un archivo .steel de SteelProgress.");
            return;
        }

        // Copia local: el archivo elegido puede no seguir accesible mientras se revisa la vista previa
        var path = Path.Combine(FileSystem.CacheDirectory, "importar" + SteelPackageFormat.Extension);
        await using (var source = await file.OpenReadAsync())
        await using (var target = File.Create(path))
            await source.CopyToAsync(target);

        await Shell.Current.GoToAsync(nameof(ImportPreviewPage), new Dictionary<string, object> { ["path"] = path });
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
