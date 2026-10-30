using SteelProgress.Data.Context;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.Sharing;

// Crea el archivo .steel que luego se comparte con el menú del sistema
public class RoutineSharingService
{
    private readonly AppDbContext _db;
    private readonly PackageExporter _exporter;
    private readonly ICurrentUserService _currentUser;

    public RoutineSharingService(AppDbContext db, PackageExporter exporter, ICurrentUserService currentUser)
    {
        _db = db;
        _exporter = exporter;
        _currentUser = currentUser;
    }

    public Task<ExportEstimate> EstimateAsync(int routineId) => _exporter.EstimateRoutineAsync(routineId);

    // Devuelve la ruta del archivo creado en `directory`
    public async Task<string> ExportRoutineToFileAsync(int routineId, string routineName, bool includeMedia, string? message, string directory)
    {
        var author = _currentUser.CurrentUserId is int id ? (await _db.UserProfiles.FindAsync(id))?.Name : null;

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, SafeFileName(routineName) + SteelPackageFormat.Extension);

        await using (var file = File.Create(path))
            await _exporter.ExportRoutineAsync(routineId, new ExportOptions(includeMedia, author, message), file);

        return path;
    }

    /// <summary>Un entreno guardado, para enviárselo al entrenador. Devuelve la ruta del archivo.</summary>
    public async Task<string> ExportWorkoutToFileAsync(int sessionId, string fileName, string directory)
    {
        var author = _currentUser.CurrentUserId is int id ? (await _db.UserProfiles.FindAsync(id))?.Name : null;

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, SafeFileName(fileName) + SteelPackageFormat.Extension);

        await using (var file = File.Create(path))
            await _exporter.ExportWorkoutsAsync(new[] { sessionId }, new ExportOptions(false, author), file);

        return path;
    }

    // "Torso / Pierna" → "Torso - Pierna"
    public static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }).ToHashSet();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim(' ', '.', '-');
        return clean.Length == 0 ? "rutina" : clean;
    }
}
