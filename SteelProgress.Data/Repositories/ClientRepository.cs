using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Data.Repositories;

// Resumen de un cliente para la lista de Clientes
public sealed record ClientSummary(Client Client, int SessionsThisWeek, int TotalSessions, DateTime? LastSession, string? ActiveRoutineName);

public class ClientRepository
{
    private readonly AppDbContext _context;

    public ClientRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Client>> GetAllAsync() =>
        await _context.Clients.OrderBy(c => c.Name).ToListAsync();

    public async Task AddAsync(Client client)
    {
        await _context.Clients.AddAsync(client);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Client client)
    {
        _context.Clients.Update(client);
        await _context.SaveChangesAsync();
    }

    // Borra el cliente con sus rutinas y sesiones (en cascada)
    public async Task DeleteAsync(Client client)
    {
        var entity = await _context.Clients.FindAsync(client.Id);
        if (entity is null) return;
        _context.Clients.Remove(entity);
        await _context.SaveChangesAsync();
    }

    // Los filtros solo dejan ver los datos del cliente activo: aquí se consultan todos los del entrenador
    public async Task<List<ClientSummary>> GetSummariesAsync(int trainerUserId, DateTime weekStart)
    {
        var clients = await _context.Clients.Where(c => c.UserId == trainerUserId).OrderBy(c => c.Name).ToListAsync();
        var ids = clients.Select(c => c.Id).ToList();

        var sessions = await _context.WorkoutSessions.IgnoreQueryFilters()
            .Where(s => s.UserId == trainerUserId && s.ClientId != null && ids.Contains(s.ClientId.Value))
            .Select(s => new { ClientId = s.ClientId!.Value, s.Date })
            .ToListAsync();

        var routines = await _context.Routines.IgnoreQueryFilters()
            .Where(r => r.UserId == trainerUserId && r.ClientId != null && ids.Contains(r.ClientId.Value))
            .Select(r => new { ClientId = r.ClientId!.Value, r.Name, r.Id })
            .ToListAsync();

        return clients.Select(c =>
        {
            var mine = sessions.Where(s => s.ClientId == c.Id).ToList();
            var routine = routines.Where(r => r.ClientId == c.Id).OrderByDescending(r => r.Id).FirstOrDefault();
            return new ClientSummary(
                c,
                mine.Count(s => s.Date >= weekStart),
                mine.Count,
                mine.Count == 0 ? null : mine.Max(s => s.Date),
                routine?.Name);
        }).ToList();
    }
}
