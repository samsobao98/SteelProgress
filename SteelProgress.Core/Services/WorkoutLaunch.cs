namespace SteelProgress.Core.Services;

/// <summary>
/// Encargo para Entreno: Inicio («Hoy toca») y Rutinas (vista previa) piden empezar un día y
/// cambian de pestaña; la pantalla de Entreno lo recoge al aparecer. Así la sesión siempre la crea
/// la misma pantalla que la muestra.
/// </summary>
public static class WorkoutLaunch
{
    private static int? _pendingDayId;

    public static void Request(int routineDayId) => _pendingDayId = routineDayId;

    /// <summary>El día pedido (y lo olvida), o null si no hay encargo.</summary>
    public static int? Take()
    {
        var id = _pendingDayId;
        _pendingDayId = null;
        return id;
    }
}
