namespace SteelProgress.Domain.Entities;

// Cliente de un entrenador. Cuelga del perfil del entrenador (UserId), no es un perfil de la app.
public class Client
{
    public int Id { get; set; }
    public Guid Uid { get; set; } = Guid.NewGuid();

    public int UserId { get; set; }
    public UserProfile? UserProfile { get; set; }

    public string Name { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#FAB387";
    public string? Goal { get; set; }
    public string? Notes { get; set; }
    // Días de la semana que suele entrenar, un bit por día (lunes = 1, martes = 2 … domingo = 64).
    // 0 = sin indicar: los avisos del Inicio usan entonces los plazos fijos.
    public int TrainingDays { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Routine> Routines { get; set; } = new List<Routine>();
    public ICollection<WorkoutSession> WorkoutSessions { get; set; } = new List<WorkoutSession>();
}
