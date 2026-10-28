using System.Globalization;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;

namespace SteelProgress.Core.ViewModels;

/// <summary>
/// Vista previa de una rutina (mantener pulsada en la lista o ⋮ → Ver resumen):
/// sus días, cuánto se ha entrenado y el último entreno, sin entrar en ella.
/// </summary>
public sealed class RoutinePreview
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    public RoutineListItem Item { get; }
    public string Name => Item.Name;
    public string Notes => Item.Routine.Notes ?? "";
    public bool HasNotes => Notes.Length > 0;
    public bool IsActive => Item.IsActive;
    public string ActiveLabel => Item.ActiveLabel;

    public IReadOnlyList<RoutinePreviewDay> Days { get; }
    public bool HasDays => Days.Count > 0;

    public string TotalText { get; }
    public string MonthText { get; }
    public string AverageDurationText { get; }

    public bool HasLastSession { get; }
    public string LastSessionTitle { get; }
    public string LastSessionDetail { get; }

    private RoutinePreview(RoutineListItem item, RoutineOverview overview, DateTime today)
    {
        Item = item;

        Days = overview.Days
            .Select(d => new RoutinePreviewDay(d.Name,
                $"{(d.ExerciseCount == 1 ? "1 ejercicio" : $"{d.ExerciseCount} ejercicios")} · " +
                (d.LastDone is DateTime last ? RoutineSchedule.AgoText(last, today) : "sin hacer")))
            .ToList();

        var sessions = overview.Sessions;
        TotalText = sessions.Count.ToString(Es);
        MonthText = sessions.Count(s => s.Date.Year == today.Year && s.Date.Month == today.Month).ToString(Es);
        var durations = sessions.Where(s => s.DurationMinutes is > 0).Select(s => s.DurationMinutes!.Value).ToList();
        AverageDurationText = durations.Count > 0 ? $"{Math.Round(durations.Average())} min" : "—";

        var lastSession = sessions.FirstOrDefault();
        HasLastSession = lastSession is not null;
        if (lastSession is null)
        {
            LastSessionTitle = "Aún no la has entrenado";
            LastSessionDetail = "";
            return;
        }

        var dayName = overview.Days.FirstOrDefault(d => d.Id == lastSession.RoutineDayId)?.Name ?? "Día";
        LastSessionTitle = $"{dayName} · {RoutineSchedule.AgoText(lastSession.Date, today)}";
        var parts = new List<string> { ShortDate(lastSession.Date) };
        if (lastSession.DurationMinutes is int min && min > 0) parts.Add($"{min} min");
        parts.Add(FormatVolume(lastSession.Volume));
        LastSessionDetail = string.Join(" · ", parts);
    }

    public static RoutinePreview Build(RoutineListItem item, RoutineOverview overview, DateTime today) => new(item, overview, today);

    // "lun 26 oct"
    internal static string ShortDate(DateTime date) =>
        date.ToString("ddd d MMM", Es).Replace(".", "");

    internal static string FormatVolume(double kg) =>
        kg >= 1000 ? $"{(kg / 1000).ToString("0.#", Es)} t" : $"{kg.ToString("0", Es)} kg";
}

public sealed record RoutinePreviewDay(string Name, string Detail);
