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

    /// <summary>En los entrenos de un cliente: «Enviado por Felipe» o «Contigo».</summary>
    public string? Origin => Session.Origin;
    public bool HasOrigin => Origin is not null;
    public bool IsReceived => Origin?.StartsWith("Enviado") == true;

    /// <summary>La nota del final del entreno, entre comillas.</summary>
    public string NoteText => string.IsNullOrWhiteSpace(Session.Notes) ? "" : $"«{Session.Notes.Trim()}»";
    public bool HasNote => NoteText.Length > 0;

    // Estado: en curso, sin guardar (la app se cerró a medias) o guardada con series sin hacer
    public SessionStatus Status { get; }
    public bool IsInProgress => Status == SessionStatus.InProgress;
    public bool IsIncomplete => Status is SessionStatus.Unsaved or SessionStatus.Incomplete;
    public bool HasStatus => Status != SessionStatus.Complete;
    public string StatusText => Status switch
    {
        SessionStatus.InProgress => "En curso",
        SessionStatus.Unsaved => "Sin guardar",
        SessionStatus.Incomplete => "Incompleto",
        _ => ""
    };

    public static SessionStatus StatusOf(LoggedSession session)
    {
        if (session.Id == WorkoutStateService.ActiveSessionId) return SessionStatus.InProgress;
        if (session.IsComplete) return SessionStatus.Complete;
        // Al guardar siempre se anota la duración: sin ella, el entreno nunca se llegó a guardar
        return session.DurationMinutes is null ? SessionStatus.Unsaved : SessionStatus.Incomplete;
    }

    /// <summary>"Ayer · 58 min · 7,9 t".</summary>
    public string Detail { get; }

    /// <summary>"lun 26 oct".</summary>
    public string DateText { get; }

    public string FeelingEmoji => Session.Feeling is >= 1 and <= 5 ? FeelingEmojis[Session.Feeling.Value - 1] : "";
    public bool HasFeeling => FeelingEmoji.Length > 0;

    public RecentSessionItem(LoggedSession session, double volume, DateTime today)
    {
        Session = session;
        Status = StatusOf(session);
        var ago = RoutineSchedule.AgoText(session.Date, today);
        var parts = new List<string> { char.ToUpper(ago[0]) + ago[1..] };
        if (session.DurationMinutes is > 0) parts.Add($"{session.DurationMinutes} min");
        if (volume > 0) parts.Add(TrainingStats.FormatVolume(volume));
        Detail = string.Join(" · ", parts);
        DateText = session.Date.ToString("ddd d MMM", System.Globalization.CultureInfo.GetCultureInfo("es-ES")).Replace(".", "");
    }
}

public enum SessionStatus { Complete, InProgress, Unsaved, Incomplete }
