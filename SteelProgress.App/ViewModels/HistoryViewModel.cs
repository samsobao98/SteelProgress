using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Domain.Entities;

namespace SteelProgress.App.ViewModels;

public class HistoryViewModel : BaseViewModel
{
    public ObservableCollection<WorkoutSession> Sessions { get; set; } = new();
    public ObservableCollection<HistoryExerciseSummary> ExerciseSummaries { get; set; } = new();

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

    private HistoryExerciseSummary? _selectedExerciseSummary;
    public HistoryExerciseSummary? SelectedExerciseSummary
    {
        get => _selectedExerciseSummary;
        set
        {
            _selectedExerciseSummary = value;
            OnPropertyChanged();
        }
    }

    public HistoryViewModel()
    {
        LoadSessions();
        SelectedSession = Sessions.FirstOrDefault();
    }

    private void LoadSessions()
    {
        var sessions = App.DbContext.WorkoutSessions
            .Include(ws => ws.RoutineDay)
            .OrderByDescending(ws => ws.Date)
            .ToList();

        Sessions.Clear();

        foreach (var session in sessions)
            Sessions.Add(session);
    }

    private void LoadSelectedSessionDetails()
    {
        ExerciseSummaries.Clear();
        SelectedExerciseSummary = null;

        if (SelectedSession is null)
            return;

        var currentSession = App.DbContext.WorkoutSessions
            .Include(ws => ws.RoutineDay)
            .Include(ws => ws.Exercises)
                .ThenInclude(we => we.Exercise)
            .Include(ws => ws.Exercises)
                .ThenInclude(we => we.Sets)
            .FirstOrDefault(ws => ws.Id == SelectedSession.Id);

        if (currentSession is null)
            return;

        var previousSession = App.DbContext.WorkoutSessions
            .Include(ws => ws.Exercises)
                .ThenInclude(we => we.Exercise)
            .Include(ws => ws.Exercises)
                .ThenInclude(we => we.Sets)
            .Where(ws => ws.RoutineDayId == currentSession.RoutineDayId
                         && ws.Date < currentSession.Date)
            .OrderByDescending(ws => ws.Date)
            .FirstOrDefault();

        foreach (var exercise in currentSession.Exercises)
        {
            var previousExercise = previousSession?.Exercises
                .FirstOrDefault(e => e.ExerciseId == exercise.ExerciseId);

            var summary = new HistoryExerciseSummary
            {
                ExerciseId = exercise.ExerciseId,
                ExerciseName = exercise.Exercise?.Name ?? "Ejercicio",
                Sets = new ObservableCollection<WorkoutSet>(
                    exercise.Sets.OrderBy(s => s.Id)),
                BestSetText = GetBestSetText(exercise),
                ComparisonText = GetComparisonText(exercise, previousExercise)
            };

            ExerciseSummaries.Add(summary);
        }
    }

    private static string GetBestSetText(WorkoutExercise exercise)
    {
        var bestSet = exercise.Sets
            .OrderByDescending(s => s.Weight)
            .ThenByDescending(s => s.Reps)
            .FirstOrDefault();

        if (bestSet is null)
            return "Sin series";

        return $"{bestSet.Weight} kg x {bestSet.Reps}";
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