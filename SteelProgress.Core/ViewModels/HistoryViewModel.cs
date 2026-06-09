using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly AppDbContext _dbContext;

    public ObservableCollection<WorkoutSession> Sessions { get; } = new();
    public ObservableCollection<HistoryExerciseSummary> ExerciseSummaries { get; } = new();

    private WorkoutSession? _selectedSession;
    public WorkoutSession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            _selectedSession = value;
            OnPropertyChanged();
            _ = LoadSelectedSessionDetailsAsync();
        }
    }

    [ObservableProperty] private HistoryExerciseSummary? _selectedExerciseSummary;

    public HistoryViewModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadSessionsAsync();
        _selectedSession = Sessions.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedSession));
        await LoadSelectedSessionDetailsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        var sessions = await _dbContext.WorkoutSessions
            .Include(ws => ws.RoutineDay)
            .OrderByDescending(ws => ws.Date)
            .ToListAsync();

        Sessions.Clear();
        foreach (var session in sessions)
            Sessions.Add(session);
    }

    private async Task LoadSelectedSessionDetailsAsync()
    {
        ExerciseSummaries.Clear();
        SelectedExerciseSummary = null;

        if (SelectedSession is null)
            return;

        var currentSession = await _dbContext.WorkoutSessions
            .Include(ws => ws.RoutineDay)
            .Include(ws => ws.Exercises)
                .ThenInclude(we => we.Exercise)
            .Include(ws => ws.Exercises)
                .ThenInclude(we => we.Sets)
            .FirstOrDefaultAsync(ws => ws.Id == SelectedSession.Id);

        if (currentSession is null)
            return;

        var previousSession = await _dbContext.WorkoutSessions
            .Include(ws => ws.Exercises)
                .ThenInclude(we => we.Exercise)
            .Include(ws => ws.Exercises)
                .ThenInclude(we => we.Sets)
            .Where(ws => ws.RoutineDayId == currentSession.RoutineDayId
                         && ws.Date < currentSession.Date)
            .OrderByDescending(ws => ws.Date)
            .FirstOrDefaultAsync();

        foreach (var exercise in currentSession.Exercises)
        {
            var previousExercise = previousSession?.Exercises
                .FirstOrDefault(e => e.ExerciseId == exercise.ExerciseId);

            ExerciseSummaries.Add(new HistoryExerciseSummary
            {
                ExerciseId = exercise.ExerciseId,
                ExerciseName = exercise.Exercise?.Name ?? "Ejercicio",
                Sets = new ObservableCollection<WorkoutSet>(exercise.Sets.OrderBy(s => s.Id)),
                BestSetText = GetBestSetText(exercise),
                ComparisonText = GetComparisonText(exercise, previousExercise)
            });
        }
    }

    private static string GetBestSetText(WorkoutExercise exercise)
    {
        var bestSet = exercise.Sets
            .OrderByDescending(s => s.Weight)
            .ThenByDescending(s => s.Reps)
            .FirstOrDefault();

        return bestSet is null ? "Sin series" : $"{bestSet.Weight} kg x {bestSet.Reps}";
    }

    private static string GetComparisonText(WorkoutExercise current, WorkoutExercise? previous)
    {
        var currentBest = current.Sets
            .OrderByDescending(s => s.Weight)
            .ThenByDescending(s => s.Reps)
            .FirstOrDefault();

        var previousBest = previous?.Sets
            .OrderByDescending(s => s.Weight)
            .ThenByDescending(s => s.Reps)
            .FirstOrDefault();

        if (currentBest is null)
            return "Sin series registradas";

        if (previousBest is null)
            return "Sin entreno anterior";

        if (currentBest.Weight > previousBest.Weight)
            return $"+{currentBest.Weight - previousBest.Weight} kg respecto al anterior";

        if (currentBest.Weight == previousBest.Weight && currentBest.Reps > previousBest.Reps)
            return $"+{currentBest.Reps - previousBest.Reps} reps con el mismo peso";

        if (currentBest.Weight == previousBest.Weight && currentBest.Reps == previousBest.Reps)
            return "Igual que el anterior";

        return "Por debajo del anterior";
    }
}

public class HistoryExerciseSummary
{
    public int ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public string BestSetText { get; set; } = string.Empty;
    public string ComparisonText { get; set; } = string.Empty;
    public ObservableCollection<WorkoutSet> Sets { get; set; } = new();
}
