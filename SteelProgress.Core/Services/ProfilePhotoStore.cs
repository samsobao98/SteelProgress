namespace SteelProgress.Core.Services;

/// <summary>
/// Fotos de perfil: una por perfil, en su propia carpeta (en la app, FileSystem.AppDataDirectory/profiles).
/// Cada foto nueva lleva un nombre nuevo, así ninguna pantalla enseña la anterior guardada en caché.
/// Mientras se edita, la foto elegida se guarda como «pendiente» y solo pasa a ser la del perfil al guardar.
/// </summary>
public class ProfilePhotoStore
{
    public ProfilePhotoStore(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        Directory.CreateDirectory(rootDirectory);
    }

    public string RootDirectory { get; }

    /// <summary>Ruta completa de la foto, o null si no hay o el archivo ya no está.</summary>
    public string? PathOf(string? photoFile)
    {
        if (string.IsNullOrWhiteSpace(photoFile)) return null;
        // Solo nombres generados aquí: nunca rutas que salgan de la carpeta
        var path = Path.Combine(RootDirectory, Path.GetFileName(photoFile));
        return File.Exists(path) ? path : null;
    }

    /// <summary>Guarda una foto recién elegida, todavía sin asignar a ningún perfil. Devuelve su nombre.</summary>
    public async Task<string> SavePendingAsync(Stream photo)
    {
        var fileName = $"pending_{Guid.NewGuid():N}.jpg";
        await using var file = File.Create(Path.Combine(RootDirectory, fileName));
        await photo.CopyToAsync(file);
        return fileName;
    }

    /// <summary>Convierte una foto pendiente en la del perfil. Devuelve el nombre definitivo.</summary>
    public string Commit(int profileId, string pendingFile)
    {
        var fileName = $"profile_{profileId}_{Guid.NewGuid():N}.jpg";
        File.Move(Path.Combine(RootDirectory, Path.GetFileName(pendingFile)), Path.Combine(RootDirectory, fileName));
        return fileName;
    }

    /// <summary>Borra un archivo de foto (la anterior de un perfil, una pendiente descartada…). Sin error si no está.</summary>
    public void Delete(string? photoFile)
    {
        if (string.IsNullOrWhiteSpace(photoFile)) return;
        try
        {
            File.Delete(Path.Combine(RootDirectory, Path.GetFileName(photoFile)));
        }
        catch (IOException)
        {
            // Si otra pantalla la tiene abierta, se queda huérfana; no pasa nada
        }
    }
}
