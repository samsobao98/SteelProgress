using System.Collections.ObjectModel;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.App.ViewModels;

public class HistoryViewModel : BaseViewModel
{
    private readonly WorkoutRepository _repository;

    public ObservableCollection<WorkoutSession> Sessions { get; set; }
    public ObservableCollection<WorkoutExercise> SessionExercises { get; set; }

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

        LoadSessions();
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