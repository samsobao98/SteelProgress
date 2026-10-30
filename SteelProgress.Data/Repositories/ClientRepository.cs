using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Data.Repositories;

// Resumen de un cliente para Clientes e Inicio. ActiveRoutineWeek: los días de su rutina activa y lo
// entrenado de ella, para saber qué día le toca (RoutineSchedule.NextDay, en Core).
public sealed record ClientSummary(Client Client, int SessionsThisWeek, int TotalSessions, DateTime? LastSession,
    string? ActiveRoutineName, RoutineWeek? ActiveRoutineWeek = null);

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

        // Siempre limitado al entrenador: sin filtros se verían los datos de todos los perfiles
        var sessions = await _context.WorkoutSessions.IgnoreQueryFilters()
            .Where(s => s.UserId == trainerUserId && s.ClientId != null && ids.Contains(s.ClientId.Value))
            .Select(s => new
            {
                ClientId = s.ClientId!.Value,
                s.Date,
                s.RoutineDayId,
                RoutineId = s.RoutineDay!.RoutineId,
                // Como en Inicio: un día cuenta como hecho si se apuntó alguna serie
                HasSets = s.Exercises.Any(e => e.Sets.Any())
            })
            .ToListAsync();

        var routines = await _context.Routines.IgnoreQueryFilters()
            .Where(r => r.UserId == trainerUserId && r.ClientId != null && ids.Contains(r.ClientId.Value))
            .Select(r => new { ClientId = r.ClientId!.Value, r.Name, r.Id, r.IsMarkedActive })
            .ToListAsync();

        // La rutina activa de cada cliente, con la misma regla que Inicio:
        // la marcada a mano; si no, la última entrenada; si no, la primera
        var active = clients.ToDictionary(c => c.Id, c =>
        {
            var own = routines.Where(r => r.ClientId == c.Id).OrderBy(r => r.Id).ToList();
            var lastTrained = sessions.Where(s => s.ClientId == c.Id).OrderByDescending(s => s.Date).Select(s => (int?)s.RoutineId).FirstOrDefault();
            return own.FirstOrDefault(r => r.IsMarkedActive)
                   ?? own.FirstOrDefault(r => r.Id == lastTrained)
                   ?? own.FirstOrDefault();
        });

        var activeIds = active.Values.Where(r => r is not null).Select(r => r!.Id).ToList();
        var days = await _context.RoutineDays
            .Where(d => activeIds.Contains(d.RoutineId))
            .OrderBy(d => d.Order)
            .ToListAsync();

        return clients.Select(c =>
        {
            var mine = sessions.Where(s => s.ClientId == c.Id).ToList();
            var routine = active[c.Id];
            RoutineWeek? week = null;
            if (routine is not null)
            {
                var trained = mine.Where(s => s.RoutineId == routine.Id && s.HasSets).ToList();
                week = new RoutineWeek(
                    days.Where(d => d.RoutineId == routine.Id).ToList(),
                    trained.Where(s => s.Date >= weekStart).Select(s => s.RoutineDayId).Distinct().ToList(),
                    trained.OrderByDescending(s => s.Date).Select(s => (int?)s.RoutineDayId).FirstOrDefault());
            }
            return new ClientSummary(
                c,
                mine.Count(s => s.Date >= weekStart),
                mine.Count,
                mine.Count == 0 ? null : mine.Max(s => s.Date),
                routine?.Name,
                week);
        }).ToList();
    }
}
