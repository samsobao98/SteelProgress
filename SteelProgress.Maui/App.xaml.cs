using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;
using SteelProgress.Maui.Pages;

namespace SteelProgress.Maui;

public partial class App : Application
{
    private readonly LauncherPage _launcherPage;
    private readonly IServiceProvider _services;

    private Task _migrationTask = Task.CompletedTask;
    public Task MigrationTask => _migrationTask;

    public App(LauncherPage launcherPage, IServiceProvider services)
    {
        InitializeComponent();
        _launcherPage = launcherPage;
        _services = services;
        _migrationTask = MigrateAsync(); // kick off before any page appears
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_launcherPage);
    }

    private async Task MigrateAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await SeedDefaultProfileIfNeededAsync(db);
    }

    private static async Task SeedDefaultProfileIfNeededAsync(AppDbContext db)
    {
        var hasProfiles = await db.UserProfiles.AnyAsync();
        if (hasProfiles) return;

        // Comprueba si hay datos previos sin perfil asignado
        var hasOrphanRoutines = await db.Routines.IgnoreQueryFilters().AnyAsync(r => r.UserId == 0);
        var hasOrphanSessions = await db.WorkoutSessions.IgnoreQueryFilters().AnyAsync(s => s.UserId == 0);
        var hasOrphanExercises = await db.Exercises.IgnoreQueryFilters().AnyAsync(e => e.UserId == null);

        if (!hasOrphanRoutines && !hasOrphanSessions && !hasOrphanExercises) return;

        // Crea el perfil predeterminado y migra los datos existentes
        var defaultProfile = new UserProfile { Name = "Mi perfil", AvatarColor = "#89B4FA" };
        db.UserProfiles.Add(defaultProfile);
        await db.SaveChangesAsync();

        await db.Routines.IgnoreQueryFilters()
            .Where(r => r.UserId == 0)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.UserId, defaultProfile.Id));

        await db.WorkoutSessions.IgnoreQueryFilters()
            .Where(s => s.UserId == 0)
            .ExecuteUpdateAsync(s => s.SetProperty(ws => ws.UserId, defaultProfile.Id));

        await db.Exercises.IgnoreQueryFilters()
            .Where(e => e.UserId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.UserId, defaultProfile.Id));
    }
}
