using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// WorkoutStateService es estático: las clases que lo tocan no se ejecutan en paralelo
[Collection("EstadoEntreno")]
public class PrescriptionEditorTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();
    private readonly int _dayId;
    private readonly int _itemId;

    public PrescriptionEditorTests()
    {
        _connection.Open();
        _user.SetCurrentUser(1);
        using var db = CreateContext();
        db.Database.Migrate();
        var profile = new UserProfile { Id = 1, Name = "Samuel" };
        var exercise = new Exercise { Name = "Press banca", MuscleGroup = "Pecho", UserProfile = profile };
        var day = new RoutineDay { Name = "Torso A", Order = 1 };
        var item = new RoutineDayExercise { Exercise = exercise, Order = 1, TargetSets = 3, TargetReps = 10 };
        day.Exercises.Add(item);
        var routine = new Routine { Name = "Torso / Pierna", UserProfile = profile };
        routine.Days.Add(day);
        db.Routines.Add(routine);
        db.SaveChanges();
        _dayId = day.Id;
        _itemId = item.Id;
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    public void Dispose() => _connection.Dispose();

    private (PrescriptionEditorViewModel editor, RoutineDayExercise item) OpenEditor(ViewOptionsService options)
    {
        var repo = new RoutineRepository(CreateContext());
        var item = repo.GetExercisesByDayIdAsync(_dayId).Result.Single();
        var editor = new PrescriptionEditorViewModel(repo, options);
        editor.Open(item);
        return (editor, item);
    }

    private static ViewOptionsService Options(UserProfile profile)
    {
        var o = new ViewOptionsService();
        o.Apply(profile);
        return o;
    }

    [Fact]
    public void SimpleViewShowsOnlyBasicFieldsForLegacyPrescription()
    {
        var (editor, _) = OpenEditor(new ViewOptionsService());

        Assert.Equal("Press banca", editor.ExerciseName);
        Assert.Equal(("3", "10"), (editor.Sets, editor.Reps));
        Assert.False(editor.ShowRepRange);
        Assert.False(editor.ShowIntensity);
        Assert.False(editor.ShowTempoAndRest);
        Assert.False(editor.ShowStructure);
    }

    [Fact]
    public void TrainerViewShowsAllFields()
    {
        var (editor, _) = OpenEditor(Options(new UserProfile { ViewMode = ProfileViewMode.Trainer }));
        Assert.True(editor.ShowRepRange && editor.ShowIntensity && editor.ShowTempoAndRest && editor.ShowStructure);
    }

    [Fact]
    public void SimpleViewNeverHidesExistingData()
    {
        // Lo puso un entrenador; el cliente usa la vista simple
        using (var db = CreateContext())
        {
            var item = db.RoutineDayExercises.Single();
            item.TargetRpe = 8;
            item.RestSeconds = 90;
            db.SaveChanges();
        }

        var (editor, _) = OpenEditor(new ViewOptionsService());
        Assert.True(editor.ShowIntensity);
        Assert.Equal("8", editor.Rpe);
        Assert.True(editor.ShowTempoAndRest);
        Assert.Equal("90", editor.RestSeconds);
        Assert.False(editor.ShowRepRange);
    }

    [Fact]
    public void SavesParsedValuesWithCommaDecimals()
    {
        var (editor, _) = OpenEditor(Options(new UserProfile { ViewMode = ProfileViewMode.Trainer }));
        var saved = false;
        editor.Saved += () => saved = true;

        editor.Sets = "4";
        editor.Reps = "6";
        editor.RepsMax = "8";
        editor.Weight = "72,5";
        editor.Rpe = "8.5";
        editor.Tempo = " 3-1-1-0 ";
        editor.RestSeconds = "90";
        editor.GroupLabel = "a1";
        editor.Notes = "Pausa en el pecho";
        editor.Save();

        Assert.True(saved);
        Assert.False(editor.IsOpen);
        Assert.Null(editor.Error);

        using var check = CreateContext();
        var p = check.RoutineDayExercises.Single(x => x.Id == _itemId);
        Assert.Equal((4, 6, 8), (p.TargetSets, p.TargetReps, p.TargetRepsMax));
        Assert.Equal((72.5, 8.5), (p.TargetWeight, p.TargetRpe));
        Assert.Equal(("3-1-1-0", 90, "A1", "Pausa en el pecho"), (p.Tempo, p.RestSeconds, p.GroupLabel, p.Notes));
    }

    [Fact]
    public void EditingInSimpleViewKeepsAdvancedValues()
    {
        using (var db = CreateContext())
        {
            db.RoutineDayExercises.Single().TargetRpe = 8;
            db.SaveChanges();
        }

        var (editor, _) = OpenEditor(new ViewOptionsService());
        editor.Weight = "70";
        editor.Save();

        using var check = CreateContext();
        var p = check.RoutineDayExercises.Single();
        Assert.Equal(8, p.TargetRpe);
        Assert.Equal(70, p.TargetWeight);
    }

    [Theory]
    [InlineData("Reps", "abc")]
    [InlineData("Sets", "0")]
    [InlineData("Rpe", "11")]
    [InlineData("Weight", "-5")]
    [InlineData("RestSeconds", "1,5")]
    public void InvalidInputShowsErrorAndDoesNotSave(string field, string value)
    {
        var (editor, _) = OpenEditor(Options(new UserProfile { ViewMode = ProfileViewMode.Trainer }));
        typeof(PrescriptionEditorViewModel).GetProperty(field)!.SetValue(editor, value);

        editor.Save();

        Assert.NotNull(editor.Error);
        Assert.True(editor.IsOpen);
        using var check = CreateContext();
        Assert.Equal((3, 10), (check.RoutineDayExercises.Single().TargetSets, check.RoutineDayExercises.Single().TargetReps));
    }

    [Fact]
    public void RangeMaxBelowMinIsRejected()
    {
        var (editor, _) = OpenEditor(Options(new UserProfile { ViewMode = ProfileViewMode.Trainer }));
        editor.Reps = "10";
        editor.RepsMax = "8";
        editor.Save();
        Assert.Contains("rango", editor.Error);
    }

    [Fact]
    public void ClearingAFieldRemovesTheValue()
    {
        var (editor, _) = OpenEditor(new ViewOptionsService());
        editor.Reps = "";
        editor.Save();

        using var check = CreateContext();
        Assert.Null(check.RoutineDayExercises.Single().TargetReps);
    }

    [Fact]
    public async Task WorkoutShowsPrescriptionOfSelectedExercise()
    {
        using (var db = CreateContext())
        {
            var p = db.RoutineDayExercises.Single();
            p.TargetRpe = 8;
            p.Notes = "Escápulas atrás";
            db.SaveChanges();
        }

        var workoutRepo = new WorkoutRepository(CreateContext());
        var vm = new WorkoutViewModel(workoutRepo, new RoutineRepository(CreateContext()));
        var session = await workoutRepo.CreateSessionFromRoutineDayAsync(_dayId);
        await vm.LoadSessionAsync(session.Id);
        await vm.LoadPrescriptionAsync();

        Assert.True(vm.HasPrescription);
        Assert.Equal("3 × 10 · RPE 8", vm.PrescriptionText);
        Assert.Equal("Escápulas atrás", vm.PrescriptionNotes);
    }
}
