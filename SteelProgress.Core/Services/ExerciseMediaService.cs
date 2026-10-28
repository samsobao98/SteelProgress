using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.Services;

// Dónde viven los archivos multimedia. En la app es FileSystem.AppDataDirectory/media.
public interface IMediaStorage
{
    string RootDirectory { get; }
    string GetFullPath(string fileName);
}

public class FileMediaStorage : IMediaStorage
{
    public FileMediaStorage(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        Directory.CreateDirectory(rootDirectory);
    }

    public string RootDirectory { get; }

    // Solo nombres generados por la app: nunca rutas que salgan de la carpeta
    public string GetFullPath(string fileName) => Path.Combine(RootDirectory, Path.GetFileName(fileName));
}

public static class MediaKinds
{
    public const long DefaultMaxBytes = 50L * 1024 * 1024;

    private static readonly Dictionary<string, MediaKind> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = MediaKind.Image, [".jpeg"] = MediaKind.Image, [".png"] = MediaKind.Image,
        [".webp"] = MediaKind.Image, [".heic"] = MediaKind.Image,
        [".gif"] = MediaKind.Gif,
        [".mp4"] = MediaKind.Video, [".mov"] = MediaKind.Video, [".m4v"] = MediaKind.Video,
        [".3gp"] = MediaKind.Video, [".webm"] = MediaKind.Video,
    };

    public static MediaKind? FromFileName(string fileName) =>
        ByExtension.TryGetValue(Path.GetExtension(fileName), out var kind) ? kind : null;

    public static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024.0):0.#} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}

public class MediaImportException(string message) : Exception(message);

// Copia los archivos elegidos al almacenamiento de la app y los asocia a ejercicios
public class ExerciseMediaService
{
    private readonly IMediaStorage _storage;
    private readonly long _maxBytes;

    public ExerciseMediaService(IMediaStorage storage, long maxBytes = MediaKinds.DefaultMaxBytes)
    {
        _storage = storage;
        _maxBytes = maxBytes;
    }

    public IMediaStorage Storage => _storage;

    // Copia el archivo y devuelve la entidad sin guardar (ExerciseId sin asignar).
    // Si es demasiado grande o de un tipo no admitido, no deja nada en disco.
    public async Task<ExerciseMedia> StoreAsync(Stream source, string originalFileName, CancellationToken ct = default)
    {
        var kind = MediaKinds.FromFileName(originalFileName)
                   ?? throw new MediaImportException("Formato no admitido. Usa un vídeo (mp4, mov), un GIF o una imagen (jpg, png, webp).");

        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(originalFileName).ToLowerInvariant()}";
        var fullPath = _storage.GetFullPath(fileName);
        long total = 0;

        try
        {
            await using var target = File.Create(fullPath);
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                total += read;
                if (total > _maxBytes)
                    throw new MediaImportException($"El archivo supera el máximo de {MediaKinds.FormatSize(_maxBytes)}.");
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        catch
        {
            File.Delete(fullPath);
            throw;
        }

        return new ExerciseMedia { Kind = kind, FileName = fileName, SizeBytes = total };
    }

    // Para archivos copiados que al final no se guardaron (p. ej. se canceló crear el ejercicio)
    public void DeleteFile(ExerciseMedia media)
    {
        var path = _storage.GetFullPath(media.FileName);
        if (File.Exists(path)) File.Delete(path);
    }

    public void DeleteFiles(IEnumerable<string> fileNames)
    {
        foreach (var f in fileNames)
        {
            var path = _storage.GetFullPath(f);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
