using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SteelProgress.Core.Services;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.ViewModels;

public partial class LauncherViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;
    private readonly ICurrentUserService _currentUserService;

    public ObservableCollection<UserProfile> Profiles { get; } = new();

    // Línea bajo el nombre de cada perfil en la lista («Último entreno ayer · Pierna»), por Id.
    // Si la consulta falla la lista se enseña igual, sin esa línea.
    private Dictionary<int, string> _summaries = new();
    public string SummaryOf(UserProfile profile) =>
        _summaries.TryGetValue(profile.Id, out var text) ? text : ProfileSummaryText(profile, null, null, 0, DateTime.Today);

    /// <summary>
    /// «Último entreno hace 3 días · Empuje», «Sin entrenar aún» o, en vista entrenador,
    /// «Vista entrenador · 6 clientes».
    /// </summary>
    public static string ProfileSummaryText(UserProfile profile, DateTime? lastSession, string? lastDayName, int clientCount, DateTime today)
    {
        if (profile.ViewMode == ProfileViewMode.Trainer)
            return clientCount switch
            {
                0 => "Vista entrenador · sin clientes aún",
                1 => "Vista entrenador · 1 cliente",
                _ => $"Vista entrenador · {clientCount} clientes"
            };

        if (lastSession is not DateTime date)
            return "Sin entrenar aún";

        var text = $"Último entreno {RoutineSchedule.AgoText(date, today)}";
        return string.IsNullOrWhiteSpace(lastDayName) ? text : $"{text} · {lastDayName}";
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotCreating))]
    [NotifyPropertyChangedFor(nameof(CanAddProfile))]
    [NotifyPropertyChangedFor(nameof(CanShowProfiles))]
    private bool _isCreating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    private bool _isLoaded;

    // Si el arranque falla, el mensaje se queda en pantalla: un spinner eterno
    // sin explicación es indistinguible de "aún cargando".
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(CanShowProfiles))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsLoading => !IsLoaded;
    public bool IsNotCreating => !IsCreating;
    public bool CanAddProfile => !IsCreating;
    public bool CanShowProfiles => !IsCreating && !HasError;
    public bool HasProfiles => Profiles.Count > 0;
    public bool IsFirstTimeSetup => !HasProfiles;
    public string SubtitleText => HasError
        ? "No se pudo cargar"
        : HasProfiles ? "¿Quién entrena hoy?" : "Crea tu primer perfil";

    public IReadOnlyList<string> AvailableColors { get; } = new[]
    {
        "#89B4FA", "#A6E3A1", "#F38BA8", "#FAB387", "#F9E2AF",
        "#CBA6F7", "#94E2D5", "#89DCEB"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionPreviewText))]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    [NotifyPropertyChangedFor(nameof(HasNoDescription))]
    private string _newProfileDescription = string.Empty;

    [ObservableProperty] private string _newProfileName = string.Empty;
    [ObservableProperty] private string _selectedColor = "#A6E3A1";

    public bool HasDescription => !string.IsNullOrWhiteSpace(NewProfileDescription);
    public bool HasNoDescription => string.IsNullOrWhiteSpace(NewProfileDescription);
    public string DescriptionPreviewText =>
        string.IsNullOrWhiteSpace(NewProfileDescription)
            ? "Tu frase aquí..."
            // NBSP final: en Android la cursiva sobresale del ancho medido y el
            // recorte se come el último glifo; así se come el espacio invisible.
            : $"\"{NewProfileDescription.Trim()}\" ";

    public event Action? ProfileChosen;
    public event Action? ProfileUpdated;

    // ── Editing ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _editingName = string.Empty;
    [ObservableProperty] private string _editingDescription = string.Empty;
    [ObservableProperty] private string _editingColor = "#A6E3A1";

    private UserProfile? _profileBeingEdited;

    public void StartEditing(UserProfile profile)
    {
        _profileBeingEdited = profile;
        EditingName = profile.Name;
        EditingDescription = profile.Description ?? string.Empty;
        EditingColor = profile.AvatarColor;
    }

    [RelayCommand]
    public async Task UpdateProfileAsync()
    {
        if (_profileBeingEdited is null) return;
        var name = EditingName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var desc = EditingDescription.Trim();
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.UserProfiles.FindAsync(_profileBeingEdited.Id);
        if (entity is null) return;

        entity.Name = name;
        entity.Description = desc.Length > 0 ? desc : null;
        entity.AvatarColor = EditingColor;
        await db.SaveChangesAsync();

        _profileBeingEdited.Name = entity.Name;
        _profileBeingEdited.Description = entity.Description;
        _profileBeingEdited.AvatarColor = entity.AvatarColor;
        _profileBeingEdited = null;
        ProfileUpdated?.Invoke();
    }

    private readonly ViewOptionsService _viewOptions;

    public LauncherViewModel(IServiceProvider sp, ICurrentUserService currentUserService, ViewOptionsService viewOptions)
    {
        _sp = sp;
        _currentUserService = currentUserService;
        _viewOptions = viewOptions;
    }

    public async Task LoadAsync(Task migrationTask)
    {
        try
        {
            await migrationTask;

            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // La consulta va antes de tocar Profiles: si falla, la lista que ya
            // se estuviera mostrando se queda como está en vez de vaciarse.
            var list = await db.UserProfiles.IgnoreQueryFilters().OrderBy(p => p.Id).ToListAsync();
            _summaries = await LoadSummariesAsync(db, list);
            Profiles.Clear();
            foreach (var p in list)
                Profiles.Add(p);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        // Pase lo que pase se apaga el spinner, o la pantalla se queda girando
        // para siempre sin decir por qué.
        IsLoaded = true;
        NotifyProfileDerived();

        if (!HasError && !HasProfiles)
            IsCreating = true;
    }

    private static async Task<Dictionary<int, string>> LoadSummariesAsync(AppDbContext db, List<UserProfile> profiles)
    {
        try
        {
            // Solo los entrenos propios: los de los clientes del entrenador llevan ClientId.
            var lastByUser = await db.WorkoutSessions.IgnoreQueryFilters()
                .Where(s => s.ClientId == null)
                .GroupBy(s => s.UserId)
                .Select(g => new { UserId = g.Key, Date = g.Max(s => s.Date) })
                .ToListAsync();

            var lastDays = new Dictionary<int, (DateTime Date, string? Day)>();
            foreach (var last in lastByUser)
            {
                var day = await db.WorkoutSessions.IgnoreQueryFilters()
                    .Where(s => s.UserId == last.UserId && s.ClientId == null && s.Date == last.Date)
                    .Select(s => s.RoutineDay!.Name)
                    .FirstOrDefaultAsync();
                lastDays[last.UserId] = (last.Date, day);
            }

            var clients = await db.Clients.IgnoreQueryFilters()
                .GroupBy(c => c.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var today = DateTime.Today;
            return profiles.ToDictionary(p => p.Id, p =>
            {
                var hasLast = lastDays.TryGetValue(p.Id, out var last);
                return ProfileSummaryText(p, hasLast ? last.Date : null, hasLast ? last.Day : null,
                    clients.GetValueOrDefault(p.Id), today);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LauncherViewModel.LoadSummariesAsync: {ex}");
            return new();
        }
    }

    [RelayCommand]
    public void SelectProfile(UserProfile profile)
    {
        _currentUserService.SetCurrentUser(profile.Id);
        _viewOptions.Apply(profile);
        ProfileChosen?.Invoke();
    }

    public async Task DeleteProfileAsync(UserProfile profile)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.UserProfiles.FindAsync(profile.Id);
        if (entity is null) return;
        db.UserProfiles.Remove(entity);
        await db.SaveChangesAsync();

        Profiles.Remove(profile);
        NotifyProfileDerived();

        if (!HasProfiles)
            IsCreating = true;
    }

    [RelayCommand]
    public void StartCreating()
    {
        NewProfileName = string.Empty;
        NewProfileDescription = string.Empty;
        SelectedColor = "#A6E3A1";
        IsCreating = true;
    }

    [RelayCommand]
    public void SetColor(string color) => SelectedColor = color;

    [RelayCommand]
    public void CancelCreating()
    {
        if (HasProfiles)
            IsCreating = false;
    }

    [RelayCommand]
    public async Task CreateProfileAsync()
    {
        var name = NewProfileName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        UserProfile profile;
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var desc = NewProfileDescription.Trim();
            profile = new UserProfile
            {
                Name = name,
                AvatarColor = SelectedColor,
                Description = desc.Length > 0 ? desc : null
            };
            db.UserProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        Profiles.Add(profile);
        IsCreating = false;
        NotifyProfileDerived();
    }

    private void NotifyProfileDerived()
    {
        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(IsFirstTimeSetup));
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(CanAddProfile));
    }
}
