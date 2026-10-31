using System.Globalization;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Repositories;

namespace SteelProgress.Core.Services;

/// <summary>Récord que se consiguió en una sesión (el mejor de ese ejercicio en esa sesión).</summary>
public sealed record RecordEvent(int SessionId, DateTime Date, int ExerciseId, string ExerciseName, double Weight, int Reps, RecordKind Kind);

/// <summary>Un día de la tira semanal de Inicio.</summary>
public sealed record WeekDayCell(string Letter, DateTime Date, bool Done, bool IsToday, bool IsFuture);

/// <summary>Un día del calendario de constancia (nivel 0 = sin entreno, 4 = el día con más volumen).</summary>
public sealed record CalendarDay(DateTime Date, int Level, bool IsToday, bool IsFuture);

/// <summary>Series de trabajo de un grupo muscular: esta semana y la media de las anteriores.</summary>
public sealed record MuscleVolume(string Muscle, int ThisWeek, double PreviousAverage);

/// <summary>Una sesión en la gráfica de un ejercicio: el peso máximo y el mejor 1RM estimado.</summary>
public sealed record ExercisePoint(DateTime Date, double MaxWeight, double BestE1rm);

/// <summary>Resumen de un ejercicio entrenado, con su evolución sesión a sesión.</summary>
public sealed record ExerciseProgress(int ExerciseId, string Name, double BestWeight, int BestReps, double BestE1rm,
    DateTime LastDate, IReadOnlyList<ExercisePoint> Points);

/// <summary>
/// Estadísticas de Inicio y Progreso a partir del historial (<see cref="TrainingLog"/>).
/// Todo es cálculo puro con la fecha de hoy como parámetro, para poder probarlo.
/// </summary>
public static class TrainingStats
{
    private const double Eps = 0.001;
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
    private static readonly string[] Letters = ["L", "M", "X", "J", "V", "S", "D"];

    // ── Récords ─────────────────────────────────────────────────────────────

    /// <summary>
    /// La regla del récord, igual en Entreno y en Progreso: de peso si se levantan más kilos que
    /// nunca; de reps si se hacen más repeticiones que nunca con ese peso o más. Sin series
    /// anteriores no hay récord.
    /// </summary>
    public static RecordKind RecordOf(IReadOnlyCollection<PreviousSet> seen, double weight, int reps)
    {
        if (seen.Count == 0) return RecordKind.None;
        if (weight > seen.Max(s => s.Weight) + Eps) return RecordKind.Weight;
        if (reps > seen.Where(s => s.Weight >= weight - Eps).Select(s => s.Reps).DefaultIfEmpty(0).Max()) return RecordKind.Reps;
        return RecordKind.None;
    }

    /// <summary>
    /// Repasa el historial como lo vivió Entreno: la primera sesión de un ejercicio no marca récords;
    /// en las siguientes cada serie se compara con todo lo anterior, incluidas las series previas de
    /// esa misma sesión. Devuelve el mejor récord de cada ejercicio en cada sesión, el más reciente primero.
    /// </summary>
    public static List<RecordEvent> Records(IReadOnlyList<LoggedSet> sets)
    {
        var events = new List<RecordEvent>();
        foreach (var exercise in sets.GroupBy(s => s.ExerciseId))
        {
            var seen = new List<PreviousSet>();
            foreach (var session in exercise.OrderBy(s => s.Date).ThenBy(s => s.SetId).GroupBy(s => s.SessionId))
            {
                var hadHistory = seen.Count > 0;
                RecordEvent? best = null;
                foreach (var set in session)
                {
                    var kind = hadHistory ? RecordOf(seen, set.Weight, set.Reps) : RecordKind.None;
                    seen.Add(new PreviousSet(set.Weight, set.Reps));
                    if (kind == RecordKind.None) continue;

                    var candidate = new RecordEvent(set.SessionId, set.Date, set.ExerciseId, set.ExerciseName, set.Weight, set.Reps, kind);
                    if (best is null || IsBetter(candidate, best)) best = candidate;
                }
                if (best is not null) events.Add(best);
            }
        }
        return events.OrderByDescending(e => e.Date).ThenBy(e => e.ExerciseName).ToList();
    }

    // Como el resumen de Entreno: primero los de peso y, entre ellos, el de más kilos y reps
    private static bool IsBetter(RecordEvent a, RecordEvent b)
    {
        if (a.Kind != b.Kind) return a.Kind == RecordKind.Weight;
        if (Math.Abs(a.Weight - b.Weight) > Eps) return a.Weight > b.Weight;
        return a.Reps > b.Reps;
    }

    /// <summary>Récords conseguidos este mes.</summary>
    public static int RecordsThisMonth(IEnumerable<RecordEvent> records, DateTime today) =>
        records.Count(r => r.Date.Year == today.Year && r.Date.Month == today.Month);

    // ── Semanas ─────────────────────────────────────────────────────────────

    /// <summary>La semana actual de lunes a domingo, con los días que se entrenó.</summary>
    public static IReadOnlyList<WeekDayCell> Week(IEnumerable<LoggedSession> sessions, DateTime today)
    {
        var start = RoutineSchedule.WeekStart(today);
        var trained = sessions.Select(s => s.Date.Date).ToHashSet();
        return Enumerable.Range(0, 7)
            .Select(i => start.AddDays(i))
            .Select((d, i) => new WeekDayCell(Letters[i], d, trained.Contains(d), d == today.Date, d > today.Date))
            .ToList();
    }

    /// <summary>Semanas anteriores con las que se compara el volumen por músculo.</summary>
    public const int MuscleCompareWeeks = 4;

    // Grupos que no son un músculo: no se cuentan
    private static readonly HashSet<string> NotAMuscle = new(StringComparer.CurrentCultureIgnoreCase) { "Cardio", "Cuerpo completo" };

    /// <summary>
    /// Series de trabajo por grupo muscular esta semana (lunes a domingo) y la media de las
    /// <see cref="MuscleCompareWeeks"/> anteriores. Cada serie cuenta 1 para el músculo principal del
    /// ejercicio; los secundarios no suman. Dato contado, sin recomendaciones.
    /// </summary>
    public static List<MuscleVolume> SetsByMuscle(IEnumerable<LoggedSet> sets, DateTime today)
    {
        var weekStart = RoutineSchedule.WeekStart(today);
        var from = weekStart.AddDays(-7 * MuscleCompareWeeks);
        var counted = sets.Where(s => s.Date >= from && !string.IsNullOrWhiteSpace(s.MuscleGroup) && !NotAMuscle.Contains(s.MuscleGroup!))
            .GroupBy(s => s.MuscleGroup!.Trim())
            .Select(g => new MuscleVolume(g.Key,
                g.Count(s => s.Date >= weekStart),
                g.Count(s => s.Date < weekStart) / (double)MuscleCompareWeeks))
            .Where(m => m.ThisWeek > 0 || m.PreviousAverage > 0)
            .OrderByDescending(m => m.ThisWeek)
            .ThenByDescending(m => m.PreviousAverage)
            .ThenBy(m => m.Muscle, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return counted;
    }

    /// <summary>Sesiones de esta semana (desde el lunes).</summary>
    public static int SessionsThisWeek(IEnumerable<LoggedSession> sessions, DateTime today)
    {
        var start = RoutineSchedule.WeekStart(today);
        return sessions.Count(s => s.Date.Date >= start && s.Date.Date <= today.Date);
    }

    /// <summary>
    /// Semanas seguidas con algún entreno. Si esta semana aún no se ha entrenado, la racha
    /// se cuenta hasta la semana pasada: no se rompe a mitad de semana.
    /// </summary>
    public static int WeekStreak(IEnumerable<LoggedSession> sessions, DateTime today)
    {
        var weeks = sessions.Select(s => RoutineSchedule.WeekStart(s.Date)).ToHashSet();
        var week = RoutineSchedule.WeekStart(today);
        if (!weeks.Contains(week)) week = week.AddDays(-7);

        var streak = 0;
        while (weeks.Contains(week))
        {
            streak++;
            week = week.AddDays(-7);
        }
        return streak;
    }

    // ── Volumen ─────────────────────────────────────────────────────────────

    /// <summary>Volumen (kg × reps) de cada sesión.</summary>
    public static Dictionary<int, double> SessionVolumes(IEnumerable<LoggedSet> sets) =>
        sets.GroupBy(s => s.SessionId).ToDictionary(g => g.Key, g => g.Sum(s => s.Weight * s.Reps));

    /// <summary>
    /// Volumen de este mes hasta hoy y el del mismo tramo del mes pasado (del 1 al mismo día),
    /// para que la comparación sea justa a mitad de mes.
    /// </summary>
    public static (double Current, double Previous) MonthVolume(IEnumerable<LoggedSet> sets, DateTime today)
    {
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var prevStart = monthStart.AddMonths(-1);
        var prevEnd = prevStart.AddDays(Math.Min(today.Day, DateTime.DaysInMonth(prevStart.Year, prevStart.Month)));

        double current = 0, previous = 0;
        foreach (var s in sets)
        {
            var d = s.Date.Date;
            if (d >= monthStart && d <= today.Date) current += s.Weight * s.Reps;
            else if (d >= prevStart && d < prevEnd) previous += s.Weight * s.Reps;
        }
        return (current, previous);
    }

    /// <summary>"7,9 t" o "480 kg".</summary>
    public static string FormatVolume(double kg) =>
        kg >= 1000 ? $"{(kg / 1000).ToString("0.#", Es)} t" : $"{kg.ToString("0", Es)} kg";

    /// <summary>"+9 %", "−4 %" o "" si no hay con qué comparar.</summary>
    public static string DeltaText(double current, double previous)
    {
        if (previous <= 0) return "";
        var pct = (int)Math.Round((current - previous) / previous * 100);
        return pct >= 0 ? $"+{pct} %" : $"−{-pct} %";
    }

    // ── Calendario de constancia ───────────────────────────────────────────

    /// <summary>
    /// Las últimas <paramref name="weeks"/> semanas (de lunes a domingo, la actual la última),
    /// columna a columna. El nivel va de 1 a 4 según el volumen del día frente al día con más volumen.
    /// </summary>
    public static IReadOnlyList<CalendarDay> Calendar(IEnumerable<LoggedSet> sets, DateTime today, int weeks)
    {
        var start = RoutineSchedule.WeekStart(today).AddDays(-7 * (weeks - 1));
        var byDay = sets.Where(s => s.Date.Date >= start)
            .GroupBy(s => s.Date.Date)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Weight * s.Reps));
        var max = byDay.Count > 0 ? byDay.Values.Max() : 0;

        var days = new List<CalendarDay>(weeks * 7);
        for (var i = 0; i < weeks * 7; i++)
        {
            var d = start.AddDays(i);
            var level = 0;
            if (byDay.TryGetValue(d, out var v))
                // Un día entrenado nunca sale vacío, aunque sea sin peso (dominadas, fondos…)
                level = max <= 0 ? 1 : Math.Clamp((int)Math.Ceiling(v / max * 4), 1, 4);
            days.Add(new CalendarDay(d, level, d == today.Date, d > today.Date));
        }
        return days;
    }

    // ── Ejercicios ─────────────────────────────────────────────────────────

    /// <summary>Cada ejercicio entrenado con su mejor serie y su evolución; el último entrenado primero.</summary>
    public static List<ExerciseProgress> Exercises(IReadOnlyList<LoggedSet> sets) =>
        sets.GroupBy(s => s.ExerciseId)
            .Select(g =>
            {
                var best = g.OrderByDescending(s => s.Weight).ThenByDescending(s => s.Reps).First();
                var points = g.GroupBy(s => s.SessionId)
                    .Select(sg => new ExercisePoint(sg.First().Date, sg.Max(s => s.Weight),
                        sg.Max(s => WorkoutRepository.EstimatedOneRepMax(s.Weight, s.Reps))))
                    .OrderBy(p => p.Date)
                    .ToList();
                return new ExerciseProgress(g.Key, g.Last().ExerciseName, best.Weight, best.Reps,
                    points.Max(p => p.BestE1rm), points[^1].Date, points);
            })
            .OrderByDescending(e => e.LastDate)
            .ThenBy(e => e.Name)
            .ToList();
}
