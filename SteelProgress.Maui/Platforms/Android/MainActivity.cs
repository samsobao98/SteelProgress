using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using SteelProgress.Maui.Services;

namespace SteelProgress.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ResizeableActivity = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// SingleTask: al abrir un archivo con la app ya abierta, Android reutiliza esta actividad (OnNewIntent).
// Con SingleTop abría otra encima, con su propia ventana, y Shell.Current dejaba de saber cuál usar.
// Abrir un .steel desde otra app (WhatsApp, correo, Drive…). Los .steel no tienen un tipo propio,
// así que llegan como binario genérico o ZIP; la vista previa avisa si no es un paquete válido.
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable }, DataScheme = "content", DataMimeType = "application/octet-stream")]
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable }, DataScheme = "content", DataMimeType = "application/zip")]
[IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "application/octet-stream")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleIntent(intent);
    }

    // Copia el archivo recibido a la caché y lo deja pendiente de importar
    private void HandleIntent(Intent? intent)
    {
        if (intent is null) return;

        Android.Net.Uri? uri = intent.Action switch
        {
            Intent.ActionView => intent.Data,
#pragma warning disable CA1422, CS0618 // la versión tipada solo existe en Android 13+
            Intent.ActionSend => intent.GetParcelableExtra(Intent.ExtraStream) as Android.Net.Uri,
#pragma warning restore CA1422, CS0618
            _ => null
        };
        if (uri is null) return;

        try
        {
            var path = Path.Combine(FileSystem.CacheDirectory, "recibido.steel");
            using (var source = ContentResolver?.OpenInputStream(uri))
            {
                if (source is null) return;
                using var target = File.Create(path);
                source.CopyTo(target);
            }
            PendingImport.Set(path);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("SteelProgress", $"No se pudo abrir el archivo recibido: {ex.Message}");
        }
    }
}
