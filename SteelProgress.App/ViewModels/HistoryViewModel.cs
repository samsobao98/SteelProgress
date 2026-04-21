using System.Collections.ObjectModel;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.App.ViewModels;

public class HistoryViewModel : BaseViewModel
{
    private readonly WorkoutRepository _repository;

    public ObservableCollection<WorkoutSession> Sessions { get; set; }
    public ObservableCollection<WorkoutExercise> SessionExercises { get; set; }

    public ObservableCollection<WorkoutSet> SelectedExerciseSets { get; set; }

    private WorkoutSession? _selectedSession;
    public WorkoutSession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            _selectedSession = value;
            OnPropertyChanged();
            LoadSelectedSessionDetails();
        }
    }

    public HistoryViewModel(WorkoutRepository repository)
    {
        _repository = repository;
        Sessions = new ObservableCollection<WorkoutSession>();
        SessionExercises = new ObservableCollection<WorkoutExercise>();
        SelectedExerciseSets = new ObservableCollection<WorkoutSet>();

        LoadSessions();
    }

    private WorkoutExercise? _selectedExercise;
    public WorkoutExercise? SelectedExercise
    {
        get => _selectedExercise;
        set
        {
            _selectedExercise = value;
            OnPropertyChanged();
            LoadSelectedExerciseSets();
        }
    }

    private void LoadSelectedExerciseSets()
    {
        SelectedExerciseSets.Clear();

        if (SelectedExercise is null)
            return;

        foreach (var set in SelectedExercise.Sets.OrderBy(s => s.Id))
            SelectedExerciseSets.Add(set);
    }

    public void LoadSessions()
    {
        var sessions = _repository.GetAllSessionsAsync().Result;

        Sessions.Clear();

        foreach (var session in sessions)
            Sessions.Add(session);
    }

    private void LoadSelectedSessionDetails()
    {
        SessionExercises.Clear();

        if (SelectedSession is null)
            return;

        var fullSession = _repository.GetSessionByIdAsync(SelectedSession.Id).Result;

        if (fullSession is null)
            return;

        foreach (var exercise in fullSession.Exercises)
            SessionExercises.Add(exercise);
    }
}