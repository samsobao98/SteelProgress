using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.ViewModels;

// Perfil actual: quién es (avatar, nombre y frase, editables) y cómo usa la app: tipo de perfil
// (personal o entrenador), vista simple o detallada, bloques extra de la vista simple y su gimnasio.
// Cada cambio de la configuración se guarda al momento, sin botón de guardar. Al final, cambiar de perfil.
public partial class ProfileViewModel : ObservableObject
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ViewOptionsService _viewOptions;
    private readonly ClientContextService? _clientContext;
    private readonly ProfilePhotoStore? _photos;
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProfileInitial))]
    private string _profileName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfileDescription))]
    private string? _profileDescription;

    [ObservableProperty] private string _profileColor = ProfileColors.All[0];

    // Foto del avatar (ruta completa) o null: entonces se ve la inicial sobre el color
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfilePhoto))]
    private string? _profilePhoto;

    public bool HasProfilePhoto => ProfilePhoto is not null;
    private string? _photoFile;

    public string ProfileInitial => string.IsNullOrWhiteSpace(ProfileName) ? "?" : ProfileName.TrimStart()[0].ToString().ToUpperInvariant();
    public bool HasProfileDescription => !string.IsNullOrWhiteSpace(ProfileDescription);
    public string KindLabel => IsCoach ? "Perfil de entrenador" : "Perfil personal";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimple))]
    [NotifyPropertyChangedFor(nameof(ModeDescription))]
    private bool _isDetailed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KindDescription))]
    [NotifyPropertyChangedFor(nameof(KindLabel))]
    private bool _isCoach;

    // ── Editar avatar, nombre y frase ──

    public IReadOnlyList<string> AvailableColors => ProfileColors.All;

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditInitial))]
    [NotifyCanExecuteChangedFor(nameof(SaveEditCommand))]
    private string _editName = "";
    [ObservableProperty] private string _editDescription = "";
    [ObservableProperty] private string _editColor = ProfileColors.All[0];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditPhoto))]
    private string? _editPhoto;

    public bool HasEditPhoto => EditPhoto is not null;
    public bool CanChoosePhoto => _photos is not null;

    // Foto elegida mientras se edita: solo se queda al guardar. Quitarla también espera a guardar.
    private string? _pendingPhoto;
    private bool _removePhoto;

    /// <summary>Foto nueva elegida de la galería o hecha con la cámara (ya reducida por quien la da).</summary>
    public async Task SetEditPhotoAsync(Stream photo)
    {
        if (_photos is null) return;
        var pending = await _photos.SavePendingAsync(photo);
        _photos.Delete(_pendingPhoto);
        _pendingPhoto = pending;
        _removePhoto = false;
        EditPhoto = _photos.PathOf(pending);
    }

    [RelayCommand]
    private void RemoveEditPhoto()
    {
        _photos?.Delete(_pendingPhoto);
        _pendingPhoto = null;
        _removePhoto = true;
        EditPhoto = null;
    }

    private void DiscardPendingPhoto()
    {
        _photos?.Delete(_pendingPhoto);
        _pendingPhoto = null;
        _removePhoto = false;
    }

    public string EditInitial => string.IsNullOrWhiteSpace(EditName) ? "?" : EditName.TrimStart()[0].ToString().ToUpperInvariant();

    [RelayCommand]
    private void StartEdit()
    {
        EditName = ProfileName;
        EditDescription = ProfileDescription ?? "";
        EditColor = ProfileColor;
        DiscardPendingPhoto();
        EditPhoto = ProfilePhoto;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        DiscardPendingPhoto();
        IsEditing = false;
    }

    [RelayCommand]
    private void SetEditColor(string color) => EditColor = color;

    private bool CanSaveEdit() => !string.IsNullOrWhiteSpace(EditName);

    [RelayCommand(CanExecute = nameof(CanSaveEdit))]
    private async Task SaveEdit()
    {
        var name = EditName.Trim();
        if (name.Length == 0) return;
        var description = EditDescription.Trim();

        await _saveLock.WaitAsync();
        try
        {
            var profile = await GetProfileAsync();
            if (profile is null) return;
            profile.Name = name;
            profile.Description = description.Length > 0 ? description : null;
            profile.AvatarColor = EditColor;

            // La foto anterior se borra después de guardar: si algo falla, el perfil sigue con la suya
            var oldPhoto = profile.PhotoFile;
            if (_photos is not null && _pendingPhoto is not null)
            {
                profile.PhotoFile = _photos.Commit(profile.Id, _pendingPhoto);
                _pendingPhoto = null;
            }
            else if (_removePhoto)
            {
                profile.PhotoFile = null;
            }
            await _dbContext.SaveChangesAsync();
            if (oldPhoto != profile.PhotoFile) _photos?.Delete(oldPhoto);
            _removePhoto = false;
            _photoFile = profile.PhotoFile;
        }
        finally
        {
            _saveLock.Release();
        }

        ProfilePhoto = _photos?.PathOf(_photoFile);
        ProfileName = name;
        ProfileDescription = description.Length > 0 ? description : null;
        ProfileColor = EditColor;
        IsEditing = false;
    }

    // ── Cambiar de perfil ──

    /// <summary>Se puede volver a la pantalla de perfiles: la página se encarga de llevar allí.</summary>
    public event Action? SwitchProfileRequested;

    /// <summary>Hay un entreno en marcha y se ha elegido ir a él antes de cambiar de perfil.</summary>
    public event Action? GoToWorkoutRequested;

    // Con un entreno en marcha no se cambia: quedaría a medias en el perfil que se deja
    [RelayCommand]
    private async Task SwitchProfile()
    {
        if (WorkoutStateService.IsWorkoutActive)
        {
            var goToWorkout = await ConfirmDialogService.ConfirmAsync(
                "Tienes un entreno en marcha",
                "Termínalo o descártalo antes de cambiar de perfil.",
                "Ir al entreno", "Seguir aquí");
            if (goToWorkout) GoToWorkoutRequested?.Invoke();
            return;
        }

        _clientContext?.Clear();
        SwitchProfileRequested?.Invoke();
    }

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

    public ProfileViewModel(AppDbContext dbContext, ICurrentUserService currentUser, ViewOptionsService viewOptions,
        ClientContextService? clientContext = null, ProfilePhotoStore? photos = null)
    {
        _photos = photos;
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
        ProfileDescription = profile.Description;
        ProfileColor = profile.AvatarColor;
        _photoFile = profile.PhotoFile;
        ProfilePhoto = _photos?.PathOf(_photoFile);
        if (IsEditing) DiscardPendingPhoto();
        IsEditing = false;
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
