using SteelProgress.Maui.Services;
using SteelProgress.Core.Services;
using SteelProgress.Maui.Pages;
using SteelProgress.Maui.Controls;

namespace SteelProgress.Maui;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        // Wire pages via DI so constructor injection works. La barra de pestañas está oculta
        // (Shell.TabBarIsVisible): cada página principal lleva el abanico del logo (Controls/FanMenu.cs).
        HomeContent.ContentTemplate = new DataTemplate(() => FanMenuHost.Wrap(services.GetRequiredService<HomePage>(), "HomePage"));
        ExerciseContent.ContentTemplate = new DataTemplate(() => FanMenuHost.Wrap(services.GetRequiredService<ExercisePage>(), "ExercisePage"));
        RoutineContent.ContentTemplate = new DataTemplate(() => FanMenuHost.Wrap(services.GetRequiredService<RoutinePage>(), "RoutinePage"));
        WorkoutContent.ContentTemplate = new DataTemplate(() => FanMenuHost.Wrap(services.GetRequiredService<WorkoutPage>(), "WorkoutPage"));
        ProgressContent.ContentTemplate = new DataTemplate(() => FanMenuHost.Wrap(services.GetRequiredService<ProgressPage>(), "ProgressPage"));
        ClientsContent.ContentTemplate = new DataTemplate(() => FanMenuHost.Wrap(services.GetRequiredService<ClientsPage>(), "ClientsPage"));

        // Apartados según el tipo de perfil y la vista (ViewOptions): Clientes en perfiles de entrenador,
        // Ejercicios en vista detallada, Rutinas salvo en la simple de entrenador. Un entrenador solo ve
        // Progreso mientras trabaja con un cliente (es el progreso y el historial de ese cliente).
        var viewOptions = services.GetRequiredService<ViewOptionsService>();
        var clientContext = services.GetRequiredService<ClientContextService>();
        // En el hilo principal, al momento: Clientes activa al cliente y navega a Progreso justo después
        void ApplyTabs()
        {
            void Apply()
            {
                var o = viewOptions.Current;
                SetVisible(ClientsTab, o.ShowClients);
                SetVisible(ExerciseTab, o.ShowExercises);
                SetVisible(RoutineTab, o.ShowRoutines);
                SetVisible(ProgressTab, o.ShowOwnTraining || clientContext.HasActiveClient);
            }

            // Antes de ocultar el apartado en el que se está (p. ej. pasar a vista simple desde Ejercicios,
            // o «Salir» del cliente en Progreso), se vuelve a Clientes si se ve, o a Inicio
            void SetVisible(Tab tab, bool visible)
            {
                if (!visible && CurrentItem?.CurrentItem == tab)
                    CurrentItem.CurrentItem = ClientsTab.IsVisible && tab != ClientsTab ? ClientsTab : CurrentItem.Items[0];
                tab.IsVisible = visible;
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
