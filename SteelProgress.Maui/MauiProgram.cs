using LiveChartsCore.SkiaSharpView.Maui;
using MauiIcons.Material;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using SteelProgress.Core.Services;
using SteelProgress.Domain.Services;
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
            .UseMaterialMauiIcons()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                // Números y títulos del sistema de diseño (Resources/Styles/Tokens.xaml)
                fonts.AddFont("SpaceGrotesk-Medium.ttf", "SpaceGroteskMedium");
                fonts.AddFont("SpaceGrotesk-Bold.ttf", "SpaceGroteskBold");
            });

        builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
        builder.Services.AddSingleton<ViewOptionsService>();

        // Vídeos e imágenes de los ejercicios, en el almacenamiento privado de la app
        builder.Services.AddSingleton<IMediaStorage>(_ =>
            new FileMediaStorage(Path.Combine(FileSystem.AppDataDirectory, "media")));
        builder.Services.AddSingleton<ExerciseMediaService>();

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "steelprogress.db");
        builder.Services.AddDbContext<AppDbContext>(
            (sp, opt) =>
            {
                opt.UseSqlite($"Data Source={dbPath}");
                // EF 8 snapshots differ in format from EF 10 model — suppress false-positive
                opt.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            },
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
        builder.Services.AddTransient<ProgressViewModel>(sp =>
            new ProgressViewModel(sp.GetRequiredService<AppDbContext>()));
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddSingleton<LauncherViewModel>();

        // Pages
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<ExercisePage>();
        builder.Services.AddTransient<RoutinePage>();
        builder.Services.AddTransient<WorkoutPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<ProgressPage>();
        builder.Services.AddTransient<SettingsPage>();

        // Shell and launcher live for the app lifetime
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<LauncherPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
