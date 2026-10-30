using System.Globalization;

namespace SteelProgress.Core.Services;

public sealed record WeightPoint(DateTime Date, double Kg);

/// <summary>Media de una semana (de lunes a domingo) y cuántos pesajes tiene.</summary>
public sealed record WeekAverage(DateTime WeekStart, double Kg, int Count);

/// <summary>1RM estimado de un ejercicio dividido entre el peso (tendencia) de ese día, ahora y unas semanas antes.</summary>
public sealed record RelativeStrength(string Exercise, double Ratio, DateTime Date, double? PreviousRatio, DateTime? PreviousDate);

/// <summary>
/// Peso corporal (docs/INVESTIGACION_DATOS_PERFIL.md): el de un día varía por agua, glucógeno y comida, así que
/// lo que se enseña es la tendencia, una media móvil exponencial como la de <i>The Hacker's Diet</i>.
/// Todo es cálculo puro con la fecha de hoy como parámetro, para poder probarlo.
/// </summary>
public static class BodyWeightStats
{
    /// <summary>Cuánto se acerca la tendencia, cada día, al peso de ese día.</summary>
    public const double Smoothing = 0.1;

    /// <summary>Días que se miran atrás para decir cuánto ha cambiado la tendencia.</summary>
    public const int ChangeDays = 28;

    /// <summary>Con la fuerza relativa de ahora se compara la de al menos estos días antes.</summary>
    public const int StrengthCompareDays = 56;

    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>
    /// La tendencia de cada día desde el primer pesaje hasta el último. Los días sin pesaje se rellenan
    /// en línea recta entre el anterior y el siguiente, como hace The Hacker's Diet, para que pesarse
    /// poco no frene la tendencia.
    /// </summary>
    public static List<WeightPoint> Trend(IReadOnlyList<WeightPoint> entries)
    {
        var sorted = entries.OrderBy(e => e.Date).ToList();
        var trend = new List<WeightPoint>();
        if (sorted.Count == 0) return trend;

        var t = sorted[0].Kg;
        trend.Add(new WeightPoint(sorted[0].Date.Date, t));
        for (var i = 1; i < sorted.Count; i++)
        {
            var (prev, next) = (sorted[i - 1], sorted[i]);
            var gap = (next.Date.Date - prev.Date.Date).Days;
            for (var d = 1; d <= gap; d++)
            {
                var kg = prev.Kg + (next.Kg - prev.Kg) * d / gap;
                t += Smoothing * (kg - t);
                trend.Add(new WeightPoint(prev.Date.Date.AddDays(d), t));
            }
        }
        return trend;
    }

    /// <summary>La tendencia de ese día, o la del último pesaje anterior; null si aún no había ninguno.</summary>
    public static double? TrendAt(IReadOnlyList<WeightPoint> trend, DateTime date)
    {
        for (var i = trend.Count - 1; i >= 0; i--)
            if (trend[i].Date <= date.Date) return trend[i].Kg;
        return null;
    }

    /// <summary>«−0,6 kg en 4 semanas», o desde el primer pesaje si hace menos.</summary>
    public static string ChangeText(IReadOnlyList<WeightPoint> trend, DateTime today)
    {
        if (trend.Count == 0) return "";
        var now = trend[^1].Kg;
        var from = today.Date.AddDays(-ChangeDays);
        if (trend[0].Date <= from && TrendAt(trend, from) is double before)
            return $"{Delta(now - before)} en {ChangeDays / 7} semanas";
        if (trend.Count == 1) return "Primer pesaje: con unos cuantos más verás la tendencia";
        return $"{Delta(now - trend[0].Kg)} desde el {trend[0].Date:dd/MM}";
    }

    /// <summary>Las últimas semanas con algún pesaje, de la más reciente a la más antigua.</summary>
    public static List<WeekAverage> WeeklyAverages(IReadOnlyList<WeightPoint> entries, int weeks) =>
        entries.GroupBy(e => RoutineSchedule.WeekStart(e.Date))
            .OrderByDescending(g => g.Key)
            .Take(weeks)
            .Select(g => new WeekAverage(g.Key, g.Average(e => e.Kg), g.Count()))
            .ToList();

    /// <summary>
    /// Fuerza respecto al propio peso en los ejercicios con 1RM estimado de los últimos dos meses, los
    /// más entrenados primero. Solo sesiones con algún pesaje antes, para no inventar el peso de ese día.
    /// </summary>
    public static List<RelativeStrength> Strength(IEnumerable<ExerciseProgress> exercises, IReadOnlyList<WeightPoint> trend,
        DateTime today, int count)
    {
        var list = new List<(RelativeStrength Item, int Sessions)>();
        foreach (var ex in exercises)
        {
            var points = ex.Points
                .Where(p => p.BestE1rm > 0 && TrendAt(trend, p.Date) is not null)
                .OrderBy(p => p.Date)
                .ToList();
            if (points.Count == 0) continue;
            var last = points[^1];
            if (last.Date < today.Date.AddDays(-StrengthCompareDays)) continue;

            var ratio = last.BestE1rm / TrendAt(trend, last.Date)!.Value;
            var previous = points.LastOrDefault(p => p.Date <= last.Date.AddDays(-StrengthCompareDays));
            double? previousRatio = previous is null ? null : previous.BestE1rm / TrendAt(trend, previous.Date)!.Value;
            list.Add((new RelativeStrength(ex.Name, ratio, last.Date, previousRatio, previous?.Date), points.Count));
        }
        return list.OrderByDescending(x => x.Sessions).ThenBy(x => x.Item.Exercise).Take(count).Select(x => x.Item).ToList();
    }

    /// <summary>«78,4»</summary>
    public static string Kg(double kg) => kg.ToString("0.0", Es);

    /// <summary>«−0,6 kg», «+0,3 kg» o «Sin cambios».</summary>
    public static string Delta(double kg) =>
        Math.Round(kg, 1) == 0 ? "Sin cambios" : $"{(kg > 0 ? "+" : "−")}{Kg(Math.Abs(kg))} kg";

    /// <summary>«1,45 ×»</summary>
    public static string Ratio(double ratio) => $"{ratio.ToString("0.00", Es)} ×";
}
