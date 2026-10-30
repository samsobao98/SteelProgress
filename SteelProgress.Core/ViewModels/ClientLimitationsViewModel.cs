using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

/// <summary>Una limitación en la lista: «Rodilla derecha», su texto y «Pautas del fisio · desde 12/10/2026».</summary>
public sealed class LimitationItem
{
    public required ClientLimitation Limitation { get; init; }
    public string Title => LimitationText.Title(Limitation);
    public string Text => Limitation.Text;
    public string Meta => Limitation.ResolvedAt is DateTime resolved
        ? $"Superada el {resolved:dd/MM/yyyy}"
        : $"{LimitationText.SourceText(Limitation.Source)} · desde {Limitation.Since:dd/MM/yyyy}";
}

/// <summary>
/// Limitaciones de un cliente (maqueta del 2026-10-30): lesiones, molestias o pautas de su fisio o médico.
/// Solo se apuntan y se recuerdan al entrenar con él. Son datos de salud: hace falta su consentimiento
/// para guardar la primera, y retirarlo las borra todas.
/// </summary>
public partial class ClientLimitationsViewModel : ObservableObject
{
    private readonly ClientRepository _repository;
    private readonly ClientContextService? _context;
    private readonly TimeProvider _clock;
    private ClientLimitation? _editing;

    public ClientLimitationsViewModel(ClientRepository repository, ClientContextService? context = null, TimeProvider? clock = null)
    {
        _repository = repository;
        _context = context;
        _clock = clock ?? TimeProvider.System;
    }

    public int ClientId { get; private set; }
    [ObservableProperty] private string _clientName = "";
    public string Title => $"Limitaciones de {ClientName}";
    partial void OnClientNameChanged(string value) => OnPropertyChanged(nameof(Title));

    // ── Consentimiento ──

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConsent), nameof(NeedsConsent), nameof(CanAdd), nameof(ConsentText))]
    private DateTime? _consentAt;

    /// <summary>
    /// La casilla «me ha dado su consentimiento». Marcarla lo registra con su fecha: vale también para
    /// apuntar su peso, aunque no tenga ninguna limitación.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    private bool _consentChecked;

    /// <summary>La página lo llama al marcar la casilla.</summary>
    public async Task GiveConsentAsync()
    {
        if (!ConsentChecked || HasConsent || ClientId == 0) return;
        await _repository.GiveHealthConsentAsync(ClientId, _clock.GetLocalNow().DateTime);
        await LoadAsync(ClientId);
    }

    public bool HasConsent => ConsentAt is not null;
    public bool NeedsConsent => !HasConsent;
    public bool CanAdd => HasConsent || ConsentChecked;
    public string ConsentText => ConsentAt is DateTime d ? $"Consentimiento de {ClientName}\ndado el {d:dd/MM/yyyy}" : "";
    public string ConsentLabel => $"{ClientName} me ha dado su consentimiento para guardar sus datos de salud (limitaciones y peso).";

    // ── Listas ──

    public ObservableCollection<LimitationItem> Current { get; } = new();
    public ObservableCollection<LimitationItem> Resolved { get; } = new();
    public bool HasResolved => Resolved.Count > 0;
    public string ResolvedHeader => $"Superadas ({Resolved.Count})";
    [ObservableProperty] private bool _showResolved;

    [RelayCommand]
    private void ToggleResolved() => ShowResolved = !ShowResolved;

    public async Task LoadAsync(int clientId)
    {
        var client = await _repository.GetWithLimitationsAsync(clientId);
        if (client is null) return;

        ClientId = client.Id;
        ClientName = client.Name;
        ConsentAt = client.HealthConsentAt;
        OnPropertyChanged(nameof(ConsentLabel));

        Current.Clear();
        foreach (var l in client.Limitations.Where(l => l.ResolvedAt is null).OrderBy(l => l.Since))
            Current.Add(new LimitationItem { Limitation = l });
        Resolved.Clear();
        foreach (var l in client.Limitations.Where(l => l.ResolvedAt is not null).OrderByDescending(l => l.ResolvedAt))
            Resolved.Add(new LimitationItem { Limitation = l });
        OnPropertyChanged(nameof(HasResolved));
        OnPropertyChanged(nameof(ResolvedHeader));

        _context?.LimitationsChanged(client.Id, client.Limitations);
    }

    // ── Añadir y editar ──

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editTitle = "Nueva limitación";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? _editZone;

    [ObservableProperty] private BodySide _editSide;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _editText = "";

    [ObservableProperty] private GuidanceSource _editSource;
    [ObservableProperty] private DateTime _editSince = DateTime.Today;

    [RelayCommand]
    private void StartNew()
    {
        if (!CanAdd) return;
        _editing = null;
        EditTitle = "Nueva limitación";
        EditZone = null;
        EditSide = BodySide.None;
        EditText = "";
        EditSource = GuidanceSource.Own;
        EditSince = Today;
        IsEditing = true;
    }

    public void StartEdit(LimitationItem item)
    {
        _editing = item.Limitation;
        EditTitle = "Editar limitación";
        EditZone = item.Limitation.Zone;
        EditSide = item.Limitation.Side;
        EditText = item.Limitation.Text;
        EditSource = item.Limitation.Source;
        EditSince = item.Limitation.Since;
        IsEditing = true;
    }

    /// <summary>Tocar el lado elegido lo quita: el lado es opcional.</summary>
    public void PickSide(BodySide side) => EditSide = EditSide == side ? BodySide.None : side;

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    private bool CanSave() => EditZone is not null && !string.IsNullOrWhiteSpace(EditText);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!CanSave() || (_editing is null && !CanAdd)) return;

        var l = _editing ?? new ClientLimitation { ClientId = ClientId };
        l.Zone = EditZone!;
        l.Side = EditSide;
        l.Text = EditText.Trim();
        l.Source = EditSource;
        l.Since = EditSince.Date;

        await _repository.SaveLimitationAsync(l, _clock.GetLocalNow().DateTime);
        IsEditing = false;
        await LoadAsync(ClientId);
    }

    // ── Acciones de una limitación ──

    /// <summary>Superada (deja de avisar y pasa abajo) o, si ya lo estaba, vuelve a estar vigente.</summary>
    public async Task ToggleResolvedAsync(LimitationItem item)
    {
        item.Limitation.ResolvedAt = item.Limitation.ResolvedAt is null ? Today : null;
        await _repository.SaveLimitationAsync(item.Limitation, _clock.GetLocalNow().DateTime);
        await LoadAsync(ClientId);
    }

    public async Task<bool> DeleteAsync(LimitationItem item)
    {
        var confirmed = await ConfirmDialogService.ConfirmAsync("Eliminar limitación",
            $"«{item.Title}» se borrará de la ficha de {ClientName}.", accept: "Eliminar", destructive: true);
        if (!confirmed) return false;

        await _repository.DeleteLimitationAsync(item.Limitation);
        await LoadAsync(ClientId);
        return true;
    }

    /// <summary>Si el cliente retira su permiso, sus datos de salud se borran (RGPD).</summary>
    [RelayCommand]
    private async Task WithdrawConsentAsync()
    {
        var confirmed = await ConfirmDialogService.ConfirmAsync("Retirar consentimiento",
            $"Se borrarán todas las limitaciones de {ClientName} (también las superadas) y sus pesajes.", accept: "Retirar y borrar", destructive: true);
        if (!confirmed) return;

        await _repository.WithdrawHealthConsentAsync(ClientId);
        ConsentChecked = false;
        ShowResolved = false;
        await LoadAsync(ClientId);
    }

    private DateTime Today => _clock.GetLocalNow().Date;
}
