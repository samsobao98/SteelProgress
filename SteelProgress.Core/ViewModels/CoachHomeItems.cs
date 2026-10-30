using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

public enum ClientChipKind { None, Neutral, Go, Warning }

/// <summary>
/// Una fila de «Hoy con tus clientes» en el Inicio de un perfil de entrenador (maqueta del 2026-10-30):
/// qué le toca, hace cuánto entrenó y una etiqueta si hay algo que mirar.
/// </summary>
public sealed class CoachClientRow
{
    public required Client Client { get; init; }
    public string Initial => string.IsNullOrEmpty(Client.Name) ? "?" : Client.Name[..1].ToUpperInvariant();
    public string Line { get; init; } = "";
    public string When { get; init; } = "";
    public string? Chip { get; init; }
    public ClientChipKind ChipKind { get; init; }
    public bool HasChip => Chip is not null;
    public bool ChipIsGo => ChipKind == ClientChipKind.Go;
    public bool ChipIsWarning => ChipKind == ClientChipKind.Warning;
}

/// <summary>Algo que mirar de un cliente, en «Pendiente».</summary>
public sealed class CoachPendingItem
{
    public required Client Client { get; init; }
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
}

public static class CoachHome
{
    /// <summary>Días sin ninguna sesión (importada o contigo) para avisar: el mismo umbral que Clientes.</summary>
    public const int QuietDays = 7;

    /// <summary>Días desde la última sesión para que, si queda día por hacer esta semana, «toque hoy».</summary>
    public const int RestDays = 2;

    /// <summary>Con días de entreno indicados: cuántos de esos días seguidos sin sesión para avisar.</summary>
    public const int MissedDays = 2;

    /// <summary>
    /// Sin noticias del cliente: con sus días de entreno, si se ha saltado <see cref="MissedDays"/>
    /// seguidos; sin ellos, a los <see cref="QuietDays"/> días. Mismo criterio en Inicio y en Clientes.
    /// </summary>
    public static bool IsQuiet(Client client, DateTime lastSession, DateTime today) =>
        client.TrainingDays == 0
            ? (today.Date - lastSession.Date).Days >= QuietDays
            : TrainingDays.MissedBetween(client.TrainingDays, lastSession, today) >= MissedDays;

    /// <summary>Si hoy es uno de sus días; sin días indicados, si ya ha descansado <see cref="RestDays"/>.</summary>
    private static bool IsDueToday(Client client, int daysSinceLast, DateTime today) =>
        client.TrainingDays == 0 ? daysSinceLast >= RestDays : TrainingDays.Includes(client.TrainingDays, today);

    public static CoachClientRow Row(ClientSummary s, DateTime today)
    {
        var week = s.ActiveRoutineWeek;
        var next = week is null ? null : RoutineSchedule.NextDay(week);
        var weekDone = week is { Days.Count: > 0 } && week.Days.All(d => week.DoneThisWeek.Contains(d.Id));
        int? days = s.LastSession is DateTime last ? (today.Date - last.Date).Days : null;

        string line;
        if (s.ActiveRoutineName is null) line = "Sin rutina activa";
        else if (next is null) line = $"{s.ActiveRoutineName} · sin días";
        else if (weekDone) line = $"Semana hecha · {s.ActiveRoutineName}";
        else line = $"Le toca {next.Name} · {s.ActiveRoutineName}";

        var when = s.LastSession is DateTime lastDate ? RoutineSchedule.AgoText(lastDate, today) : "sin sesiones";
        var quiet = s.LastSession is DateTime l && IsQuiet(s.Client, l, today);

        (string? chip, ClientChipKind kind) = (s.ActiveRoutineName, days) switch
        {
            (null, _) => ("Crear rutina", ClientChipKind.Neutral),
            (_, 0) => ("Hecho hoy", ClientChipKind.Go),
            (_, null) => ("Por empezar", ClientChipKind.Neutral),
            _ when quiet => ("Sin noticias", ClientChipKind.Warning),
            (_, int d) when next is not null && !weekDone && IsDueToday(s.Client, d, today) => ("Toca hoy", ClientChipKind.Go),
            _ => (null, ClientChipKind.None)
        };

        return new CoachClientRow { Client = s.Client, Line = line, When = when, Chip = chip, ChipKind = kind };
    }

    /// <summary>Lo que hay que mirar: clientes sin rutina y clientes sin sesiones desde hace días.</summary>
    public static List<CoachPendingItem> Pending(IEnumerable<ClientSummary> summaries, DateTime today)
    {
        var items = new List<CoachPendingItem>();
        foreach (var s in summaries)
        {
            if (s.ActiveRoutineName is null)
                items.Add(new CoachPendingItem
                {
                    Client = s.Client,
                    Title = $"{s.Client.Name} no tiene rutina",
                    Detail = "Entra en su ficha para prepararle una"
                });
            else if (s.LastSession is DateTime last && IsQuiet(s.Client, last, today))
                items.Add(new CoachPendingItem
                {
                    Client = s.Client,
                    Title = $"{s.Client.Name}: {(today.Date - last.Date).Days} días sin sesiones",
                    // Sin servidor no se sabe si ha entrenado: solo lo que se ha importado o hecho juntos
                    Detail = "Importa sus entrenos o haced una sesión juntos"
                });
        }
        return items;
    }
}
