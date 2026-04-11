using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.App.ViewModels;

internal class WorkoutViewModel : BaseViewModel
{
    private readonly WorkoutRepository _repository;

    public ObservableCollection<WorkoutExercise> Exercises { get; set; }

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

    public WorkoutViewModel(WorkoutRepository repository)
    {
        _repository = repository;
        Exercises = new ObservableCollection<WorkoutExercise>();
    }

    public void LoadSession(int sessionId)
    {
        var session = _repository.GetSessionByIdAsync(sessionId).Result;

        if (session is null)
        {
            return;
        }

        CurrentSession = session;

        Exercises.Clear();

        foreach(var ex in session.Exercises)
        {
            Exercises.Add(ex);
        }
    }


}
