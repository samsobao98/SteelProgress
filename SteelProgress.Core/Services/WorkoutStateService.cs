namespace SteelProgress.Core.Services;

public static class WorkoutStateService
{
    public static bool IsWorkoutActive { get; set; }

    /// <summary>La sesión del entreno en curso (las listas la marcan «En curso», no «Incompleto»).</summary>
    public static int? ActiveSessionId { get; set; }
}
