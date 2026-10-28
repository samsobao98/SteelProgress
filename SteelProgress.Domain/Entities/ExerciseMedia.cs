namespace SteelProgress.Domain.Entities;

public enum MediaKind
{
    Image = 0,
    Gif = 1,
    Video = 2
}

// Vídeo, GIF o imagen propios de un ejercicio, importados de la galería.
// El archivo vive en el almacenamiento privado de la app; aquí solo se guarda su nombre relativo.
public class ExerciseMedia
{
    public int Id { get; set; }
    public Guid Uid { get; set; } = Guid.NewGuid();

    public int ExerciseId { get; set; }
    public Exercise? Exercise { get; set; }

    public MediaKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int Order { get; set; }
    public string? Caption { get; set; }
}
