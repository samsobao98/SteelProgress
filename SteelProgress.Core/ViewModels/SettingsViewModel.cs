using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.ViewModels;

// Ajustes del perfil actual: tipo de perfil (personal o entrenador), vista simple o detallada y
// bloques extra de la vista simple. Cada cambio se guarda al momento, sin botón de guardar.
public partial class SettingsViewModel : ObservableObject
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ViewOptionsService _viewOptions;
    private readonly ClientContextService? _clientContext;
    private bool _loading;

    [ObservableProperty] private string _profileName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimple))]
    [NotifyPropertyChangedFor(nameof(ModeDescription))]
    private bool _isDetailed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KindDescription))]
    private bool _isCoach;

    [ObservableProperty] private bool _showIntensity;
    [ObservableProperty] private bool _showTempoAndRest;
    [ObservableProperty] private bool _showAdvancedStructure;
    [ObservableProperty] private bool _showRepRanges;
    [ObservableProperty] private bool _showDetailedNotes;

    public bool IsSimple => !IsDetailed;

    // ── Tu gimnasio: discos que hay y peso de la barra ──

    public IReadOnlyList<PlateOption> Plates { get; } = GymSetup.AllPlates.Select(p => new PlateOption(p, false)).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BarWeightText))]
    private double _barWeight = GymSetup.DefaultBarWeight;

    public string BarWeightText => $"{WorkoutExerciseState.FormatKg(BarWeight)} kg";

    /// <summary>Marca o quita un disco. Siempre queda al menos uno.</summary>
    [RelayCommand]
    private void TogglePlate(PlateOption plate)
    {
        if (plate.IsAvailable && Plates.Count(p => p.IsAvailable) == 1) return;
        plate.IsAvailable = !plate.IsAvailable;
        _ = SaveAsync();
    }

    /// <summary>−/+ en el peso de la barra, de 2,5 en 2,5 (entre 0 y 50 kg).</summary>
    [RelayCommand]
    private void ChangeBarWeight(string step) =>
        BarWeight = Math.Clamp(BarWeight + (step == "-" ? -2.5 : 2.5), 0, 50);

    partial void OnBarWeightChanged(double value) => _ = SaveAsync();

    public string KindDescription => IsCoach
        ? "Para llevar a tus clientes: su ficha, sus rutinas y las sesiones con ellos. Tu propio entreno, en otro perfil."
        : "Para tus propios entrenos.";

    public string ModeDescription => IsDetailed
        ? "Todas las opciones: biblioteca de ejercicios, crear los tuyos con su material e instrucciones, intensidad, tempo, superseries y notas en cada serie."
        : "Lo justo para entrenar y consultar, con menos apartados. Los datos que tenga una rutina se ven siempre.";

    public SettingsViewModel(AppDbContext dbContext, ICurrentUserService currentUser, ViewOptionsService viewOptions,
        ClientContextService? clientContext = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _viewOptions = viewOptions;
        _clientContext = clientContext;
    }

    public async Task LoadAsync()
    {
        var profile = await GetProfileAsync();
        if (profile is null) return;

        _loading = true;
        ProfileName = profile.Name;
        IsCoach = profile.Kind == ProfileKind.Coach;
        IsDetailed = profile.ViewMode == ProfileViewMode.Detailed;
        ShowIntensity = profile.SimpleViewBlocks.HasFlag(SimpleViewBlocks.Intensity);
        ShowTempoAndRest = profile.SimpleViewBlocks.HasFlag(SimpleViewBlocks.TempoAndRest);
        ShowAdvancedStructure = profile.SimpleViewBlocks.HasFlag(SimpleViewBlocks.AdvancedStructure);
        ShowRepRanges = profile.SimpleViewBlocks.HasFlag(SimpleViewBlocks.RepRanges);
        ShowDetailedNotes = profile.SimpleViewBlocks.HasFlag(SimpleViewBlocks.DetailedNotes);
        var gym = GymSetup.From(profile.GymPlates, profile.BarWeight);
        foreach (var plate in Plates)
            plate.IsAvailable = gym.Plates.Contains(plate.Weight);
        BarWeight = gym.BarWeight;
        _loading = false;

        _viewOptions.Apply(profile);
    }

    [RelayCommand]
    private void SetSimple() => IsDetailed = false;

    [RelayCommand]
    private void SetDetailed() => IsDetailed = true;

    [RelayCommand]
    private Task SetPersonal() => ChangeKindAsync(coach: false);

    [RelayCommand]
    private Task SetCoach() => ChangeKindAsync(coach: true);

    // Cambiar el tipo no borra nada, pero cambia qué datos se ven: se pide confirmación
    public async Task ChangeKindAsync(bool coach)
    {
        if (coach == IsCoach) return;
        var ok = await ConfirmDialogService.ConfirmAsync(
            coach ? "Pasar a perfil de entrenador" : "Pasar a perfil personal",
            coach
                ? "Aparecerán Clientes y el Inicio de clientes. Tus propios entrenos de este perfil dejarán de verse (no se borran); para entrenar tú, usa un perfil personal."
                : "Dejarán de verse tus clientes con sus rutinas y sesiones (no se borran). Volverán si lo pones otra vez como entrenador.",
            coach ? "Pasar a entrenador" : "Pasar a personal");
        if (!ok) return;

        // Un perfil personal no trabaja con clientes: con uno activo, sus datos seguirían filtrándolo todo
        if (!coach) _clientContext?.Clear();
        IsCoach = coach;
    }

    partial void OnIsDetailedChanged(bool value) => _ = SaveAsync();
    partial void OnIsCoachChanged(bool value) => _ = SaveAsync();
    partial void OnShowIntensityChanged(bool value) => _ = SaveAsync();
    partial void OnShowTempoAndRestChanged(bool value) => _ = SaveAsync();
    partial void OnShowAdvancedStructureChanged(bool value) => _ = SaveAsync();
    partial void OnShowRepRangesChanged(bool value) => _ = SaveAsync();
    partial void OnShowDetailedNotesChanged(bool value) => _ = SaveAsync();

    public SimpleViewBlocks CurrentBlocks =>
        (ShowIntensity ? SimpleViewBlocks.Intensity : 0)
        | (ShowTempoAndRest ? SimpleViewBlocks.TempoAndRest : 0)
        | (ShowAdvancedStructure ? SimpleViewBlocks.AdvancedStructure : 0)
        | (ShowRepRanges ? SimpleViewBlocks.RepRanges : 0)
        | (ShowDetailedNotes ? SimpleViewBlocks.DetailedNotes : 0);

    public async Task SaveAsync()
    {
        if (_loading) return;

        // Varios interruptores seguidos no pueden usar el mismo DbContext a la vez
        await _saveLock.WaitAsync();
        try
        {
            var profile = await GetProfileAsync();
            if (profile is null) return;

            profile.Kind = IsCoach ? ProfileKind.Coach : ProfileKind.Personal;
            profile.ViewMode = IsDetailed ? ProfileViewMode.Detailed : ProfileViewMode.Simple;
            profile.SimpleViewBlocks = CurrentBlocks;
            profile.GymPlates = GymSetup.Format(Plates.Where(p => p.IsAvailable).Select(p => p.Weight));
            profile.BarWeight = BarWeight;
            await _dbContext.SaveChangesAsync();

            _viewOptions.Apply(profile);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private async Task<UserProfile?> GetProfileAsync() =>
        _currentUser.CurrentUserId is int id ? await _dbContext.UserProfiles.FindAsync(id) : null;
}
