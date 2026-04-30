using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using SteelProgress.App.Commands;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;
using SteelProgress.App.Services;

namespace SteelProgress.App.ViewModels;

public class WorkoutViewModel : BaseViewModel
{
    private readonly WorkoutRepository _workoutRepository;
    private readonly RoutineRepository _routineRepository;

    public ICommand FinishWorkoutCommand { get; }
    public ObservableCollection<Routine> Routines { get; set; } = new();
    public ObservableCollection<RoutineDay> RoutineDays { get; set; } = new();
    public ObservableCollection<WorkoutExercise> Exercises { get; set; } = new();
    public ObservableCollection<WorkoutSet> SelectedExerciseSets { get; set; } = new();

    private Routine? _selectedRoutine;
    public Routine? SelectedRoutine
    {
        get => _selectedRoutine;
        set
        {
            _selectedRoutine = value;
            OnPropertyChanged();
            LoadRoutineDays();
        }
    }

    private RoutineDay? _selectedRoutineDay;
    public RoutineDay? SelectedRoutineDay
    {
        get => _selectedRoutineDay;
        set
        {
            _selectedRoutineDay = value;
            OnPropertyChanged();
        }
    }

    private bool _isSessionActive;
    public bool IsSessionActive
    {
        get => _isSessionActive;
        set
        {
            _isSessionActive = value;
            OnPropertyChanged();
        }
    }

    private WorkoutSession? _currentSession;
    public WorkoutSession? CurrentSession
    {
        get => _currentSession;
        set
        {
            _currentSession = value;
            OnPropertyChanged();
        }
    }

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

    private WorkoutSet? _selectedWorkoutSet;
    public WorkoutSet? SelectedWorkoutSet
    {
        get => _selectedWorkoutSet;
        set
        {
            _selectedWorkoutSet = value;
            OnPropertyChanged();
        }
    }

    private int _reps;
    public int Reps
    {
        get => _reps;
        set
        {
            _reps = value;
            OnPropertyChanged();
        }
    }

    private double _weight;
    public double Weight
    {
        get => _weight;
        set
        {
            _weight = value;
            OnPropertyChanged();
        }
    }

    public ICommand StartWorkoutCommand { get; }
    public ICommand AddSetCommand { get; }
    public ICommand DeleteSetCommand { get; }

    public WorkoutViewModel(WorkoutRepository workoutRepository, RoutineRepository routineRepository)
    {
        _workoutRepository = workoutRepository;
        _routineRepository = routineRepository;

        StartWorkoutCommand = new RelayCommand(StartWorkout);
        AddSetCommand = new RelayCommand(AddSet);
        DeleteSetCommand = new RelayCommand(DeleteSet);
        FinishWorkoutCommand = new RelayCommand(FinishWorkout);

        LoadRoutines();
    }

    private async void FinishWorkout()
    {
        var confirmed = await ConfirmDialogService
     .ConfirmAsync("Finalizar entrenamiento", "¿Quieres finalizar el entrenamiento?");

        if (!confirmed)
            return;

        CurrentSession = null;
        SelectedWorkoutExercise = null;
        SelectedWorkoutSet = null;

        Exercises.Clear();
        SelectedExerciseSets.Clear();

        Reps = 0;
        Weight = 0;

        IsSessionActive = false;
    }

    private void LoadRoutines()
    {
        var routines = _routineRepository.GetAllAsync().Result;

        Routines.Clear();

        foreach (var routine in routines)
            Routines.Add(routine);
    }

    private void LoadRoutineDays()
    {
        RoutineDays.Clear();
        SelectedRoutineDay = null;

        if (SelectedRoutine is null)
            return;

        var days = _routineRepository.GetDaysByRoutineIdAsync(SelectedRoutine.Id).Result;

        foreach (var day in days)
            RoutineDays.Add(day);
    }

    private void StartWorkout()
    {
        if (SelectedRoutineDay is null)
        {
            NotificationService.Error("Selecciona una rutina y un día antes de iniciar el entrenamiento.");
            return;
        }

        var session = _workoutRepository
            .CreateSessionFromRoutineDayAsync(SelectedRoutineDay.Id)
            .Result;

        LoadSession(session.Id);
        IsSessionActive = true;
    }

    public void LoadSession(int sessionId)
    {
        var session = _workoutRepository.GetSessionByIdAsync(sessionId).Result;

        if (session is null)
            return;

        CurrentSession = session;

        Exercises.Clear();

        foreach (var ex in session.Exercises)
            Exercises.Add(ex);

        SelectedExerciseSets.Clear();
        SelectedWorkoutExercise = Exercises.FirstOrDefault();
    }

    private void LoadSelectedExerciseSets()
    {
        SelectedExerciseSets.Clear();

        if (SelectedWorkoutExercise is null)
            return;

        foreach (var set in SelectedWorkoutExercise.Sets.OrderBy(s => s.Id))
            SelectedExerciseSets.Add(set);
    }

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

        _workoutRepository.AddSetAsync(workoutSet).Wait();

        ReloadCurrentSession();

        Reps = 0;
        Weight = 0;
    }

    private void DeleteSet()
    {
        if (SelectedWorkoutSet is null)
        {
            NotificationService.Error("Selecciona una serie.");
            return;
        }

        _workoutRepository.DeleteSetAsync(SelectedWorkoutSet).Wait();

        ReloadCurrentSession();
    }

    private void ReloadCurrentSession()
    {
        if (CurrentSession is null)
            return;

        int currentSessionId = CurrentSession.Id;
        int? selectedExerciseId = SelectedWorkoutExercise?.Id;

        LoadSession(currentSessionId);

        if (selectedExerciseId.HasValue)
            SelectedWorkoutExercise = Exercises.FirstOrDefault(e => e.Id == selectedExerciseId.Value);
    }
}