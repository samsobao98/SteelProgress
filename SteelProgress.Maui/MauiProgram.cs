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

        // ViewModels
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<ExerciseViewModel>();
        builder.Services.AddTransient<RoutineViewModel>();
        builder.Services.AddTransient<WorkoutViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<ProgressViewModel>();

        // Pages
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<ExercisePage>();
        builder.Services.AddTransient<RoutinePage>();
        builder.Services.AddTransient<WorkoutPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<ProgressPage>();

        // Shell y bienvenida viven toda la vida de la app
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<WelcomePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
