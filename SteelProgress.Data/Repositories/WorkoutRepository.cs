using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Data.Repositories;

public class WorkoutRepository //Busca RoutineDay, crea una WorkoutSession, copia todos los ejercicios del día y cre la WorkoutExercise de esa sesión
{
    private readonly AppDbContext _context;

    public WorkoutRepository(AppDbContext context)
    {
        _context = context;
    }
    public async Task<WorkoutSession?> GetSessionByIdAsync(int sessionId)
    {
        return await _context.WorkoutSessions
            .Include(ws => ws.RoutineDay)
            .Include(ws => ws.Exercises)
                .ThenInclude(we => we.Exercise)
                    .ThenInclude(e => e!.Media)
            .Include(ws => ws.Exercises)
                .ThenInclude(we => we.Sets)
            .FirstOrDefaultAsync(ws => ws.Id == sessionId);
    }

    public async Task<WorkoutSession> CreateSessionFromRoutineDayAsync(int routineDayId)
    {
        var routineDay = await _context.RoutineDays
            .Include(rd => rd.Exercises)
            .FirstOrDefaultAsync(rd => rd.Id == routineDayId);

        if (routineDay is null)
        {
            throw new InvalidOperationException("No se encontró el día de rutina.");

        }

        var session = new WorkoutSession
        {
            Date = DateTime.Now,
            RoutineDayId = routineDayId,
            // Incompleta hasta que se guarda desde el resumen: un entreno abandonado no cuenta como terminado
            IsComplete = false
        };

        await _context.WorkoutSessions.AddAsync(session);
        await _context.SaveChangesAsync();

        var workoutExercises = routineDay.Exercises
            .OrderBy(e => e.Order)
            .Select(e => new WorkoutExercise
            {
                WorkoutSessionId = session.Id,
                ExerciseId = e.ExerciseId
            })
            .ToList();

        await _context.WorkoutExercises.AddRangeAsync(workoutExercises);
        await _context.SaveChangesAsync();

        return session;
    }

    public async Task<List<WorkoutExercise>> GetExercisesBySessionIdAsync(int workoutSessionId)
    {
        return await _context.WorkoutExercises
            .Include(we => we.Exercise)
            .Include(we => we.Sets)
            .Where(we => we.WorkoutSessionId == workoutSessionId)
            .ToListAsync();
    }

    public async Task DeleteSessionAsync(WorkoutSession session)
    {
        var sessionToDelete = await _context.WorkoutSessions
            .Include(ws => ws.Exercises)
                .ThenInclude(we => we.Sets)
            .FirstOrDefaultAsync(ws => ws.Id == session.Id);

        if (sessionToDelete is null)
            return;

        foreach (var exercise in sessionToDelete.Exercises)
        {
            _context.WorkoutSets.RemoveRange(exercise.Sets);
        }

        _context.WorkoutExercises.RemoveRange(sessionToDelete.Exercises);
        _context.WorkoutSessions.Remove(sessionToDelete);

        await _context.SaveChangesAsync();
    }

    public async Task AddSetAsync(WorkoutSet workoutSet)
    {
        await _context.WorkoutSets.AddAsync(workoutSet);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteSetAsync(WorkoutSet workoutSet)
    {
        _context.WorkoutSets.Remove(workoutSet);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateSessionAsync(WorkoutSession session)
    {
        _context.WorkoutSessions.Update(session);
        await _context.SaveChangesAsync();
    }

    // Historial de un ejercicio en otras sesiones. Se consulta a través de WorkoutSessions para que
    // el filtro por perfil y cliente se aplique: WorkoutSets no lo tiene y los ejercicios de la
    // biblioteca comparten ExerciseId entre perfiles. Las series de calentamiento no cuentan.
    public async Task<ExerciseHistory> GetExerciseHistoryAsync(int exerciseId, int excludeSessionId)
    {
        var sets = await _context.WorkoutSessions
            .Where(ws => ws.Id != excludeSessionId)
            .SelectMany(ws => ws.Exercises, (ws, we) => new { ws.Date, we.ExerciseId, we.Sets })
            .Where(x => x.ExerciseId == exerciseId)
            .SelectMany(x => x.Sets, (x, set) => new { x.Date, set.Id, set.Weight, set.Reps, set.IsWarmup })
            .Where(x => !x.IsWarmup)
            .ToListAsync();

        if (sets.Count == 0)
            return ExerciseHistory.Empty;

        var lastDate = sets.Max(x => x.Date);
        var last = sets.Where(x => x.Date == lastDate)
            .OrderBy(x => x.Id)
            .Select(x => new PreviousSet(x.Weight, x.Reps))
            .ToList();

        var bestRepsByWeight = sets
            .GroupBy(x => x.Weight)
            .Select(g => new PreviousSet(g.Key, g.Max(x => x.Reps)))
            .ToList();

        return new ExerciseHistory(last, bestRepsByWeight);
    }

    // Volumen (kg × reps, sin calentamiento) de la última vez que se entrenó este día
    public async Task<double?> GetPreviousVolumeAsync(int routineDayId, int excludeSessionId)
    {
        var last = await _context.WorkoutSessions
            .Where(ws => ws.RoutineDayId == routineDayId && ws.Id != excludeSessionId)
            .Where(ws => ws.Exercises.Any(we => we.Sets.Any()))
            .OrderByDescending(ws => ws.Date)
            .Select(ws => ws.Id)
            .FirstOrDefaultAsync();

        if (last == 0)
            return null;

        var sets = await _context.WorkoutSessions
            .Where(ws => ws.Id == last)
            .SelectMany(ws => ws.Exercises)
            .SelectMany(we => we.Sets)
            .Where(set => !set.IsWarmup)
            .Select(set => new { set.Weight, set.Reps })
            .ToListAsync();

        return sets.Sum(x => x.Weight * x.Reps);
    }

    // Rutina de la última sesión con alguna serie: la «activa» (null = nunca se ha entrenado)
    public Task<int?> GetLastTrainedRoutineIdAsync() =>
        _context.WorkoutSessions
            .Where(ws => ws.Exercises.Any(we => we.Sets.Any()))
            .OrderByDescending(ws => ws.Date)
            .Select(ws => (int?)ws.RoutineDay!.RoutineId)
            .FirstOrDefaultAsync();

    // Última vez que se entrenó cada día (solo sesiones con alguna serie)
    public async Task<Dictionary<int, DateTime>> GetLastSessionDatesAsync(IEnumerable<int> routineDayIds)
    {
        var ids = routineDayIds.ToList();
        return await _context.WorkoutSessions
            .Where(ws => ids.Contains(ws.RoutineDayId) && ws.Exercises.Any(we => we.Sets.Any()))
            .GroupBy(ws => ws.RoutineDayId)
            .Select(g => new { DayId = g.Key, Last = g.Max(ws => ws.Date) })
            .ToDictionaryAsync(x => x.DayId, x => x.Last);
    }

    // 1RM estimado (Epley; con 1 repetición es el propio peso). Orientativo: no decide los récords
    public static double EstimatedOneRepMax(double weight, int reps) =>
        reps <= 1 ? weight : weight * (1 + reps / 30.0);

    // Todo el historial del perfil y cliente activos en dos consultas planas, para las
    // estadísticas de Inicio y Progreso. Solo cuentan las sesiones con alguna serie que no sea
    // de calentamiento: al empezar un entreno ya se crea su sesión, todavía vacía.
    public async Task<TrainingLog> GetTrainingLogAsync()
    {
        var sets = await _context.WorkoutSessions
            .SelectMany(ws => ws.Exercises, (ws, we) => new { SessionId = ws.Id, ws.Date, we.ExerciseId, we.Exercise!.Name, we.Sets })
            .SelectMany(x => x.Sets, (x, set) => new { x.SessionId, x.Date, x.ExerciseId, x.Name, set.Id, set.Weight, set.Reps, set.IsWarmup })
            .Where(x => !x.IsWarmup)
            .ToListAsync();

        var sessions = await _context.WorkoutSessions
            .Where(ws => ws.Exercises.Any(we => we.Sets.Any(s => !s.IsWarmup)))
            .Select(ws => new LoggedSession(ws.Id, ws.Date, ws.DurationMinutes, ws.Feeling, ws.Notes, ws.IsComplete,
                ws.RoutineDayId, ws.RoutineDay!.Name, ws.RoutineDay.Routine!.Name))
            .ToListAsync();

        return new TrainingLog(
            sessions.OrderByDescending(s => s.Date).ToList(),
            sets.OrderBy(x => x.Date).ThenBy(x => x.Id)
                .Select(x => new LoggedSet(x.SessionId, x.Date, x.ExerciseId, x.Name ?? "Ejercicio", x.Id, x.Weight, x.Reps))
                .ToList());
    }

    public async Task<List<WorkoutSession>> GetAllSessionsAsync()
    {
        return await _context.WorkoutSessions.Include(ws=>ws.RoutineDay).OrderByDescending(ws=>ws.Date).ToListAsync();
    }

}

public sealed record PreviousSet(double Weight, int Reps);

// Historial para estadísticas: sesiones (la más reciente primero) y sus series en orden
public sealed record TrainingLog(IReadOnlyList<LoggedSession> Sessions, IReadOnlyList<LoggedSet> Sets)
{
    public static TrainingLog Empty { get; } = new(Array.Empty<LoggedSession>(), Array.Empty<LoggedSet>());
}

public sealed record LoggedSession(int Id, DateTime Date, int? DurationMinutes, int? Feeling, string? Notes, bool IsComplete,
    int RoutineDayId, string DayName, string RoutineName);

public sealed record LoggedSet(int SessionId, DateTime Date, int ExerciseId, string ExerciseName, int SetId, double Weight, int Reps);

// Lo que se sabe de un ejercicio antes de la sesión actual: las series de la última vez
// y, para los récords, el máximo de repeticiones que se ha hecho con cada peso
public sealed record ExerciseHistory(IReadOnlyList<PreviousSet> LastSessionSets, IReadOnlyList<PreviousSet> BestRepsByWeight)
{
    public static ExerciseHistory Empty { get; } = new(Array.Empty<PreviousSet>(), Array.Empty<PreviousSet>());

    public bool HasData => BestRepsByWeight.Count > 0;
}
