namespace SteelProgress.Domain.Entities;

public class UserProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#89B4FA";
    public string? Description { get; set; }

    // Foto del avatar: nombre del archivo en la carpeta de fotos de perfil (ProfilePhotoStore).
    // Null = la inicial sobre el color.
    public string? PhotoFile { get; set; }

    // Personal o de entrenador: se elige al crear el perfil
    public ProfileKind Kind { get; set; } = ProfileKind.Personal;

    // Vista simple o detallada; cada perfil tiene la suya
    public ProfileViewMode ViewMode { get; set; } = ProfileViewMode.Simple;

    // Bloques de edición avanzada que se muestran también en la vista simple
    public SimpleViewBlocks SimpleViewBlocks { get; set; } = SimpleViewBlocks.None;

    // Ya no se usa (2026-10-30): «Yo también entreno» se quitó al separar el tipo de perfil de la vista.
    // La columna se queda para no rehacer la tabla en SQLite.
    public bool TrainsSelf { get; set; }

    // Tu gimnasio: discos que hay (kg separados por «;», p. ej. «25;20;15;10;5;2.5») y lo que pesa la barra.
    // Null = lo de siempre (GymSetup.Default). Sirve para enseñar cómo cargar la barra.
    public string? GymPlates { get; set; }
    public double? BarWeight { get; set; }

    public ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
    public ICollection<Routine> Routines { get; set; } = new List<Routine>();
    public ICollection<WorkoutSession> WorkoutSessions { get; set; } = new List<WorkoutSession>();
}
