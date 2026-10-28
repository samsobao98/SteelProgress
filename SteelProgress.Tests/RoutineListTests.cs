using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// Lista de rutinas: rutina activa, semana, «Toca», orden, borrar con deshacer y panel de añadir ejercicios
public class RoutineListTests : IDisposable
{
    // Miércoles 28 de octubre de 2026: la semana empieza el lunes 26
    private static readonly DateTime Today = new(2026, 10, 28, 18, 0, 0);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();
    private readonly FixedClock _clock = new(Today);
    private readonly int _benchId;
    private readonly int _squatId;

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Unspecified), TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    public RoutineListTests()
    {
        _connection.Open();
        _user.SetCurrentUser(1);
        using var db = Db();
        db.Database.Migrate();
        db.UserProfiles.Add(new UserProfile { Id = 1, Name = "Laura" });
        db.SaveChanges();

        var bench = new Exercise { Name = "Press banca", MuscleGroup = "Pecho", IsSystem = true };
        var squat = new Exercise { Name = "Sentadilla", MuscleGroup = "Cuádriceps", IsSystem = true };
        var curl = new Exercise { Name = "Curl de bíceps", MuscleGroup = "Bíceps", IsSystem = true };
        db.Exercises.AddRange(bench, squat, curl);
        db.SaveChanges();
        _benchId = bench.Id;
        _squatId = squat.Id;
    }

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    public void Dispose() => _connection.Dispose();

    private Routine AddRoutine(string name, params string[] days)
    {
        using var db = Db();
        var routine = new Routine { Name = name, UserId = 1 };
        var order = 1;
        foreach (var d in days)
        {
            var day = new RoutineDay { Name = d, Order = order++ };
            day.Exercises.Add(new RoutineDayExercise { ExerciseId = _benchId, Order = 1, TargetSets = 3 });
            routine.Days.Add(day);
        }
        db.Routines.Add(routine);
        db.SaveChanges();
        return routine;
    }

    private async Task TrainAsync(Routine routine, string dayName, DateTime date)
    {
        using var db = Db();
        var day = db.RoutineDays.Single(d => d.RoutineId == routine.Id && d.Name == dayName);
        var repo = new WorkoutRepository(db);
        var session = await repo.CreateSessionFromRoutineDayAsync(day.Id);
        session.Date = date;
        var we = db.WorkoutExercises.First(e => e.WorkoutSessionId == session.Id);
        db.WorkoutSets.Add(new WorkoutSet { WorkoutExerciseId = we.Id, Weight = 60, Reps = 8 });
        await db.SaveChangesAsync();
    }

    private RoutineViewModel Vm() => new(new RoutineRepository(Db()), _clock);

    private static string[] Names(RoutineViewModel vm) => vm.Items.Select(i => i.Name).ToArray();

    // ── Activa, semana y «Toca» ───────────────────────────────────────────

    [Fact]
    public async Task LastTrainedRoutineIsActiveAndFirst()
    {
        var full = AddRoutine("Full body", "Día 1");
        var tp = AddRoutine("Torso / Pierna", "Torso A", "Pierna A", "Torso B", "Pierna B");
        AddRoutine("Antigua", "Día 1");
        await TrainAsync(full, "Día 1", Today.AddDays(-20));
        await TrainAsync(tp, "Torso A", Today.AddDays(-2)); // lunes

        var vm = Vm();
        await vm.RefreshAsync();

        Assert.Equal(new[] { "Torso / Pierna", "Full body", "Antigua" }, Names(vm));
        var active = vm.Items[0];
        Assert.True(active.IsActive);
        Assert.Equal("4 días · 4 ejercicios", active.SubtitleText);
        Assert.Equal(new[] { true, false, false, false }, active.Week.Select(w => w.Done));
        Assert.Equal("Esta semana: 1 de 4", active.WeekText);
        Assert.Equal("Pierna A", active.NextDayName);
        Assert.Equal("1 día · última vez hace 2 semanas", vm.Items[1].SubtitleText);
        Assert.Equal("1 día · sin entrenar aún", vm.Items[2].SubtitleText);
        Assert.Equal("3 rutinas · 1 activa", vm.HeaderText);
    }

    [Fact]
    public async Task SundayBelongsToThePreviousWeek()
    {
        var tp = AddRoutine("Torso / Pierna", "Torso A", "Pierna A");
        await TrainAsync(tp, "Torso A", new DateTime(2026, 10, 25, 20, 0, 0)); // domingo
        await TrainAsync(tp, "Pierna A", new DateTime(2026, 10, 26, 9, 0, 0)); // lunes

        var vm = Vm();
        await vm.RefreshAsync();

        Assert.Equal(new[] { false, true }, vm.Items[0].Week.Select(w => w.Done));
        Assert.Equal("Torso A", vm.Items[0].NextDayName); // tras el último día vuelve al primero
    }

    [Fact]
    public void NextDayRules()
    {
        var days = new List<RoutineDay> { new() { Id = 1, Name = "A" }, new() { Id = 2, Name = "B" }, new() { Id = 3, Name = "C" } };

        Assert.Equal("A", RoutineSchedule.NextDay(new RoutineWeek(days, [], null))!.Name);        // nunca entrenada
        Assert.Equal("C", RoutineSchedule.NextDay(new RoutineWeek(days, [1, 2], 2))!.Name);       // el siguiente pendiente
        Assert.Equal("A", RoutineSchedule.NextDay(new RoutineWeek(days, [3], 3))!.Name);          // da la vuelta
        Assert.Equal("B", RoutineSchedule.NextDay(new RoutineWeek(days, [1, 3], 3))!.Name);       // salta lo ya hecho
        Assert.Equal("C", RoutineSchedule.NextDay(new RoutineWeek(days, [1, 2, 3], 2))!.Name);    // semana completa: el siguiente
        Assert.Null(RoutineSchedule.NextDay(new RoutineWeek([], [], null)));
    }

    [Fact]
    public async Task SessionsWithAClientDoNotMakeYourRoutineActive()
    {
        var mine = AddRoutine("Mi rutina", "Día 1");
        var client = new Client { Name = "Marta" };
        await new ClientRepository(Db()).AddAsync(client);

        _user.SetCurrentClient(client.Id);
        await TrainAsync(mine, "Día 1", Today.AddDays(-1));
        _user.SetCurrentClient(null);

        var vm = Vm();
        await vm.RefreshAsync();

        Assert.False(vm.Items.Single().IsActive);
        Assert.Equal("1 día · sin entrenar aún", vm.Items[0].SubtitleText);
    }

    [Theory]
    [InlineData("Fuerza 5×5", "5×5")]
    [InlineData("Fuerza 5x5", "5×5")]
    [InlineData("Full body", "FB")]
    [InlineData("Torso / Pierna", "TP")]
    [InlineData("Push", "Pu")]
    [InlineData("  ", "?")]
    public void Monogram(string name, string expected) => Assert.Equal(expected, RoutineListItem.MonogramOf(name));

    // ── Orden ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ManualOrderIsKeptAndCanBeReset()
    {
        var a = AddRoutine("A", "Día");
        AddRoutine("B", "Día");
        AddRoutine("C", "Día");
        await TrainAsync(a, "Día", Today.AddDays(-1));

        var vm = Vm();
        await vm.RefreshAsync();
        Assert.Equal(new[] { "A", "B", "C" }, Names(vm));

        // Arrastrar C arriba del todo y terminar
        vm.StartReorderCommand.Execute(null);
        vm.Items.Move(2, 0);
        await vm.FinishReorderCommand.ExecuteAsync(null);

        var again = Vm();
        await again.RefreshAsync();
        Assert.True(again.IsManualOrder);
        Assert.Equal(new[] { "C", "A", "B" }, Names(again));

        // Una rutina nueva va al final
        again.NewName = "D";
        await again.CreateRoutineCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "C", "A", "B", "D" }, Names(again));

        await again.ResetOrderCommand.ExecuteAsync(null);
        Assert.False(again.IsManualOrder);
        Assert.Equal(new[] { "A", "B", "C", "D" }, Names(again));
    }

    [Fact]
    public void RoutinesWithoutPositionGoLastInManualOrder()
    {
        RoutineSummary S(string name, int? order) => new() { Routine = new Routine { Name = name, SortOrder = order } };

        var sorted = RoutineViewModel.Sort([S("Importada", null), S("Dos", 2), S("Uno", 1)], activeId: null);

        Assert.Equal(new[] { "Uno", "Dos", "Importada" }, sorted.Select(s => s.Name));
    }

    // ── Eliminar con deshacer ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteCanBeUndoneUntilCommitted()
    {
        var a = AddRoutine("A", "Día");
        AddRoutine("B", "Día");
        await TrainAsync(a, "Día", Today.AddDays(-1));
        var vm = Vm();
        await vm.RefreshAsync();

        var item = vm.Items[0];
        Assert.Equal(1, await vm.CountSessionsAsync(item));
        await vm.DeleteWithUndoAsync(item);
        Assert.Equal(new[] { "B" }, Names(vm));
        Assert.Equal("«A» eliminada", vm.PendingDeleteText);

        await vm.RefreshAsync(); // volver a la pantalla no la resucita
        Assert.Equal(new[] { "B" }, Names(vm));

        vm.UndoDeleteCommand.Execute(null);
        Assert.Equal(new[] { "A", "B" }, Names(vm));
        using (var db = Db()) Assert.Equal(2, db.Routines.Count());

        await vm.DeleteWithUndoAsync(vm.Items[0]);
        await vm.CommitPendingDeleteAsync();
        using var check = Db();
        Assert.Equal("B", check.Routines.Single().Name);
        Assert.Empty(check.WorkoutSessions); // el historial se borra con la rutina
    }

    [Fact]
    public async Task StartingAnotherDeleteCommitsThePreviousOne()
    {
        AddRoutine("A", "Día");
        AddRoutine("B", "Día");
        AddRoutine("C", "Día");
        var vm = Vm();
        await vm.RefreshAsync();

        await vm.DeleteWithUndoAsync(vm.Items[0]);
        await vm.DeleteWithUndoAsync(vm.Items[0]);

        Assert.Equal("«B» eliminada", vm.PendingDeleteText);
        using var db = Db();
        Assert.Equal(new[] { "B", "C" }, db.Routines.Select(r => r.Name).OrderBy(n => n).ToArray());
    }

    // ── Panel «Añadir ejercicio» ──────────────────────────────────────────

    [Fact]
    public async Task PickerFiltersAndAddsInTheOrderChosen()
    {
        var routine = AddRoutine("A", "Día");
        var vm = new RoutineDetailViewModel(new RoutineRepository(Db()), new ExerciseRepository(Db()));
        await vm.InitializeAsync(routine);
        var picker = vm.Picker;

        picker.SearchText = "biceps"; // sin tilde
        Assert.Equal("Curl de bíceps", Assert.Single(picker.Items).Name);

        picker.SearchText = "";
        picker.GroupFilter = "Cuádriceps";
        picker.ToggleCommand.Execute(picker.Items.Single());   // Sentadilla
        picker.GroupFilter = null;
        picker.ToggleCommand.Execute(picker.Items.First(i => i.Name == "Press banca"));
        Assert.Equal("Añadir 2 ejercicios", picker.ConfirmText);

        await vm.AddSelectedExercisesCommand.ExecuteAsync(null);

        // Ya tenía Press banca: se puede repetir. Orden: el que había, Sentadilla, Press banca
        Assert.Equal(new[] { _benchId, _squatId, _benchId }, vm.DayExercises.Select(e => e.ExerciseId));
        Assert.Equal(new[] { 1, 2, 3 }, vm.DayExercises.Select(e => e.Order));
        Assert.Null(vm.DayExercises[1].TargetSets); // sin inventar series: se piden después
        Assert.Equal(0, picker.SelectedCount);
    }

    [Fact]
    public async Task DetailWarnsHowManySessionsADayDeleteRemoves()
    {
        var routine = AddRoutine("A", "Día 1", "Día 2");
        await TrainAsync(routine, "Día 1", Today.AddDays(-1));
        var vm = new RoutineDetailViewModel(new RoutineRepository(Db()), new ExerciseRepository(Db()));
        await vm.InitializeAsync(routine);

        Assert.Equal(1, await vm.CountSessionsOfDayAsync(vm.Days[0]));
        Assert.Equal(0, await vm.CountSessionsOfDayAsync(vm.Days[1]));
        Assert.Equal(1, await vm.CountSessionsOfRoutineAsync());
        Assert.Equal("2 días", vm.SubtitleText);
    }
}
