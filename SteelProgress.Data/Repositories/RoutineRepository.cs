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

    public async Task AddAsync(Routine routine)
    {
        await _context.Routines.AddAsync(routine);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Routine routine)
    {
        _context.Routines.Remove(routine);
        await _context.SaveChangesAsync();
    }

    public async Task<List<RoutineDay>> GetDaysByRoutineIdAsync(int routineId)
    {
        return await _context.RoutineDays.Where(d=>d.RoutineId == routineId).ToListAsync();
    }
    public async Task AddDayAsync(RoutineDay routineDay)
    {
        await _context.RoutineDays.AddAsync(routineDay);
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
}
