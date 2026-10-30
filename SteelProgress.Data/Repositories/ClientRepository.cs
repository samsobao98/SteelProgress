using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Data.Repositories;

// Resumen de un cliente para Clientes e Inicio. ActiveRoutineWeek: los días de su rutina activa y lo
// entrenado de ella, para saber qué día le toca (RoutineSchedule.NextDay, en Core).
public sealed record ClientSummary(Client Client, int SessionsThisWeek, int TotalSessions, DateTime? LastSession,
    string? ActiveRoutineName, RoutineWeek? ActiveRoutineWeek = null, int ReceivedThisWeek = 0)
{
    public bool IsArchived => Client.ArchivedAt is not null;
}

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
        // Solo la ficha: sus limitaciones (cargadas para el aviso) se guardan aparte
        _context.Entry(client).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    // Borra el cliente con sus rutinas y sesiones (en cascada)
    public async Task DeleteAsync(Client client)
    {
        var entity = await _context.Clients.FindAsync(client.Id);
        if (entity is null) return;
        _context.Clients.Remove(entity);
        await _context.SaveChangesAsync();
        await CompactAsync(_context);
    }

    /// <summary>
    /// SQLite no encoge el archivo al borrar: reutiliza el hueco. Tras borrar mucho (un cliente, un perfil)
    /// se compacta para que ocupe lo que de verdad hay.
    /// </summary>
    public static async Task CompactAsync(AppDbContext context)
    {
        if (context.Database.IsSqlite())
            await context.Database.ExecuteSqlRawAsync("VACUUM;");
    }

    /// <summary>Archiva un cliente (o lo recupera): su historial no se toca.</summary>
    public async Task SetArchivedAsync(int clientId, DateTime? archivedAt)
    {
        var client = await _context.Clients.FirstAsync(c => c.Id == clientId);
        client.ArchivedAt = archivedAt;
        await _context.SaveChangesAsync();
    }

    // ── Limitaciones (datos de salud: solo con consentimiento) ──

    public async Task<Client?> GetWithLimitationsAsync(int clientId) =>
        await _context.Clients.Include(c => c.Limitations).FirstOrDefaultAsync(c => c.Id == clientId);

    /// <summary>Guarda una limitación nueva o cambiada. La primera registra el consentimiento.</summary>
    public async Task SaveLimitationAsync(ClientLimitation limitation, DateTime now)
    {
        var client = await _context.Clients.FirstAsync(c => c.Id == limitation.ClientId);
        client.HealthConsentAt ??= now;
        if (limitation.Id == 0) _context.ClientLimitations.Add(limitation);
        else _context.ClientLimitations.Update(limitation);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteLimitationAsync(ClientLimitation limitation)
    {
        var entity = await _context.ClientLimitations.FindAsync(limitation.Id);
        if (entity is null) return;
        _context.ClientLimitations.Remove(entity);
        await _context.SaveChangesAsync();
    }

    /// <summary>El cliente da su consentimiento para guardar datos de salud (limitaciones y peso).</summary>
    public async Task GiveHealthConsentAsync(int clientId, DateTime now)
    {
        var client = await _context.Clients.FirstAsync(c => c.Id == clientId);
        client.HealthConsentAt ??= now;
        await _context.SaveChangesAsync();
    }

    /// <summary>El cliente retira su consentimiento: se borran sus limitaciones y sus pesajes.</summary>
    public async Task WithdrawHealthConsentAsync(int clientId)
    {
        var client = await _context.Clients.Include(c => c.Limitations).FirstAsync(c => c.Id == clientId);
        _context.ClientLimitations.RemoveRange(client.Limitations);
        // Sus pesajes, aunque no sea el cliente activo (los filtros solo dejan ver los del activo)
        _context.BodyWeights.RemoveRange(await _context.BodyWeights.IgnoreQueryFilters()
            .Where(w => w.UserId == client.UserId && w.ClientId == clientId).ToListAsync());
        client.HealthConsentAt = null;
        await _context.SaveChangesAsync();
    }

    // Los filtros solo dejan ver los datos del cliente activo: aquí se consultan todos los del entrenador
    public async Task<List<ClientSummary>> GetSummariesAsync(int trainerUserId, DateTime weekStart)
    {
        // Con sus limitaciones, para el aviso al trabajar con él (ActiveClientBanner)
        var clients = await _context.Clients.Where(c => c.UserId == trainerUserId).Include(c => c.Limitations)
            .OrderBy(c => c.Name).ToListAsync();
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
                HasSets = s.Exercises.Any(e => e.Sets.Any()),
                s.ReceivedAt
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
                week,
                mine.Count(s => s.ReceivedAt >= weekStart));
        }).ToList();
    }
}
