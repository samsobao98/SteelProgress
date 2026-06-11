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

    public ObservableCollection<Exercise> Exercises { get; } = new();

    public IReadOnlyList<string> MuscleGroups { get; } =
    [
        "Pecho",
        "Espalda",
        "Hombros",
        "Bíceps",
        "Tríceps",
        "Antebrazo",
        "Abdominales",
        "Lumbares",
        "Cuádriceps",
        "Isquiotibiales",
        "Glúteos",
        "Gemelos",
        "Cardio",
        "Cuerpo completo",
    ];

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

    public ExerciseViewModel(ExerciseRepository repository)
    {
        _repository = repository;
        _ = LoadExercisesAsync();
    }

    public Task RefreshAsync() => LoadExercisesAsync();

    private async Task LoadExercisesAsync()
    {
        var exercises = await _repository.GetAllAsync();
        Exercises.Clear();
        foreach (var exercise in exercises)
            Exercises.Add(exercise);
    }

    public void ClearForm()
    {
        Name = string.Empty;
        Notes = string.Empty;
        MuscleGroup = null;
        SelectedExercise = null;
    }

    private void LoadSelectedExerciseIntoForm()
    {
        if (SelectedExercise is null)
        {
            Name = string.Empty;
            MuscleGroup = null;
            Notes = string.Empty;
            return;
        }

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

        await _repository.AddAsync(exercise);
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

        var confirmed = await ConfirmDialogService
            .ConfirmAsync("Eliminar Ejercicio", $"¿Seguro que quieres eliminar '{SelectedExercise.Name}'?");

        if (!confirmed)
            return;

        await _repository.DeleteAsync(SelectedExercise);
        await LoadExercisesAsync();
        ClearForm();
    }
}
