using SteelProgress.Core.Services;
using SteelProgress.Maui.Pages;

namespace SteelProgress.Maui;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        // Wire pages via DI so constructor injection works
        HomeContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<HomePage>());
        ExerciseContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<ExercisePage>());
        RoutineContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<RoutinePage>());
        WorkoutContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<WorkoutPage>());
        HistoryContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<HistoryPage>());

        Routing.RegisterRoute("ProgressPage", typeof(ProgressPage));

        // Wire static service delegates — AppShell lives for app lifetime, so one subscription is correct
        ConfirmDialogService.OnConfirmationRequested += (title, msg) =>
            DisplayAlertAsync(title, msg, "Sí", "No");

        NotificationService.OnNotificationRequested += (msg, type) =>
        {
            var title = type == NotificationType.Success ? "Correcto" :
                        type == NotificationType.Error ? "Error" : "Aviso";
            _ = DisplayAlertAsync(title, msg, "OK");
        };
    }
}
