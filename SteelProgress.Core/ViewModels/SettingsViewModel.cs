using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.ViewModels;

// Ajustes del perfil actual: vista simple o de entrenador y bloques extra de la vista simple.
// Cada cambio se guarda al momento, sin botón de guardar.
public partial class SettingsViewModel : ObservableObject
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ViewOptionsService _viewOptions;
    private bool _loading;

    [ObservableProperty] private string _profileName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimple))]
    [NotifyPropertyChangedFor(nameof(ModeDescription))]
    private bool _isTrainer;

    [ObservableProperty] private bool _showIntensity;
    [ObservableProperty] private bool _showTempoAndRest;
    [ObservableProperty] private bool _showAdvancedStructure;
    [ObservableProperty] private bool _showRepRanges;
    [ObservableProperty] private bool _showDetailedNotes;
    [ObservableProperty] private bool _trainsSelf;

    public bool IsSimple => !IsTrainer;

    public string ModeDescription => IsTrainer
        ? "Todas las opciones de edición, notas en cada serie, material e instrucciones de los ejercicios, ficha de clientes y envío de rutinas."
        : "Solo lo necesario para entrenar y editar rutinas de forma sencilla. Los datos que tenga una rutina se ven siempre.";

    public SettingsViewModel(AppDbContext dbContext, ICurrentUserService currentUser, ViewOptionsService viewOptions)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _viewOptions = viewOptions;
    }

    public async Task LoadAsync()
    {
        var profile = await GetProfileAsync();
        if (profile is null) return;

        _loading = true;
        ProfileName = profile.Name;
        IsTrainer = profile.ViewMode == ProfileViewMode.Trainer;
        ShowIntensity = profile.SimpleViewBlocks.HasFlag(SimpleViewBlocks.Intensity);
        ShowTempoAndRest = profile.SimpleViewBlocks.HasFlag(SimpleViewBlocks.TempoAndRest);
        ShowAdvancedStructure = profile.SimpleViewBlocks.HasFlag(SimpleViewBlocks.AdvancedStructure);
        ShowRepRanges = profile.SimpleViewBlocks.HasFlag(SimpleViewBlocks.RepRanges);
        ShowDetailedNotes = profile.SimpleViewBlocks.HasFlag(SimpleViewBlocks.DetailedNotes);
        TrainsSelf = profile.TrainsSelf;
        _loading = false;

        _viewOptions.Apply(profile);
    }

    [RelayCommand]
    private void SetSimple() => IsTrainer = false;

    [RelayCommand]
    private void SetTrainer() => IsTrainer = true;

    partial void OnIsTrainerChanged(bool value) => _ = SaveAsync();
    partial void OnShowIntensityChanged(bool value) => _ = SaveAsync();
    partial void OnShowTempoAndRestChanged(bool value) => _ = SaveAsync();
    partial void OnShowAdvancedStructureChanged(bool value) => _ = SaveAsync();
    partial void OnShowRepRangesChanged(bool value) => _ = SaveAsync();
    partial void OnShowDetailedNotesChanged(bool value) => _ = SaveAsync();
    partial void OnTrainsSelfChanged(bool value) => _ = SaveAsync();

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

            profile.ViewMode = IsTrainer ? ProfileViewMode.Trainer : ProfileViewMode.Simple;
            profile.SimpleViewBlocks = CurrentBlocks;
            profile.TrainsSelf = TrainsSelf;
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
