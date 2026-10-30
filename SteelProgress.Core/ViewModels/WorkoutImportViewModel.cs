using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Core.Sharing;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.ViewModels;

/// <summary>Un entreno recibido en la vista previa: «Pierna · Rutina A», el día, sus cifras, la nota y las series.</summary>
public sealed class ReceivedWorkoutItem
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    public ReceivedWorkoutItem(ReceivedWorkout r)
    {
        var w = r.Workout;
        Title = string.IsNullOrWhiteSpace(w.RoutineName) ? w.DayName : $"{w.DayName} · {w.RoutineName}";
        DateText = w.Date.ToString("dddd d/MM", Es);
        DurationText = w.DurationMinutes is int m ? $"{m} min" : "—";
        FeelingText = w.Feeling is int f ? $"{f} / 5" : "—";
        SetsText = r.SetCount.ToString(Es);
        NoteText = string.IsNullOrWhiteSpace(w.Notes) ? "" : $"«{w.Notes.Trim()}»";
        Lines = r.Lines.Select(l => new ReceivedLine(l.Exercise, l.Sets)).ToList();
        IsUpdate = r.IsUpdate;
    }

    public string Title { get; }
    public string DateText { get; }
    public string DurationText { get; }
    public string FeelingText { get; }
    public string SetsText { get; }
    public string NoteText { get; }
    public bool HasNote => NoteText.Length > 0;
    public IReadOnlyList<ReceivedLine> Lines { get; }
    public bool IsUpdate { get; }
}

public sealed record ReceivedLine(string Exercise, string Sets);

/// <summary>
/// Entreno recibido de un cliente (maqueta del 2026-10-30): de quién es (reconocido por la rutina que le
/// pasaste, o eligiéndolo), qué trae y «Guardar en Felipe». Solo en perfiles de entrenador.
/// </summary>
public partial class WorkoutImportViewModel : ObservableObject, IDisposable
{
    private readonly WorkoutImporter _importer;
    private readonly ICurrentUserService _currentUser;
    private readonly ViewOptionsService? _viewOptions;
    private SteelPackage? _package;

    public WorkoutImportViewModel(WorkoutImporter importer, ICurrentUserService currentUser, ViewOptionsService? viewOptions = null)
    {
        _importer = importer;
        _currentUser = currentUser;
        _viewOptions = viewOptions;
    }

    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public bool HasError => Error is not null;

    /// <summary>Se abrió en un perfil personal: se ofrece abrirlo en otro (el de entrenador).</summary>
    [ObservableProperty] private bool _needsCoachProfile;

    /// <summary>El cliente reconocido; si no, el que elija el entrenador.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClient), nameof(ClientName), nameof(ClientInitial), nameof(ClientColor), nameof(SaveText))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private Client? _client;

    public bool HasClient => Client is not null;
    public string ClientName => Client?.Name ?? "";
    public string ClientInitial => string.IsNullOrEmpty(ClientName) ? "?" : ClientName[..1].ToUpperInvariant();
    public string ClientColor => Client?.AvatarColor ?? "#8A91A8";

    [ObservableProperty] private bool _isRecognized;
    [ObservableProperty] private string _senderText = "";
    [ObservableProperty] private bool _isUpdate;

    public ObservableCollection<ReceivedWorkoutItem> Workouts { get; } = new();
    public ObservableCollection<Client> Clients { get; } = new();

    public string SaveText => Client is null ? "Elige de quién es"
        : IsUpdate ? $"Actualizar en {Client.Name}" : $"Guardar en {Client.Name}";
    partial void OnIsUpdateChanged(bool value) => OnPropertyChanged(nameof(SaveText));

    public async Task LoadAsync(Stream stream)
    {
        Error = null;
        if (_viewOptions?.Current.IsCoach != true || _currentUser.CurrentUserId is not int coach)
        {
            Error = "Es un entreno que un cliente le envía a su entrenador. Ábrelo desde tu perfil de entrenador.";
            NeedsCoachProfile = true;
            return;
        }
        try
        {
            _package?.Dispose();
            _package = SteelPackage.Open(stream);
            var preview = await _importer.PreviewAsync(_package, coach);

            Workouts.Clear();
            foreach (var w in preview.Workouts) Workouts.Add(new ReceivedWorkoutItem(w));
            IsUpdate = preview.Workouts.Any(w => w.IsUpdate);

            Client = preview.Client;
            IsRecognized = preview.Client is not null;
            var sender = preview.AthleteName is { Length: > 0 } n ? n : "tu cliente";
            SenderText = IsRecognized
                ? "Es suyo: lo hizo con una rutina que le pasaste."
                : $"Lo envía {sender}, pero no es una rutina tuya: elige de qué cliente es.";

            Clients.Clear();
            if (!IsRecognized)
                foreach (var c in await _importer.ClientsAsync(coach)) Clients.Add(c);

            IsLoaded = true;
        }
        catch (SteelPackageException ex)
        {
            Error = ex.Message;
        }
    }

    public void Choose(Client client) => Client = client;

    public event Action<Client>? Saved;

    private bool CanSave() => Client is not null && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (_package is null || Client is not { } client || _currentUser.CurrentUserId is not int coach) return;
        IsBusy = true;
        try
        {
            var count = await _importer.ImportAsync(_package, coach, client.Id);
            NotificationService.Success(count == 1
                ? $"Entreno guardado en {client.Name}."
                : $"{count} entrenos guardados en {client.Name}.");
            Saved?.Invoke(client);
        }
        catch (SteelPackageException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        _package?.Dispose();
        _package = null;
    }
}
