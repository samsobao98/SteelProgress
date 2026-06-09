using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Maui.Pages;

namespace SteelProgress.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseLiveCharts()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "steelprogress.db");
        builder.Services.AddDbContext<AppDbContext>(
            opt => opt.UseSqlite($"Data Source={dbPath}"),
            ServiceLifetime.Transient,
            ServiceLifetime.Transient);

        // Repositories
        builder.Services.AddTransient<ExerciseRepository>();
        builder.Services.AddTransient<RoutineRepository>();
        builder.Services.AddTransient<WorkoutRepository>();

        // ViewModels — add more in Phase 3 as pages are built
        builder.Services.AddTransient<HomeViewModel>();

        // Pages
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<ExercisePage>();
        builder.Services.AddTransient<RoutinePage>();
        builder.Services.AddTransient<WorkoutPage>();
        builder.Services.AddTransient<HistoryPage>();

        // Shell lives for app lifetime
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Auto-migrate on startup — no "dotnet ef database update" on mobile
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        return app;
    }
}
