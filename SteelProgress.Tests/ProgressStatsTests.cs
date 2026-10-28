using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// Estadísticas de Inicio y Progreso: récords del historial, semanas, volumen y calendario
// WorkoutStateService y WorkoutLaunch son estáticos: no se ejecutan en paralelo con Entreno
[Collection("EstadoEntreno")]
public class ProgressStatsTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();
    private readonly int _routineId;
    private readonly int _dayA;
    private readonly int _dayB;
    private readonly int _benchId;

    // Miércoles 28 de octubre de 2026
    private static readonly DateTime Today = new(2026, 10, 28);

    private sealed class FixedClock(DateTime local) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private readonly FixedClock _clock = new(Today.AddHours(10));

    public ProgressStatsTests()
    {
        _connection.Open();
        _user.SetCurrentUser(1);
        WorkoutStateService.IsWorkoutActive = false;
        WorkoutStateService.ActiveSessionId = null;
        WorkoutLaunch.Take();
        using var db = Db();
        db.Database.Migrate();
        db.UserProfiles.Add(new UserProfile { Id = 1, Name = "Laura" });
        var bench = new Exercise { Name = "Press banca", MuscleGroup = "Pecho", IsSystem = true };
        var squat = new Exercise { Name = "Sentadilla", MuscleGroup = "Pierna", IsSystem = true };
        db.Exercises.AddRange(bench, squat);
        db.SaveChanges();
        _benchId = bench.Id;

        var routine = new Routine { Name = "Torso / Pierna", UserId = 1 };
        var a = new RoutineDay { Name = "Torso A", Order = 1 };
        a.Exercises.Add(new RoutineDayExercise { ExerciseId = bench.Id, Order = 1, TargetSets = 3, TargetReps = 8 });
        var b = new RoutineDay { Name = "Pierna A", Order = 2 };
        b.Exercises.Add(new RoutineDayExercise { ExerciseId = squat.Id, Order = 1, TargetSets = 3, TargetReps = 5 });
        routine.Days.Add(a);
        routine.Days.Add(b);
        db.Routines.Add(routine);
        db.SaveChanges();
        (_routineId, _dayA, _dayB) = (routine.Id, a.Id, b.Id);
    }

    public void Dispose()
    {
        WorkoutStateService.IsWorkoutActive = false;
        WorkoutStateService.ActiveSessionId = null;
        WorkoutLaunch.Take();
        _connection.Dispose();
    }

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    private async Task<int> SeedAsync(int dayId, DateTime date, params (double weight, int reps)[] sets) =>
        await SeedAsync(dayId, date, warmups: Array.Empty<(double, int)>(), sets);

    private async Task<int> SeedAsync(int dayId, DateTime date, (double weight, int reps)[] warmups, params (double weight, int reps)[] sets)
    {
        using var db = Db();
        var session = await new WorkoutRepository(db).CreateSessionFromRoutineDayAsync(dayId);
        session.Date = date;
        session.DurationMinutes = 50;
        var first = db.WorkoutExercises.Where(we => we.WorkoutSessionId == session.Id).OrderBy(we => we.Id).First();
        foreach (var (weight, reps) in warmups)
            db.WorkoutSets.Add(new WorkoutSet { WorkoutExerciseId = first.Id, Weight = weight, Reps = reps, IsWarmup = true });
        foreach (var (weight, reps) in sets)
            db.WorkoutSets.Add(new WorkoutSet { WorkoutExerciseId = first.Id, Weight = weight, Reps = reps });
        await db.SaveChangesAsync();
        return session.Id;
    }

    private static LoggedSet Set(int session, DateTime date, double weight, int reps, int exercise = 1)
    {
        _setId++;
        return new LoggedSet(session, date, exercise, "Press banca", _setId, weight, reps);
    }
    private static int _setId;

    private static LoggedSession Session(int id, DateTime date) => new(id, date, 50, null, null, true, 1, "Torso A", "Rutina");

    // ── Récords ──

    [Fact]
    public void HistoryRecordsFollowTheWorkoutRule()
    {
        var d1 = Today.AddDays(-14);
        var d2 = Today.AddDays(-7);
        var d3 = Today;
        var sets = new[]
        {
            Set(1, d1, 60, 8),                         // primera vez: nunca es récord
            Set(2, d2, 62.5, 5), Set(2, d2, 60, 9),    // récord de peso y de reps: cuenta el de peso
            Set(3, d3, 50, 9),                         // menos peso y las mismas reps que con 60: no
            Set(3, d3, 55, 10),                        // más reps que nunca con 55 o más: récord de reps
        };

        var records = TrainingStats.Records(sets);

        Assert.Equal(2, records.Count);
        Assert.Equal((3, 55.0, 10, RecordKind.Reps), (records[0].SessionId, records[0].Weight, records[0].Reps, records[0].Kind));
        Assert.Equal((2, 62.5, 5, RecordKind.Weight), (records[1].SessionId, records[1].Weight, records[1].Reps, records[1].Kind));
        Assert.Equal(2, TrainingStats.RecordsThisMonth(records, Today));
    }

    [Fact]
    public void LessWeightWithTheSameRepsIsNotARecord()
    {
        // Ya se hicieron 8 reps con más peso: con 75 × 8 no se mejora nada
        var sets = new[] { Set(1, Today.AddDays(-3), 80, 8), Set(2, Today, 75, 8) };
        Assert.Empty(TrainingStats.Records(sets));
    }

    // ── Semanas ──

    [Fact]
    public void StreakDoesNotBreakBeforeTrainingThisWeek()
    {
        // Semanas anteriores seguidas; esta semana todavía nada
        var sessions = new[] { Session(1, Today.AddDays(-7)), Session(2, Today.AddDays(-14)), Session(3, Today.AddDays(-28)) };
        Assert.Equal(2, TrainingStats.WeekStreak(sessions, Today));

        var withToday = sessions.Append(Session(4, Today)).ToList();
        Assert.Equal(3, TrainingStats.WeekStreak(withToday, Today));
        Assert.Equal(0, TrainingStats.WeekStreak(new[] { Session(1, Today.AddDays(-21)) }, Today));
    }

    [Fact]
    public void WeekStripStartsOnMonday()
    {
        var week = TrainingStats.Week(new[] { Session(1, Today.AddDays(-2).AddHours(18)) }, Today);

        Assert.Equal(7, week.Count);
        Assert.Equal(new DateTime(2026, 10, 26), week[0].Date);
        Assert.Equal("L", week[0].Letter);
        Assert.True(week[0].Done);
        Assert.True(week[2].IsToday);
        Assert.True(week[3].IsFuture);
        Assert.Equal(1, TrainingStats.SessionsThisWeek(new[] { Session(1, Today.AddDays(-2)), Session(2, Today.AddDays(-3)) }, Today));
    }

    // ── Volumen y calendario ──

    [Fact]
    public void MonthVolumeComparesTheSamePartOfLastMonth()
    {
        var sets = new[]
        {
            Set(1, new DateTime(2026, 10, 5), 100, 10),   // 1000 este mes
            Set(2, new DateTime(2026, 9, 20), 50, 10),    // 500 el mes pasado, antes del día 28
            Set(3, new DateTime(2026, 9, 29), 100, 10),   // después del día 28 del mes pasado: no cuenta
        };

        var (current, previous) = TrainingStats.MonthVolume(sets, Today);

        Assert.Equal(1000, current);
        Assert.Equal(500, previous);
        Assert.Equal("+100 %", TrainingStats.DeltaText(current, previous));
        Assert.Equal("1 t", TrainingStats.FormatVolume(current));
        Assert.Equal("480 kg", TrainingStats.FormatVolume(480));
    }

    [Fact]
    public void CalendarCoversWholeWeeksWithLevels()
    {
        var sets = new[] { Set(1, Today, 100, 10), Set(2, Today.AddDays(-1), 20, 10), Set(3, Today.AddDays(-2), 0, 12) };

        var days = TrainingStats.Calendar(sets, Today, 15);

        Assert.Equal(105, days.Count);
        Assert.Equal(DayOfWeek.Monday, days[0].Date.DayOfWeek);
        Assert.Equal(4, days.Single(d => d.IsToday).Level);
        Assert.Equal(1, days.Single(d => d.Date == Today.AddDays(-1)).Level);
        Assert.Equal(1, days.Single(d => d.Date == Today.AddDays(-2)).Level); // sin peso: cuenta igual
        Assert.True(days[^1].IsFuture);
    }

    // ── Base de datos ──

    [Fact]
    public async Task LogSkipsEmptySessionsAndWarmups()
    {
        await new WorkoutRepository(Db()).CreateSessionFromRoutineDayAsync(_dayA); // empezada y sin series
        await SeedAsync(_dayA, Today.AddDays(-1), warmups: new[] { (40.0, 10) }, (60, 8));

        var log = await new WorkoutRepository(Db()).GetTrainingLogAsync();

        Assert.Single(log.Sessions);
        Assert.Equal(("Torso A", "Torso / Pierna"), (log.Sessions[0].DayName, log.Sessions[0].RoutineName));
        var set = Assert.Single(log.Sets);
        Assert.Equal(("Press banca", 60.0, 8), (set.ExerciseName, set.Weight, set.Reps));
    }

    // ── Inicio ──

    [Fact]
    public async Task HomeShowsTodaysDayAndStartsItInWorkout()
    {
        await SeedAsync(_dayA, Today.AddDays(-2), (60, 8));
        await SeedAsync(_dayA, Today.AddDays(-9), (57.5, 8));

        var home = new HomeViewModel(Db(), _user, new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);
        await home.RefreshAsync();

        Assert.Equal("Pierna A", home.TodayDayName); // Torso A ya está hecho esta semana
        Assert.Equal(("Hoy toca", false), (home.TodayLabel, home.HasTodayDone));
        Assert.Equal("Torso / Pierna · activa", home.TodayRoutineText);
        Assert.Equal("1 ejercicio · sin entrenar aún", home.TodayDetail);
        Assert.Equal(new[] { "Sentadilla" }, home.TodayExercises);
        Assert.Equal(2, home.SessionCount);
        Assert.Equal(2, home.WeekStreak);
        Assert.Equal(1, home.MonthRecords); // 60 × 8 supera a 57,5 × 8
        Assert.Equal("Hace 2 días · 50 min · 480 kg", home.RecentSessions[0].Detail);

        Assert.True(home.RequestStartToday());
        var workout = new WorkoutViewModel(new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);
        Assert.Equal(_dayB, WorkoutLaunch.Take());
        await workout.StartDayAsync(_dayB);
        Assert.True(workout.IsSessionActive);
        Assert.Equal("Pierna A", workout.CurrentSession!.RoutineDay!.Name);
        Assert.Null(WorkoutLaunch.Take());
    }

    [Fact]
    public async Task HomeAveragesDurationOfTheDay()
    {
        await SeedAsync(_dayB, Today.AddDays(-9), (100, 5));
        await SeedAsync(_dayA, Today.AddDays(-8), (60, 8));

        var home = new HomeViewModel(Db(), _user, new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);
        await home.RefreshAsync();

        Assert.Equal("Pierna A", home.TodayDayName);
        Assert.Equal("1 ejercicio · ~50 min · última vez hace 9 días", home.TodayDetail);
        Assert.Equal(0, home.MonthRecords);
    }

    [Fact]
    public async Task AfterTrainingTodayTheCardShowsWhatIsNext()
    {
        await SeedAsync(_dayA, Today.AddHours(9), (60, 8));

        var home = new HomeViewModel(Db(), _user, new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);
        await home.RefreshAsync();

        Assert.Equal(("Siguiente", "Pierna A"), (home.TodayLabel, home.TodayDayName));
        Assert.Equal("✓ Hoy ya has entrenado Torso A", home.TodayDoneText);
    }

    [Fact]
    public async Task HomeShowsRenamesMadeInOtherScreens()
    {
        // En la app, Inicio conserva sus repositorios: lo renombrado en Rutinas (otro contexto) debe verse
        var home = new HomeViewModel(Db(), _user, new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);
        await home.RefreshAsync();
        Assert.Equal("Torso A", home.TodayDayName);

        using (var db = Db())
        {
            db.RoutineDays.Single(d => d.Id == _dayA).Name = "Empuje";
            db.Routines.Single(r => r.Id == _routineId).Name = "PPL";
            await db.SaveChangesAsync();
        }
        await home.RefreshAsync();

        Assert.Equal("Empuje", home.TodayDayName);
        Assert.StartsWith("PPL", home.TodayRoutineText);
    }

    [Fact]
    public async Task HomeWithAWorkoutRunningOffersToContinue()
    {
        WorkoutStateService.IsWorkoutActive = true;
        var home = new HomeViewModel(Db(), _user, new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);
        await home.RefreshAsync();

        Assert.True(home.IsWorkoutActive);
        Assert.True(home.RequestStartToday());
        Assert.Null(WorkoutLaunch.Take()); // no pide empezar otro
    }

    // ── Progreso ──

    [Fact]
    public async Task ProgressSummaryAndSessionDetail()
    {
        await SeedAsync(_dayA, Today.AddDays(-7), (60, 8));
        var last = await SeedAsync(_dayA, Today.AddDays(-1), (62.5, 6), (60, 9));

        var vm = new ProgressViewModel(new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);
        await vm.RefreshAsync();

        Assert.Equal((1, 2), (vm.WeekDone, vm.WeekGoal)); // objetivo: los 2 días de la rutina
        Assert.Equal("2 semanas", vm.StreakText);
        var record = Assert.Single(vm.RecentRecords);
        Assert.Equal(("Press banca", "62,5 × 6", "★ Récord de peso", "Ayer"), (record.ExerciseName, record.ValueText, record.Kind, record.WhenText));

        var exercise = Assert.Single(vm.ExerciseItems);
        Assert.Equal("Mejor: 62,5 kg × 6", exercise.BestText);
        vm.SelectExerciseById(_benchId);
        Assert.Same(exercise, vm.SelectedExercise);
        Assert.Equal(2, vm.Series.Length);

        vm.OpenSessionById(last);
        Assert.True(vm.IsHistory);
        var detail = vm.SessionDetail!;
        var ex = Assert.Single(detail.Exercises);
        Assert.Equal("62,5 kg × 6   60 kg × 9", ex.SetsText.Replace((char)0xA0, ' '));
        Assert.Equal("+2,5 kg respecto a la anterior", ex.ComparisonText);
        Assert.Equal("★ Récord de peso", ex.RecordText);
    }

    [Fact]
    public async Task ProgressCanOpenAnExerciseBeforeLoading()
    {
        await SeedAsync(_dayA, Today.AddDays(-1), (60, 8));
        var vm = new ProgressViewModel(new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);

        vm.SelectExerciseById(_benchId);
        await vm.RefreshAsync();

        Assert.True(vm.IsExercises);
        Assert.Equal(_benchId, vm.SelectedExercise!.Progress.ExerciseId);
    }

    [Fact]
    public async Task HistoryTellsInProgressUnsavedAndIncompleteApart()
    {
        var incomplete = await SeedAsync(_dayA, Today.AddDays(-3), (60, 8)); // guardado (50 min) con series sin hacer
        var unsaved = await SeedAsync(_dayA, Today.AddDays(-2), (60, 8));
        using (var db = Db())
        {
            db.WorkoutSessions.Single(s => s.Id == unsaved).DurationMinutes = null; // la app se cerró a medias
            await db.SaveChangesAsync();
        }

        var workout = new WorkoutViewModel(new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);
        await workout.StartDayAsync(_dayA);
        workout.Weight = 60;
        workout.Reps = 8;
        await workout.CompleteSetCommand.ExecuteAsync(null);
        var current = workout.CurrentSession!.Id;

        var vm = new ProgressViewModel(new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);
        await vm.RefreshAsync();
        string Status(int id) => vm.Sessions.Single(s => s.SessionId == id).StatusText;
        Assert.Equal(("En curso", "Sin guardar", "Incompleto"), (Status(current), Status(unsaved), Status(incomplete)));

        vm.OpenSessionById(current);
        Assert.Equal("Entreno en curso: aún no está guardado", vm.SessionDetail!.StatusText);

        await workout.DiscardAsync();
        Assert.Null(WorkoutStateService.ActiveSessionId);
    }

    // ── Notas por serie ──

    private static ViewOptionsService Options(ProfileViewMode mode, SimpleViewBlocks blocks = SimpleViewBlocks.None)
    {
        var options = new ViewOptionsService();
        options.Apply(new UserProfile { ViewMode = mode, SimpleViewBlocks = blocks });
        return options;
    }

    [Fact]
    public void SetNotesNeedTrainerViewOrDetailedNotes()
    {
        WorkoutViewModel Workout(ViewOptionsService? o) => new(new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock, o);

        Assert.False(Workout(null).CanNoteSets);
        Assert.False(Workout(Options(ProfileViewMode.Simple)).CanNoteSets);
        Assert.True(Workout(Options(ProfileViewMode.Simple, SimpleViewBlocks.DetailedNotes)).CanNoteSets);
        Assert.True(Workout(Options(ProfileViewMode.Trainer)).CanNoteSets);
    }

    [Fact]
    public async Task SetNoteShowsInHistoryAndNextTime()
    {
        var workout = new WorkoutViewModel(new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock, Options(ProfileViewMode.Trainer));
        await workout.StartDayAsync(_dayA);
        workout.Weight = 60;
        workout.Reps = 8;
        await workout.CompleteSetCommand.ExecuteAsync(null);
        workout.Reps = 6;
        await workout.CompleteSetCommand.ExecuteAsync(null);

        var second = workout.ExerciseStates[0].Sets[1];
        await workout.SetNoteAsync(second, "  molestia en el hombro ");
        Assert.Equal(("molestia en el hombro", true), (second.Notes, second.HasNotes));
        var sessionId = workout.CurrentSession!.Id;

        // Progreso → Historial: debajo de las series de ese ejercicio
        var progress = new ProgressViewModel(new WorkoutRepository(Db()), new RoutineRepository(Db()), _clock);
        await progress.RefreshAsync();
        progress.OpenSessionById(sessionId);
        var ex = Assert.Single(progress.SessionDetail!.Exercises);
        Assert.Equal("Serie 2: molestia en el hombro", ex.SetNotes);

        // La próxima vez, junto a «La última vez»
        var history = await new WorkoutRepository(Db()).GetExerciseHistoryAsync(_benchId, excludeSessionId: -1);
        var state = new WorkoutExerciseState(new WorkoutExercise(), null, history, 1);
        Assert.Equal("Serie 2: molestia en el hombro", state.LastTimeNotesText);

        // Vacía, se borra
        await workout.SetNoteAsync(second, " ");
        Assert.False(second.HasNotes);
        using var db = Db();
        Assert.Null(db.WorkoutSets.Single(s => s.Id == second.Set.Id).Notes);
    }

    // ── Material e instrucciones ──

    [Fact]
    public async Task ExerciseDetailsAreForTrainersButExistingOnesStayVisible()
    {
        var simple = new ExerciseViewModel(new ExerciseRepository(Db()), viewOptions: Options(ProfileViewMode.Simple));
        Assert.False(simple.ShowDetailFields);

        var trainer = new ExerciseViewModel(new ExerciseRepository(Db()), viewOptions: Options(ProfileViewMode.Trainer));
        trainer.ClearForm();
        Assert.True(trainer.ShowDetailFields);
        trainer.Name = "Remo en polea";
        trainer.MuscleGroup = "Espalda";
        trainer.Equipment = " Polea ";
        trainer.Instructions = "Espalda recta";
        await trainer.AddExerciseCommand.ExecuteAsync(null);

        Exercise saved;
        using (var db = Db())
            saved = db.Exercises.Single(e => e.Name == "Remo en polea");
        Assert.Equal(("Polea", "Espalda recta"), (saved.Equipment, saved.Instructions));

        // En vista simple no se ofrecen, pero si el ejercicio ya los tiene se ven
        simple.SelectedExercise = saved;
        Assert.True(simple.ShowDetailFields);
        Assert.Equal("Polea", simple.Equipment);
    }
}
