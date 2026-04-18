using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using SteelProgress.App.Commands;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.App.ViewModels;

public class WorkoutViewModel : BaseViewModel
{
    private readonly WorkoutRepository _repository;

    public ObservableCollection<WorkoutExercise> Exercises { get; set; }
    public ObservableCollection<WorkoutSet> SelectedExerciseSets { get; set; }

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

    public ICommand AddSetCommand { get; }
    public ICommand DeleteSetCommand { get; }

    public WorkoutViewModel(WorkoutRepository repository)
    {
        _repository = repository;
        Exercises = new ObservableCollection<WorkoutExercise>();
        SelectedExerciseSets = new ObservableCollection<WorkoutSet>();

        AddSetCommand = new RelayCommand(AddSet);
        DeleteSetCommand = new RelayCommand(DeleteSet);
    }

    public void LoadSession(int sessionId)
    {
        var session = _repository.GetSessionByIdAsync(sessionId).Result;

        if (session is null)
            return;

        CurrentSession = session;

        Exercises.Clear();

        foreach (var ex in session.Exercises)
            Exercises.Add(ex);

        SelectedExerciseSets.Clear();
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
            MessageBox.Show("Selecciona un ejercicio.");
            return;
        }

        if (Reps <= 0)
        {
            MessageBox.Show("Las repeticiones deben ser mayores que 0.");
            return;
        }

        if (Weight < 0)
        {
            MessageBox.Show("El peso no puede ser negativo.");
            return;
        }

        var workoutSet = new WorkoutSet
        {
            WorkoutExerciseId = SelectedWorkoutExercise.Id,
            Reps = Reps,
            Weight = Weight
        };

        _repository.AddSetAsync(workoutSet).Wait();

        ReloadCurrentSession();
        Reps = 0;
        Weight = 0;
    }

    private void DeleteSet()
    {
        if (SelectedWorkoutSet is null)
        {
            MessageBox.Show("Selecciona una serie.");
            return;
        }

        _repository.DeleteSetAsync(SelectedWorkoutSet).Wait();

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
        {
            SelectedWorkoutExercise = Exercises.FirstOrDefault(e => e.Id == selectedExerciseId.Value);
        }
    }
}