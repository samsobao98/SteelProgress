namespace SteelProgress.Core.Services;

/// <summary>Pregunta del tipo «¿Eliminar…?»; Destructive pinta el botón en rojo.</summary>
public sealed record ConfirmRequest(string Title, string Message, string Accept = "Sí", string Cancel = "Cancelar", bool Destructive = false);

public static class ConfirmDialogService
{
    public static event Func<ConfirmRequest, Task<bool>>? OnConfirmationRequested;

    public static async Task<bool> ConfirmAsync(string title, string message, string accept = "Sí", string cancel = "Cancelar", bool destructive = false)
    {
        if (OnConfirmationRequested is null)
            return false;

        return await OnConfirmationRequested.Invoke(new ConfirmRequest(title, message, accept, cancel, destructive));
    }
}
