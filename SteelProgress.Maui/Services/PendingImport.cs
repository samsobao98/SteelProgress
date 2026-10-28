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

    public static async Task TryOpenAsync()
    {
        if (_path is null || Shell.Current is not AppShell) return;
        var user = IPlatformApplication.Current?.Services.GetService<ICurrentUserService>();
        if (user is not { HasUser: true }) return;

        var path = _path;
        _path = null;
        await Shell.Current.GoToAsync(nameof(ImportPreviewPage), new Dictionary<string, object> { ["path"] = path });
    }
}
