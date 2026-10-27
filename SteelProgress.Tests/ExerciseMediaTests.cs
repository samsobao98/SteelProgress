using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

public class ExerciseMediaTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"sp-media-{Guid.NewGuid():N}");
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();

    public ExerciseMediaTests()
    {
        _connection.Open();
        _user.SetCurrentUser(1);
        using var db = CreateContext();
        db.Database.Migrate();
        db.UserProfiles.Add(new UserProfile { Id = 1, Name = "Samuel" });
        db.UserProfiles.Add(new UserProfile { Id = 2, Name = "Lucía" });
        db.SaveChanges();
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private ExerciseMediaService Service(long max = MediaKinds.DefaultMaxBytes) => new(new FileMediaStorage(_root), max);

    private static MemoryStream Bytes(int count) => new(new byte[count]);

    private int FilesOnDisk() => Directory.Exists(_root) ? Directory.GetFiles(_root).Length : 0;

    [Theory]
    [InlineData("clip.MP4", MediaKind.Video)]
    [InlineData("press.mov", MediaKind.Video)]
    [InlineData("anim.gif", MediaKind.Gif)]
    [InlineData("foto.jpeg", MediaKind.Image)]
    [InlineData("foto.webp", MediaKind.Image)]
    public void DetectsKindFromExtension(string name, MediaKind kind) =>
        Assert.Equal(kind, MediaKinds.FromFileName(name));

    [Fact]
    public async Task StoresCopyWithGeneratedName()
    {
        var media = await Service().StoreAsync(Bytes(1234), "IMG_0001.MOV");

        Assert.Equal(MediaKind.Video, media.Kind);
        Assert.Equal(1234, media.SizeBytes);
        Assert.EndsWith(".mov", media.FileName);
        Assert.DoesNotContain("IMG_0001", media.FileName);
        Assert.True(File.Exists(Path.Combine(_root, media.FileName)));
    }

    [Fact]
    public async Task RejectsUnsupportedFormatWithoutLeavingFiles()
    {
        var ex = await Assert.ThrowsAsync<MediaImportException>(() => Service().StoreAsync(Bytes(10), "documento.pdf"));
        Assert.Contains("Formato", ex.Message);
        Assert.Equal(0, FilesOnDisk());
    }

    [Fact]
    public async Task RejectsTooLargeFileWithoutLeavingFiles()
    {
        var ex = await Assert.ThrowsAsync<MediaImportException>(() => Service(max: 1000).StoreAsync(Bytes(5000), "clip.mp4"));
        Assert.Contains("máximo", ex.Message);
        Assert.Equal(0, FilesOnDisk());
    }

    [Fact]
    public void StorageNeverLeavesItsFolder()
    {
        var storage = new FileMediaStorage(_root);
        Assert.Equal(Path.Combine(_root, "evil.mp4"), storage.GetFullPath("../../evil.mp4"));
    }

    private ExerciseViewModel ViewModel() => new(new ExerciseRepository(CreateContext()), Service());

    [Fact]
    public async Task NewExerciseSavesMediaPickedBeforeSaving()
    {
        var vm = ViewModel();
        vm.Name = "Aperturas en polea";
        vm.MuscleGroup = "Pecho";
        await vm.AddMediaAsync(Bytes(100), "clip.mp4");
        await vm.AddMediaAsync(Bytes(50), "foto.png");

        await vm.AddExerciseCommand.ExecuteAsync(null);

        using var check = CreateContext();
        var ex = check.Exercises.Include(e => e.Media).Single();
        Assert.Equal(new[] { MediaKind.Video, MediaKind.Image }, ex.Media.OrderBy(m => m.Order).Select(m => m.Kind));
        Assert.Equal(2, FilesOnDisk());
        Assert.Empty(vm.Media);
    }

    [Fact]
    public async Task CancellingNewExerciseDeletesPickedFiles()
    {
        var vm = ViewModel();
        await vm.AddMediaAsync(Bytes(100), "clip.mp4");
        Assert.Equal(1, FilesOnDisk());

        vm.ClearForm();

        Assert.Equal(0, FilesOnDisk());
        using var check = CreateContext();
        Assert.Empty(check.ExerciseMedia);
    }

    [Fact]
    public async Task EditingExerciseAddsAndRemovesMediaImmediately()
    {
        using (var db = CreateContext())
        {
            db.Exercises.Add(new Exercise { Name = "Press banca", MuscleGroup = "Pecho" });
            db.SaveChanges();
        }

        var vm = ViewModel();
        await vm.RefreshAsync();
        vm.SelectedExercise = vm.Exercises.Single();

        await vm.AddMediaAsync(Bytes(100), "clip.mp4");
        using (var check = CreateContext())
            Assert.Single(check.ExerciseMedia);

        await vm.RemoveMediaCommand.ExecuteAsync(vm.Media.Single());
        using (var check = CreateContext())
            Assert.Empty(check.ExerciseMedia);
        Assert.Equal(0, FilesOnDisk());
    }

    [Fact]
    public async Task SwitchingToEditKeepsSavedMediaAndDropsPending()
    {
        using (var db = CreateContext())
        {
            var e = new Exercise { Name = "Press banca", MuscleGroup = "Pecho" };
            db.Exercises.Add(e);
            db.SaveChanges();
        }
        var saved = await Service().StoreAsync(Bytes(10), "foto.jpg");
        using (var db = CreateContext())
        {
            await new ExerciseRepository(db).AddMediaAsync(db.Exercises.Single().Id, saved);
        }

        var vm = ViewModel();
        await vm.RefreshAsync();
        await vm.AddMediaAsync(Bytes(10), "pendiente.gif"); // sin guardar, en modo "nuevo"

        vm.SelectedExercise = vm.Exercises.Single();        // el usuario toca otro ejercicio

        Assert.Single(vm.Media);
        Assert.Equal(saved.FileName, vm.Media[0].FileName);
        Assert.Equal(1, FilesOnDisk());
    }

    [Fact]
    public async Task DeletingExerciseDeletesItsFiles()
    {
        var vm = ViewModel();
        vm.Name = "Press banca";
        vm.MuscleGroup = "Pecho";
        await vm.AddMediaAsync(Bytes(100), "clip.mp4");
        await vm.AddExerciseCommand.ExecuteAsync(null);
        Assert.Equal(1, FilesOnDisk());

        ConfirmDialogService.OnConfirmationRequested += AlwaysYes;
        try
        {
            vm.SelectedExercise = vm.Exercises.Single();
            await vm.DeleteExerciseCommand.ExecuteAsync(null);
        }
        finally
        {
            ConfirmDialogService.OnConfirmationRequested -= AlwaysYes;
        }

        Assert.Equal(0, FilesOnDisk());
        using var check = CreateContext();
        Assert.Empty(check.ExerciseMedia);
    }

    private static Task<bool> AlwaysYes(string title, string message) => Task.FromResult(true);

    [Fact]
    public async Task OtherProfilesDoNotSeeMedia()
    {
        var vm = ViewModel();
        vm.Name = "Press banca";
        vm.MuscleGroup = "Pecho";
        await vm.AddMediaAsync(Bytes(100), "clip.mp4");
        await vm.AddExerciseCommand.ExecuteAsync(null);

        _user.SetCurrentUser(2);
        using var other = CreateContext();
        Assert.Empty(other.ExerciseMedia);
    }
}
