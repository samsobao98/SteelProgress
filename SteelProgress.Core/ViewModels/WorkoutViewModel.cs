using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

public partial class WorkoutViewModel : ObservableObject
{
    private readonly WorkoutRepository _workoutRepository;
    private readonly RoutineRepository _routineRepository;

    public ObservableCollection<Routine> Routines { get; } = new();
    public ObservableCollection<RoutineDay> RoutineDays { get; } = new();
    public ObservableCollection<WorkoutExercise> Exercises { get; } = new();
    public ObservableCollection<WorkoutSet> SelectedExerciseSets { get; } = new();
    public ObservableCollection<WorkoutSet> CurrentSets { get; } = new();

    [ObservableProperty] private bool _isSessionActive;
    [ObservableProperty] private WorkoutSession? _currentSession;
    [ObservableProperty] private WorkoutSet? _selectedWorkoutSet;
    [ObservableProperty] private int _reps;
    [ObservableProperty] private double _weight;

    private Routine? _selectedRoutine;
    public Routine? SelectedRoutine
    {
        get => _selectedRoutine;
        set
        {
            _selectedRoutine = value;
            OnPropertyChanged();
            _ = LoadRoutineDaysAsync();
        }
    }

    [ObservableProperty] private RoutineDay? _selectedRoutineDay;

    private WorkoutExercise? _selectedWorkoutExercise;
    public WorkoutExercise? SelectedWorkoutExercise
    {
        get => _selectedWorkoutExercise;
        set
        {
            _selectedWorkoutExercise = value;
            OnPropertyChanged();
            LoadSelectedExerciseSets();
        }
    }

    public WorkoutViewModel(WorkoutRepository workoutRepository, RoutineRepository routineRepository)
    {
        _workoutRepository = workoutRepository;
        _routineRepository = routineRepository;
        _ = LoadRoutinesAsync();
    }

    private async Task LoadRoutinesAsync()
    {
        var routines = await _routineRepository.GetAllAsync();
        Routines.Clear();
        foreach (var routine in routines)
            Routines.Add(routine);
    }

    private async Task LoadRoutineDaysAsync()
    {
        RoutineDays.Clear();
        SelectedRoutineDay = null;

        if (SelectedRoutine is null)
            return;

        var days = await _routineRepository.GetDaysByRoutineIdAsync(SelectedRoutine.Id);
        foreach (var day in days)
            RoutineDays.Add(day);
    }

    private void LoadSelectedExerciseSets()
    {
        SelectedExerciseSets.Clear();

        if (SelectedWorkoutExercise is null)
            return;

        var sets = CurrentSets
            .Where(s => s.WorkoutExerciseId == SelectedWorkoutExercise.Id)
            .ToList();

        foreach (var set in sets)
            SelectedExerciseSets.Add(set);
    }

    [RelayCommand]
    private async Task StartWorkout()
    {
        if (SelectedRoutineDay is null)
        {
            NotificationService.Error("Selecciona una rutina y un día antes de iniciar el entrenamiento.");
            return;
        }

        CurrentSets.Clear();
        SelectedExerciseSets.Clear();

        var session = await _workoutRepository.CreateSessionFromRoutineDayAsync(SelectedRoutineDay.Id);

        await LoadSessionAsync(session.Id);

        IsSessionActive = true;
        WorkoutStateService.IsWorkoutActive = true;
    }

    public async Task LoadSessionAsync(int sessionId)
    {
        var session = await _workoutRepository.GetSessionByIdAsync(sessionId);

        if (session is null)
            return;

        CurrentSession = session;
        Exercises.Clear();

        foreach (var ex in session.Exercises)
            Exercises.Add(ex);

        SelectedExerciseSets.Clear();
        SelectedWorkoutExercise = Exercises.FirstOrDefault();
    }

    [RelayCommand]
    private void AddSet()
    {
        if (SelectedWorkoutExercise is null)
        {
            NotificationService.Error("Selecciona un ejercicio.");
            return;
        }

        if (Reps <= 0)
        {
            NotificationService.Error("Las repeticiones deben ser mayores que 0.");
            return;
        }

        if (Weight < 0)
        {
            NotificationService.Error("El peso no puede ser negativo.");
            return;
        }

        var workoutSet = new WorkoutSet
        {
            WorkoutExerciseId = SelectedWorkoutExercise.Id,
            Reps = Reps,
            Weight = Weight
        };

        CurrentSets.Add(workoutSet);
        SelectedExerciseSets.Add(workoutSet);

        Reps = 0;
        Weight = 0;

        NotificationService.Success("Serie añadida.");
    }

    [RelayCommand]
    private void DeleteSet()
    {
        if (SelectedWorkoutSet is null)
        {
            NotificationService.Error("Selecciona una serie.");
            return;
        }

        CurrentSets.Remove(SelectedWorkoutSet);
        SelectedExerciseSets.Remove(SelectedWorkoutSet);
        SelectedWorkoutSet = null;

        NotificationService.Info("Serie eliminada.");
    }

    [RelayCommand]
    private async Task CancelWorkout()
    {
        var confirmed = await ConfirmDialogService
            .ConfirmAsync("Cancelar entrenamiento", "¿Seguro que quieres cancelar? No se guardarán las series registradas.");

        if (!confirmed)
            return;

        if (CurrentSession is not null)
            await _workoutRepository.DeleteSessionAsync(CurrentSession);

        ResetSession();
        NotificationService.Info("Entrenamiento cancelado.");
    }

    [RelayCommand]
    private async Task FinishWorkout()
    {
        if (!CurrentSets.Any())
        {
            NotificationService.Error("No hay series registradas.");
            return;
        }

        var confirmed = await ConfirmDialogService
            .ConfirmAsync("Finalizar entrenamiento", "¿Quieres finalizar y guardar el entrenamiento?");

        if (!confirmed)
            return;

        foreach (var set in CurrentSets)
            await _workoutRepository.AddSetAsync(set);

        ResetSession();
        NotificationService.Success("Entrenamiento guardado correctamente.");
    }

    private void ResetSession()
    {
        CurrentSets.Clear();
        CurrentSession = null;
        SelectedWorkoutExercise = null;
        SelectedWorkoutSet = null;
        Exercises.Clear();
        SelectedExerciseSets.Clear();
        Reps = 0;
        Weight = 0;
        IsSessionActive = false;
        WorkoutStateService.IsWorkoutActive = false;
    }
}
