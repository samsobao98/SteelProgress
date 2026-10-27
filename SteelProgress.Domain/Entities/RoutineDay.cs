namespace SteelProgress.Domain.Entities;

public class RoutineDay
{
    public int Id { get; set; }

    public Guid Uid { get; set; } = Guid.NewGuid();

    public int RoutineId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public Routine? Routine { get; set; }

    public ICollection<RoutineDayExercise> Exercises { get; set; } = new List<RoutineDayExercise>();
}