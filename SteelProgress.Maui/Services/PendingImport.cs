using SteelProgress.Domain.Services;
using SteelProgress.Maui.Pages;

namespace SteelProgress.Maui.Services;

// Un .steel abierto desde otra app (WhatsApp, correo…) espera aquí hasta que haya un perfil elegido:
// importar siempre va al perfil actual.
public static class PendingImport
{
    private static string? _path;

    public static void Set(string path)
    {
        _path = path;
        MainThread.BeginInvokeOnMainThread(() => _ = TryOpenAsync());
    }

    /// <summary>Guarda el archivo hasta que se elija un perfil, sin abrirlo ya en el actual.</summary>
    public static void Hold(string path) => _path = path;

    public static async Task TryOpenAsync()
    {
        if (_path is null || Shell.Current is not AppShell) return;
        var user = IPlatformApplication.Current?.Services.GetService<ICurrentUserService>();
        if (user is not { HasUser: true }) return;

        var path = _path;
        _path = null;
        await OpenAsync(path);
    }

    /// <summary>Abre la vista previa que toca: entrenos de un cliente, o rutinas y ejercicios.</summary>
    public static Task OpenAsync(string path)
    {
        var page = SteelProgress.Core.Sharing.SteelPackage.PeekKind(path) == SteelProgress.Core.Sharing.PackageKind.Workouts
            ? nameof(WorkoutImportPage)
            : nameof(ImportPreviewPage);
        return Shell.Current.GoToAsync(page, new Dictionary<string, object> { ["path"] = path });
    }
}
