namespace SteelProgress.Domain.Entities;

/// <summary>
/// DTO de solo lectura para la lista de rutinas: la rutina junto a sus
/// contadores de días y ejercicios, para mostrar un resumen sin navegar.
/// </summary>
public class RoutineSummary
{
    public Routine Routine { get; set; } = null!;
    public int DayCount { get; set; }
    public int ExerciseCount { get; set; }

    public string Name => Routine.Name;
    public string? Notes => Routine.Notes;
    public string Initial =>
        Name.Trim().Length > 0 ? Name.Trim()[0].ToString().ToUpperInvariant() : "?";

    public string SummaryText =>
        $"{DayCount} {(DayCount == 1 ? "día" : "días")} · " +
        $"{ExerciseCount} {(ExerciseCount == 1 ? "ejercicio" : "ejercicios")}";
}
