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

        return new ExerciseHistory(last, sets.Max(x => EstimatedOneRepMax(x.Weight, x.Reps)));
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

    // Fórmula de Epley; con 1 repetición es el propio peso
    public static double EstimatedOneRepMax(double weight, int reps) =>
        reps <= 1 ? weight : weight * (1 + reps / 30.0);

    public async Task<List<WorkoutSession>> GetAllSessionsAsync()
    {
        return await _context.WorkoutSessions.Include(ws=>ws.RoutineDay).OrderByDescending(ws=>ws.Date).ToListAsync();
    }

}

public sealed record PreviousSet(double Weight, int Reps);

// Lo que se sabe de un ejercicio antes de la sesión actual
public sealed record ExerciseHistory(IReadOnlyList<PreviousSet> LastSessionSets, double? BestEstimatedOneRepMax)
{
    public static ExerciseHistory Empty { get; } = new(Array.Empty<PreviousSet>(), null);
}
