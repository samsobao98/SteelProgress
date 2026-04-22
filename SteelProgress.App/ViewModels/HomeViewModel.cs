using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Domain.Entities;

namespace SteelProgress.App.ViewModels;

public class HomeViewModel : BaseViewModel
{
    public int ExerciseCount { get; set; }
    public int RoutineCount { get; set; }
    public int SessionCount { get; set; }
    public string LastWorkoutDay { get; set; } = "Sin datos";

    public ObservableCollection<WorkoutSession> RecentSessions { get; set; } = new();

    public HomeViewModel()
    {
        LoadData();
    }

    private void LoadData()
    {
        ExerciseCount = App.DbContext.Exercises.Count();
        RoutineCount = App.DbContext.Routines.Count();
        SessionCount = App.DbContext.WorkoutSessions.Count();

        var lastSession = App.DbContext.WorkoutSessions
            .Include(ws => ws.RoutineDay)
            .OrderByDescending(ws => ws.Date)
            .FirstOrDefault();

        if (lastSession is not null)
            LastWorkoutDay = lastSession.RoutineDay?.Name ?? "Sin datos";

        var recent = App.DbContext.WorkoutSessions
            .Include(ws => ws.RoutineDay)
            .OrderByDescending(ws => ws.Date)
            .Take(5)
            .ToList();

        RecentSessions.Clear();

        foreach (var session in recent)
            RecentSessions.Add(session);

        OnPropertyChanged(nameof(ExerciseCount));
        OnPropertyChanged(nameof(RoutineCount));
        OnPropertyChanged(nameof(SessionCount));
        OnPropertyChanged(nameof(LastWorkoutDay));
        OnPropertyChanged(nameof(RecentSessions));
    }
}