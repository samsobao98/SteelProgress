using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

public class ProfileViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();

    public ProfileViewModelTests()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.Migrate();
        db.UserProfiles.Add(new UserProfile { Id = 1, Name = "Samuel" });
        db.UserProfiles.Add(new UserProfile { Id = 2, Name = "Lucía", SimpleViewBlocks = SimpleViewBlocks.Intensity });
        db.SaveChanges();
        _user.SetCurrentUser(1);
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    private readonly string _photoDir = Path.Combine(Path.GetTempPath(), "sp_photos_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        _connection.Dispose();
        if (Directory.Exists(_photoDir)) Directory.Delete(_photoDir, true);
    }

    private ProfileViewModel PhotoViewModel(out ProfilePhotoStore photos)
    {
        photos = new ProfilePhotoStore(_photoDir);
        return new ProfileViewModel(CreateContext(), _user, new ViewOptionsService(), photos: photos);
    }

    private static MemoryStream FakePhoto(byte value) => new(new byte[] { value, value, value });

    [Fact]
    public async Task LoadsCurrentProfileSettings()
    {
        _user.SetCurrentUser(2);
        var vm = new ProfileViewModel(CreateContext(), _user, new ViewOptionsService());
        await vm.LoadAsync();

        Assert.Equal("Lucía", vm.ProfileName);
        Assert.True(vm.IsSimple);
        Assert.True(vm.ShowIntensity);
        Assert.False(vm.ShowTempoAndRest);
    }

    [Fact]
    public async Task ChangesAreSavedAndSharedImmediately()
    {
        var options = new ViewOptionsService();
        var vm = new ProfileViewModel(CreateContext(), _user, options);
        await vm.LoadAsync();

        vm.ShowTempoAndRest = true;
        vm.ShowRepRanges = true;
        await vm.SaveAsync();

        Assert.True(options.Current.EditTempoAndRest);
        Assert.True(options.Current.EditRepRanges);
        Assert.False(options.Current.EditIntensity);

        using var check = CreateContext();
        var saved = check.UserProfiles.Single(p => p.Id == 1);
        Assert.Equal(SimpleViewBlocks.TempoAndRest | SimpleViewBlocks.RepRanges, saved.SimpleViewBlocks);
    }

    [Fact]
    public async Task SwitchingToDetailedKeepsBlocksForWhenSwitchingBack()
    {
        var options = new ViewOptionsService();
        var vm = new ProfileViewModel(CreateContext(), _user, options);
        await vm.LoadAsync();
        vm.ShowIntensity = true;
        await vm.SaveAsync();

        vm.SetDetailedCommand.Execute(null);
        await vm.SaveAsync();
        Assert.True(options.Current.IsDetailed);
        Assert.True(options.Current.EditTempoAndRest);
        Assert.False(options.Current.ShowClients);   // la vista no cambia el tipo de perfil

        vm.SetSimpleCommand.Execute(null);
        await vm.SaveAsync();
        Assert.True(options.Current.EditIntensity);
        Assert.False(options.Current.EditTempoAndRest);
    }

    // Cambiar el tipo cambia qué datos se ven: solo se hace si se confirma
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ChangingKindAsksFirst(bool confirms, bool becomesCoach)
    {
        var options = new ViewOptionsService();
        var vm = new ProfileViewModel(CreateContext(), _user, options);
        await vm.LoadAsync();

        Func<ConfirmRequest, Task<bool>> answer = _ => Task.FromResult(confirms);
        ConfirmDialogService.OnConfirmationRequested += answer;
        try
        {
            await vm.ChangeKindAsync(coach: true);
            await vm.SaveAsync();
        }
        finally
        {
            ConfirmDialogService.OnConfirmationRequested -= answer;
        }

        Assert.Equal(becomesCoach, options.Current.ShowClients);
        using var check = CreateContext();
        Assert.Equal(becomesCoach ? ProfileKind.Coach : ProfileKind.Personal, check.UserProfiles.Single(p => p.Id == 1).Kind);
    }

    [Fact]
    public async Task LoadingDoesNotWriteAnything()
    {
        var vm = new ProfileViewModel(CreateContext(), _user, new ViewOptionsService());
        await vm.LoadAsync();

        using var check = CreateContext();
        Assert.Equal(SimpleViewBlocks.None, check.UserProfiles.Single(p => p.Id == 1).SimpleViewBlocks);
        Assert.Equal(SimpleViewBlocks.Intensity, check.UserProfiles.Single(p => p.Id == 2).SimpleViewBlocks);
    }

    [Fact]
    public async Task RapidTogglesDoNotCollide()
    {
        var options = new ViewOptionsService();
        var vm = new ProfileViewModel(CreateContext(), _user, options);
        await vm.LoadAsync();

        // Varios interruptores casi a la vez, como al tocarlos seguidos
        vm.ShowIntensity = true;
        vm.ShowTempoAndRest = true;
        vm.ShowAdvancedStructure = true;
        vm.ShowRepRanges = true;
        vm.ShowDetailedNotes = true;
        await vm.SaveAsync();

        using var check = CreateContext();
        Assert.Equal(SimpleViewBlocks.All, check.UserProfiles.Single(p => p.Id == 1).SimpleViewBlocks);
    }

    [Fact]
    public async Task GymPlatesAndBarAreSavedPerProfile()
    {
        var options = new ViewOptionsService();
        var vm = new ProfileViewModel(CreateContext(), _user, options);
        await vm.LoadAsync();

        // Por defecto: todos menos el de 1,25, y barra de 20
        Assert.Equal(new[] { 25, 20, 15, 10, 5, 2.5 }, vm.Plates.Where(p => p.IsAvailable).Select(p => p.Weight));
        Assert.Equal("20 kg", vm.BarWeightText);

        vm.TogglePlateCommand.Execute(vm.Plates.Single(p => p.Weight == 1.25));
        vm.TogglePlateCommand.Execute(vm.Plates.Single(p => p.Weight == 15));
        vm.ChangeBarWeightCommand.Execute("-");
        await vm.SaveAsync();

        Assert.Equal("17,5 kg", vm.BarWeightText);
        Assert.Equal(new[] { 25, 20, 10, 5, 2.5, 1.25 }, options.Current.Gym.Plates);
        Assert.Equal(17.5, options.Current.Gym.BarWeight);

        using (var check = CreateContext())
        {
            Assert.Equal(("25;20;10;5;2.5;1.25", (double?)17.5), (check.UserProfiles.Single(p => p.Id == 1).GymPlates, check.UserProfiles.Single(p => p.Id == 1).BarWeight));
            Assert.Null(check.UserProfiles.Single(p => p.Id == 2).GymPlates);
        }

        // Siempre queda al menos un disco
        foreach (var plate in vm.Plates.Where(p => p.IsAvailable).ToList())
            vm.TogglePlateCommand.Execute(plate);
        Assert.Single(vm.Plates, p => p.IsAvailable);
    }

    [Fact]
    public async Task EditingSavesNameDescriptionAndColor()
    {
        var vm = new ProfileViewModel(CreateContext(), _user, new ViewOptionsService());
        await vm.LoadAsync();

        vm.StartEditCommand.Execute(null);
        Assert.True(vm.IsEditing);
        vm.EditName = "  Samu ";
        vm.EditDescription = "Constancia";
        vm.EditColor = ProfileColors.All[3];
        await vm.SaveEditCommand.ExecuteAsync(null);

        Assert.False(vm.IsEditing);
        Assert.Equal(("Samu", "S", "Constancia"), (vm.ProfileName, vm.ProfileInitial, vm.ProfileDescription));
        using var check = CreateContext();
        var saved = check.UserProfiles.Single(p => p.Id == 1);
        Assert.Equal(("Samu", "Constancia", ProfileColors.All[3]), (saved.Name, saved.Description, saved.AvatarColor));
    }

    [Fact]
    public async Task CancellingOrEmptyNameDoesNotSave()
    {
        var vm = new ProfileViewModel(CreateContext(), _user, new ViewOptionsService());
        await vm.LoadAsync();

        vm.StartEditCommand.Execute(null);
        vm.EditName = "   ";
        Assert.False(vm.SaveEditCommand.CanExecute(null));

        vm.EditName = "Otro";
        vm.CancelEditCommand.Execute(null);
        Assert.False(vm.IsEditing);
        Assert.Equal("Samuel", vm.ProfileName);
        using var check = CreateContext();
        Assert.Equal("Samuel", check.UserProfiles.Single(p => p.Id == 1).Name);
    }

    [Fact]
    public async Task PhotoIsKeptOnlyWhenSaving()
    {
        var vm = PhotoViewModel(out var photos);
        await vm.LoadAsync();
        Assert.False(vm.HasProfilePhoto);

        // Elegida y cancelada: ni foto ni archivo suelto
        vm.StartEditCommand.Execute(null);
        await vm.SetEditPhotoAsync(FakePhoto(1));
        Assert.True(vm.HasEditPhoto);
        vm.CancelEditCommand.Execute(null);
        Assert.False(vm.HasProfilePhoto);
        Assert.Empty(Directory.GetFiles(_photoDir));

        // Elegida dos veces y guardada: solo queda la última, ya del perfil
        vm.StartEditCommand.Execute(null);
        await vm.SetEditPhotoAsync(FakePhoto(2));
        await vm.SetEditPhotoAsync(FakePhoto(3));
        await vm.SaveEditCommand.ExecuteAsync(null);

        var file = Assert.Single(Directory.GetFiles(_photoDir));
        Assert.Equal(file, vm.ProfilePhoto);
        Assert.Equal(new byte[] { 3, 3, 3 }, File.ReadAllBytes(file));
        using var check = CreateContext();
        Assert.Equal(Path.GetFileName(file), check.UserProfiles.Single(p => p.Id == 1).PhotoFile);
    }

    [Fact]
    public async Task ReplacingOrRemovingThePhotoDeletesTheOldFile()
    {
        var vm = PhotoViewModel(out _);
        await vm.LoadAsync();
        vm.StartEditCommand.Execute(null);
        await vm.SetEditPhotoAsync(FakePhoto(1));
        await vm.SaveEditCommand.ExecuteAsync(null);
        var first = vm.ProfilePhoto;

        vm.StartEditCommand.Execute(null);
        await vm.SetEditPhotoAsync(FakePhoto(2));
        await vm.SaveEditCommand.ExecuteAsync(null);
        Assert.NotEqual(first, vm.ProfilePhoto);
        Assert.False(File.Exists(first));

        vm.StartEditCommand.Execute(null);
        vm.RemoveEditPhotoCommand.Execute(null);
        Assert.True(vm.HasProfilePhoto); // hasta guardar sigue la que había
        await vm.SaveEditCommand.ExecuteAsync(null);
        Assert.False(vm.HasProfilePhoto);
        Assert.Empty(Directory.GetFiles(_photoDir));
        using var check = CreateContext();
        Assert.Null(check.UserProfiles.Single(p => p.Id == 1).PhotoFile);
    }

    [Fact]
    public void PhotoStoreNeverLeavesItsFolder()
    {
        var photos = new ProfilePhotoStore(_photoDir);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "sp_outside.jpg"), "x");
        Assert.Null(photos.PathOf("../sp_outside.jpg"));
        Assert.Null(photos.PathOf(null));
    }
}
