using SteelProgress.Core.Services;
using SteelProgress.Core.Sharing;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class RoutinePage : ContentPage
{
    private readonly RoutineViewModel _vm;
    private readonly RoutineSharingService _sharing;

    public RoutinePage(RoutineViewModel vm, RoutineSharingService sharing)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _sharing = sharing;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();
    }

    private void ShowSection(int section)
    {
        SectionRutinas.IsVisible    = section == 0;
        SectionDias.IsVisible       = section == 1;
        SectionEjercicios.IsVisible = section == 2;

        FormRutinas.IsVisible    = section == 0;
        FormDias.IsVisible       = section == 1;
        FormEjercicios.IsVisible = section == 2;

        var active       = Color.FromArgb("#89B4FA");
        var inactive     = Color.FromArgb("#45475A");
        var textActive   = Color.FromArgb("#1E1E2E");
        var textInactive = Color.FromArgb("#CDD6F4");

        BtnRutinas.BackgroundColor    = section == 0 ? active : inactive;
        BtnRutinas.TextColor          = section == 0 ? textActive : textInactive;
        BtnDias.BackgroundColor       = section == 1 ? active : inactive;
        BtnDias.TextColor             = section == 1 ? textActive : textInactive;
        BtnEjercicios.BackgroundColor = section == 2 ? active : inactive;
        BtnEjercicios.TextColor       = section == 2 ? textActive : textInactive;
    }

    private void OnRutinasClicked(object? sender, EventArgs e)    => ShowSection(0);
    private void OnDiasClicked(object? sender, EventArgs e)       => ShowSection(1);
    private void OnEjerciciosClicked(object? sender, EventArgs e) => ShowSection(2);

    // ── Compartir e importar (.steel) ─────────────────────────────────────

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (_vm.SelectedRoutine is not { } routine)
        {
            NotificationService.Error("Selecciona una rutina para compartirla.");
            return;
        }

        var estimate = await _sharing.EstimateAsync(routine.Id);
        bool includeMedia = false;
        if (estimate.MediaCount > 0)
        {
            var withVideos = $"Con vídeos ({estimate.MediaCount} · {MediaKinds.FormatSize(estimate.MediaBytes)})";
            const string withoutVideos = "Sin vídeos (solo datos)";
            var choice = await DisplayActionSheetAsync($"Compartir «{routine.Name}»", "Cancelar", null, withVideos, withoutVideos);
            if (choice != withVideos && choice != withoutVideos) return;
            includeMedia = choice == withVideos;
        }

        var message = await DisplayPromptAsync("Mensaje (opcional)", "Lo verá quien abra la rutina.",
            accept: "Compartir", cancel: "Cancelar", placeholder: "Ej. Empezamos el lunes", maxLength: 300);
        if (message is null) return; // cancelado

        ShareButton.IsEnabled = false;
        try
        {
            var folder = Path.Combine(FileSystem.CacheDirectory, "compartir");
            var path = await _sharing.ExportRoutineToFileAsync(routine.Id, routine.Name, includeMedia, message, folder);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Compartir «{routine.Name}»",
                File = new ShareFile(path, "application/octet-stream")
            });
        }
        catch (Exception ex) when (ex is SteelPackageException or IOException)
        {
            NotificationService.Error($"No se pudo crear el archivo: {ex.Message}");
        }
        finally
        {
            ShareButton.IsEnabled = true;
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
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
}
