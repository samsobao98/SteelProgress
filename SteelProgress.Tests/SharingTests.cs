using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.Sharing;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// Exportar en un "móvil" (entrenadora) e importar en otro (cliente): cada uno con su base de datos y su carpeta
public class SharingTests : IDisposable
{
    private sealed class Phone : IDisposable
    {
        public readonly SqliteConnection Connection = new("Data Source=:memory:");
        public readonly CurrentUserService User = new();
        public readonly string MediaRoot = Path.Combine(Path.GetTempPath(), $"sp-share-{Guid.NewGuid():N}");
        public ExerciseMediaService Media => new(new FileMediaStorage(MediaRoot));

        public Phone(params string[] profiles)
        {
            Connection.Open();
            User.SetCurrentUser(1);
            using var db = Db();
            db.Database.Migrate();
            for (var i = 0; i < profiles.Length; i++)
                db.UserProfiles.Add(new UserProfile { Id = i + 1, Name = profiles[i] });
            db.SaveChanges();
        }

        public AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options, User);
        public int FilesOnDisk => Directory.Exists(MediaRoot) ? Directory.GetFiles(MediaRoot).Length : 0;

        public void Dispose()
        {
            Connection.Dispose();
            if (Directory.Exists(MediaRoot)) Directory.Delete(MediaRoot, true);
        }
    }

    private readonly Phone _coach = new("Laura");
    private readonly Phone _client = new("Marta", "Pablo");

    public void Dispose()
    {
        _coach.Dispose();
        _client.Dispose();
    }

    private async Task<int> CreateCoachRoutineAsync(bool withVideo = true)
    {
        using var db = _coach.Db();
        var bench = new Exercise { Name = "Press banca", MuscleGroup = "Pecho", Equipment = "Barra" };
        var row = new Exercise { Name = "Remo con barra", MuscleGroup = "Espalda" };
        db.Exercises.AddRange(bench, row);
        await db.SaveChangesAsync();

        if (withVideo)
        {
            var video = await _coach.Media.StoreAsync(new MemoryStream(new byte[2048]), "press.mp4");
            await new ExerciseRepository(db).AddMediaAsync(bench.Id, video);
        }

        var dayA = new RoutineDay { Name = "Torso A", Order = 1 };
        dayA.Exercises.Add(new RoutineDayExercise { Exercise = bench, Order = 1, TargetSets = 4, TargetReps = 6, TargetRepsMax = 8, TargetRpe = 8, GroupLabel = "A1", Notes = "Pausa en el pecho" });
        dayA.Exercises.Add(new RoutineDayExercise { Exercise = row, Order = 2, TargetSets = 4, TargetReps = 10, GroupLabel = "A2" });
        var dayB = new RoutineDay { Name = "Pierna A", Order = 2 };
        var routine = new Routine { Name = "Torso / Pierna", AuthorName = "Laura", Version = 3 };
        routine.Days.Add(dayA);
        routine.Days.Add(dayB);
        db.Routines.Add(routine);
        await db.SaveChangesAsync();
        return routine.Id;
    }

    private async Task<byte[]> ExportAsync(int routineId, bool includeMedia, string? message = null)
    {
        using var db = _coach.Db();
        var exporter = new PackageExporter(db, new FileMediaStorage(_coach.MediaRoot));
        using var ms = new MemoryStream();
        await exporter.ExportRoutineAsync(routineId, new ExportOptions(includeMedia, "Laura", message), ms);
        return ms.ToArray();
    }

    private async Task<(ImportPreview preview, ImportResult result)> ImportAsync(byte[] file, ImportMode mode = ImportMode.UpdateExisting)
    {
        using var package = SteelPackage.Open(new MemoryStream(file));
        using var db = _client.Db();
        var importer = new PackageImporter(db, _client.Media);
        var preview = await importer.PreviewAsync(package);
        var result = await importer.ImportAsync(package, mode);
        return (preview, result);
    }

    [Fact]
    public async Task RoutineTravelsWithPrescriptionAndVideo()
    {
        var file = await ExportAsync(await CreateCoachRoutineAsync(), includeMedia: true, message: "Empezamos el lunes");

        var (preview, result) = await ImportAsync(file);

        Assert.Equal("Torso / Pierna", preview.Title);
        Assert.Equal("Laura", preview.AuthorName);
        Assert.Equal("Empezamos el lunes", preview.Message);
        Assert.Equal((2, 2, 0, 1), (preview.DayCount, preview.NewExerciseCount, preview.ExistingExerciseCount, preview.MediaCount));
        Assert.False(preview.IsUpdate);
        Assert.Equal((1, 0, 2, 1), (result.RoutinesCreated, result.RoutinesUpdated, result.ExercisesCreated, result.MediaImported));

        using var db = _client.Db();
        var routine = db.Routines.Include(r => r.Days).ThenInclude(d => d.Exercises).ThenInclude(x => x.Exercise).Single();
        Assert.Equal(("Laura", 3), (routine.AuthorName, routine.Version));
        var bench = routine.Days.Single(d => d.Order == 1).Exercises.Single(x => x.Order == 1);
        Assert.Equal(("Press banca", 4, 6, 8, 8.0, "A1", "Pausa en el pecho"),
            (bench.Exercise!.Name, bench.TargetSets, bench.TargetReps, bench.TargetRepsMax, bench.TargetRpe, bench.GroupLabel, bench.Notes));
        Assert.Equal("Barra", bench.Exercise.Equipment);
        Assert.Single(db.ExerciseMedia.Where(m => m.ExerciseId == bench.ExerciseId));
        Assert.Equal(1, _client.FilesOnDisk);
        Assert.All(db.Routines, r => Assert.Equal(1, r.UserId));
    }

    [Fact]
    public async Task WithoutVideosOnlyDataTravels()
    {
        var file = await ExportAsync(await CreateCoachRoutineAsync(), includeMedia: false);

        using (var zip = new ZipArchive(new MemoryStream(file)))
            Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("media/"));

        var (preview, result) = await ImportAsync(file);
        Assert.Equal(0, preview.MediaCount);
        Assert.Equal(0, result.MediaImported);
        Assert.Equal(0, _client.FilesOnDisk);
    }

    [Fact]
    public async Task SmallerFileWithoutVideos()
    {
        var id = await CreateCoachRoutineAsync();
        var with = await ExportAsync(id, includeMedia: true);
        var without = await ExportAsync(id, includeMedia: false);
        Assert.True(with.Length > without.Length + 1500);

        using var db = _coach.Db();
        var estimate = await new PackageExporter(db, new FileMediaStorage(_coach.MediaRoot)).EstimateRoutineAsync(id);
        Assert.Equal((2, 1, 2048L), (estimate.ExerciseCount, estimate.MediaCount, estimate.MediaBytes));
    }

    [Fact]
    public async Task NewVersionUpdatesRoutineKeepingHistory()
    {
        var id = await CreateCoachRoutineAsync();
        await ImportAsync(await ExportAsync(id, includeMedia: true));

        // El cliente entrena el día "Torso A"
        int sessionDayId;
        using (var db = _client.Db())
        {
            var day = db.RoutineDays.Single(d => d.Name == "Torso A");
            sessionDayId = day.Id;
            var session = await new WorkoutRepository(db).CreateSessionFromRoutineDayAsync(day.Id);
        }

        // La entrenadora cambia la rutina: sube versión, renombra el día 1, quita el día 2
        using (var db = _coach.Db())
        {
            var r = db.Routines.Include(x => x.Days).ThenInclude(d => d.Exercises).Single();
            r.Version = 4;
            r.Days.Single(d => d.Order == 1).Name = "Torso A (pesado)";
            r.Days.Single(d => d.Order == 1).Exercises.Single(x => x.Order == 1).TargetSets = 5;
            var dayB = r.Days.Single(d => d.Order == 2);
            r.Days.Remove(dayB);
            db.RoutineDays.Remove(dayB);
            await db.SaveChangesAsync();
        }

        var (preview, result) = await ImportAsync(await ExportAsync(id, includeMedia: true));

        Assert.True(preview.IsUpdate);
        Assert.True(preview.IsNewerVersion);
        Assert.Equal((3, 4), (preview.ExistingVersion!.Value, preview.IncomingVersion));
        Assert.Equal(2, preview.ExistingExerciseCount);
        Assert.Equal((0, 1, 0, 0), (result.RoutinesCreated, result.RoutinesUpdated, result.ExercisesCreated, result.MediaImported));

        using var check = _client.Db();
        var routine = check.Routines.Include(r => r.Days).ThenInclude(d => d.Exercises).Single();
        Assert.Equal(4, routine.Version);
        var day1 = routine.Days.Single(d => d.Id == sessionDayId);       // el mismo día: el historial sigue enlazado
        Assert.Equal("Torso A (pesado)", day1.Name);
        Assert.Equal(5, day1.Exercises.Single(x => x.Order == 1).TargetSets);
        Assert.Single(check.WorkoutSessions);                             // el entreno no se ha perdido
        Assert.DoesNotContain(routine.Days, d => d.Name == "Pierna A");   // el día sin entrenos sí se quita
        Assert.Equal(1, _client.FilesOnDisk);                             // el vídeo no se duplica
    }

    [Fact]
    public async Task RemovedDayWithHistoryIsKept()
    {
        var id = await CreateCoachRoutineAsync(withVideo: false);
        await ImportAsync(await ExportAsync(id, includeMedia: false));
        using (var db = _client.Db())
            await new WorkoutRepository(db).CreateSessionFromRoutineDayAsync(db.RoutineDays.Single(d => d.Name == "Pierna A").Id);

        using (var db = _coach.Db())
        {
            var r = db.Routines.Include(x => x.Days).Single();
            r.Version = 4;
            var dayB = r.Days.Single(d => d.Order == 2);
            r.Days.Remove(dayB);
            db.RoutineDays.Remove(dayB);
            await db.SaveChangesAsync();
        }
        await ImportAsync(await ExportAsync(id, includeMedia: false));

        using var check = _client.Db();
        Assert.Contains(check.RoutineDays, d => d.Name == "Pierna A");
        Assert.Single(check.WorkoutSessions);
    }

    [Fact]
    public async Task SaveAsCopyKeepsBothRoutines()
    {
        var id = await CreateCoachRoutineAsync(withVideo: false);
        var file = await ExportAsync(id, includeMedia: false);
        await ImportAsync(file);

        var (_, result) = await ImportAsync(file, ImportMode.SaveAsCopy);

        Assert.Equal(1, result.RoutinesCreated);
        using var db = _client.Db();
        Assert.Equal(2, db.Routines.Count());
        Assert.Contains(db.Routines, r => r.Name == "Torso / Pierna (v3)");
        Assert.Equal(2, db.Exercises.Count()); // los ejercicios se reutilizan
    }

    [Fact]
    public async Task TwoProfilesOnTheSamePhoneCanImportTheSameRoutine()
    {
        var file = await ExportAsync(await CreateCoachRoutineAsync(), includeMedia: true);
        await ImportAsync(file);

        _client.User.SetCurrentUser(2);
        var (preview, result) = await ImportAsync(file);

        Assert.False(preview.IsUpdate);                // Pablo no la tenía
        Assert.Equal(2, preview.NewExerciseCount);
        Assert.Equal(2, result.ExercisesCreated);
        using var db = _client.Db();
        Assert.Single(db.Routines);                    // Pablo solo ve la suya
        Assert.Equal(2, _client.FilesOnDisk);          // cada perfil tiene su copia del vídeo
    }

    [Fact]
    public async Task ExistingExerciseWithSameNameGetsDistinguished()
    {
        using (var db = _client.Db())
        {
            db.Exercises.Add(new Exercise { Name = "Press banca", MuscleGroup = "Pecho" });
            db.SaveChanges();
        }

        await ImportAsync(await ExportAsync(await CreateCoachRoutineAsync(withVideo: false), includeMedia: false));

        using var check = _client.Db();
        Assert.Contains(check.Exercises, e => e.Name == "Press banca");
        Assert.Contains(check.Exercises, e => e.Name == "Press banca (2)");
    }

    [Fact]
    public async Task ExercisesPackageImportsLibrary()
    {
        await CreateCoachRoutineAsync();
        byte[] file;
        using (var db = _coach.Db())
        {
            var ids = db.Exercises.Select(e => e.Id).ToList();
            using var ms = new MemoryStream();
            await new PackageExporter(db, new FileMediaStorage(_coach.MediaRoot)).ExportExercisesAsync(ids, new ExportOptions(true, "Laura"), ms);
            file = ms.ToArray();
        }

        var (preview, result) = await ImportAsync(file);

        Assert.Equal(PackageKind.Exercises, preview.Kind);
        Assert.Equal("2 ejercicios", preview.Title);
        Assert.Equal((0, 2, 1), (result.RoutinesCreated, result.ExercisesCreated, result.MediaImported));
    }

    [Fact]
    public void RejectsFilesThatAreNotPackages()
    {
        var ex = Assert.Throws<SteelPackageException>(() => SteelPackage.Open(new MemoryStream(new byte[] { 1, 2, 3, 4 })));
        Assert.Contains("no es un paquete", ex.Message);
    }

    [Fact]
    public void RejectsPackagesFromANewerApp()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var w = new StreamWriter(zip.CreateEntry("manifest.json").Open()))
                w.Write("{\"formatVersion\": 99, \"kind\": \"routine\"}");
            using (var w = new StreamWriter(zip.CreateEntry("data.json").Open()))
                w.Write("{}");
        }

        var ex = Assert.Throws<SteelPackageException>(() => SteelPackage.Open(new MemoryStream(ms.ToArray())));
        Assert.Contains("versión más nueva", ex.Message);
    }

    [Fact]
    public async Task FailedImportLeavesNothingBehind()
    {
        var file = await ExportAsync(await CreateCoachRoutineAsync(), includeMedia: true);

        using var package = SteelPackage.Open(new MemoryStream(file));
        using var db = _client.Db();
        // Límite de vídeo minúsculo: el vídeo del paquete no cabe y la importación falla
        var importer = new PackageImporter(db, new ExerciseMediaService(new FileMediaStorage(_client.MediaRoot), maxBytes: 10));

        await Assert.ThrowsAsync<MediaImportException>(() => importer.ImportAsync(package, ImportMode.UpdateExisting));

        using var check = _client.Db();
        Assert.Empty(check.Routines);
        Assert.Empty(check.Exercises);
        Assert.Equal(0, _client.FilesOnDisk);
    }
}
