using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.Sharing;

/// <summary>Un entreno recibido, antes de guardarlo.</summary>
public sealed record ReceivedWorkout(PackageWorkout Workout, int SetCount, IReadOnlyList<(string Exercise, string Sets)> Lines, bool IsUpdate);

/// <summary>
/// Lo que trae un paquete de entrenos: de quién son (si se reconoce la rutina que le pasaste) y cada entreno.
/// </summary>
public sealed record WorkoutImportPreview(string? AthleteName, Client? Client, IReadOnlyList<ReceivedWorkout> Workouts);

/// <summary>
/// Entrenos que un cliente le envía a su entrenador (maqueta del 2026-10-30). Se guardan en el historial
/// del cliente, en el día de la rutina que le pasaste; si alguno ya estaba (lo ha reenviado), se actualiza.
/// </summary>
public class WorkoutImporter
{
    private readonly AppDbContext _db;

    public WorkoutImporter(AppDbContext db)
    {
        _db = db;
    }

    public async Task<WorkoutImportPreview> PreviewAsync(SteelPackage package, int coachId)
    {
        if (package.Manifest.Kind != PackageKind.Workouts || package.Data.Workouts.Count == 0)
            throw new SteelPackageException("Este archivo no trae ningún entreno.");

        var names = package.Data.Exercises.ToDictionary(e => e.Uid, e => e.Name);
        var uids = package.Data.Workouts.Select(w => w.Uid).ToList();
        var existing = await _db.WorkoutSessions.IgnoreQueryFilters()
            .Where(s => s.UserId == coachId && s.SourceUid != null && uids.Contains(s.SourceUid.Value))
            .Select(s => s.SourceUid!.Value)
            .ToListAsync();

        var client = await MatchClientAsync(package, coachId);
        var workouts = package.Data.Workouts.OrderBy(w => w.Date).Select(w =>
        {
            var lines = w.Exercises
                .Select(e => (Exercise: names.GetValueOrDefault(e.ExerciseUid, "Ejercicio"), Sets: SetsText(e.Sets)))
                .Where(l => l.Sets.Length > 0)
                .ToList();
            return new ReceivedWorkout(w, w.Exercises.Sum(e => e.Sets.Count(s => !s.IsWarmup)), lines, existing.Contains(w.Uid));
        }).ToList();

        return new WorkoutImportPreview(package.Manifest.AuthorName, client, workouts);
    }

    /// <summary>
    /// Guarda los entrenos en el cliente indicado (el reconocido, o el que elija el entrenador si no se
    /// reconoció la rutina). Devuelve cuántos se guardaron.
    /// </summary>
    public async Task<int> ImportAsync(SteelPackage package, int coachId, int clientId)
    {
        var client = await _db.Clients.IgnoreQueryFilters().FirstAsync(c => c.Id == clientId && c.UserId == coachId);
        var exerciseIds = await MapExercisesAsync(package, coachId);

        foreach (var w in package.Data.Workouts)
        {
            var dayId = await DayForAsync(w, coachId, client);
            var session = await _db.WorkoutSessions.IgnoreQueryFilters()
                .Include(s => s.Exercises)
                .FirstOrDefaultAsync(s => s.UserId == coachId && s.SourceUid == w.Uid);
            if (session is null)
            {
                session = new WorkoutSession { UserId = coachId, ClientId = client.Id, IsInPerson = false, SourceUid = w.Uid };
                _db.WorkoutSessions.Add(session);
            }
            else
            {
                // Reenviado: lo de antes se sustituye por lo nuevo
                _db.WorkoutExercises.RemoveRange(session.Exercises);
                session.Exercises.Clear();
            }

            session.ClientId = client.Id;
            session.RoutineDayId = dayId;
            session.Date = w.Date;
            session.DurationMinutes = w.DurationMinutes;
            session.Feeling = w.Feeling;
            session.Notes = w.Notes;
            session.IsComplete = w.IsComplete;
            foreach (var e in w.Exercises.Where(e => exerciseIds.ContainsKey(e.ExerciseUid)))
            {
                var we = new WorkoutExercise { ExerciseId = exerciseIds[e.ExerciseUid] };
                foreach (var s in e.Sets)
                    we.Sets.Add(new WorkoutSet { Weight = s.Weight, Reps = s.Reps, IsWarmup = s.IsWarmup, Rpe = s.Rpe, Notes = s.Notes });
                session.Exercises.Add(we);
            }
        }

        await _db.SaveChangesAsync();
        return package.Data.Workouts.Count;
    }

    /// <summary>Los clientes del entrenador, por si hay que elegir a quién pertenece.</summary>
    public Task<List<Client>> ClientsAsync(int coachId) =>
        _db.Clients.IgnoreQueryFilters().Where(c => c.UserId == coachId).OrderBy(c => c.Name).ToListAsync();

    // ── Cómo se reconoce cada cosa ──

    // Las rutinas de los clientes, sin los filtros del cliente activo
    private IQueryable<Routine> ClientRoutines(int coachId) =>
        _db.Routines.IgnoreQueryFilters().Where(r => r.UserId == coachId && r.ClientId != null);

    private async Task<Client?> MatchClientAsync(SteelPackage package, int coachId)
    {
        foreach (var uid in package.Data.Workouts.Select(w => w.RoutineUid).OfType<Guid>().Distinct())
        {
            var clientId = await ClientRoutines(coachId)
                .Where(r => r.Uid == uid || r.SourceUid == uid)
                .Select(r => r.ClientId)
                .FirstOrDefaultAsync();
            if (clientId is int id)
                return await _db.Clients.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id);
        }
        return null;
    }

    /// <summary>
    /// El día de la rutina que le pasaste (por posición y nombre). Si la rutina no es tuya, una con su
    /// nombre en la ficha del cliente, con ese día, para no perder el entreno.
    /// </summary>
    private async Task<int> DayForAsync(PackageWorkout w, int coachId, Client client)
    {
        var routine = w.RoutineUid is Guid uid
            ? await ClientRoutines(coachId).Include(r => r.Days)
                .FirstOrDefaultAsync(r => r.ClientId == client.Id && (r.Uid == uid || r.SourceUid == uid))
            : null;
        routine ??= await ClientRoutines(coachId).Include(r => r.Days)
            .FirstOrDefaultAsync(r => r.ClientId == client.Id && r.Name == RoutineNameOf(w));
        if (routine is null)
        {
            routine = new Routine { UserId = coachId, ClientId = client.Id, Name = RoutineNameOf(w) };
            _db.Routines.Add(routine);
        }

        var day = routine.Days.FirstOrDefault(d => d.Order == w.DayOrder && d.Name == w.DayName)
                  ?? routine.Days.FirstOrDefault(d => d.Name == w.DayName)
                  ?? routine.Days.FirstOrDefault(d => d.Order == w.DayOrder);
        if (day is null)
        {
            day = new RoutineDay { Name = w.DayName, Order = routine.Days.Count == 0 ? 1 : routine.Days.Max(d => d.Order) + 1 };
            routine.Days.Add(day);
        }
        if (day.Id == 0) await _db.SaveChangesAsync();
        return day.Id;
    }

    private static string RoutineNameOf(PackageWorkout w) => string.IsNullOrWhiteSpace(w.RoutineName) ? "Entrenos enviados" : w.RoutineName;

    /// <summary>
    /// Cada ejercicio del paquete con uno tuyo: el mismo (por identidad), uno de la biblioteca o tuyo con
    /// el mismo nombre, o uno nuevo con su nombre si no tienes ninguno así.
    /// </summary>
    private async Task<Dictionary<Guid, int>> MapExercisesAsync(SteelPackage package, int coachId)
    {
        var mine = await _db.Exercises.IgnoreQueryFilters()
            .Where(e => e.IsSystem || e.UserId == coachId)
            .ToListAsync();
        var map = new Dictionary<Guid, int>();
        foreach (var pkg in package.Data.Exercises)
        {
            var match = mine.FirstOrDefault(e => e.Uid == pkg.Uid && (e.UserId == coachId || e.IsSystem))
                        ?? mine.FirstOrDefault(e => e.IsSystem && string.Equals(e.Name, pkg.Name, StringComparison.CurrentCultureIgnoreCase))
                        ?? mine.FirstOrDefault(e => string.Equals(e.Name, pkg.Name, StringComparison.CurrentCultureIgnoreCase));
            if (match is null)
            {
                match = new Exercise
                {
                    Uid = pkg.Uid, Name = pkg.Name, MuscleGroup = pkg.MuscleGroup, Equipment = pkg.Equipment,
                    EquipmentType = pkg.EquipmentType ?? EquipmentType.Other, Instructions = pkg.Instructions,
                    UserId = coachId, AuthorName = pkg.AuthorName ?? package.Manifest.AuthorName
                };
                _db.Exercises.Add(match);
                mine.Add(match);
            }
            if (match.Id == 0) await _db.SaveChangesAsync();
            map[pkg.Uid] = match.Id;
        }
        return map;
    }

    /// <summary>«165×10 · 165×9 · 165×7»: las series de trabajo, sin calentamientos.</summary>
    public static string SetsText(IEnumerable<PackageSet> sets) =>
        string.Join(" · ", sets.Where(s => !s.IsWarmup).Select(s => $"{ViewModels.WorkoutExerciseState.FormatKg(s.Weight)}×{s.Reps}"));
}
