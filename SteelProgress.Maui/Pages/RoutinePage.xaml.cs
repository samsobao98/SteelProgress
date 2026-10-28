using System.ComponentModel;
using CommunityToolkit.Maui.Behaviors;
using MauiIcons.Core;
using Microsoft.Extensions.DependencyInjection;
using SteelProgress.Core.Services;
using SteelProgress.Core.Sharing;
using SteelProgress.Core.ViewModels;
using SteelProgress.Maui.Services;

namespace SteelProgress.Maui.Pages;

public partial class RoutinePage : ContentPage
{
    private static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(5);

    private readonly RoutineViewModel _vm;
    private readonly IServiceProvider _services;
    private CancellationTokenSource? _undoTimer;

    // Un TouchBehavior por tarjeta; se desactivan al ordenar para no robarle el arrastre a la lista
    private readonly List<WeakReference<TouchBehavior>> _cardTouches = new();

    public RoutinePage(RoutineViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        _ = new MauiIcon();
        _services = services;
        BindingContext = _vm = vm;
        _vm.RoutineCreated += () => _ = HideOverlayAsync();
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private ClientContextService? _clientContext;

    // Al entrar o salir de un cliente, la pantalla vuelve a cargar sus datos
    private void OnClientChanged() => MainThread.BeginInvokeOnMainThread(async () => await _vm.RefreshAsync());

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        if (_clientContext is not null) _clientContext.Changed -= OnClientChanged;

        PreviewLayer.IsVisible = false;
        _vm.ClosePreviewCommand.Execute(null);

        // Al salir (otra pestaña o el detalle) ya no se puede deshacer: se borra de verdad
        _undoTimer?.Cancel();
        await _vm.CommitPendingDeleteAsync();
        if (_vm.IsReordering)
            await _vm.FinishReorderCommand.ExecuteAsync(null);
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

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoutineViewModel.IsReordering))
        {
            OrderButtonLabel.Text = _vm.IsReordering ? "Hecho" : "Ordenar";
            foreach (var weak in _cardTouches)
                if (weak.TryGetTarget(out var touch)) touch.IsEnabled = !_vm.IsReordering;
        }
    }

    private async Task RunEntryAnimationsAsync()
    {
        if (IsReduceMotionEnabled())
        {
            RoutineList.Opacity = 1;
            return;
        }

        RoutineList.Opacity = 0;
        RoutineList.TranslationY = 12;
        // El FAB no se anima: animar Scale/Opacity sobre un elemento con Shadow
        // deja la sombra rasterizada borrosa en Android (el detalle lo hace igual).
        await Task.WhenAll(
            RoutineList.FadeToAsync(1, 280, Easing.CubicOut),
            RoutineList.TranslateToAsync(0, 0, 280, Easing.CubicOut));
    }

    // ── Tocar y mantener pulsado ──────────────────────────────────────────

    // MAUI no tiene «mantener pulsado»: se usa TouchBehavior del Toolkit. Se crea aquí, no en la
    // plantilla, para no depender de que herede el BindingContext; los comandos leen la rutina
    // que muestra la tarjeta en ese momento (la lista recicla tarjetas al desplazarse).
    private void OnCardLoaded(object? sender, EventArgs e)
    {
        if (sender is not Border card || card.Behaviors.OfType<TouchBehavior>().Any()) return;

        var touch = new TouchBehavior
        {
            PressedScale = 0.97,
            DefaultAnimationDuration = 150,
            LongPressDuration = 450,
            IsEnabled = !_vm.IsReordering,
            Command = new Command(async () =>
            {
                if (card.BindingContext is RoutineListItem item) await OpenRoutineAsync(item);
            }),
            LongPressCommand = new Command(async () =>
            {
                if (card.BindingContext is not RoutineListItem item) return;
                Haptic();
                await ShowPreviewAsync(item);
            })
        };
        card.Behaviors.Add(touch);
        _cardTouches.RemoveAll(w => !w.TryGetTarget(out _));
        _cardTouches.Add(new WeakReference<TouchBehavior>(touch));
    }

    private bool _opening;

    private async Task OpenRoutineAsync(RoutineListItem item)
    {
        if (_vm.IsReordering || _opening) return;
        _opening = true;
        try
        {
            var page = _services.GetRequiredService<RoutineDetailPage>();
            await page.LoadAsync(item.Routine);
            await Navigation.PushAsync(page);
        }
        finally { _opening = false; }
    }

    // ── Menú ⋮ ─────────────────────────────────────────────────────────────

    private async void OnMoreTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not RoutineListItem item) return;
        await PressAsync(sender as VisualElement);

        const string summary = "Ver resumen";
        const string rename = "Cambiar nombre y notas";
        const string share = "Compartir";
        const string delete = "Eliminar";
        var mark = item.Routine.IsMarkedActive ? "Quitar como activa" : "Marcar como activa";

        var choice = await DisplayActionSheetAsync(item.Name, "Cancelar", delete, summary, rename, mark, share);
        switch (choice)
        {
            case summary:
                await ShowPreviewAsync(item);
                break;
            case rename:
                await RenameAsync(item);
                break;
            case share:
                await RoutineShareFlow.ShareAsync(this, _services.GetRequiredService<RoutineSharingService>(), item.Routine);
                break;
            case delete:
                await DeleteAsync(item);
                break;
            default:
                if (choice == mark) await _vm.ToggleMarkedActiveAsync(item);
                break;
        }
    }

    private async Task RenameAsync(RoutineListItem item)
    {
        var name = await DisplayPromptAsync("Nombre", "Nombre de la rutina", accept: "Siguiente", cancel: "Cancelar",
            initialValue: item.Name, maxLength: 60);
        if (string.IsNullOrWhiteSpace(name)) return;

        var notes = await DisplayPromptAsync("Notas (opcional)", "Objetivo, frecuencia…", accept: "Guardar", cancel: "Cancelar",
            initialValue: item.Routine.Notes ?? "", maxLength: 120);
        if (notes is null) return; // cancelado

        await _vm.RenameAsync(item, name, notes);
    }

    // ── Vista previa ───────────────────────────────────────────────────────

    private async Task ShowPreviewAsync(RoutineListItem item)
    {
        if (_vm.IsReordering) return;
        await _vm.OpenPreviewAsync(item);

        PreviewLayer.IsVisible = true;
        if (IsReduceMotionEnabled())
        {
            PreviewBackdrop.Opacity = 0.6;
            PreviewCard.Scale = 1;
            PreviewCard.Opacity = 1;
            return;
        }

        PreviewBackdrop.Opacity = 0;
        PreviewCard.Scale = 0.92;
        PreviewCard.Opacity = 0;
        await Task.WhenAll(
            PreviewBackdrop.FadeToAsync(0.6, 200, Easing.CubicOut),
            PreviewCard.FadeToAsync(1, 200, Easing.CubicOut),
            PreviewCard.ScaleToAsync(1, 260, Easing.SpringOut));
    }

    private async Task HidePreviewAsync()
    {
        if (!PreviewLayer.IsVisible) return;
        if (!IsReduceMotionEnabled())
        {
            await Task.WhenAll(
                PreviewBackdrop.FadeToAsync(0, 160, Easing.CubicIn),
                PreviewCard.FadeToAsync(0, 160, Easing.CubicIn),
                PreviewCard.ScaleToAsync(0.95, 160, Easing.CubicIn));
        }
        PreviewLayer.IsVisible = false;
        _vm.ClosePreviewCommand.Execute(null);
    }

    private void OnPreviewBackdropTapped(object? sender, TappedEventArgs e) => _ = HidePreviewAsync();

    private async void OnPreviewEnterTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.Preview?.Item is not { } item) return;
        await PressAsync(sender as VisualElement);
        await HidePreviewAsync();
        await OpenRoutineAsync(item);
    }

    // ── Ordenar ────────────────────────────────────────────────────────────

    private async void OnOrderTapped(object? sender, TappedEventArgs e)
    {
        await PressAsync(OrderButton);

        if (_vm.IsReordering)
        {
            await _vm.FinishReorderCommand.ExecuteAsync(null);
            return;
        }

        const string manual = "Ordenar a mano";
        const string automatic = "Volver al orden automático";
        var options = _vm.IsManualOrder ? new[] { manual, automatic } : new[] { manual };
        var choice = await DisplayActionSheetAsync("Orden de las rutinas", "Cancelar", null, options);

        if (choice == manual)
            _vm.StartReorderCommand.Execute(null);
        else if (choice == automatic)
            await _vm.ResetOrderCommand.ExecuteAsync(null);
    }

    // ── Eliminar deslizando, con deshacer ──────────────────────────────────

    private async void OnSwipeDeleteInvoked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is RoutineListItem item)
            await DeleteAsync(item);
    }

    private async Task DeleteAsync(RoutineListItem item)
    {
        if (!RoutineSchedule.CanDeleteNow()) return;

        // Borrar una rutina borra también su historial: si lo hay, se avisa antes
        var sessions = await _vm.CountSessionsAsync(item);
        if (sessions > 0)
        {
            var ok = await DisplayAlertAsync($"Eliminar «{item.Name}»",
                $"Se borrarán también {(sessions == 1 ? "su entreno" : $"sus {sessions} entrenos")} del historial.",
                "Eliminar", "Cancelar");
            if (!ok) return;
        }

        await _vm.DeleteWithUndoAsync(item);
        StartUndoTimer();
    }

    private void StartUndoTimer()
    {
        _undoTimer?.Cancel();
        var cts = _undoTimer = new CancellationTokenSource();
        _ = Task.Delay(UndoWindow, cts.Token).ContinueWith(async t =>
        {
            if (t.IsCanceled) return;
            await MainThread.InvokeOnMainThreadAsync(_vm.CommitPendingDeleteAsync);
        }, TaskScheduler.Default);
    }

    // ── Importar (.steel) ──────────────────────────────────────────────────

    private async void OnImportTapped(object? sender, TappedEventArgs e)
    {
        await PressAsync(sender as VisualElement);
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
        if (_vm.CreateRoutineCommand.IsRunning) return;
        NameEntry.Unfocus();
        await PressAsync(sender as VisualElement);
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
            OverlayBackdrop.Opacity = 0.55;
            return;
        }

        await Task.WhenAll(
            BottomSheet.TranslateToAsync(0, 0, 320, Easing.CubicOut),
            OverlayBackdrop.FadeToAsync(0.55, 280, Easing.CubicOut));
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

    // Escala 0,96 con rebote (guía visual §12.1)
    private static async Task PressAsync(VisualElement? element)
    {
        if (element is null || IsReduceMotionEnabled()) return;
        await element.ScaleToAsync(0.96, 60, Easing.CubicIn);
        await element.ScaleToAsync(1.0, 150, Easing.SpringOut);
    }

    private static void Haptic()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); }
        catch (Exception) { /* sin vibración en este dispositivo (p. ej. escritorio) */ }
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
