using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Tests;

// Comprueba que la migración AddSharingAndPrescription actualiza una base de datos con datos ya existentes
public class MigrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sp-migration-{Guid.NewGuid():N}.db");

    // Los filtros por perfil de AppDbContext necesitan un usuario actual para poder consultar
    private sealed class TestUser : ICurrentUserService
    {
        public int? CurrentUserId => 1;
        public bool HasUser => true;
        public void SetCurrentUser(int userId) { }
        public void ClearCurrentUser() { }
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_dbPath}").Options, new TestUser());

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    private void SeedBeforeMigration()
    {
        using var db = CreateContext();
        db.GetService<IMigrator>().Migrate("20260611120000_AddProfileDescription");

        db.Database.ExecuteSqlRaw("""
            INSERT INTO UserProfiles (Id, Name, AvatarColor) VALUES (1, 'Samuel', '#89B4FA');
            INSERT INTO Exercises (Id, Name, MuscleGroup, UserId) VALUES (1, 'Press banca', 'Pecho', 1), (2, 'Sentadilla', 'Pierna', 1);
            INSERT INTO Routines (Id, UserId, Name) VALUES (1, 1, 'Torso / Pierna'), (2, 1, 'Full body');
            INSERT INTO RoutineDays (Id, RoutineId, Name, "Order") VALUES (1, 1, 'Torso A', 1), (2, 1, 'Pierna A', 2);
            INSERT INTO RoutineDayExercises (Id, RoutineDayId, ExerciseId, "Order", TargetSets, TargetReps) VALUES (1, 1, 1, 1, 4, 8), (2, 2, 2, 1, 5, 5);
            INSERT INTO WorkoutSessions (Id, UserId, Date, RoutineDayId) VALUES (1, 1, '2026-10-20 18:00:00', 1), (2, 1, '2026-10-22 18:00:00', 2);
            """);
    }

    // Si falla, falta una migración o el snapshot quedó desfasado (p. ej. tras fusionar ramas con migraciones)
    [Fact]
    public void ModelSnapshotMatchesCurrentModel()
    {
        using var db = CreateContext();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        if (snapshot is IMutableModel mutable)
            snapshot = mutable.FinalizeModel();
        snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot);

        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshot.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Empty(differences);
    }

    [Fact]
    public void ExistingRowsGetUniqueUidsAndSensibleDefaults()
    {
        SeedBeforeMigration();

        using (var db = CreateContext())
            db.Database.Migrate();

        using var check = CreateContext();
        var uids = new List<Guid>();
        uids.AddRange(check.Exercises.Select(e => e.Uid));
        uids.AddRange(check.Routines.Select(r => r.Uid));
        uids.AddRange(check.RoutineDays.Select(d => d.Uid));
        uids.AddRange(check.RoutineDayExercises.Select(x => x.Uid));
        uids.AddRange(check.WorkoutSessions.Select(s => s.Uid));

        Assert.Equal(10, uids.Count);
        Assert.DoesNotContain(Guid.Empty, uids);
        Assert.Equal(uids.Count, uids.Distinct().Count());

        Assert.All(check.Routines, r => Assert.Equal(1, r.Version));
        Assert.All(check.Routines, r => Assert.Null(r.SortOrder)); // orden automático hasta que se ordene a mano
        Assert.All(check.Routines, r => Assert.False(r.IsMarkedActive)); // activa automática hasta que se elija
        Assert.All(check.WorkoutSessions, s => Assert.True(s.IsComplete));

        var profile = check.UserProfiles.Single();
        Assert.Equal(ProfileViewMode.Simple, profile.ViewMode);
        Assert.Equal(SimpleViewBlocks.None, profile.SimpleViewBlocks);
        Assert.False(profile.TrainsSelf);

        // Los datos de antes siguen intactos
        var prescription = check.RoutineDayExercises.Single(x => x.Id == 1);
        Assert.Equal(4, prescription.TargetSets);
        Assert.Equal(8, prescription.TargetReps);
        Assert.Null(prescription.TargetRepsMax);
    }

    [Fact]
    public void BackfilledUidCanBeFoundByValue()
    {
        SeedBeforeMigration();
        using (var db = CreateContext())
            db.Database.Migrate();

        using var check = CreateContext();
        var uid = check.Exercises.Single(e => e.Id == 1).Uid;

        // Buscar por Uid tiene que funcionar: así se reconocerán los ejercicios al importar
        Assert.Equal(1, check.Exercises.Single(e => e.Uid == uid).Id);
    }

    // AddProfileKind: la antigua «vista entrenador» es la detallada, y solo con clientes el perfil es de entrenador
    [Fact]
    public void AddProfileKind_SoloLosPerfilesConClientesPasanAEntrenador()
    {
        using (var db = CreateContext())
        {
            db.GetService<IMigrator>().Migrate("20261029201625_AddProfileGym");
            db.Database.ExecuteSqlRaw("""
                INSERT INTO UserProfiles (Id, Name, AvatarColor, ViewMode, SimpleViewBlocks, TrainsSelf) VALUES
                    (1, 'Samuel', '#89B4FA', 1, 0, 0),
                    (2, 'Laura', '#A6E3A1', 1, 0, 0),
                    (3, 'Álex', '#F9E2AF', 0, 0, 0);
                INSERT INTO Clients (Id, Uid, UserId, Name, AvatarColor, CreatedAt) VALUES
                    (1, '6f1c2a3e-0000-4000-8000-000000000001', 2, 'Marta', '#FAB387', '2026-10-01 10:00:00');
                """);
            db.Database.Migrate();
        }

        using var check = CreateContext();
        var profiles = check.UserProfiles.IgnoreQueryFilters().OrderBy(p => p.Id)
            .Select(p => new { p.Kind, p.ViewMode }).ToList();
        Assert.Equal(new { Kind = ProfileKind.Personal, ViewMode = ProfileViewMode.Detailed }, profiles[0]);  // entrenaba él mismo
        Assert.Equal(new { Kind = ProfileKind.Coach, ViewMode = ProfileViewMode.Detailed }, profiles[1]);     // tiene clientes
        Assert.Equal(new { Kind = ProfileKind.Personal, ViewMode = ProfileViewMode.Simple }, profiles[2]);
    }

    [Fact]
    public void NewPrescriptionAndSessionFieldsRoundTrip()
    {
        using (var db = CreateContext())
        {
            db.Database.Migrate();
            var profile = new UserProfile { Name = "Laura", ViewMode = ProfileViewMode.Detailed, SimpleViewBlocks = SimpleViewBlocks.Intensity | SimpleViewBlocks.TempoAndRest };
            var exercise = new Exercise { Name = "Press banca", MuscleGroup = "Pecho", Equipment = "Barra", UserProfile = profile };
            var day = new RoutineDay { Name = "Torso A", Order = 1 };
            day.Exercises.Add(new RoutineDayExercise
            {
                Exercise = exercise, Order = 1, TargetSets = 4, TargetReps = 6, TargetRepsMax = 8,
                TargetRpe = 8, Tempo = "3-1-1-0", RestSeconds = 90, GroupLabel = "A1", Notes = "Pausa en el pecho"
            });
            var routine = new Routine { Name = "Torso / Pierna", UserProfile = profile, AuthorName = "Laura", Version = 3 };
            routine.Days.Add(day);
            db.Routines.Add(routine);
            db.WorkoutSessions.Add(new WorkoutSession { UserProfile = profile, RoutineDay = day, Date = DateTime.Now, Feeling = 4, DurationMinutes = 58, IsComplete = false });
            db.SaveChanges();
        }

        using var check = CreateContext();
        var p = check.RoutineDayExercises.Single();
        Assert.Equal((6, 8, 8.0, "3-1-1-0", 90, "A1"), (p.TargetReps, p.TargetRepsMax, p.TargetRpe, p.Tempo, p.RestSeconds, p.GroupLabel));
        Assert.Equal(3, check.Routines.Single().Version);
        var s = check.WorkoutSessions.Single();
        Assert.Equal((4, 58, false), (s.Feeling, s.DurationMinutes, s.IsComplete));
        Assert.Equal(SimpleViewBlocks.Intensity | SimpleViewBlocks.TempoAndRest, check.UserProfiles.Single().SimpleViewBlocks);
    }
}
