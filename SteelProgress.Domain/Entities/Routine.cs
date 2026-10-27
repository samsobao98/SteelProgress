namespace SteelProgress.Domain.Entities;

public class Routine
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public UserProfile? UserProfile { get; set; }

    public Guid Uid { get; set; } = Guid.NewGuid();

    // Rutina preparada para un cliente del entrenador (null = rutina propia)
    public int? ClientId { get; set; }
    public Client? Client { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }

    // Autoría y versión: quién la creó, de qué rutina original viene y qué versión es
    public string? AuthorName { get; set; }
    public Guid? SourceUid { get; set; }
    public int Version { get; set; } = 1;

    public ICollection<RoutineDay> Days { get; set; } = new List<RoutineDay>();
}