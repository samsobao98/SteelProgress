using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Data.Repositories;

public class ExerciseRepository //Se encarga de trabajar con la tabla Exercise
{
    private readonly AppDbContext _context;

    public ExerciseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Exercise>> GetAllAsync()
    {
        return await _context.Exercises
            .Include(e => e.Media.OrderBy(m => m.Order))
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Pone el tipo de material a los ejercicios que no lo tienen (los anteriores a que fuera obligatorio),
    /// deducido de su material o de su nombre. De todos los perfiles y de la biblioteca. Devuelve cuántos.
    /// </summary>
    public async Task<int> FillMissingEquipmentTypesAsync()
    {
        var missing = await _context.Exercises.IgnoreQueryFilters()
            .Where(e => e.EquipmentType == null)
            .ToListAsync();
        foreach (var e in missing)
            e.EquipmentType = EquipmentCatalog.Classify(e.Equipment, e.Name);
        if (missing.Count > 0) await _context.SaveChangesAsync();
        return missing.Count;
    }

    // ── Vídeos e imágenes ─────────────────────────────────────────────────

    public async Task AddMediaAsync(int exerciseId, ExerciseMedia media)
    {
        media.ExerciseId = exerciseId;
        media.Order = await _context.ExerciseMedia.CountAsync(m => m.ExerciseId == exerciseId);
        await _context.ExerciseMedia.AddAsync(media);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveMediaAsync(ExerciseMedia media)
    {
        var entity = await _context.ExerciseMedia.FindAsync(media.Id);
        if (entity is null) return;
        // Si sigue en la lista del ejercicio cargado, EF lo "resucita" al guardar en vez de borrarlo
        entity.Exercise?.Media.Remove(entity);
        _context.ExerciseMedia.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<List<string>> GetMediaFileNamesAsync(int exerciseId)
    {
        return await _context.ExerciseMedia
            .Where(m => m.ExerciseId == exerciseId)
            .Select(m => m.FileName)
            .ToListAsync();
    }

    public async Task AddAsync(Exercise exercise)
    {
        await _context.Exercises.AddAsync(exercise);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Exercise exercise)
    {
        _context.Exercises.Remove(exercise);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Exercise exercise)
    {
        _context.Exercises.Update(exercise);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        return await _context.Exercises
            .AnyAsync(e => e.Name == name);
    }

    public async Task<bool> ExistsByNameExceptIdAsync(string name, int id)
    {
        return await _context.Exercises
            .AnyAsync(e => e.Name == name && e.Id != id);
    }
}