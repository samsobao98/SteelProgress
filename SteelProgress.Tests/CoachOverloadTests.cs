using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// Que el entrenador no se sature: Inicio por prioridad y con límites, recibidos en una línea,
// clientes archivados y el historial por meses
public class CoachOverloadTests : IDisposable
{
    private static readonly DateTime Today = new(2026, 10, 30);
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();
    private readonly ClientContextService _clientContext;

    public CoachOverloadTests()
    {
        _connection.Open();
        _clientContext = new ClientContextService(_user);
        _user.SetCurrentUser(1);
        using var db = Db();
        db.Database.Migrate();
        db.UserProfiles.Add(new UserProfile { Id = 1, Name = "Álex", Kind = ProfileKind.Coach });
        db.SaveChanges();
    }

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    public void Dispose() => _connection.Dispose();

    private static ClientSummary Summary(string name, DateTime? last, bool routine = true, int received = 0, bool archived = false)
    {
        var days = new List<RoutineDay> { new() { Id = 1, Name = "Día 1", Order = 1 } };
        var client = new Client { Name = name, ArchivedAt = archived ? Today.AddDays(-5) : null };
        return new ClientSummary(client, 0, last is null ? 0 : 1, last, routine ? "R" : null,
            routine ? new RoutineWeek(days, Array.Empty<int>(), null) : null, received);
    }

    [Fact]
    public void HomeShowsWhatNeedsAttentionFirst()
    {
        var rows = CoachHome.Rows(new[]
        {
            Summary("Ana", Today.AddHours(9)),           // hecho hoy
            Summary("Bea", Today.AddDays(-1)),           // nada que hacer
            Summary("Carla", null, routine: false),      // crear rutina
            Summary("Dani", Today.AddDays(-3)),          // toca hoy
            Summary("Eva", Today.AddDays(-10)),          // sin noticias
            Summary("Fran", null),                       // por empezar
            Summary("Gema", Today.AddDays(-20), archived: true)
        }, Today);

        Assert.Equal(new[] { "Eva", "Dani", "Carla", "Fran", "Bea", "Ana" }, rows.Select(r => r.Client.Name));
    }

    [Fact]
    public void ReceivedWorkoutsAreOneLine()
    {
        Assert.Equal("", CoachHome.ReceivedText(new[] { Summary("Ana", null) }));
        Assert.Equal("Esta semana te han enviado 1 entreno: Ana.", CoachHome.ReceivedText(new[] { Summary("Ana", null, received: 1) }));
        Assert.Equal("Esta semana te han enviado 4 entrenos: Felipe, Ana y Lucía.", CoachHome.ReceivedText(new[]
        {
            Summary("Ana", null, received: 1), Summary("Felipe", null, received: 2), Summary("Lucía", null, received: 1),
            Summary("Gema", null, received: 3, archived: true)
        }));
    }

    [Fact]
    public async Task ArchivedClientsLeaveTheListsButKeepTheirHistory()
    {
        using (var db = Db())
        {
            foreach (var n in new[] { "Ana", "Bea" }) db.Clients.Add(new Client { Name = n, UserId = 1 });
            db.SaveChanges();
            var bea = db.Clients.Single(c => c.Name == "Bea");
            var routine = new Routine { Name = "R", UserId = 1, ClientId = bea.Id };
            routine.Days.Add(new RoutineDay { Name = "D", Order = 1 });
            db.Routines.Add(routine);
            db.SaveChanges();
            db.WorkoutSessions.Add(new WorkoutSession { UserId = 1, ClientId = bea.Id, RoutineDayId = routine.Days.First().Id, Date = Today.AddDays(-30) });
            db.SaveChanges();
            await new ClientRepository(db).SetArchivedAsync(bea.Id, Today);
        }

        var vm = new ClientsViewModel(new ClientRepository(Db()), _user, _clientContext);
        await vm.RefreshAsync(Today);
        Assert.Equal("Ana", Assert.Single(vm.Clients).Client.Name);
        var archived = Assert.Single(vm.ArchivedClients);
        Assert.StartsWith("Archivado el 30/10/2026 · 1 sesión", archived.Subtitle);
        Assert.Equal("1 cliente · 0 activos esta semana", vm.Summary);
        Assert.Empty(CoachHome.Pending(await new ClientRepository(Db()).GetSummariesAsync(1, Today.AddDays(-4)), Today)
            .Where(p => p.Client.Name == "Bea"));

        // Recuperarlo lo devuelve tal cual, con su sesión
        await vm.RestoreAsync(archived.Client);
        Assert.Equal(2, vm.Clients.Count);
        Assert.Empty(vm.ArchivedClients);
        using var check = Db();
        Assert.Single(check.WorkoutSessions.IgnoreQueryFilters());
    }

    [Fact]
    public async Task DeletingAClientCompactsTheDatabase()
    {
        // Sin errores aunque compacte (VACUUM) justo después de borrar
        using var db = Db();
        db.Clients.Add(new Client { Name = "Ana", UserId = 1 });
        db.SaveChanges();
        await new ClientRepository(db).DeleteAsync(db.Clients.Single());
        Assert.Empty(db.Clients.IgnoreQueryFilters());
    }

    [Fact]
    public void HistoryIsGroupedByMonth()
    {
        RecentSessionItem S(int id, DateTime d) =>
            new(new LoggedSession(id, d, 50, null, null, true, 1, "Día", "R"), 0, Today);
        var months = HistoryMonth.Group(new[]
        {
            S(1, new DateTime(2026, 10, 28)), S(2, new DateTime(2026, 10, 2)), S(3, new DateTime(2026, 9, 15)), S(4, new DateTime(2025, 12, 1))
        });
        Assert.Equal(new[] { "Octubre 2026", "Septiembre 2026", "Diciembre 2025" }, months.Select(m => m.Title));
        Assert.Equal(("2 sesiones", "1 sesión"), (months[0].CountText, months[1].CountText));
        Assert.Equal(new[] { 1, 2 }, months[0].Items.Select(i => i.SessionId));
    }
}
