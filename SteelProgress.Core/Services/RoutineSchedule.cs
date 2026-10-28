using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.Services;

// Avance semanal de una rutina y qué día toca. Lo usan la lista de Rutinas y, más adelante, Inicio.
public static class RoutineSchedule
{
    public const string DeleteBlockedMessage = "Termina o descarta el entreno en curso antes de eliminar.";

    /// <summary>
    /// Borrar una rutina o un día borra en cascada sus sesiones, incluida la del entreno en curso,
    /// y Entreno fallaría al guardar la siguiente serie: con un entreno activo no se permite.
    /// </summary>
    public static bool CanDeleteNow()
    {
        if (!WorkoutStateService.IsWorkoutActive) return true;
        NotificationService.Error(DeleteBlockedMessage);
        return false;
    }

    /// <summary>Lunes de la semana de <paramref name="today"/>.</summary>
    public static DateTime WeekStart(DateTime today)
    {
        var date = today.Date;
        var sinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-sinceMonday);
    }

    /// <summary>
    /// El día que toca: el siguiente al último entrenado que aún no se haya hecho esta semana.
    /// Si ya se hicieron todos, el siguiente al último. Sin entrenos, el primero.
    /// </summary>
    public static RoutineDay? NextDay(RoutineWeek week)
    {
        var days = week.Days;
        if (days.Count == 0) return null;

        var last = days.ToList().FindIndex(d => d.Id == week.LastTrainedDayId);
        if (last < 0) return days[0];

        for (var k = 1; k <= days.Count; k++)
        {
            var candidate = days[(last + k) % days.Count];
            if (!week.DoneThisWeek.Contains(candidate.Id)) return candidate;
        }
        return days[(last + 1) % days.Count];
    }

    /// <summary>"última vez hace 3 semanas" o "sin entrenar aún".</summary>
    public static string LastTrainedText(DateTime? last, DateTime today) =>
        last is DateTime date ? $"última vez {AgoText(date, today)}" : "sin entrenar aún";

    /// <summary>"hoy", "ayer", "hace 5 días", "hace 3 semanas", "hace 2 meses".</summary>
    public static string AgoText(DateTime date, DateTime today)
    {
        var days = (today.Date - date.Date).Days;
        return days switch
        {
            <= 0 => "hoy",
            1 => "ayer",
            < 14 => $"hace {days} días",
            < 60 => $"hace {days / 7} semanas",
            _ => $"hace {days / 30} meses"
        };
    }
}
