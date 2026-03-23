namespace SteelProgress.Domain.Entities;

public class Routine
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public ICollection<RoutineDay> Days { get; set; } = new List<RoutineDay>();
}