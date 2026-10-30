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
}

public partial class ClientsViewModel : ObservableObject
{
    private readonly ClientRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ClientContextService _context;

    public ObservableCollection<ClientItem> Clients { get; } = new();

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
        foreach (var s in summaries)
            Clients.Add(ToItem(s, today));

        var active = summaries.Count(s => s.LastSession is DateTime d && (today - d.Date).TotalDays < 7);
        Summary = summaries.Count == 0 ? "Añade tu primer cliente"
            : $"{summaries.Count} {(summaries.Count == 1 ? "cliente" : "clientes")} · {active} {(active == 1 ? "activo" : "activos")} esta semana";
    }

    public static ClientItem ToItem(ClientSummary s, DateTime today)
    {
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

        if (_editing is null) await _repository.AddAsync(client);
        else await _repository.UpdateAsync(client);

        IsEditing = false;
        await RefreshAsync();
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
