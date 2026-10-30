using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.Sharing;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// Un cliente le envía su entreno al entrenador: se reconoce por la rutina, se guarda en su historial
// y, si lo reenvía, se actualiza. Los dos «móviles» son dos perfiles de la misma base de datos.
public class WorkoutSendingTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();
    private readonly string _media = Path.Combine(Path.GetTempPath(), "sp-send-" + Guid.NewGuid().ToString("N"));
    private const int Coach = 1, Athlete = 2;
    private int _clientId, _coachRoutineId, _athleteDayId, _squatId;

    public WorkoutSendingTests()
    {
        _connection.Open();
        using var db = Db();
        db.Database.Migrate();
        db.UserProfiles.Add(new UserProfile { Id = Coach, Name = "Álex", Kind = ProfileKind.Coach });
        db.UserProfiles.Add(new UserProfile { Id = Athlete, Name = "Felipe" });
        var squat = new Exercise { Name = "Sentadilla", MuscleGroup = "Piernas", IsSystem = true };
        db.Exercises.Add(squat);
        db.SaveChanges();
        _squatId = squat.Id;

        // El entrenador: Felipe con su Rutina A (Empuje, Pierna)
        var client = new Client { Name = "Felipe", UserId = Coach };
        db.Clients.Add(client);
        var routine = new Routine { Name = "Rutina A", UserId = Coach, Client = client };
        routine.Days.Add(new RoutineDay { Name = "Empuje", Order = 1 });
        routine.Days.Add(new RoutineDay { Name = "Pierna", Order = 2 });
        db.Routines.Add(routine);
        db.SaveChanges();
        _clientId = client.Id;
        _coachRoutineId = routine.Id;

        // Felipe, en su móvil: la rutina importada (recuerda de quién es) y un ejercicio propio
        var mine = new Routine { Name = "Rutina A", UserId = Athlete, SourceUid = routine.Uid, AuthorName = "Álex" };
        mine.Days.Add(new RoutineDay { Name = "Empuje", Order = 1 });
        var pierna = new RoutineDay { Name = "Pierna", Order = 2 };
        mine.Days.Add(pierna);
        db.Routines.Add(mine);
        db.SaveChanges();
        _athleteDayId = pierna.Id;
    }

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_media)) Directory.Delete(_media, true);
    }

    // Felipe guarda un entreno de Pierna y lo exporta
    private async Task<(int SessionId, MemoryStream File)> AthleteWorkoutAsync(string? notes, int routineDayId = 0)
    {
        _user.SetCurrentUser(Athlete);
        using var db = Db();
        var session = new WorkoutSession
        {
            UserId = Athlete, RoutineDayId = routineDayId == 0 ? _athleteDayId : routineDayId, Date = new DateTime(2026, 10, 29, 19, 0, 0),
            DurationMinutes = 52, Feeling = 4, Notes = notes
        };
        var we = new WorkoutExercise { ExerciseId = _squatId };
        we.Sets.Add(new WorkoutSet { Weight = 60, Reps = 5, IsWarmup = true });
        we.Sets.Add(new WorkoutSet { Weight = 100, Reps = 8, Rpe = 8 });
        we.Sets.Add(new WorkoutSet { Weight = 100, Reps = 7 });
        session.Exercises.Add(we);
        db.WorkoutSessions.Add(session);
        await db.SaveChangesAsync();
        return (session.Id, await ExportAsync(db, session.Id));
    }

    private async Task<MemoryStream> ExportAsync(AppDbContext db, int sessionId)
    {
        var ms = new MemoryStream();
        await new PackageExporter(db, new FileMediaStorage(_media)).ExportWorkoutsAsync(new[] { sessionId }, new ExportOptions(false, "Felipe"), ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task CoachRecognizesTheClientAndSavesItInTheRoutineDay()
    {
        var (_, file) = await AthleteWorkoutAsync("La rodilla bien.");

        _user.SetCurrentUser(Coach);
        using var db = Db();
        using var package = SteelPackage.Open(file);
        var importer = new WorkoutImporter(db);
        var preview = await importer.PreviewAsync(package, Coach);
        Assert.Equal(("Felipe", "Felipe"), (preview.AthleteName, preview.Client?.Name));
        var item = Assert.Single(preview.Workouts);
        Assert.Equal((2, false), (item.SetCount, item.IsUpdate));
        Assert.Equal("100×8 · 100×7", Assert.Single(item.Lines).Sets); // sin el calentamiento

        Assert.Equal(1, await importer.ImportAsync(package, Coach, preview.Client!.Id));

        var saved = db.WorkoutSessions.IgnoreQueryFilters().Include(s => s.RoutineDay).Include(s => s.Exercises).ThenInclude(e => e.Sets)
            .Single(s => s.UserId == Coach);
        Assert.Equal((_clientId, "Pierna", _coachRoutineId, false), (saved.ClientId, saved.RoutineDay!.Name, saved.RoutineDay.RoutineId, saved.IsInPerson));
        Assert.Equal(("La rodilla bien.", 4, 52, 3), (saved.Notes, saved.Feeling, saved.DurationMinutes, saved.Exercises.Single().Sets.Count));
    }

    [Fact]
    public async Task ResendingUpdatesInsteadOfDuplicating()
    {
        var (sessionId, first) = await AthleteWorkoutAsync("Primera nota");
        _user.SetCurrentUser(Coach);
        using (var db = Db())
        using (var package = SteelPackage.Open(first))
            await new WorkoutImporter(db).ImportAsync(package, Coach, _clientId);

        // Felipe cambia la nota y lo reenvía
        _user.SetCurrentUser(Athlete);
        MemoryStream second;
        using (var db = Db())
        {
            db.WorkoutSessions.Single(s => s.Id == sessionId).Notes = "Nota corregida";
            await db.SaveChangesAsync();
            second = await ExportAsync(db, sessionId);
        }

        _user.SetCurrentUser(Coach);
        using (var db = Db())
        using (var package = SteelPackage.Open(second))
        {
            var importer = new WorkoutImporter(db);
            Assert.True(Assert.Single((await importer.PreviewAsync(package, Coach)).Workouts).IsUpdate);
            await importer.ImportAsync(package, Coach, _clientId);
        }

        using var check = Db();
        var only = Assert.Single(check.WorkoutSessions.IgnoreQueryFilters().Where(s => s.UserId == Coach));
        Assert.Equal("Nota corregida", only.Notes);
        Assert.Equal(3, check.WorkoutSets.Count(s => s.WorkoutExercise!.WorkoutSessionId == only.Id));
    }

    [Fact]
    public async Task AnotherRoutineAsksForTheClientAndKeepsTheDay()
    {
        // Felipe entrena con una rutina suya, no del entrenador
        int ownDay;
        using (var db = Db())
        {
            var own = new Routine { Name = "Mi rutina", UserId = Athlete };
            var day = new RoutineDay { Name = "Brazo", Order = 1 };
            own.Days.Add(day);
            db.Routines.Add(own);
            await db.SaveChangesAsync();
            ownDay = day.Id;
        }
        var (_, file) = await AthleteWorkoutAsync(null, ownDay);

        _user.SetCurrentUser(Coach);
        using var db2 = Db();
        using var package = SteelPackage.Open(file);
        var importer = new WorkoutImporter(db2);
        Assert.Null((await importer.PreviewAsync(package, Coach)).Client);
        Assert.Single(await importer.ClientsAsync(Coach));

        await importer.ImportAsync(package, Coach, _clientId);
        var saved = db2.WorkoutSessions.IgnoreQueryFilters().Include(s => s.RoutineDay!).ThenInclude(d => d.Routine).Single(s => s.UserId == Coach);
        Assert.Equal(("Mi rutina", "Brazo", _clientId), (saved.RoutineDay!.Routine!.Name, saved.RoutineDay.Name, saved.RoutineDay.Routine.ClientId));
    }

    [Fact]
    public async Task HistoryShowsWhereEachWorkoutCameFrom()
    {
        var (_, file) = await AthleteWorkoutAsync("Bien");
        _user.SetCurrentUser(Coach);
        using (var db = Db())
        using (var package = SteelPackage.Open(file))
            await new WorkoutImporter(db).ImportAsync(package, Coach, _clientId);

        // Y uno hecho juntos, en el móvil del entrenador con Felipe activo
        _user.SetCurrentClient(_clientId);
        using (var db = Db())
        {
            var day = db.RoutineDays.First(d => d.RoutineId == _coachRoutineId);
            var together = new WorkoutSession { RoutineDayId = day.Id, Date = new DateTime(2026, 10, 27, 18, 0, 0) };
            together.Exercises.Add(new WorkoutExercise { ExerciseId = _squatId, Sets = { new WorkoutSet { Weight = 95, Reps = 8 } } });
            db.WorkoutSessions.Add(together);
            await db.SaveChangesAsync();
        }

        using var read = Db();
        var log = await new WorkoutRepository(read).GetTrainingLogAsync();
        Assert.Equal(new[] { "Enviado por Felipe", "Contigo" }, log.Sessions.Select(s => s.Origin));
    }

    [Fact]
    public async Task OnlyCoachProfilesCanOpenIt()
    {
        var (_, file) = await AthleteWorkoutAsync(null);
        _user.SetCurrentUser(Athlete);
        var options = new ViewOptionsService();
        options.Apply(new UserProfile { Id = Athlete });
        using var db = Db();
        var vm = new Core.ViewModels.WorkoutImportViewModel(new WorkoutImporter(db), _user, options);
        await vm.LoadAsync(file);
        Assert.False(vm.IsLoaded);
        Assert.Contains("perfil de entrenador", vm.Error);

        // Y la importación de rutinas lo rechaza
        file.Position = 0;
        using var package = SteelPackage.Open(file);
        await Assert.ThrowsAsync<SteelPackageException>(() => new PackageImporter(db, new ExerciseMediaService(new FileMediaStorage(_media))).PreviewAsync(package));
    }
}
