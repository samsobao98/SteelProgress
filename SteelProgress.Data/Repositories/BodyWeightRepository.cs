using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Data.Repositories;

// Pesajes del perfil o del cliente activo (filtros de AppDbContext)
public class BodyWeightRepository
{
    private readonly AppDbContext _context;

    public BodyWeightRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<BodyWeightEntry>> GetAllAsync() =>
        await _context.BodyWeights.AsNoTracking().OrderBy(w => w.Date).ToListAsync();

    /// <summary>Apunta el peso de un día; si ese día ya tenía uno, lo sustituye.</summary>
    public async Task SaveAsync(DateTime date, double kg)
    {
        var day = date.Date;
        var entry = await _context.BodyWeights.FirstOrDefaultAsync(w => w.Date == day);
        if (entry is null) _context.BodyWeights.Add(new BodyWeightEntry { Date = day, Kg = kg });
        else entry.Kg = kg;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(DateTime date)
    {
        var day = date.Date;
        var entry = await _context.BodyWeights.FirstOrDefaultAsync(w => w.Date == day);
        if (entry is null) return;
        _context.BodyWeights.Remove(entry);
        await _context.SaveChangesAsync();
    }

    /// <summary>Si el cliente ha dado su consentimiento para guardar datos de salud.</summary>
    public async Task<bool> HasHealthConsentAsync(int clientId) =>
        await _context.Clients.AnyAsync(c => c.Id == clientId && c.HealthConsentAt != null);
}
