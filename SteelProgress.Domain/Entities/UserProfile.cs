namespace SteelProgress.Domain.Entities;

public class UserProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#89B4FA";
    public string? Description { get; set; }

    // Vista simple o de entrenador; cada perfil tiene la suya
    public ProfileViewMode ViewMode { get; set; } = ProfileViewMode.Simple;

    // Bloques de edición avanzada que se muestran también en la vista simple
    public SimpleViewBlocks SimpleViewBlocks { get; set; } = SimpleViewBlocks.None;

    // Solo en vista entrenador: si el perfil también registra sus propios entrenos
    public bool TrainsSelf { get; set; }

    public ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
    public ICollection<Routine> Routines { get; set; } = new List<Routine>();
    public ICollection<WorkoutSession> WorkoutSessions { get; set; } = new List<WorkoutSession>();
}
