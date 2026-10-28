using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;


namespace SteelProgress.Data.Repositories;

public class RoutineRepository
{
    private readonly AppDbContext _context;

    public RoutineRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Routine>> GetAllAsync()
    {
        return await _context.Routines.OrderBy(r=>r.Name).ToListAsync();
    }

    public async Task<List<RoutineSummary>> GetRoutineSummariesAsync()
    {
        return await _context.Routines
            .OrderBy(r => r.Name)
            .Select(r => new RoutineSummary
            {
                Routine = r,
                DayCount = r.Days.Count,
                ExerciseCount = r.Days.SelectMany(d => d.Exercises).Count(),
                // Vía WorkoutSessions para que se apliquen los filtros de perfil y cliente
                LastTrained = _context.WorkoutSessions
                    .Where(s => s.RoutineDay!.RoutineId == r.Id && s.Exercises.Any(e => e.Sets.Any()))
                    .Max(s => (DateTime?)s.Date)
            })
            .ToListAsync();
    }

    // Avance de una rutina desde weekStart: qué días se han entrenado y cuál fue el último
    public async Task<RoutineWeek> GetRoutineWeekAsync(int routineId, DateTime weekStart)
    {
        var days = await GetDaysByRoutineIdAsync(routineId);

        var trained = _context.WorkoutSessions
            .Where(s => s.RoutineDay!.RoutineId == routineId && s.Exercises.Any(e => e.Sets.Any()));

        var doneThisWeek = await trained
            .Where(s => s.Date >= weekStart)
            .Select(s => s.RoutineDayId)
            .Distinct()
            .ToListAsync();

        var lastDayId = await trained
            .OrderByDescending(s => s.Date)
            .Select(s => (int?)s.RoutineDayId)
            .FirstOrDefaultAsync();

        return new RoutineWeek(days, doneThisWeek, lastDayId);
    }

    // Entrenos que se borrarían en cascada con la rutina o el día
    public Task<int> CountSessionsOfRoutineAsync(int routineId) =>
        _context.WorkoutSessions.CountAsync(s => s.RoutineDay!.RoutineId == routineId);

    public Task<int> CountSessionsOfDayAsync(int routineDayId) =>
        _context.WorkoutSessions.CountAsync(s => s.RoutineDayId == routineDayId);

    // Guarda el orden elegido a mano (o null en todas para volver al automático)
    public async Task SetSortOrderAsync(IReadOnlyList<Routine> ordered, bool manual)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SortOrder = manual ? i + 1 : null;
            _context.Routines.Update(ordered[i]);
        }
        await _context.SaveChangesAsync();
    }

    public async Task AddAsync(Routine routine)
    {
        await _context.Routines.AddAsync(routine);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Routine routine)
    {
        _context.Routines.Update(routine);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Routine routine)
    {
        _context.Routines.Remove(routine);
        await _context.SaveChangesAsync();
    }

    public async Task<List<RoutineDay>> GetDaysByRoutineIdAsync(int routineId)
    {
        return await _context.RoutineDays
            .Where(d => d.RoutineId == routineId)
            .OrderBy(d => d.Order)
            .ToListAsync();
    }
    public async Task AddDayAsync(RoutineDay routineDay)
    {
        await _context.RoutineDays.AddAsync(routineDay);
        await _context.SaveChangesAsync();
    }
    public async Task UpdateDayAsync(RoutineDay routineDay)
    {
        _context.RoutineDays.Update(routineDay);
        await _context.SaveChangesAsync();
    }
    public async Task DeleteDayAsync(RoutineDay routineDay)
    {
        _context.RoutineDays.Remove(routineDay);
        await _context.SaveChangesAsync();
    }

    public async Task<List<RoutineDayExercise>> GetExercisesByDayIdAsync(int routineDayId)
    {
        return await _context.RoutineDayExercises
            .Include(rde => rde.Exercise)
            .Where(rde => rde.RoutineDayId == routineDayId)
            .OrderBy(rde => rde.Order)
            .ToListAsync();
    }

    public async Task AddExerciseToDayAsync(RoutineDayExercise rde)
    {
        await _context.RoutineDayExercises.AddAsync(rde);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteExerciseFromDayAsync(RoutineDayExercise rde)
    {
        _context.RoutineDayExercises.Remove(rde);
        await _context.SaveChangesAsync();
    }

    // Lo prescrito para un ejercicio dentro de un día (si se repite, el primero por orden)
    public async Task<RoutineDayExercise?> GetPrescriptionAsync(int routineDayId, int exerciseId)
    {
        return await _context.RoutineDayExercises
            .Where(rde => rde.RoutineDayId == routineDayId && rde.ExerciseId == exerciseId)
            .OrderBy(rde => rde.Order)
            .FirstOrDefaultAsync();
    }

    public void UpdateRoutineDayExercise(RoutineDayExercise exercise)
    {
        _context.RoutineDayExercises.Update(exercise);
        _context.SaveChanges();
    }

    public async Task UpdateRoutineDayExerciseAsync(RoutineDayExercise exercise)
    {
        _context.RoutineDayExercises.Update(exercise);
        await _context.SaveChangesAsync();
    }

    /// <summary>Reasigna Order según la posición en la lista y guarda en bloque.</summary>
    public async Task ReorderDayExercisesAsync(IList<RoutineDayExercise> ordered)
    {
        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].Order = i + 1;
            _context.RoutineDayExercises.Update(ordered[i]);
        }
        await _context.SaveChangesAsync();
    }
}

public sealed record RoutineWeek(IReadOnlyList<RoutineDay> Days, IReadOnlyCollection<int> DoneThisWeek, int? LastTrainedDayId);
