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

    /// <summary>Orden en Inicio: primero lo que pide atención (0), al final lo ya hecho hoy.</summary>
    public int Priority { get; init; }
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

    /// <summary>Meses que vale el PAR-Q+ firmado.</summary>
    public const int ParqMonths = 12;

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

    /// <summary>Clientes que se ven en Inicio; el resto, en «Todos».</summary>
    public const int HomeRows = 5;

    /// <summary>Avisos de «Pendiente» que se ven sin desplegar.</summary>
    public const int HomePending = 3;

    /// <summary>Los clientes de Inicio por lo que piden: sin noticias, toca hoy, sin rutina, por empezar, el resto y lo hecho hoy.</summary>
    public static List<CoachClientRow> Rows(IEnumerable<ClientSummary> summaries, DateTime today) =>
        summaries.Where(s => !s.IsArchived)
            .Select(s => Row(s, today))
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Client.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>«Esta semana te han enviado 3 entrenos: Lucía y Felipe.»; vacío si ninguno.</summary>
    public static string ReceivedText(IEnumerable<ClientSummary> summaries)
    {
        var from = summaries.Where(s => s.ReceivedThisWeek > 0 && !s.IsArchived).ToList();
        var total = from.Sum(s => s.ReceivedThisWeek);
        if (total == 0) return "";
        var names = from.OrderByDescending(s => s.ReceivedThisWeek)
            .ThenBy(s => s.Client.Name, StringComparer.CurrentCultureIgnoreCase).Select(s => s.Client.Name).ToList();
        var who = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " y " + names[^1];
        return total == 1 ? $"Esta semana te han enviado 1 entreno: {who}." : $"Esta semana te han enviado {total} entrenos: {who}.";
    }

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

        var priority = chip switch
        {
            "Sin noticias" => 0,
            "Toca hoy" => 1,
            "Crear rutina" => 2,
            "Por empezar" => 3,
            "Hecho hoy" => 5,
            _ => 4
        };
        return new CoachClientRow { Client = s.Client, Line = line, When = when, Chip = chip, ChipKind = kind, Priority = priority };
    }

    /// <summary>
    /// Lo que hay que mirar: clientes sin rutina, clientes sin sesiones desde hace días y PAR-Q+ caducados.
    /// </summary>
    public static List<CoachPendingItem> Pending(IEnumerable<ClientSummary> summaries, DateTime today)
    {
        var items = new List<CoachPendingItem>();
        foreach (var s in summaries.Where(s => !s.IsArchived))
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
                    // Sin servidor no se sabe si ha entrenado: solo lo que te envía o hacéis juntos
                    Detail = "Pídele que te envíe sus entrenos o haced una sesión juntos"
                });
            if (s.Client.ParqSignedAt is DateTime signed && today.Date >= signed.Date.AddMonths(ParqMonths))
                items.Add(new CoachPendingItem
                {
                    Client = s.Client,
                    Title = $"{s.Client.Name}: renovar el PAR-Q+",
                    Detail = $"Lo firmó el {signed:dd/MM/yyyy}; vale {ParqMonths} meses"
                });
        }
        return items;
    }
}
