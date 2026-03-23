namespace SteelProgress.Domain.Entities;

public class RoutineDayExercise
{
    public int Id { get; set; }

    public int RoutineDayId { get; set; }

    public int ExerciseId { get; set; }

    public int Order { get; set; }

    public int? TargetSets { get; set; }

    public int? TargetReps { get; set; }

    public RoutineDay? RoutineDay { get; set; }

    public Exercise? Exercise { get; set; }
}