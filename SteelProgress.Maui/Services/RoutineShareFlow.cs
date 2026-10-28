using SteelProgress.Core.Services;
using SteelProgress.Core.Sharing;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Services;

// Compartir una rutina como .steel: con o sin vídeos, mensaje opcional y menú de compartir del móvil.
// Lo usan el detalle de la rutina y el menú ⋮ de la lista.
public static class RoutineShareFlow
{
    public static async Task ShareAsync(Page page, RoutineSharingService sharing, Routine routine, VisualElement? button = null)
    {
        var estimate = await sharing.EstimateAsync(routine.Id);
        bool includeMedia = false;
        if (estimate.MediaCount > 0)
        {
            var withVideos = $"Con vídeos ({estimate.MediaCount} · {MediaKinds.FormatSize(estimate.MediaBytes)})";
            const string withoutVideos = "Sin vídeos (solo datos)";
            var choice = await page.DisplayActionSheetAsync($"Compartir «{routine.Name}»", "Cancelar", null, withVideos, withoutVideos);
            if (choice != withVideos && choice != withoutVideos) return;
            includeMedia = choice == withVideos;
        }

        var message = await page.DisplayPromptAsync("Mensaje (opcional)", "Lo verá quien abra la rutina.",
            accept: "Compartir", cancel: "Cancelar", placeholder: "Ej. Empezamos el lunes", maxLength: 300);
        if (message is null) return; // cancelado

        if (button is not null) button.IsEnabled = false;
        try
        {
            var folder = Path.Combine(FileSystem.CacheDirectory, "compartir");
            var path = await sharing.ExportRoutineToFileAsync(routine.Id, routine.Name, includeMedia, message, folder);
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
            if (button is not null) button.IsEnabled = true;
        }
    }
}
