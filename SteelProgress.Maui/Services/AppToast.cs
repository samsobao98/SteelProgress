using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace SteelProgress.Maui.Services;

public enum ToastKind { Info, Success, Error, Record }

/// <summary>
/// Aviso breve abajo que desaparece solo (opción A de docs/PLAN_VENTANAS.md: Snackbar del
/// CommunityToolkit con los colores del diseño). Todas las llamadas pasan por aquí, así que
/// cambiarlo por un aviso propio no tocaría las pantallas.
/// </summary>
public static class AppToast
{
    private static ISnackbar? _current;

    public static void Show(string message, ToastKind kind = ToastKind.Info, string? actionText = null, Action? action = null, TimeSpan? duration = null)
        => MainThread.BeginInvokeOnMainThread(async () =>
        {
            var (background, text) = kind switch
            {
                ToastKind.Success => ("#1F3327", "#A6E3A1"),
                ToastKind.Error => ("#3A2030", "#F38BA8"),
                ToastKind.Record => ("#3A3322", "#F9E2AF"),
                _ => ("#2C2D42", "#CDD6F4")
            };

            var options = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb(background),
                TextColor = Color.FromArgb(text),
                ActionButtonTextColor = Color.FromArgb("#B4BEFE"),
                CornerRadius = new CornerRadius(14),
                Font = Microsoft.Maui.Font.OfSize("OpenSansRegular", 14),
                ActionButtonFont = Microsoft.Maui.Font.OfSize("OpenSansSemibold", 14)
            };

            // Los errores se leen con más calma; con acción («Deshacer») también hace falta tiempo
            var time = duration ?? (kind == ToastKind.Error || action is not null ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(2.5));

            // En Windows el Snackbar exige registrar la app para notificaciones del sistema:
            // allí se usa una tarjeta propia abajo que se cierra sola
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                await AppDialogs.ShowToastCardAsync(message, Color.FromArgb(background), Color.FromArgb(text), actionText, action, time);
                return;
            }

            try
            {
                if (_current is not null)
                    await _current.Dismiss();

                _current = Snackbar.Make(message, action, actionText ?? "", time, options);
                await _current.Show();
            }
            catch (Exception)
            {
                // Si el Snackbar falla, el aviso sale como tarjeta propia
                await AppDialogs.ShowToastCardAsync(message, Color.FromArgb(background), Color.FromArgb(text), actionText, action, time);
            }
        });
}
