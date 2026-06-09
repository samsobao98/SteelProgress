using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SteelProgress.Data.Context;

namespace SteelProgress.Spike;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Ruta correcta para SQLite en móvil — equivalente a AppData en escritorio
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "steelprogress_spike.db");
        builder.Services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath}"));

        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Aplicar migraciones automáticamente al arrancar (no hay "dotnet ef database update" en móvil)
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        return app;
    }
}
