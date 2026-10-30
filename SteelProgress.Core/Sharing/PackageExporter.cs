using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.Sharing;

public sealed record ExportOptions(bool IncludeMedia, string? AuthorName = null, string? Message = null);

// Lo que pesará el archivo, para mostrarlo antes de elegir con o sin vídeos
public sealed record ExportEstimate(int ExerciseCount, int MediaCount, long MediaBytes);

public class PackageExporter
{
    private readonly AppDbContext _db;
    private readonly IMediaStorage _storage;

    public PackageExporter(AppDbContext db, IMediaStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<ExportEstimate> EstimateRoutineAsync(int routineId)
    {
        var exercises = await ExercisesOfRoutineAsync(routineId);
        var media = exercises.SelectMany(e => e.Media).ToList();
        return new ExportEstimate(exercises.Count, media.Count, media.Sum(m => m.SizeBytes));
    }

    public async Task ExportRoutineAsync(int routineId, ExportOptions options, Stream output)
    {
        var routine = await _db.Routines
            .Include(r => r.Days.OrderBy(d => d.Order))
                .ThenInclude(d => d.Exercises.OrderBy(x => x.Order))
            .FirstOrDefaultAsync(r => r.Id == routineId)
            ?? throw new SteelPackageException("La rutina no existe.");

        var exercises = await ExercisesOfRoutineAsync(routineId);
        var byId = exercises.ToDictionary(e => e.Id);

        var data = new PackageData
        {
            Exercises = exercises.Select(e => ToPackage(e, options.IncludeMedia)).ToList(),
            Routines =
            {
                new PackageRoutine
                {
                    Uid = routine.SourceUid ?? routine.Uid,
                    Name = routine.Name,
                    Notes = routine.Notes,
                    AuthorName = options.AuthorName ?? routine.AuthorName,
                    Version = routine.Version,
                    Days = routine.Days.OrderBy(d => d.Order).Select(d => new PackageDay
                    {
                        Name = d.Name,
                        Order = d.Order,
                        Exercises = d.Exercises.OrderBy(x => x.Order)
                            .Where(x => byId.ContainsKey(x.ExerciseId))
                            .Select(x => ToPackage(x, byId[x.ExerciseId].Uid)).ToList()
                    }).ToList()
                }
            }
        };

        await WriteAsync(PackageKind.Routine, options, data, exercises, output);
    }

    public async Task ExportExercisesAsync(IReadOnlyCollection<int> exerciseIds, ExportOptions options, Stream output)
    {
        var exercises = await _db.Exercises
            .Include(e => e.Media.OrderBy(m => m.Order))
            .Where(e => exerciseIds.Contains(e.Id))
            .OrderBy(e => e.Name)
            .ToListAsync();

        var data = new PackageData { Exercises = exercises.Select(e => ToPackage(e, options.IncludeMedia)).ToList() };
        await WriteAsync(PackageKind.Exercises, options, data, exercises, output);
    }

    /// <summary>Entrenos guardados, para enviárselos al entrenador (sin vídeos).</summary>
    public async Task ExportWorkoutsAsync(IReadOnlyCollection<int> sessionIds, ExportOptions options, Stream output)
    {
        var sessions = await _db.WorkoutSessions
            .Include(s => s.RoutineDay!).ThenInclude(d => d.Routine)
            .Include(s => s.Exercises).ThenInclude(e => e.Sets)
            .Include(s => s.Exercises).ThenInclude(e => e.Exercise)
            .Where(s => sessionIds.Contains(s.Id))
            .OrderBy(s => s.Date)
            .ToListAsync();
        if (sessions.Count == 0) throw new SteelPackageException("El entreno no existe.");

        var exercises = sessions.SelectMany(s => s.Exercises).Select(e => e.Exercise!).DistinctBy(e => e.Id).OrderBy(e => e.Name).ToList();
        var data = new PackageData
        {
            Exercises = exercises.Select(e => ToPackage(e, includeMedia: false)).ToList(),
            Workouts = sessions.Select(s => new PackageWorkout
            {
                Uid = s.Uid,
                RoutineUid = s.RoutineDay?.Routine is { } r ? r.SourceUid ?? r.Uid : null,
                RoutineName = s.RoutineDay?.Routine?.Name ?? "",
                DayName = s.RoutineDay?.Name ?? "Entreno",
                DayOrder = s.RoutineDay?.Order ?? 0,
                Date = s.Date,
                DurationMinutes = s.DurationMinutes,
                Feeling = s.Feeling,
                Notes = s.Notes,
                IsComplete = s.IsComplete,
                Exercises = s.Exercises.OrderBy(e => e.Id).Select(e => new PackageWorkoutExercise
                {
                    ExerciseUid = e.Exercise!.Uid,
                    Sets = e.Sets.OrderBy(x => x.Id).Select(x => new PackageSet
                    {
                        Weight = x.Weight, Reps = x.Reps, IsWarmup = x.IsWarmup, Rpe = x.Rpe, Notes = x.Notes
                    }).ToList()
                }).ToList()
            }).ToList()
        };

        await WriteAsync(PackageKind.Workouts, options with { IncludeMedia = false }, data, exercises, output);
    }

    private async Task<List<Exercise>> ExercisesOfRoutineAsync(int routineId)
    {
        var ids = await _db.RoutineDayExercises
            .Where(x => x.RoutineDay!.RoutineId == routineId)
            .Select(x => x.ExerciseId)
            .Distinct()
            .ToListAsync();

        return await _db.Exercises
            .Include(e => e.Media.OrderBy(m => m.Order))
            .Where(e => ids.Contains(e.Id))
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    private async Task WriteAsync(PackageKind kind, ExportOptions options, PackageData data, List<Exercise> exercises, Stream output)
    {
        var manifest = new PackageManifest
        {
            Kind = kind,
            CreatedAt = DateTime.UtcNow,
            AuthorName = options.AuthorName,
            Message = string.IsNullOrWhiteSpace(options.Message) ? null : options.Message.Trim(),
            IncludesMedia = options.IncludeMedia && exercises.Any(e => e.Media.Count > 0)
        };

        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        await WriteJsonAsync(zip, SteelPackageFormat.ManifestEntry, manifest);
        await WriteJsonAsync(zip, SteelPackageFormat.DataEntry, data);

        if (!options.IncludeMedia) return;

        foreach (var media in exercises.SelectMany(e => e.Media))
        {
            var path = _storage.GetFullPath(media.FileName);
            if (!File.Exists(path)) continue;

            // Los vídeos ya vienen comprimidos: guardarlos sin comprimir es más rápido y pesa lo mismo
            var entry = zip.CreateEntry(SteelPackageFormat.MediaFolder + media.FileName, CompressionLevel.NoCompression);
            await using var target = entry.Open();
            await using var source = File.OpenRead(path);
            await source.CopyToAsync(target);
        }
    }

    private PackageExercise ToPackage(Exercise e, bool includeMedia) => new()
    {
        Uid = e.Uid,
        Name = e.Name,
        MuscleGroup = e.MuscleGroup,
        Notes = e.Notes,
        Equipment = e.Equipment,
        EquipmentType = e.EquipmentType,
        Instructions = e.Instructions,
        Tags = e.Tags,
        AuthorName = e.AuthorName,
        Media = includeMedia
            ? e.Media.OrderBy(m => m.Order)
                .Where(m => File.Exists(_storage.GetFullPath(m.FileName)))
                .Select(m => new PackageMedia
                {
                    Uid = m.Uid, Kind = m.Kind, File = SteelPackageFormat.MediaFolder + m.FileName,
                    SizeBytes = m.SizeBytes, Order = m.Order, Caption = m.Caption
                }).ToList()
            : new List<PackageMedia>()
    };

    private static PackageDayExercise ToPackage(RoutineDayExercise x, Guid exerciseUid) => new()
    {
        ExerciseUid = exerciseUid,
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
    };

    private static async Task WriteJsonAsync<T>(ZipArchive zip, string name, T value)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var s = entry.Open();
        await JsonSerializer.SerializeAsync(s, value, SteelPackageFormat.Json);
    }
}
