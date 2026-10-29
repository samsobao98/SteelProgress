using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.Sharing;

public enum ImportMode
{
    // Si ya tienes esta rutina, se actualiza sin perder tu historial
    UpdateExisting,
    // Siempre se guarda como una rutina nueva
    SaveAsCopy
}

// Lo que se enseña antes de importar
public sealed record ImportPreview(
    PackageKind Kind,
    string? AuthorName,
    string? Message,
    string Title,
    int DayCount,
    int NewExerciseCount,
    int ExistingExerciseCount,
    int MediaCount,
    long MediaBytes,
    int? ExistingVersion,
    int IncomingVersion)
{
    public bool IsUpdate => ExistingVersion is not null;
    public bool IsNewerVersion => ExistingVersion is int v && IncomingVersion > v;
}

public sealed record ImportResult(int RoutinesCreated, int RoutinesUpdated, int ExercisesCreated, int MediaImported);

public class PackageImporter
{
    private readonly AppDbContext _db;
    private readonly ExerciseMediaService _media;

    public PackageImporter(AppDbContext db, ExerciseMediaService media)
    {
        _db = db;
        _media = media;
    }

    public async Task<ImportPreview> PreviewAsync(SteelPackage package)
    {
        var data = package.Data;
        var uids = data.Exercises.Select(e => e.Uid).ToList();
        var existing = await _db.Exercises.CountAsync(e => uids.Contains(e.Uid));
        var media = data.Exercises.SelectMany(e => e.Media).Where(m => m.File is not null).ToList();

        var routine = data.Routines.FirstOrDefault();
        var mine = routine is null ? null : await FindExistingRoutineAsync(routine.Uid);

        return new ImportPreview(
            package.Manifest.Kind,
            package.Manifest.AuthorName ?? routine?.AuthorName,
            package.Manifest.Message,
            routine?.Name ?? (data.Exercises.Count == 1 ? data.Exercises[0].Name : $"{data.Exercises.Count} ejercicios"),
            routine?.Days.Count ?? 0,
            data.Exercises.Count - existing,
            existing,
            media.Count,
            media.Sum(m => m.SizeBytes),
            mine?.Version,
            routine?.Version ?? 1);
    }

    public async Task<ImportResult> ImportAsync(SteelPackage package, ImportMode mode)
    {
        var storedFiles = new List<string>();
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var (exercisesByUid, created, mediaCount) = await ImportExercisesAsync(package, storedFiles);

            int routinesCreated = 0, routinesUpdated = 0;
            foreach (var pkgRoutine in package.Data.Routines)
            {
                var existing = mode == ImportMode.UpdateExisting ? await FindExistingRoutineAsync(pkgRoutine.Uid) : null;
                if (existing is not null)
                {
                    await UpdateRoutineAsync(existing, pkgRoutine, exercisesByUid);
                    routinesUpdated++;
                }
                else
                {
                    var isCopy = await FindExistingRoutineAsync(pkgRoutine.Uid) is not null;
                    _db.Routines.Add(NewRoutine(pkgRoutine, exercisesByUid, isCopy));
                    routinesCreated++;
                }
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return new ImportResult(routinesCreated, routinesUpdated, created, mediaCount);
        }
        catch
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
            _media.DeleteFiles(storedFiles);
            throw;
        }
    }

    // Rutina del perfil actual que viene de la misma rutina original
    private Task<Routine?> FindExistingRoutineAsync(Guid originUid) =>
        _db.Routines
            .Include(r => r.Days).ThenInclude(d => d.Exercises)
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(r => r.SourceUid == originUid || r.Uid == originUid);

    private async Task<(Dictionary<Guid, Exercise>, int created, int media)> ImportExercisesAsync(SteelPackage package, List<string> storedFiles)
    {
        var uids = package.Data.Exercises.Select(e => e.Uid).ToList();
        var byUid = await _db.Exercises.Include(e => e.Media)
            .Where(e => uids.Contains(e.Uid))
            .ToDictionaryAsync(e => e.Uid);

        int created = 0, mediaCount = 0;
        foreach (var pkg in package.Data.Exercises)
        {
            if (!byUid.TryGetValue(pkg.Uid, out var exercise))
            {
                exercise = new Exercise
                {
                    Uid = pkg.Uid,
                    Name = await UniqueNameAsync(pkg.Name),
                    MuscleGroup = pkg.MuscleGroup,
                    Notes = pkg.Notes,
                    Equipment = pkg.Equipment,
                    EquipmentType = pkg.EquipmentType ?? EquipmentCatalog.Classify(pkg.Equipment, pkg.Name),
                    Instructions = pkg.Instructions,
                    Tags = pkg.Tags,
                    // Su autor original si lo trae (reenviado); si no, quien envía el paquete
                    AuthorName = pkg.AuthorName ?? package.Manifest.AuthorName
                };
                _db.Exercises.Add(exercise);
                byUid[pkg.Uid] = exercise;
                created++;
            }

            // Solo los vídeos que todavía no tiene
            foreach (var pkgMedia in pkg.Media.OrderBy(m => m.Order))
            {
                if (exercise.Media.Any(m => m.Uid == pkgMedia.Uid)) continue;
                await using var source = package.OpenMedia(pkgMedia);
                if (source is null) continue;

                var stored = await _media.StoreAsync(source, pkgMedia.File!);
                storedFiles.Add(stored.FileName);
                stored.Uid = pkgMedia.Uid;
                stored.Kind = pkgMedia.Kind;
                stored.Caption = pkgMedia.Caption;
                stored.Order = exercise.Media.Count;
                exercise.Media.Add(stored);
                mediaCount++;
            }
        }

        return (byUid, created, mediaCount);
    }

    // Si ya tienes un ejercicio con ese nombre (pero es otro), el importado se distingue
    private async Task<string> UniqueNameAsync(string name)
    {
        if (!await _db.Exercises.AnyAsync(e => e.Name == name) && !_db.Exercises.Local.Any(e => e.Name == name)) return name;
        for (var i = 2; ; i++)
        {
            var candidate = $"{name} ({i})";
            if (!await _db.Exercises.AnyAsync(e => e.Name == candidate) && !_db.Exercises.Local.Any(e => e.Name == candidate))
                return candidate;
        }
    }

    private static Routine NewRoutine(PackageRoutine pkg, Dictionary<Guid, Exercise> exercises, bool isCopy)
    {
        var routine = new Routine
        {
            SourceUid = pkg.Uid,
            Name = isCopy ? $"{pkg.Name} (v{pkg.Version})" : pkg.Name,
            Notes = pkg.Notes,
            AuthorName = pkg.AuthorName,
            Version = pkg.Version
        };
        foreach (var pkgDay in pkg.Days.OrderBy(d => d.Order))
            routine.Days.Add(NewDay(pkgDay, exercises));
        return routine;
    }

    private static RoutineDay NewDay(PackageDay pkgDay, Dictionary<Guid, Exercise> exercises)
    {
        var day = new RoutineDay { Name = pkgDay.Name, Order = pkgDay.Order };
        AddDayExercises(day, pkgDay, exercises);
        return day;
    }

    private static void AddDayExercises(RoutineDay day, PackageDay pkgDay, Dictionary<Guid, Exercise> exercises)
    {
        foreach (var x in pkgDay.Exercises.OrderBy(x => x.Order))
        {
            if (!exercises.TryGetValue(x.ExerciseUid, out var exercise)) continue;
            day.Exercises.Add(new RoutineDayExercise
            {
                Exercise = exercise,
                Order = x.Order,
                TargetSets = x.TargetSets,
                TargetReps = x.TargetReps,
                TargetRepsMax = x.TargetRepsMax,
                TargetWeight = x.TargetWeight,
                TargetPercent1RM = x.TargetPercent1RM,
                TargetRpe = x.TargetRpe,
                TargetRir = x.TargetRir,
                Tempo = x.Tempo,
                RestSeconds = x.RestSeconds,
                WarmupSets = x.WarmupSets,
                GroupLabel = x.GroupLabel,
                Notes = x.Notes
            });
        }
    }

    // Actualizar sin perder historial: las sesiones cuelgan de los días, así que los días se
    // reutilizan por orden (cambian nombre y ejercicios) y nunca se borra uno que tenga sesiones.
    private async Task UpdateRoutineAsync(Routine routine, PackageRoutine pkg, Dictionary<Guid, Exercise> exercises)
    {
        routine.Name = pkg.Name;
        routine.Notes = pkg.Notes;
        routine.AuthorName = pkg.AuthorName;
        routine.Version = pkg.Version;
        routine.SourceUid ??= routine.Uid == pkg.Uid ? null : pkg.Uid;

        var existingDays = routine.Days.OrderBy(d => d.Order).ToList();
        var incomingDays = pkg.Days.OrderBy(d => d.Order).ToList();

        for (var i = 0; i < incomingDays.Count; i++)
        {
            if (i < existingDays.Count)
            {
                var day = existingDays[i];
                day.Name = incomingDays[i].Name;
                day.Order = incomingDays[i].Order;
                _db.RoutineDayExercises.RemoveRange(day.Exercises);
                day.Exercises.Clear();
                AddDayExercises(day, incomingDays[i], exercises);
            }
            else
            {
                routine.Days.Add(NewDay(incomingDays[i], exercises));
            }
        }

        // Días que ya no están en la versión nueva: se borran solo si no tienen entrenos guardados
        var nextOrder = incomingDays.Count == 0 ? 1 : incomingDays.Max(d => d.Order) + 1;
        foreach (var extra in existingDays.Skip(incomingDays.Count))
        {
            if (await _db.WorkoutSessions.AnyAsync(s => s.RoutineDayId == extra.Id))
            {
                extra.Order = nextOrder++;
            }
            else
            {
                // Quitarlo también de la lista: si no, EF lo "resucita" al guardar
                routine.Days.Remove(extra);
                _db.RoutineDays.Remove(extra);
            }
        }
    }
}
