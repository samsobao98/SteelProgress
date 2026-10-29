using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.Sharing;

// Formato del archivo .steel (docs/DISENO_ENTRENADORES_Y_COMPARTIR.md §6):
// un ZIP con manifest.json, data.json y, si se exporta con vídeos, la carpeta media/.
public static class SteelPackageFormat
{
    public const int CurrentVersion = 1;
    public const string Extension = ".steel";
    public const string ManifestEntry = "manifest.json";
    public const string DataEntry = "data.json";
    public const string MediaFolder = "media/";

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}

public enum PackageKind
{
    Routine,
    Exercises
}

public sealed class PackageManifest
{
    public int FormatVersion { get; set; } = SteelPackageFormat.CurrentVersion;
    public PackageKind Kind { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AuthorName { get; set; }
    public string? Message { get; set; }
    public bool IncludesMedia { get; set; }
}

public sealed class PackageData
{
    public List<PackageExercise> Exercises { get; set; } = new();
    public List<PackageRoutine> Routines { get; set; } = new();
}

public sealed class PackageExercise
{
    public Guid Uid { get; set; }
    public string Name { get; set; } = "";
    public string MuscleGroup { get; set; } = "";
    public string? Notes { get; set; }
    public string? Equipment { get; set; }
    public string? Instructions { get; set; }
    // Etiquetas y autor original (si se reenvía un ejercicio que llegó de otro); null en paquetes antiguos
    public string? Tags { get; set; }
    public string? AuthorName { get; set; }
    public List<PackageMedia> Media { get; set; } = new();
}

public sealed class PackageMedia
{
    public Guid Uid { get; set; }
    public MediaKind Kind { get; set; }
    // Ruta dentro del ZIP ("media/…") o null si se exportó sin vídeos
    public string? File { get; set; }
    public long SizeBytes { get; set; }
    public int Order { get; set; }
    public string? Caption { get; set; }
}

public sealed class PackageRoutine
{
    // Identidad de la rutina original: así el cliente reconoce una versión nueva de la misma rutina
    public Guid Uid { get; set; }
    public string Name { get; set; } = "";
    public string? Notes { get; set; }
    public string? AuthorName { get; set; }
    public int Version { get; set; } = 1;
    public List<PackageDay> Days { get; set; } = new();
}

public sealed class PackageDay
{
    public string Name { get; set; } = "";
    public int Order { get; set; }
    public List<PackageDayExercise> Exercises { get; set; } = new();
}

public sealed class PackageDayExercise
{
    public Guid ExerciseUid { get; set; }
    public int Order { get; set; }
    public int? TargetSets { get; set; }
    public int? TargetReps { get; set; }
    public int? TargetRepsMax { get; set; }
    public double? TargetWeight { get; set; }
    public double? TargetPercent1RM { get; set; }
    public double? TargetRpe { get; set; }
    public int? TargetRir { get; set; }
    public string? Tempo { get; set; }
    public int? RestSeconds { get; set; }
    public int? WarmupSets { get; set; }
    public string? GroupLabel { get; set; }
    public string? Notes { get; set; }
}

public class SteelPackageException(string message) : Exception(message);

// Un .steel abierto para leer. Mantiene el archivo abierto para sacar los vídeos al importar.
public sealed class SteelPackage : IDisposable
{
    private readonly ZipArchive _zip;

    public PackageManifest Manifest { get; }
    public PackageData Data { get; }

    private SteelPackage(ZipArchive zip, PackageManifest manifest, PackageData data)
    {
        _zip = zip;
        Manifest = manifest;
        Data = data;
    }

    public static SteelPackage Open(Stream stream)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            throw new SteelPackageException("El archivo no es un paquete de SteelProgress válido.");
        }

        try
        {
            var manifest = ReadJson<PackageManifest>(zip, SteelPackageFormat.ManifestEntry);
            if (manifest.FormatVersion > SteelPackageFormat.CurrentVersion)
                throw new SteelPackageException("Este archivo se creó con una versión más nueva de SteelProgress. Actualiza la app para abrirlo.");

            var data = ReadJson<PackageData>(zip, SteelPackageFormat.DataEntry);
            return new SteelPackage(zip, manifest, data);
        }
        catch
        {
            zip.Dispose();
            throw;
        }
    }

    public Stream? OpenMedia(PackageMedia media)
    {
        if (media.File is null) return null;
        return _zip.GetEntry(media.File)?.Open();
    }

    public void Dispose() => _zip.Dispose();

    private static T ReadJson<T>(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName) ?? throw new SteelPackageException("El archivo no es un paquete de SteelProgress válido.");
        try
        {
            using var s = entry.Open();
            return JsonSerializer.Deserialize<T>(s, SteelPackageFormat.Json)
                   ?? throw new SteelPackageException("El paquete está vacío o dañado.");
        }
        catch (JsonException)
        {
            throw new SteelPackageException("El paquete está dañado y no se puede leer.");
        }
    }
}
