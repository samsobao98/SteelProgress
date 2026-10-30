using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.ViewModels;

// Una fila de la lista de clientes (maqueta 3)
public sealed class ClientItem
{
    public required Client Client { get; init; }
    public string Initial => string.IsNullOrEmpty(Client.Name) ? "?" : Client.Name[..1].ToUpperInvariant();
    public string Subtitle { get; init; } = "";
    public string? Badge { get; init; }
    public bool BadgeIsWarning { get; init; }
    public bool HasBadge => Badge is not null;
    /// <summary>«2 limitaciones» si tiene alguna vigente.</summary>
    public string? LimitationsText { get; init; }
    public bool HasLimitations => LimitationsText is not null;
    public bool IsArchived => Client.ArchivedAt is not null;
}

public partial class ClientsViewModel : ObservableObject
{
    private readonly ClientRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ClientContextService _context;

    public ObservableCollection<ClientItem> Clients { get; } = new();

    // Archivados: fuera de la lista y de Inicio, pero con su historial; plegados al final
    public ObservableCollection<ClientItem> ArchivedClients { get; } = new();
    public bool HasArchived => ArchivedClients.Count > 0;
    public string ArchivedHeader => $"Archivados ({ArchivedClients.Count})";
    [ObservableProperty] private bool _showArchived;

    [RelayCommand]
    private void ToggleArchived() => ShowArchived = !ShowArchived;

    public IReadOnlyList<string> AvailableColors { get; } = new[]
    {
        "#FAB387", "#94E2D5", "#CBA6F7", "#F9E2AF", "#89B4FA", "#A6E3A1", "#F38BA8", "#89DCEB"
    };

    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editGoal = "";
    [ObservableProperty] private string _editNotes = "";
    [ObservableProperty] private string _editColor = "#FAB387";
    [ObservableProperty] private int _editTrainingDays;
    [ObservableProperty] private bool _editHasParq;
    [ObservableProperty] private DateTime _editParqDate = DateTime.Today;
    [ObservableProperty] private string _editTitle = "Nuevo cliente";
    private Client? _editing;

    public ClientsViewModel(ClientRepository repository, ICurrentUserService currentUser, ClientContextService context)
    {
        _repository = repository;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task RefreshAsync(DateTime? now = null)
    {
        if (_currentUser.CurrentUserId is not int trainer) return;

        var today = (now ?? DateTime.Now).Date;
        // Semana de lunes a domingo
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var summaries = await _repository.GetSummariesAsync(trainer, weekStart);

        Clients.Clear();
        ArchivedClients.Clear();
        foreach (var s in summaries)
            (s.IsArchived ? ArchivedClients : Clients).Add(ToItem(s, today));
        OnPropertyChanged(nameof(HasArchived));
        OnPropertyChanged(nameof(ArchivedHeader));

        var current = summaries.Where(s => !s.IsArchived).ToList();
        var active = current.Count(s => s.LastSession is DateTime d && (today - d.Date).TotalDays < 7);
        Summary = current.Count == 0 ? (HasArchived ? "Sin clientes activos" : "Añade tu primer cliente")
            : $"{current.Count} {(current.Count == 1 ? "cliente" : "clientes")} · {active} {(active == 1 ? "activo" : "activos")} esta semana";
    }

    public static ClientItem ToItem(ClientSummary s, DateTime today)
    {
        if (s.Client.ArchivedAt is DateTime archived)
            return new ClientItem
            {
                Client = s.Client,
                Subtitle = $"Archivado el {archived:dd/MM/yyyy} · {(s.TotalSessions == 1 ? "1 sesión" : $"{s.TotalSessions} sesiones")}"
            };

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.Client.Goal)) parts.Add(s.Client.Goal!);
        if (s.ActiveRoutineName is not null) parts.Add(s.ActiveRoutineName);
        if (parts.Count == 0) parts.Add(s.TotalSessions == 0 ? "Sin sesiones todavía" : $"{s.TotalSessions} sesiones");

        string? badge = null;
        var warning = false;
        if (s.SessionsThisWeek > 0)
        {
            badge = s.SessionsThisWeek == 1 ? "1 esta semana" : $"{s.SessionsThisWeek} esta semana";
        }
        else if (s.LastSession is DateTime last)
        {
            var days = (int)(today - last.Date).TotalDays;
            if (CoachHome.IsQuiet(s.Client, last, today)) { badge = $"{days} días"; warning = true; }
        }

        var limitations = s.Client.Limitations.Count(l => l.ResolvedAt is null);
        return new ClientItem
        {
            Client = s.Client, Subtitle = string.Join(" · ", parts), Badge = badge, BadgeIsWarning = warning,
            LimitationsText = limitations switch { 0 => null, 1 => "1 limitación", _ => $"{limitations} limitaciones" }
        };
    }

    // Trabajar con un cliente: las demás pantallas pasan a mostrar sus datos
    public void Open(Client client) => _context.Activate(client);

    [RelayCommand]
    private void StartNew()
    {
        _editing = null;
        EditTitle = "Nuevo cliente";
        EditName = "";
        EditGoal = "";
        EditNotes = "";
        EditColor = AvailableColors[Clients.Count % AvailableColors.Count];
        EditTrainingDays = 0;
        EditHasParq = false;
        EditParqDate = DateTime.Today;
        IsEditing = true;
    }

    public void StartEdit(Client client)
    {
        _editing = client;
        EditTitle = "Editar cliente";
        EditName = client.Name;
        EditGoal = client.Goal ?? "";
        EditNotes = client.Notes ?? "";
        EditColor = client.AvatarColor;
        EditTrainingDays = client.TrainingDays;
        EditHasParq = client.ParqSignedAt is not null;
        EditParqDate = client.ParqSignedAt ?? DateTime.Today;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    public void ToggleTrainingDay(DayOfWeek day) => EditTrainingDays ^= TrainingDays.Bit(day);

    [RelayCommand]
    public async Task SaveAsync()
    {
        var name = EditName.Trim();
        if (name.Length == 0)
        {
            NotificationService.Error("El nombre del cliente es obligatorio.");
            return;
        }

        var client = _editing ?? new Client();
        client.Name = name;
        client.Goal = string.IsNullOrWhiteSpace(EditGoal) ? null : EditGoal.Trim();
        client.Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim();
        client.AvatarColor = EditColor;
        client.TrainingDays = EditTrainingDays;
        client.ParqSignedAt = EditHasParq ? EditParqDate.Date : null;

        if (_editing is null) await _repository.AddAsync(client);
        else await _repository.UpdateAsync(client);

        IsEditing = false;
        await RefreshAsync();
    }

    /// <summary>Ya no entrena contigo: sale de la lista y de Inicio, sin borrar nada.</summary>
    public async Task<bool> ArchiveAsync(Client client)
    {
        var confirmed = await ConfirmDialogService.ConfirmAsync("Archivar cliente",
            $"{client.Name} deja de salir en Inicio y en la lista. Sus rutinas, sesiones y datos se guardan: puedes recuperarlo cuando quieras.",
            accept: "Archivar");
        if (!confirmed) return false;

        if (_context.ActiveClient?.Id == client.Id) _context.Clear();
        await _repository.SetArchivedAsync(client.Id, DateTime.Now);
        await RefreshAsync();
        return true;
    }

    public async Task RestoreAsync(Client client)
    {
        await _repository.SetArchivedAsync(client.Id, null);
        await RefreshAsync();
        NotificationService.Success($"{client.Name} vuelve a tu lista de clientes.");
    }

    public async Task<bool> DeleteAsync(Client client)
    {
        var confirmed = await ConfirmDialogService.ConfirmAsync("Eliminar cliente",
            $"Se borrarán las rutinas y sesiones de {client.Name}.", accept: "Eliminar", destructive: true);
        if (!confirmed) return false;

        if (_context.ActiveClient?.Id == client.Id) _context.Clear();
        await _repository.DeleteAsync(client);
        IsEditing = false;
        await RefreshAsync();
        return true;
    }
}
