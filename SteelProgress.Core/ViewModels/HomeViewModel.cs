using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly AppDbContext _dbContext;

    [ObservableProperty] private int _exerciseCount;
    [ObservableProperty] private int _routineCount;
    [ObservableProperty] private int _sessionCount;
    [ObservableProperty] private string _lastWorkoutDay = "Sin datos";

    public ObservableCollection<WorkoutSession> RecentSessions { get; } = new();

    public HomeViewModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _ = LoadDataAsync();
    }

    public Task RefreshAsync() => LoadDataAsync();

    private async Task LoadDataAsync()
    {
        ExerciseCount = await _dbContext.Exercises.CountAsync();
        RoutineCount = await _dbContext.Routines.CountAsync();
        SessionCount = await _dbContext.WorkoutSessions.CountAsync();

        var lastSession = await _dbContext.WorkoutSessions
            .Include(ws => ws.RoutineDay)
            .OrderByDescending(ws => ws.Date)
            .FirstOrDefaultAsync();

        if (lastSession is not null)
            LastWorkoutDay = lastSession.RoutineDay?.Name ?? "Sin datos";

        var recent = await _dbContext.WorkoutSessions
            .Include(ws => ws.RoutineDay)
            .OrderByDescending(ws => ws.Date)
            .Take(5)
            .ToListAsync();

        RecentSessions.Clear();
        foreach (var session in recent)
            RecentSessions.Add(session);
    }
}
