using SteelProgress.Maui.Services;
using SteelProgress.Core.Services;
using SteelProgress.Maui.Pages;
using SteelProgress.Maui.Controls;

namespace SteelProgress.Maui;

// Se crea uno nuevo cada vez que se entra en un perfil (LauncherPage): un Shell que ya se quitó de la ventana
// no se puede volver a usar en Android (al navegar, su renderer falla). Al salir del perfil, Detach.
public partial class AppShell : Shell
{
    // Rutas y avisos son globales: se registran con el primer Shell, no con cada uno
    private static bool _globalsWired;

    private readonly Action _detach;

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
        _detach = () =>
        {
            viewOptions.Changed -= ApplyTabs;
            clientContext.Changed -= ApplyTabs;
        };
        ApplyTabs();

        if (_globalsWired) return;
        _globalsWired = true;

        Routing.RegisterRoute("ProfilePage", typeof(ProfilePage));
        Routing.RegisterRoute(nameof(ImportPreviewPage), typeof(ImportPreviewPage));
        Routing.RegisterRoute(nameof(ClientLimitationsPage), typeof(ClientLimitationsPage));
        Routing.RegisterRoute(nameof(BodyWeightPage), typeof(BodyWeightPage));
        Routing.RegisterRoute(nameof(WorkoutImportPage), typeof(WorkoutImportPage));

        // Avisos de los servicios: una sola suscripción para toda la vida de la app
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

    /// <summary>Al salir del perfil: este Shell deja de escuchar cambios de vista y de cliente.</summary>
    public void Detach() => _detach();
}
