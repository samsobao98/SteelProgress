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
            RoutineDayId = routineDayId
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

    public async Task<List<WorkoutSession>> GetAllSessionsAsync()
    {
        return await _context.WorkoutSessions.Include(ws=>ws.RoutineDay).OrderByDescending(ws=>ws.Date).ToListAsync();
    }

}