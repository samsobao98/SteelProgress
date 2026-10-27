using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.Sharing;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

public class ImportViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"sp-import-{Guid.NewGuid():N}");

    public ImportViewModelTests()
    {
        _connection.Open();
        _user.SetCurrentUser(1);
        using var db = Db();
        db.Database.Migrate();
        // Con navegación al perfil: el SaveChanges síncrono no pasa por la asignación automática de UserId
        var profile = new UserProfile { Id = 1, Name = "Laura" };
        var ex = new Exercise { Name = "Press banca", MuscleGroup = "Pecho", UserProfile = profile };
        var day = new RoutineDay { Name = "Torso A", Order = 1 };
        day.Exercises.Add(new RoutineDayExercise { Exercise = ex, Order = 1, TargetSets = 3, TargetReps = 10 });
        var routine = new Routine { Name = "Torso / Pierna", Version = 2, UserProfile = profile };
        routine.Days.Add(day);
        db.Routines.Add(routine);
        db.SaveChanges();
    }

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private ImportViewModel Vm() => new(new PackageImporter(Db(), new ExerciseMediaService(new FileMediaStorage(_root))));

    private async Task<string> ExportToFileAsync(string? message = null)
    {
        using var db = Db();
        var service = new RoutineSharingService(db, new PackageExporter(db, new FileMediaStorage(_root)), _user);
        return await service.ExportRoutineToFileAsync(db.Routines.Single().Id, "Torso / Pierna", includeMedia: false, message, Path.Combine(_root, "out"));
    }

    [Fact]
    public async Task ExportUsesProfileNameAsAuthorAndSafeFileName()
    {
        var path = await ExportToFileAsync("Hola");
        Assert.Equal("Torso - Pierna.steel", Path.GetFileName(path));

        using var package = SteelPackage.Open(File.OpenRead(path));
        Assert.Equal("Laura", package.Manifest.AuthorName);
        Assert.Equal("Hola", package.Manifest.Message);
    }

    [Fact]
    public async Task PreviewOfOwnRoutineShowsSameVersionUpdate()
    {
        var vm = Vm();
        await vm.LoadAsync(File.OpenRead(await ExportToFileAsync("Empezamos el lunes")));

        Assert.True(vm.IsLoaded);
        Assert.Equal(("Torso / Pierna", "Rutina", "Laura"), (vm.Title, vm.KindLabel, vm.AuthorName));
        Assert.True(vm.HasMessage);
        Assert.True(vm.IsUpdate);
        Assert.True(vm.IsOlderOrSameVersion);
        Assert.Equal("Actualización v2 → v2", vm.VersionBadge);
        Assert.Equal(("1 día", "Ninguno (ya los tienes)", "Sin vídeos"), (vm.DaysText, vm.NewExercisesText, vm.MediaText));
    }

    [Fact]
    public async Task SaveCopyFinishesWithMessage()
    {
        var vm = Vm();
        await vm.LoadAsync(File.OpenRead(await ExportToFileAsync()));

        await vm.SaveCopyCommand.ExecuteAsync(null);

        Assert.True(vm.IsDone);
        Assert.Equal("Rutina importada.", vm.DoneMessage);
        using var db = Db();
        Assert.Equal(2, db.Routines.Count());
    }

    [Fact]
    public async Task InvalidFileShowsError()
    {
        var vm = Vm();
        await vm.LoadAsync(new MemoryStream(new byte[] { 9, 9, 9 }));

        Assert.False(vm.IsLoaded);
        Assert.NotNull(vm.Error);
    }

    [Theory]
    [InlineData("Torso / Pierna", "Torso - Pierna")]
    [InlineData("  ¿Full: body?  ", "¿Full- body")]
    [InlineData("///", "rutina")]
    public void SafeFileNames(string input, string expected) =>
        Assert.Equal(expected, RoutineSharingService.SafeFileName(input));
}
