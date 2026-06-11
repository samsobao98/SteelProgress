using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    [ObservableProperty] private int _exerciseCount;
    [ObservableProperty] private int _routineCount;
    [ObservableProperty] private int _sessionCount;
    [ObservableProperty] private string _profileName = "";
    [ObservableProperty] private string _profileColor = "#89B4FA";
    [ObservableProperty] private string _profileInitial = "?";
    [ObservableProperty] private string _lastWorkoutRelative = "Sin datos";
    [ObservableProperty] private string _lastWorkoutName = "";
    [ObservableProperty] private string _greeting = "Hola";
    [ObservableProperty] private int _weekSessionCount;

    public ObservableCollection<RecentSessionItem> RecentSessions { get; } = new();

    public HomeViewModel(AppDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _ = LoadDataAsync();
    }

    public Task RefreshAsync() => LoadDataAsync();

    private async Task LoadDataAsync()
    {
        Greeting = DateTime.Now.Hour switch
        {
            >= 5 and < 12 => "Buenos días",
            >= 12 and < 20 => "Buenas tardes",
            _ => "Buenas noches"
        };

        if (_currentUser.CurrentUserId is int userId)
        {
            var profile = await _dbContext.UserProfiles.FindAsync(userId);
            if (profile is not null)
            {
                ProfileName = profile.Name;
                ProfileColor = profile.AvatarColor;
                ProfileInitial = string.IsNullOrEmpty(profile.Name)
                    ? "?" : profile.Name[0].ToString().ToUpper();
            }
        }

        // Global query filters in AppDbContext handle per-user scoping automatically
        ExerciseCount = await _dbContext.Exercises.CountAsync();
        RoutineCount = await _dbContext.Routines.CountAsync();
        SessionCount = await _dbContext.WorkoutSessions.CountAsync();

        var weekAgo = DateTime.Today.AddDays(-7);
        WeekSessionCount = await _dbContext.WorkoutSessions
            .CountAsync(ws => ws.Date >= weekAgo);

        var lastSession = await _dbContext.WorkoutSessions
            .Include(ws => ws.RoutineDay)
            .OrderByDescending(ws => ws.Date)
            .FirstOrDefaultAsync();

        if (lastSession is not null)
        {
            LastWorkoutName = lastSession.RoutineDay?.Name ?? "";
            LastWorkoutRelative = RelativeDay(lastSession.Date);
        }
        else
        {
            LastWorkoutRelative = "Sin datos";
            LastWorkoutName = "";
        }

        var recent = await _dbContext.WorkoutSessions
            .Include(ws => ws.RoutineDay)
            .Include(ws => ws.Exercises)
            .OrderByDescending(ws => ws.Date)
            .Take(5)
            .ToListAsync();

        RecentSessions.Clear();
        foreach (var s in recent)
        {
            RecentSessions.Add(new RecentSessionItem
            {
                DayName = s.RoutineDay?.Name ?? "Entrenamiento",
                Date = s.Date,
                ExerciseCount = s.Exercises.Count
            });
        }
    }

    private static string RelativeDay(DateTime date) =>
        (DateTime.Today - date.Date).Days switch
        {
            0 => "Hoy",
            1 => "Ayer",
            var d when d < 7 => $"Hace {d} días",
            var d when d < 30 => $"Hace {d / 7} sem.",
            var d => $"Hace {d / 30} mes."
        };
}
