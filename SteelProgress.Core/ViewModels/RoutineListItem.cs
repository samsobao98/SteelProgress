using System.Text.RegularExpressions;
using SteelProgress.Core.Services;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

/// <summary>Una rutina en la lista: tarjeta normal o, si es la activa, placa con su semana.</summary>
public partial class RoutineListItem
{
    public RoutineSummary Summary { get; }
    public Routine Routine => Summary.Routine;
    public string Name => Summary.Name;

    public bool IsActive { get; }

    /// <summary>Días de la rutina en orden y si se han hecho esta semana (solo la activa).</summary>
    public IReadOnlyList<WeekDayMark> Week { get; }
    public string WeekText { get; }
    public string NextDayName { get; }
    public bool HasNextDay => NextDayName.Length > 0;

    public string SubtitleText { get; }

    public RoutineListItem(RoutineSummary summary, DateTime today, bool isActive = false, IReadOnlyList<WeekDayMark>? week = null, string nextDay = "")
    {
        Summary = summary;
        IsActive = isActive;
        Week = week ?? Array.Empty<WeekDayMark>();
        NextDayName = nextDay;

        var done = Week.Count(w => w.Done);
        WeekText = Week.Count > 0 ? $"Esta semana: {done} de {Week.Count}" : "";

        var days = summary.DayCount == 1 ? "1 día" : $"{summary.DayCount} días";
        SubtitleText = isActive
            ? $"{days} · {(summary.ExerciseCount == 1 ? "1 ejercicio" : $"{summary.ExerciseCount} ejercicios")}"
            : $"{days} · {RoutineSchedule.LastTrainedText(summary.LastTrained, today)}";
    }

    /// <summary>Insignia de la tarjeta: "5×5" si el nombre lo lleva, si no las iniciales ("FB").</summary>
    public string Monogram => MonogramOf(Name);

    /// <summary>Color de la insignia (0–3), estable para cada rutina.</summary>
    public int AccentIndex => (Routine.Uid.GetHashCode() & int.MaxValue) % 4;

    public static string MonogramOf(string name)
    {
        var scheme = Regex.Match(name, @"(\d+)\s*[x×X]\s*(\d+)");
        if (scheme.Success) return $"{scheme.Groups[1].Value}×{scheme.Groups[2].Value}";

        var words = Regex.Matches(name, @"[\p{L}\d]+").Select(m => m.Value).ToList();
        if (words.Count == 0) return "?";
        if (words.Count == 1)
        {
            var w = words[0];
            return w.Length == 1 ? w.ToUpperInvariant() : char.ToUpperInvariant(w[0]) + w[1..2].ToLowerInvariant();
        }
        return $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}";
    }
}

public sealed record WeekDayMark(string DayName, bool Done);
