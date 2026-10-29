using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

public class SettingsViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();

    public SettingsViewModelTests()
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

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task LoadsCurrentProfileSettings()
    {
        _user.SetCurrentUser(2);
        var vm = new SettingsViewModel(CreateContext(), _user, new ViewOptionsService());
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
        var vm = new SettingsViewModel(CreateContext(), _user, options);
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
    public async Task SwitchingToTrainerKeepsBlocksForWhenSwitchingBack()
    {
        var options = new ViewOptionsService();
        var vm = new SettingsViewModel(CreateContext(), _user, options);
        await vm.LoadAsync();
        vm.ShowIntensity = true;
        await vm.SaveAsync();

        vm.SetTrainerCommand.Execute(null);
        await vm.SaveAsync();
        Assert.True(options.Current.IsTrainer);
        Assert.True(options.Current.ShowClients);

        vm.SetSimpleCommand.Execute(null);
        await vm.SaveAsync();
        Assert.False(options.Current.ShowClients);
        Assert.True(options.Current.EditIntensity);
        Assert.False(options.Current.EditTempoAndRest);
    }

    [Fact]
    public async Task LoadingDoesNotWriteAnything()
    {
        var vm = new SettingsViewModel(CreateContext(), _user, new ViewOptionsService());
        await vm.LoadAsync();

        using var check = CreateContext();
        Assert.Equal(SimpleViewBlocks.None, check.UserProfiles.Single(p => p.Id == 1).SimpleViewBlocks);
        Assert.Equal(SimpleViewBlocks.Intensity, check.UserProfiles.Single(p => p.Id == 2).SimpleViewBlocks);
    }

    [Fact]
    public async Task RapidTogglesDoNotCollide()
    {
        var options = new ViewOptionsService();
        var vm = new SettingsViewModel(CreateContext(), _user, options);
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
        var vm = new SettingsViewModel(CreateContext(), _user, options);
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
}
