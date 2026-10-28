namespace SteelProgress.Domain.Entities;

public class RoutineDayExercise
{
    public int Id { get; set; }

    public Guid Uid { get; set; } = Guid.NewGuid();

    public int RoutineDayId { get; set; }

    public int ExerciseId { get; set; }

    public int Order { get; set; }

    public int? TargetSets { get; set; }

    // Repeticiones objetivo; con TargetRepsMax se convierte en rango (TargetReps–TargetRepsMax)
    public int? TargetReps { get; set; }

    public int? TargetRepsMax { get; set; }

    // Intensidad: peso fijo o porcentaje del 1RM, y esfuerzo percibido (RPE) o repeticiones en reserva (RIR)
    public double? TargetWeight { get; set; }

    public double? TargetPercent1RM { get; set; }

    public double? TargetRpe { get; set; }

    public int? TargetRir { get; set; }

    // Tempo en texto libre, p. ej. "3-1-1-0"
    public string? Tempo { get; set; }

    public int? RestSeconds { get; set; }

    public int? WarmupSets { get; set; }

    // Superseries: los ejercicios con la misma letra se hacen seguidos ("A1", "A2"...)
    public string? GroupLabel { get; set; }

    public string? Notes { get; set; }

    public RoutineDay? RoutineDay { get; set; }

    public Exercise? Exercise { get; set; }
}
