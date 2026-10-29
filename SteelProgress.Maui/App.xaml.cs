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
        // El seed de perfil debe ir ANTES que el de la biblioteca: adopta los
        // ejercicios legacy con UserId null que aún no son de sistema.
        await SeedDefaultProfileIfNeededAsync(db);
        await SeedExerciseLibraryIfNeededAsync(db);
        await NormalizeLibraryUidsAsync(db);
        // Tipo de material obligatorio (2026-10-29): se deduce para lo que ya había
        await new SteelProgress.Data.Repositories.ExerciseRepository(db).FillMissingEquipmentTypesAsync();
    }

    private static async Task SeedDefaultProfileIfNeededAsync(AppDbContext db)
    {
        var hasProfiles = await db.UserProfiles.AnyAsync();
        if (hasProfiles) return;

        // Comprueba si hay datos previos sin perfil asignado
        var hasOrphanRoutines = await db.Routines.IgnoreQueryFilters().AnyAsync(r => r.UserId == 0);
        var hasOrphanSessions = await db.WorkoutSessions.IgnoreQueryFilters().AnyAsync(s => s.UserId == 0);
        var hasOrphanExercises = await db.Exercises.IgnoreQueryFilters().AnyAsync(e => e.UserId == null && !e.IsSystem);

        if (!hasOrphanRoutines && !hasOrphanSessions && !hasOrphanExercises) return;

        // Crea el perfil predeterminado y migra los datos existentes
        var defaultProfile = new UserProfile { Name = "Mi perfil", AvatarColor = "#9DB4E6" };
        db.UserProfiles.Add(defaultProfile);
        await db.SaveChangesAsync();

        await db.Routines.IgnoreQueryFilters()
            .Where(r => r.UserId == 0)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.UserId, defaultProfile.Id));

        await db.WorkoutSessions.IgnoreQueryFilters()
            .Where(s => s.UserId == 0)
            .ExecuteUpdateAsync(s => s.SetProperty(ws => ws.UserId, defaultProfile.Id));

        await db.Exercises.IgnoreQueryFilters()
            .Where(e => e.UserId == null && !e.IsSystem)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.UserId, defaultProfile.Id));
    }

    private sealed record LibraryExercise(
        string Name,
        string MuscleGroup,
        string? SecondaryMuscles,
        string? Equipment,
        string? Instructions);

    private static async Task SeedExerciseLibraryIfNeededAsync(AppDbContext db)
    {
        var hasLibrary = await db.Exercises.IgnoreQueryFilters().AnyAsync(e => e.IsSystem);
        if (hasLibrary) return;

        using var stream = await FileSystem.OpenAppPackageFileAsync("exercise_library.json");
        var items = await System.Text.Json.JsonSerializer.DeserializeAsync<List<LibraryExercise>>(
            stream,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (items is null || items.Count == 0) return;

        db.Exercises.AddRange(items.Select(i => new Exercise
        {
            Name = i.Name,
            MuscleGroup = i.MuscleGroup,
            SecondaryMuscles = i.SecondaryMuscles,
            Equipment = i.Equipment,
            EquipmentType = SteelProgress.Domain.Services.EquipmentCatalog.Classify(i.Equipment, i.Name),
            Instructions = i.Instructions,
            UserId = null,
            IsSystem = true,
            Uid = LibraryUid(i.Name)
        }));

        await db.SaveChangesAsync();
    }

    // Uid estable por nombre: el mismo ejercicio de biblioteca tiene el mismo Uid
    // en todos los dispositivos, así una rutina importada lo reconoce y no lo duplica.
    private static Guid LibraryUid(string name)
    {
        var hash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes("steelprogress.library/" + name.Trim().ToLowerInvariant()));
        return new Guid(hash);
    }

    // Bibliotecas sembradas antes de existir LibraryUid tienen Uids aleatorios
    private static async Task NormalizeLibraryUidsAsync(AppDbContext db)
    {
        var library = await db.Exercises.IgnoreQueryFilters().Where(e => e.IsSystem).ToListAsync();
        var changed = false;
        foreach (var ex in library)
        {
            var uid = LibraryUid(ex.Name);
            if (ex.Uid == uid) continue;
            ex.Uid = uid;
            changed = true;
        }
        if (changed)
            await db.SaveChangesAsync();
    }
}
