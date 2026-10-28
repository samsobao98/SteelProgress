using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

public partial class ExerciseViewModel : ObservableObject
{
    private readonly ExerciseRepository _repository;
    private List<Exercise> _allExercises = new();
    private readonly ExerciseMediaService? _media;

    // Vídeos e imágenes del ejercicio que se está creando o editando.
    // Al crear, se copian a la app al elegirlos y se asocian al guardar; si se cancela, se borran.
    public ObservableCollection<ExerciseMedia> Media { get; } = new();

    public bool MediaEnabled => _media is not null;

    [ObservableProperty] private bool _isImportingMedia;

    public ObservableCollection<Exercise> Exercises { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;

    /// <summary>Grupo muscular activo en el filtro; null = todos.</summary>
    [ObservableProperty] private string? _selectedGroupFilter;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedGroupFilterChanged(string? value) => ApplyFilter();

    public IReadOnlyList<string> MuscleGroups => ExerciseCatalog.MuscleGroups;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _muscleGroup;
    [ObservableProperty] private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAddMode), nameof(OverlayTitle), nameof(SaveButtonText))]
    private bool _isEditMode;

    public bool IsAddMode => !IsEditMode;
    public string OverlayTitle => IsEditMode ? "Editar ejercicio" : "Nuevo ejercicio";
    public string SaveButtonText => IsEditMode ? "Guardar cambios" : "Añadir ejercicio";

    private Exercise? _selectedExercise;
    public Exercise? SelectedExercise
    {
        get => _selectedExercise;
        set
        {
            _selectedExercise = value;
            OnPropertyChanged();
            IsEditMode = value is not null;
            LoadSelectedExerciseIntoForm();
        }
    }

    // media es opcional para que la app de escritorio, que crea el ViewModel a mano, siga compilando
    public ExerciseViewModel(ExerciseRepository repository, ExerciseMediaService? media = null)
    {
        _repository = repository;
        _media = media;
        _ = LoadExercisesAsync();
    }

    public Task RefreshAsync() => LoadExercisesAsync();

    private async Task LoadExercisesAsync()
    {
        _allExercises = await _repository.GetAllAsync();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var result = ExerciseCatalog.Filter(_allExercises, SelectedGroupFilter, SearchText);

        Exercises.Clear();
        foreach (var exercise in result)
            Exercises.Add(exercise);
    }

    public void ClearForm()
    {
        DiscardPendingMedia();
        Name = string.Empty;
        Notes = string.Empty;
        MuscleGroup = null;
        SelectedExercise = null;
    }

    private void LoadSelectedExerciseIntoForm()
    {
        DiscardPendingMedia();

        if (SelectedExercise is null)
        {
            Name = string.Empty;
            MuscleGroup = null;
            Notes = string.Empty;
            return;
        }

        foreach (var m in SelectedExercise.Media.OrderBy(m => m.Order))
            Media.Add(m);

        Name = SelectedExercise.Name;
        MuscleGroup = SelectedExercise.MuscleGroup;
        Notes = SelectedExercise.Notes ?? string.Empty;
    }

    [RelayCommand]
    private async Task AddExercise()
    {
        string name = Name.Trim();
        string muscleGroup = MuscleGroup?.Trim() ?? string.Empty;
        string notes = Notes.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            NotificationService.Error("El nombre del ejercicio es obligatorio.");
            return;
        }

        if (string.IsNullOrWhiteSpace(muscleGroup))
        {
            NotificationService.Error("El grupo muscular es obligatorio.");
            return;
        }

        if (await _repository.ExistsByNameAsync(name))
        {
            NotificationService.Error("Ya existe un ejercicio con ese nombre.");
            return;
        }

        var exercise = new Exercise
        {
            Name = name,
            MuscleGroup = muscleGroup,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
        };
        for (var i = 0; i < Media.Count; i++)
        {
            Media[i].Order = i;
            exercise.Media.Add(Media[i]);
        }

        await _repository.AddAsync(exercise);
        Media.Clear(); // ya guardados: que ClearForm no los borre
        await LoadExercisesAsync();
        ClearForm();
        NotificationService.Success("Ejercicio añadido correctamente.");
    }

    [RelayCommand]
    private async Task UpdateExercise()
    {
        if (SelectedExercise is null)
        {
            NotificationService.Info("Selecciona un ejercicio para actualizar.");
            return;
        }

        if (SelectedExercise.IsSystem)
        {
            NotificationService.Error("Los ejercicios de la biblioteca no se pueden editar.");
            return;
        }

        string name = Name.Trim();
        string muscleGroup = MuscleGroup?.Trim() ?? string.Empty;
        string notes = Notes.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            NotificationService.Error("El nombre del ejercicio es obligatorio.");
            return;
        }

        if (string.IsNullOrWhiteSpace(muscleGroup))
        {
            NotificationService.Error("El grupo muscular es obligatorio.");
            return;
        }

        if (await _repository.ExistsByNameExceptIdAsync(name, SelectedExercise.Id))
        {
            NotificationService.Error("Ya existe otro ejercicio con ese nombre.");
            return;
        }

        SelectedExercise.Name = name;
        SelectedExercise.MuscleGroup = muscleGroup;
        SelectedExercise.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;

        await _repository.UpdateAsync(SelectedExercise);
        await LoadExercisesAsync();
        ClearForm();
        NotificationService.Success("Ejercicio actualizado correctamente.");
    }

    [RelayCommand]
    private async Task DeleteExercise()
    {
        if (SelectedExercise is null)
        {
            NotificationService.Info("Selecciona un ejercicio para eliminar.");
            return;
        }

        if (SelectedExercise.IsSystem)
        {
            NotificationService.Error("Los ejercicios de la biblioteca no se pueden eliminar.");
            return;
        }

        var confirmed = await ConfirmDialogService
            .ConfirmAsync("Eliminar ejercicio", $"¿Eliminar «{SelectedExercise.Name}»?", accept: "Eliminar", destructive: true);

        if (!confirmed)
            return;

        var files = _media is null ? new List<string>() : await _repository.GetMediaFileNamesAsync(SelectedExercise.Id);
        await _repository.DeleteAsync(SelectedExercise);
        _media?.DeleteFiles(files);
        Media.Clear();
        await LoadExercisesAsync();
        ClearForm();
    }

    // ── Vídeos e imágenes ─────────────────────────────────────────────────

    // La página abre la galería y pasa aquí el archivo elegido
    public async Task AddMediaAsync(Stream source, string originalFileName)
    {
        if (_media is null) return;

        IsImportingMedia = true;
        try
        {
            var media = await _media.StoreAsync(source, originalFileName);
            if (SelectedExercise is not null)
            {
                await _repository.AddMediaAsync(SelectedExercise.Id, media);
                SelectedExercise.Media.Add(media);
            }
            Media.Add(media);
        }
        catch (MediaImportException ex)
        {
            NotificationService.Error(ex.Message);
        }
        finally
        {
            IsImportingMedia = false;
        }
    }

    [RelayCommand]
    private async Task RemoveMedia(ExerciseMedia? media)
    {
        if (media is null || _media is null) return;

        if (SelectedExercise is not null)
        {
            await _repository.RemoveMediaAsync(media);
            SelectedExercise.Media.Remove(media);
        }
        _media.DeleteFile(media);
        Media.Remove(media);
    }

    public string? GetMediaPath(ExerciseMedia media) => _media?.Storage.GetFullPath(media.FileName);

    // Archivos elegidos para un ejercicio nuevo que no llegó a guardarse
    private void DiscardPendingMedia()
    {
        if (_media is null) return;
        foreach (var m in Media.Where(m => m.Id == 0).ToList())
            _media.DeleteFile(m);
        Media.Clear();
    }
}
