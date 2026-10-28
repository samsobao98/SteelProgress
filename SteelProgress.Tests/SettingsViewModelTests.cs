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
}
