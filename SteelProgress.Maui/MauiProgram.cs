using CommunityToolkit.Maui;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Maui;
using MauiIcons.Material;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using SteelProgress.Core.Services;
using SteelProgress.Core.Sharing;
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
            // Mantener pulsado (TouchBehavior) en la lista de rutinas
            .UseMauiCommunityToolkit()
            // Vídeo de los ejercicios dentro de la app (decisión D1)
            .UseMauiCommunityToolkitMediaElement(static _ => { })
            // LiveCharts dibuja sobre un lienzo de SkiaSharp: sin esto la gráfica de Progreso no tiene handler
            .UseSkiaSharp()
            .UseLiveCharts(charts => charts.AddDarkTheme())
            .UseMaterialMauiIcons()
            .ConfigureFonts(fonts =>
            {
                // Estilo «Acero + Discos»: Space Grotesk en toda la app (Resources/Styles/Tokens.xaml).
                // No hay SemiBold estático: lo que iba en semibold usa Medium.
                fonts.AddFont("SpaceGrotesk-Regular.ttf", "SpaceGroteskRegular");
                fonts.AddFont("SpaceGrotesk-Medium.ttf", "SpaceGroteskMedium");
                fonts.AddFont("SpaceGrotesk-Bold.ttf", "SpaceGroteskBold");
            });

        builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();
        builder.Services.AddSingleton<ViewOptionsService>();
        builder.Services.AddSingleton<ClientContextService>();

        // Vídeos e imágenes de los ejercicios, en el almacenamiento privado de la app
        builder.Services.AddSingleton<IMediaStorage>(_ =>
            new FileMediaStorage(Path.Combine(FileSystem.AppDataDirectory, "media")));
        // Fotos de perfil
        builder.Services.AddSingleton(_ => new ProfilePhotoStore(Path.Combine(FileSystem.AppDataDirectory, "profiles")));
        builder.Services.AddSingleton<ExerciseMediaService>();

        // Compartir rutinas (.steel)
        builder.Services.AddTransient<PackageExporter>();
        builder.Services.AddTransient<PackageImporter>();
        builder.Services.AddTransient<RoutineSharingService>();

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
        builder.Services.AddTransient<ClientRepository>();
        builder.Services.AddTransient<BodyWeightRepository>();

        // ViewModels
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<ExerciseViewModel>();
        builder.Services.AddTransient<RoutineViewModel>();
        builder.Services.AddTransient<RoutineDetailViewModel>();
        builder.Services.AddTransient<WorkoutViewModel>();
        builder.Services.AddTransient<ProgressViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<ImportViewModel>();
        builder.Services.AddTransient<ClientsViewModel>();
        builder.Services.AddTransient<ClientLimitationsViewModel>();
        builder.Services.AddTransient<BodyWeightViewModel>();
        builder.Services.AddSingleton<LauncherViewModel>();

        // Pages
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<ExercisePage>();
        builder.Services.AddTransient<RoutinePage>();
        builder.Services.AddTransient<RoutineDetailPage>();
        builder.Services.AddTransient<WorkoutPage>();
        builder.Services.AddTransient<ProgressPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<ImportPreviewPage>();
        builder.Services.AddTransient<ClientsPage>();
        builder.Services.AddTransient<ClientLimitationsPage>();
        builder.Services.AddTransient<BodyWeightPage>();

        // La pantalla de perfiles vive toda la app; el Shell, uno nuevo por cada entrada en un perfil
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddSingleton<LauncherPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
