using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// Pantalla de entreno: registrar series, sugerencias, récords, descanso y resumen
// WorkoutStateService es estático: las clases que lo tocan no se ejecutan en paralelo
[Collection("EstadoEntreno")]
public class WorkoutSessionTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();
    private readonly FakeClock _clock = new();
    private readonly int _dayId;
    private readonly int _benchId;

    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = At(DateTime.Now);
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public void Advance(int seconds) => Now = Now.AddSeconds(seconds);

        // Hora local de la sesión vista como "ahora" del reloj (zona UTC en las pruebas)
        public static DateTimeOffset At(DateTime local) => new(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.Zero);
    }

    public WorkoutSessionTests()
    {
        _connection.Open();
        _user.SetCurrentUser(1);
        WorkoutStateService.IsWorkoutActive = false;
        WorkoutStateService.ActiveSessionId = null;
        using var db = Db();
        db.Database.Migrate();
        db.UserProfiles.Add(new UserProfile { Id = 1, Name = "Laura" });
        db.UserProfiles.Add(new UserProfile { Id = 2, Name = "Pablo" });
        db.SaveChanges();

        // Press banca es de la biblioteca: lo comparten los dos perfiles
        var bench = new Exercise { Name = "Press banca", MuscleGroup = "Pecho", IsSystem = true, EquipmentType = EquipmentType.Barbell };
        var row = new Exercise { Name = "Remo", MuscleGroup = "Espalda", IsSystem = true, EquipmentType = EquipmentType.PlateMachine };
        db.Exercises.AddRange(bench, row);
        db.SaveChanges();
        _benchId = bench.Id;

        _dayId = AddDay(db, 1, (bench.Id, 2, 8, 120), (row.Id, 1, 10, null));
    }

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    // Guardado lento, como en el móvil: con SQLite en memoria terminaría antes del segundo toque
    private sealed class SlowSave : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await Task.Delay(50, cancellationToken);
            return result;
        }
    }

    public void Dispose() => _connection.Dispose();

    private int AddDay(AppDbContext db, int userId, params (int exerciseId, int sets, int reps, int? rest)[] items)
    {
        var day = new RoutineDay { Name = "Torso A", Order = 1 };
        var order = 1;
        foreach (var (exerciseId, sets, reps, rest) in items)
            day.Exercises.Add(new RoutineDayExercise { ExerciseId = exerciseId, Order = order++, TargetSets = sets, TargetReps = reps, RestSeconds = rest });
        var routine = new Routine { Name = "Mi rutina", UserId = userId };
        routine.Days.Add(day);
        db.Routines.Add(routine);
        db.SaveChanges();
        return day.Id;
    }

    // Sesión ya terminada del perfil (y cliente) actual, con series del primer ejercicio del día
    private async Task SeedFinishedSessionAsync(int dayId, DateTime date, params (double weight, int reps)[] sets)
    {
        using var db = Db();
        var repo = new WorkoutRepository(db);
        var session = await repo.CreateSessionFromRoutineDayAsync(dayId);
        session.Date = date;
        var first = db.WorkoutExercises.Where(we => we.WorkoutSessionId == session.Id).OrderBy(we => we.Id).First();
        foreach (var (weight, reps) in sets)
            db.WorkoutSets.Add(new WorkoutSet { WorkoutExerciseId = first.Id, Weight = weight, Reps = reps });
        await db.SaveChangesAsync();
    }

    private WorkoutViewModel Vm() => new(new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);

    private async Task<WorkoutViewModel> StartedVmAsync()
    {
        var vm = Vm();
        var session = await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(_dayId);
        _clock.Now = FakeClock.At(session.Date);
        await vm.LoadSessionAsync(session.Id);
        return vm;
    }

    private async Task DoSetAsync(WorkoutViewModel vm, double weight, int reps)
    {
        vm.Weight = weight;
        vm.Reps = reps;
        await vm.CompleteSetCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task ShowsHowToLoadTheBarWithTheGymPlates()
    {
        var vm = await StartedVmAsync();

        vm.Weight = 72.5;
        Assert.True(vm.ShowsPlates);
        // Sin discos de 1,25 (lo de siempre) no sale justo
        Assert.Equal("Por lado: 25, en barra de 20 · con tus discos sale 70 kg", vm.LoadText);
        vm.Weight = 100;
        Assert.Equal("Por lado: 25 + 15, en barra de 20", vm.LoadText);
        Assert.Equal(new[] { 25.0, 15 }, vm.Load!.PerSide);
        vm.Weight = 20;
        Assert.Equal("Solo la barra", vm.LoadText);
        vm.Weight = 10;
        Assert.Equal("Menos que la barra sola (20 kg)", vm.LoadText);

        // Máquina de discos: solo los discos, sin barra
        vm.NextExerciseCommand.Execute(null);
        Assert.True(vm.IsPlateMachine);
        vm.Weight = 110;
        Assert.Equal("Por lado: 25 + 25 + 5", vm.LoadText);
        vm.Weight = 0;
        Assert.False(vm.ShowsPlates);
    }

    [Fact]
    public async Task EachSetIsSavedWhenMarked()
    {
        var vm = await StartedVmAsync();

        await DoSetAsync(vm, 60, 8);

        using var db = Db();
        var set = db.WorkoutSets.Single();
        Assert.Equal((60.0, 8), (set.Weight, set.Reps));
        Assert.Equal("Serie 2 de 2", vm.CurrentSetLabel);
    }

    [Fact]
    public async Task SuggestsWhatWasDoneLastTime()
    {
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddDays(-9), (65, 8));
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddDays(-2), (70, 8), (72.5, 7));

        var vm = await StartedVmAsync();

        Assert.Equal((70.0, 8), (vm.Weight, vm.Reps));
        Assert.Equal("La última vez: 72,5 kg × 7", vm.CurrentExercise!.LastTimeText);
        await DoSetAsync(vm, 70, 8);
        Assert.Equal((72.5, 7), (vm.Weight, vm.Reps));
    }

    [Fact]
    public async Task WithoutHistoryStartsFromThePrescription()
    {
        using (var db = Db())
        {
            db.RoutineDayExercises.First(r => r.ExerciseId == _benchId).TargetWeight = 50;
            db.SaveChanges();
        }

        var vm = await StartedVmAsync();

        Assert.Equal((50.0, 8), (vm.Weight, vm.Reps));
        Assert.Equal("", vm.CurrentExercise!.LastTimeText);
    }

    [Fact]
    public async Task HistoryOfAnotherProfileIsNotUsed()
    {
        // Pablo hace el mismo ejercicio de biblioteca con mucho más peso
        _user.SetCurrentUser(2);
        using (var db = Db())
        {
            var pabloDay = AddDay(db, 2, (_benchId, 3, 5, null));
            await SeedFinishedSessionAsync(pabloDay, DateTime.Now.AddDays(-1), (200, 5));
        }
        _user.SetCurrentUser(1);
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddDays(-3), (60, 8));

        var vm = await StartedVmAsync();

        Assert.Equal((60.0, 8), (vm.Weight, vm.Reps));
        await DoSetAsync(vm, 62.5, 8);
        Assert.Equal(RecordKind.Weight, vm.ExerciseStates[0].Sets[0].Record); // frente a lo suyo, no frente a Pablo
    }

    [Fact]
    public async Task HistoryOfAClientIsNotMixedWithYours()
    {
        var client = new Client { Name = "Marta" };
        await new ClientRepository(Db()).AddAsync(client);

        _user.SetCurrentClient(client.Id);
        int clientDay;
        using (var db = Db()) clientDay = AddDay(db, 1, (_benchId, 3, 5, null));
        await SeedFinishedSessionAsync(clientDay, DateTime.Now.AddDays(-1), (30, 12));
        _user.SetCurrentClient(null);

        var vm = await StartedVmAsync();

        Assert.Equal("", vm.CurrentExercise!.LastTimeText);
        Assert.Equal(0, vm.Weight);
    }

    [Fact]
    public async Task RecordOnlyWhenBeatingPreviousBest()
    {
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddDays(-2), (70, 8), (72.5, 7));
        var vm = await StartedVmAsync();
        var records = new List<bool>();
        vm.SetCompleted += records.Add;

        await DoSetAsync(vm, 72.5, 8); // con 72,5 kg nunca había pasado de 7
        await DoSetAsync(vm, 70, 8);   // igual que la última vez

        Assert.Equal(new[] { true, false }, records);
        Assert.Equal(new[] { RecordKind.Reps, RecordKind.None }, vm.ExerciseStates[0].Sets.Select(s => s.Record));
        Assert.Equal("★ Récord de reps", vm.ExerciseStates[0].Sets[0].RecordText);
    }

    [Theory]
    [InlineData(102.5, 3, RecordKind.Weight)] // más kilos que nunca, aunque sean pocas reps
    [InlineData(100, 6, RecordKind.Reps)]     // con 100 kg nunca había pasado de 5
    [InlineData(100, 5, RecordKind.None)]     // igual que el mejor
    [InlineData(97.5, 5, RecordKind.None)]    // menos peso y las mismas reps: no
    [InlineData(95, 7, RecordKind.None)]      // con 95 kg ya había hecho 8
    [InlineData(95, 9, RecordKind.Reps)]
    public async Task RecordIsByWeightOrByRepsAtThatWeight(double weight, int reps, RecordKind expected)
    {
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddDays(-5), (95, 8));
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddDays(-2), (100, 5));
        var vm = await StartedVmAsync();

        await DoSetAsync(vm, weight, reps);

        Assert.Equal(expected, vm.ExerciseStates[0].Sets[0].Record);
    }

    [Fact]
    public async Task EarlierSetsOfTodayRaiseTheBar()
    {
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddDays(-2), (100, 5));
        var vm = await StartedVmAsync();

        await DoSetAsync(vm, 102.5, 3);
        vm.SelectExercise(vm.ExerciseStates[0]);
        await DoSetAsync(vm, 102.5, 3);

        Assert.Equal(new[] { RecordKind.Weight, RecordKind.None }, vm.ExerciseStates[0].Sets.Select(s => s.Record));
    }

    [Fact]
    public async Task BodyweightExerciseCountsRepRecords()
    {
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddDays(-2), (0, 12));
        var vm = await StartedVmAsync();

        await DoSetAsync(vm, 0, 13);

        Assert.Equal(RecordKind.Reps, vm.ExerciseStates[0].Sets[0].Record);
    }

    [Fact]
    public async Task FirstWorkoutHasNoRecords()
    {
        var vm = await StartedVmAsync();

        await DoSetAsync(vm, 100, 5);

        Assert.False(vm.ExerciseStates[0].Sets[0].IsRecord);
    }

    [Fact]
    public async Task RestCountsDownWithTheClock()
    {
        var vm = await StartedVmAsync();
        var finished = 0;
        vm.RestFinished += () => finished++;

        await DoSetAsync(vm, 60, 8);
        Assert.True(vm.IsResting);
        Assert.Equal(120, vm.RestRemainingSeconds); // lo prescrito
        Assert.Equal("2:00", vm.RestRemainingText);

        _clock.Advance(30);
        vm.Tick();
        Assert.Equal(90, vm.RestRemainingSeconds);
        Assert.Equal(0.75, vm.RestProgress, 3);

        vm.AddRestTimeCommand.Execute(null);
        Assert.Equal(105, vm.RestRemainingSeconds);
        vm.SubtractRestTimeCommand.Execute(null);
        Assert.Equal(90, vm.RestRemainingSeconds);

        _clock.Advance(200);
        vm.Tick();
        Assert.False(vm.IsResting);
        Assert.Equal(1, finished);
    }

    [Fact]
    public async Task RestDefaultsWhenNotPrescribed()
    {
        var vm = await StartedVmAsync();
        vm.SelectExercise(vm.ExerciseStates[1]); // Remo, sin descanso indicado... y con 1 serie
        await DoSetAsync(vm, 40, 10);

        Assert.True(vm.IsResting);
        Assert.Equal(WorkoutViewModel.DefaultRestSeconds, vm.RestRemainingSeconds);
        Assert.Same(vm.ExerciseStates[0], vm.CurrentExercise); // Remo hecho: vuelve al pendiente
    }

    [Fact]
    public async Task RepeatedExerciseKeepsItsOwnPrescription()
    {
        int dayId;
        using (var db = Db())
            dayId = AddDay(db, 1, (_benchId, 4, 5, null), (_benchId, 2, 12, null));
        var vm = Vm();
        var session = await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(dayId);
        await vm.LoadSessionAsync(session.Id);

        Assert.Equal(new int?[] { 4, 2 }, vm.ExerciseStates.Select(e => e.TargetSets));
        Assert.Equal("Torso A · ejercicio 1 de 2", vm.PositionText);
    }

    [Fact]
    public async Task LastSetOfLastExerciseOpensTheSummary()
    {
        var vm = await StartedVmAsync();

        await DoSetAsync(vm, 60, 8);
        await DoSetAsync(vm, 60, 8);
        Assert.Equal("Remo", vm.CurrentExercise!.Name);
        _clock.Advance(25 * 60);
        await DoSetAsync(vm, 40, 10);

        Assert.True(vm.IsFinishing);
        Assert.False(vm.IsResting);
        Assert.False(vm.IsIncomplete);
        Assert.Equal(3, vm.SummarySets);
        Assert.Equal("1,4 t", vm.SummaryVolume); // 60·8·2 + 40·10 = 1360 kg
        Assert.Equal("25:00", vm.SummaryDuration);
    }

    [Fact]
    public async Task ContinuingAfterTheSummaryDoesNotReopenIt()
    {
        var vm = await StartedVmAsync();
        await DoSetAsync(vm, 60, 8);
        await DoSetAsync(vm, 60, 8);
        await DoSetAsync(vm, 40, 10);
        Assert.True(vm.IsFinishing);

        vm.ContinueWorkoutCommand.Execute(null);
        await DoSetAsync(vm, 40, 10); // una serie de más

        Assert.False(vm.IsFinishing);
        Assert.True(vm.IsResting);
    }

    [Fact]
    public async Task DoubleTapOnSharedContextRecordsOneSet()
    {
        // En la app los dos repositorios comparten el mismo DbContext
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).AddInterceptors(new SlowSave()).Options, _user);
        var vm = new WorkoutViewModel(new WorkoutRepository(db), new RoutineRepository(db), _clock);
        var session = await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(_dayId);
        await vm.LoadSessionAsync(session.Id);
        vm.Weight = 60;
        vm.Reps = 8;

        var first = vm.CompleteSetCommand.ExecuteAsync(null);
        var second = vm.CompleteSetCommand.ExecuteAsync(null);
        await Task.WhenAll(first, second);

        using var check = Db();
        Assert.Single(check.WorkoutSets);
        Assert.Single(vm.ExerciseStates[0].Sets);
    }

    [Fact]
    public async Task DayWithoutExercisesCannotStart()
    {
        int routineId;
        using (var db = Db())
        {
            var emptyDay = AddDay(db, 1);
            routineId = db.RoutineDays.Single(d => d.Id == emptyDay).RoutineId;
        }
        var vm = Vm();
        await vm.RefreshAsync();
        vm.PickRoutine(vm.Routines.Single(r => r.Id == routineId));
        await vm.RefreshAsync();
        Assert.Equal(routineId, vm.SelectedRoutineDay!.RoutineId);

        Assert.False(vm.CanStart);
        await vm.StartWorkoutCommand.ExecuteAsync(null);

        Assert.False(vm.IsSessionActive);
        using var check = Db();
        Assert.Empty(check.WorkoutSessions);
    }

    [Fact]
    public async Task AbandonedSessionIsNotComplete()
    {
        var vm = await StartedVmAsync();
        await DoSetAsync(vm, 60, 8);

        // La app se cierra aquí: la sesión queda guardada pero sin terminar
        using var db = Db();
        Assert.False(db.WorkoutSessions.Single().IsComplete);
    }

    [Fact]
    public async Task FinishingEarlyWarnsAndSavesAsIncomplete()
    {
        var vm = await StartedVmAsync();
        await DoSetAsync(vm, 60, 8);
        _clock.Advance(40 * 60);

        await vm.FinishCommand.ExecuteAsync(null);
        Assert.True(vm.IsFinishing);
        Assert.Equal("Te faltan series en 2 ejercicios (Press banca, Remo). Se guardará como incompleto.", vm.MissingText);

        vm.SelectFeelingCommand.Execute("4");
        vm.FinishNotes = "  Hombro cargado ";
        await vm.SaveWorkoutCommand.ExecuteAsync(null);

        Assert.False(vm.IsSessionActive);
        Assert.False(WorkoutStateService.IsWorkoutActive);
        using var db = Db();
        var saved = db.WorkoutSessions.Single();
        Assert.Equal((false, 4, "Hombro cargado", 40), (saved.IsComplete, saved.Feeling, saved.Notes, saved.DurationMinutes));
    }

    [Fact]
    public async Task OneMissingSetIsNamed()
    {
        var vm = await StartedVmAsync();
        await DoSetAsync(vm, 60, 8);
        vm.SelectExercise(vm.ExerciseStates[1]);
        await DoSetAsync(vm, 40, 10);

        await vm.FinishCommand.ExecuteAsync(null);

        Assert.Equal("Te falta 1 serie de Press banca. Se guardará como incompleto.", vm.MissingText);
    }

    [Fact]
    public async Task DeletingASetRemovesItAndRenumbers()
    {
        var vm = await StartedVmAsync();
        await DoSetAsync(vm, 60, 8);
        vm.SelectExercise(vm.ExerciseStates[0]);
        await DoSetAsync(vm, 65, 6);

        var bench = vm.ExerciseStates[0];
        await vm.DeleteSetCommand.ExecuteAsync(bench.Sets[0]);

        Assert.Equal(new[] { 1 }, bench.Sets.Select(s => s.Number));
        Assert.Equal(65, bench.Sets[0].Set.Weight);
        using var db = Db();
        Assert.Equal(65, db.WorkoutSets.Single().Weight);
    }

    [Fact]
    public async Task DiscardDeletesTheSession()
    {
        var vm = await StartedVmAsync();
        await DoSetAsync(vm, 60, 8);

        await vm.DiscardAsync();

        using var db = Db();
        Assert.Empty(db.WorkoutSessions);
        Assert.Empty(db.WorkoutSets);
        Assert.False(vm.IsSessionActive);
    }

    [Fact]
    public async Task VolumeIsComparedWithLastTime()
    {
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddDays(-2), (50, 10)); // 500 kg
        var vm = await StartedVmAsync();

        await DoSetAsync(vm, 53, 10); // 530 kg

        Assert.Equal("530 kg", vm.TodayVolumeText);
        Assert.Equal("+6 %", vm.VolumeDeltaText);
        Assert.True(vm.IsVolumeUp);
    }

    [Fact]
    public async Task WeightCanBeTypedWithComma()
    {
        var vm = await StartedVmAsync();

        Assert.True(vm.TrySetWeight("72,5"));
        Assert.Equal("72,5", vm.WeightText);
        Assert.False(vm.TrySetWeight("mucho"));
        vm.DecreaseWeightCommand.Execute(null);
        Assert.Equal(70, vm.Weight);
    }

    [Fact]
    public async Task PrepareProposesTheActiveRoutineAndTheDayThatIsNext()
    {
        int otherRoutine, nextDay;
        using (var db = Db())
        {
            // «A Full body» va antes por nombre, pero la última entrenada es «Mi rutina»
            var full = AddDay(db, 1, (_benchId, 3, 5, null));
            otherRoutine = db.RoutineDays.Single(d => d.Id == full).RoutineId;
            db.Routines.Single(r => r.Id == otherRoutine).Name = "A Full body";
            var mine = db.RoutineDays.Single(d => d.Id == _dayId);
            nextDay = AddDayTo(db, mine.RoutineId, "Pierna A", 2);
            db.SaveChanges();
        }
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddHours(-3), (60, 8));

        var vm = Vm();
        await vm.RefreshAsync();

        Assert.Equal("Mi rutina", vm.SelectedRoutine!.Name);
        Assert.Equal(nextDay, vm.SelectedRoutineDay!.Id);
        Assert.True(vm.DayOptions.Single(o => o.Day.Id == nextDay).IsNext);

        // Lo elegido a mano se respeta al volver a la pestaña
        vm.PickRoutine(vm.Routines.Single(r => r.Id == otherRoutine));
        await vm.RefreshAsync();
        Assert.Equal("A Full body", vm.SelectedRoutine!.Name);

        vm.PickRoutine(vm.Routines.Single(r => r.Name == "Mi rutina"));
        await vm.RefreshAsync();
        vm.SelectDay(vm.DayOptions.Single(o => o.Day.Id == _dayId));
        await vm.RefreshAsync();
        Assert.Equal(_dayId, vm.SelectedRoutineDay!.Id);
    }

    [Fact]
    public async Task PrepareProposesTheMarkedRoutine()
    {
        int planRoutine;
        using (var db = Db())
        {
            var planDay = AddDay(db, 1, (_benchId, 3, 5, null));
            planRoutine = db.RoutineDays.Single(d => d.Id == planDay).RoutineId;
        }
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddHours(-3), (60, 8)); // la otra es la última entrenada
        await new RoutineRepository(Db()).SetMarkedActiveAsync(planRoutine);

        var vm = Vm();
        await vm.RefreshAsync();

        Assert.Equal(planRoutine, vm.SelectedRoutine!.Id);
    }

    [Fact]
    public async Task AfterAWorkoutItProposesTheNextDay()
    {
        int nextDay;
        using (var db = Db())
        {
            var mine = db.RoutineDays.Single(d => d.Id == _dayId);
            nextDay = AddDayTo(db, mine.RoutineId, "Pierna A", 2);
        }
        var vm = Vm();
        await vm.RefreshAsync();
        Assert.Equal(_dayId, vm.SelectedRoutineDay!.Id); // nunca entrenada: el primero

        _clock.Now = FakeClock.At(DateTime.Now);
        await vm.StartWorkoutCommand.ExecuteAsync(null);
        await DoSetAsync(vm, 60, 8);
        await vm.FinishCommand.ExecuteAsync(null);
        await vm.SaveWorkoutCommand.ExecuteAsync(null);
        await vm.RefreshAsync();

        Assert.Equal(nextDay, vm.SelectedRoutineDay!.Id);
    }

    private static int AddDayTo(AppDbContext db, int routineId, string name, int order)
    {
        var day = new RoutineDay { RoutineId = routineId, Name = name, Order = order };
        day.Exercises.Add(new RoutineDayExercise { ExerciseId = db.Exercises.First().Id, Order = 1, TargetSets = 1 });
        db.RoutineDays.Add(day);
        db.SaveChanges();
        return day.Id;
    }

    [Fact]
    public async Task PrepareListsDaysWithWhenTheyWereLastDone()
    {
        await SeedFinishedSessionAsync(_dayId, DateTime.Now.AddDays(-7), (60, 8));
        var vm = Vm();
        await vm.RefreshAsync();

        var option = Assert.Single(vm.DayOptions);
        Assert.Equal("Hace 7 días", option.LastDoneText);
        Assert.True(option.IsSelected);
        Assert.Equal("▶ Empezar Torso A", vm.StartButtonText);
    }
}
