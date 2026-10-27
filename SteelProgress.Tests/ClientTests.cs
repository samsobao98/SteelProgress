using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

public class ClientTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();
    private readonly ClientContextService _clientContext;
    private readonly int _dayId;

    public ClientTests()
    {
        _connection.Open();
        _clientContext = new ClientContextService(_user);
        _user.SetCurrentUser(1);
        using var db = Db();
        db.Database.Migrate();
        db.UserProfiles.Add(new UserProfile { Id = 1, Name = "Laura", ViewMode = ProfileViewMode.Trainer });
        db.UserProfiles.Add(new UserProfile { Id = 2, Name = "Pablo" });
        db.SaveChanges();

        // Rutina propia de Laura con un día
        var ex = new Exercise { Name = "Press banca", MuscleGroup = "Pecho", UserId = 1 };
        var day = new RoutineDay { Name = "Torso A", Order = 1 };
        day.Exercises.Add(new RoutineDayExercise { Exercise = ex, Order = 1 });
        var routine = new Routine { Name = "Mi rutina", UserId = 1 };
        routine.Days.Add(day);
        db.Routines.Add(routine);
        db.SaveChanges();
        _dayId = day.Id;
    }

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    public void Dispose() => _connection.Dispose();

    private async Task<Client> AddClientAsync(string name, string? goal = null)
    {
        var client = new Client { Name = name, Goal = goal };
        await new ClientRepository(Db()).AddAsync(client);
        return client;
    }

    // Crea una rutina (y opcionalmente una sesión) como lo haría la app con el cliente activo
    private async Task<int> CreateRoutineForActiveClientAsync(string name)
    {
        using var db = Db();
        var ex = db.Exercises.First();
        var day = new RoutineDay { Name = "Día 1", Order = 1 };
        day.Exercises.Add(new RoutineDayExercise { ExerciseId = ex.Id, Order = 1 });
        var routine = new Routine { Name = name };
        routine.Days.Add(day);
        await new RoutineRepository(db).AddAsync(routine);
        return day.Id;
    }

    [Fact]
    public async Task RoutinesAndSessionsBelongToTheActiveClient()
    {
        var marta = await AddClientAsync("Marta");
        _clientContext.Activate(marta);

        var dayId = await CreateRoutineForActiveClientAsync("Torso / Pierna de Marta");
        var session = await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(dayId);

        using (var db = Db())
        {
            Assert.Equal("Torso / Pierna de Marta", db.Routines.Single().Name);  // solo las de Marta
            var s = db.WorkoutSessions.Single();
            Assert.Equal(marta.Id, s.ClientId);
            Assert.True(s.IsInPerson);
        }

        _clientContext.Clear();
        using (var db = Db())
        {
            Assert.Equal("Mi rutina", db.Routines.Single().Name);                 // de nuevo las propias
            Assert.Empty(db.WorkoutSessions);
        }
    }

    [Fact]
    public async Task OwnStatsDoNotIncludeClientSessions()
    {
        await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(_dayId); // entreno propio

        var marta = await AddClientAsync("Marta");
        _clientContext.Activate(marta);
        var dayId = await CreateRoutineForActiveClientAsync("Rutina de Marta");
        await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(dayId);
        await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(dayId);
        _clientContext.Clear();

        var home = new HomeViewModel(Db(), _user);
        await home.RefreshAsync();
        Assert.Equal(1, home.SessionCount);
        Assert.Equal(1, home.RoutineCount);
    }

    [Fact]
    public async Task SummariesShowWeekActivityAndInactiveClients()
    {
        var marta = await AddClientAsync("Marta", "Hipertrofia");
        var ana = await AddClientAsync("Ana");
        var david = await AddClientAsync("David");

        _clientContext.Activate(marta);
        var dayId = await CreateRoutineForActiveClientAsync("Torso / Pierna v3");
        await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(dayId);
        await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(dayId);

        _clientContext.Activate(ana);
        var anaDay = await CreateRoutineForActiveClientAsync("Recomposición");
        var old = await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(anaDay);
        using (var db = Db())
        {
            var s = db.WorkoutSessions.Single();
            s.Date = DateTime.Now.AddDays(-10);
            await db.SaveChangesAsync();
        }
        _clientContext.Clear();

        var vm = new ClientsViewModel(new ClientRepository(Db()), _user, _clientContext);
        await vm.RefreshAsync();

        Assert.Equal(3, vm.Clients.Count);
        Assert.Equal("3 clientes · 1 activo esta semana", vm.Summary);

        var m = vm.Clients.Single(c => c.Client.Name == "Marta");
        Assert.Equal("Hipertrofia · Torso / Pierna v3", m.Subtitle);
        Assert.Equal("2 esta semana", m.Badge);
        Assert.False(m.BadgeIsWarning);

        var a = vm.Clients.Single(c => c.Client.Name == "Ana");
        Assert.Equal("10 días", a.Badge);
        Assert.True(a.BadgeIsWarning);

        var d = vm.Clients.Single(c => c.Client.Name == "David");
        Assert.Equal("Sin sesiones todavía", d.Subtitle);
        Assert.False(d.HasBadge);
        Assert.Equal("D", d.Initial);
    }

    [Fact]
    public async Task DeletingClientRemovesOnlyTheirData()
    {
        await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(_dayId);
        var marta = await AddClientAsync("Marta");
        _clientContext.Activate(marta);
        var dayId = await CreateRoutineForActiveClientAsync("Rutina de Marta");
        await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(dayId);

        ConfirmDialogService.OnConfirmationRequested += Yes;
        try
        {
            var vm = new ClientsViewModel(new ClientRepository(Db()), _user, _clientContext);
            await vm.RefreshAsync();
            Assert.True(await vm.DeleteAsync(vm.Clients.Single().Client));
        }
        finally { ConfirmDialogService.OnConfirmationRequested -= Yes; }

        Assert.False(_clientContext.HasActiveClient);   // se deja de trabajar con el cliente borrado
        using var check = Db();
        Assert.Empty(check.Clients);
        Assert.Equal("Mi rutina", check.Routines.IgnoreQueryFilters().Single().Name);
        Assert.Single(check.WorkoutSessions.IgnoreQueryFilters());
    }

    private static Task<bool> Yes(string t, string m) => Task.FromResult(true);

    [Fact]
    public async Task OtherProfilesDoNotSeeClients()
    {
        await AddClientAsync("Marta");
        _user.SetCurrentUser(2);
        using var db = Db();
        Assert.Empty(db.Clients);
    }

    [Fact]
    public async Task ChangingProfileLeavesClientMode()
    {
        _clientContext.Activate(await AddClientAsync("Marta"));
        var changed = 0;
        _clientContext.Changed += () => changed++;

        _user.SetCurrentUser(1);

        Assert.Null(_user.CurrentClientId);
        Assert.False(_clientContext.HasActiveClient);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task SaveValidatesAndCreatesClient()
    {
        var vm = new ClientsViewModel(new ClientRepository(Db()), _user, _clientContext);
        vm.StartNewCommand.Execute(null);
        vm.EditName = "  ";
        await vm.SaveAsync();
        Assert.True(vm.IsEditing);

        vm.EditName = " Marta ";
        vm.EditGoal = "Fuerza";
        await vm.SaveAsync();

        Assert.False(vm.IsEditing);
        var item = Assert.Single(vm.Clients);
        Assert.Equal(("Marta", "Fuerza"), (item.Client.Name, item.Client.Goal));
    }

    [Fact]
    public async Task ImportingWithClientActiveCreatesTheClientsRoutine()
    {
        // Exporta la rutina propia y la importa para Marta
        var root = Path.Combine(Path.GetTempPath(), $"sp-client-{Guid.NewGuid():N}");
        try
        {
            byte[] file;
            using (var db = Db())
            using (var ms = new MemoryStream())
            {
                await new Core.Sharing.PackageExporter(db, new FileMediaStorage(root))
                    .ExportRoutineAsync(db.Routines.Single().Id, new Core.Sharing.ExportOptions(false, "Laura"), ms);
                file = ms.ToArray();
            }

            _clientContext.Activate(await AddClientAsync("Marta"));
            using (var package = Core.Sharing.SteelPackage.Open(new MemoryStream(file)))
            using (var db = Db())
            {
                var importer = new Core.Sharing.PackageImporter(db, new ExerciseMediaService(new FileMediaStorage(root)));
                var preview = await importer.PreviewAsync(package);
                Assert.False(preview.IsUpdate);           // Marta aún no la tiene
                await importer.ImportAsync(package, Core.Sharing.ImportMode.UpdateExisting);
            }

            using var check = Db();
            Assert.Single(check.Routines);                 // la de Marta
            _clientContext.Clear();
            using var mine = Db();
            Assert.Single(mine.Routines);                  // la propia sigue igual, sin duplicar
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
