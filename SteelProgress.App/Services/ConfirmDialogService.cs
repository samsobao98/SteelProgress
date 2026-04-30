namespace SteelProgress.App.Services;

public static class ConfirmDialogService
{
    public static event Func<string, string, Task<bool>>? OnConfirmationRequested;

    public static async Task<bool> ConfirmAsync(string title, string message)
    {
        if (OnConfirmationRequested is null)
            return false;

        return await OnConfirmationRequested.Invoke(title, message);
    }
}
