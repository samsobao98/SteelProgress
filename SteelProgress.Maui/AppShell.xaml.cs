using SteelProgress.Maui.Services;
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
        ProgressContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<ProgressPage>());
        ClientsContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<ClientsPage>());

        // Barra según la vista: en vista entrenador aparece Clientes y, si el perfil no entrena, Progreso
        // solo se ve mientras se trabaja con un cliente (es su progreso e historial)
        var viewOptions = services.GetRequiredService<ViewOptionsService>();
        var clientContext = services.GetRequiredService<ClientContextService>();
        // En el hilo principal, al momento: Clientes activa al cliente y navega a Progreso justo después
        void ApplyTabs()
        {
            void Apply()
            {
                var o = viewOptions.Current;
                ClientsTab.IsVisible = o.ShowClients;
                ProgressTab.IsVisible = o.ShowOwnTraining || clientContext.HasActiveClient;
            }
            if (MainThread.IsMainThread) Apply();
            else MainThread.BeginInvokeOnMainThread(Apply);
        }
        viewOptions.Changed += ApplyTabs;
        clientContext.Changed += ApplyTabs;
        ApplyTabs();

        Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
        Routing.RegisterRoute(nameof(ImportPreviewPage), typeof(ImportPreviewPage));

        // Wire static service delegates — AppShell lives for app lifetime, so one subscription is correct
        // Con el diseño de la app (docs/PLAN_VENTANAS.md): confirmación propia y avisos breves que se van solos
        ConfirmDialogService.OnConfirmationRequested += request =>
            AppDialogs.ConfirmAsync(request.Title, request.Message, request.Accept, request.Cancel, request.Destructive);

        NotificationService.OnNotificationRequested += (msg, type) =>
            AppToast.Show(msg, type switch
            {
                NotificationType.Success => ToastKind.Success,
                NotificationType.Error => ToastKind.Error,
                _ => ToastKind.Info
            });
    }
}
