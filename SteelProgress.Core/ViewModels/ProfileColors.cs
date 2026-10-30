namespace SteelProgress.Core.ViewModels;

/// <summary>Colores de avatar que se pueden elegir al crear o editar un perfil.</summary>
public static class ProfileColors
{
    public static IReadOnlyList<string> All { get; } = new[]
    {
        "#89B4FA", "#A6E3A1", "#F38BA8", "#FAB387", "#F9E2AF",
        "#CBA6F7", "#94E2D5", "#89DCEB"
    };
}
