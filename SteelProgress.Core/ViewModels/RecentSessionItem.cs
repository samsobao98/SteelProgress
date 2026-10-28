using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;

namespace SteelProgress.Core.ViewModels;

/// <summary>Una sesión en las listas de Inicio y del historial de Progreso.</summary>
public class RecentSessionItem
{
    // Los mismos que la pantalla de fin de entreno (1–5)
    public static readonly string[] FeelingEmojis = ["😫", "😕", "😐", "💪", "🔥"];

    public LoggedSession Session { get; }
    public int SessionId => Session.Id;
    public string Title => Session.DayName;
    public string RoutineName => Session.RoutineName;
    public bool IsComplete => Session.IsComplete;
    public bool IsIncomplete => !Session.IsComplete;

    /// <summary>"Ayer · 58 min · 7,9 t".</summary>
    public string Detail { get; }

    /// <summary>"lun 26 oct".</summary>
    public string DateText { get; }

    public string FeelingEmoji => Session.Feeling is >= 1 and <= 5 ? FeelingEmojis[Session.Feeling.Value - 1] : "";
    public bool HasFeeling => FeelingEmoji.Length > 0;

    public RecentSessionItem(LoggedSession session, double volume, DateTime today)
    {
        Session = session;
        var ago = RoutineSchedule.AgoText(session.Date, today);
        var parts = new List<string> { char.ToUpper(ago[0]) + ago[1..] };
        if (session.DurationMinutes is > 0) parts.Add($"{session.DurationMinutes} min");
        if (volume > 0) parts.Add(TrainingStats.FormatVolume(volume));
        Detail = string.Join(" · ", parts);
        DateText = session.Date.ToString("ddd d MMM", System.Globalization.CultureInfo.GetCultureInfo("es-ES")).Replace(".", "");
    }
}
