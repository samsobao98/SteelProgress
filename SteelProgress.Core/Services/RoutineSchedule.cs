using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.Services;

// Avance semanal de una rutina y qué día toca. Lo usan la lista de Rutinas y, más adelante, Inicio.
public static class RoutineSchedule
{
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

    /// <summary>"hoy", "ayer", "hace 5 días", "hace 3 semanas", "hace 2 meses".</summary>
    public static string LastTrainedText(DateTime? last, DateTime today)
    {
        if (last is not DateTime date) return "sin entrenar aún";
        var days = (today.Date - date.Date).Days;
        return days switch
        {
            <= 0 => "última vez hoy",
            1 => "última vez ayer",
            < 14 => $"última vez hace {days} días",
            < 60 => $"última vez hace {days / 7} semanas",
            _ => $"última vez hace {days / 30} meses"
        };
    }
}
