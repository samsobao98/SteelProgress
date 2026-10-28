using System.Globalization;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.Services;

// Texto corto de lo prescrito para un ejercicio: "4 × 6–8 · 72,5 kg · RPE 8 · 3-1-1-0 · 90 s"
public static class PrescriptionFormatter
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    public static string Format(RoutineDayExercise p)
    {
        var parts = new List<string>();

        var reps = FormatReps(p.TargetReps, p.TargetRepsMax);
        if (p.TargetSets is int sets && reps is not null) parts.Add($"{sets} × {reps}");
        else if (p.TargetSets is int onlySets) parts.Add($"{onlySets} series");
        else if (reps is not null) parts.Add($"{reps} reps");

        if (p.TargetWeight is double kg) parts.Add($"{Number(kg)} kg");
        else if (p.TargetPercent1RM is double pct) parts.Add($"{Number(pct)} % 1RM");

        if (p.TargetRpe is double rpe) parts.Add($"RPE {Number(rpe)}");
        else if (p.TargetRir is int rir) parts.Add($"RIR {rir}");

        if (!string.IsNullOrWhiteSpace(p.Tempo)) parts.Add(p.Tempo.Trim());
        if (p.RestSeconds is int rest) parts.Add(FormatRest(rest));
        if (p.WarmupSets is int warm && warm > 0) parts.Add($"+{warm} calent.");

        return string.Join(" · ", parts);
    }

    public static string? FormatReps(int? min, int? max)
    {
        if (min is null && max is null) return null;
        if (min is int a && max is int b && b > a) return $"{a}–{b}";
        return (min ?? max)!.Value.ToString(Es);
    }

    // 45 → "45 s", 120 → "2 min", 90 → "1:30"
    public static string FormatRest(int seconds)
    {
        if (seconds < 60) return $"{seconds} s";
        if (seconds % 60 == 0) return $"{seconds / 60} min";
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    private static string Number(double value) => value.ToString("0.##", Es);
}
