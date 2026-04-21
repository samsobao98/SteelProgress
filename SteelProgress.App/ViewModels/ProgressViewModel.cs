using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Domain.Entities;
using System.Collections.ObjectModel;

namespace SteelProgress.App.ViewModels;

public class ProgressViewModel : BaseViewModel
{
    public ObservableCollection<Exercise> Exercises { get; set; } = new();

    private Exercise? _selectedExercise;
    public Exercise? SelectedExercise
    {
        get => _selectedExercise;
        set
        {
            _selectedExercise = value;
            OnPropertyChanged();
            LoadChart();
        }
    }

    public ISeries[] Series { get; set; } = Array.Empty<ISeries>();
    public Axis[] XAxes { get; set; } = Array.Empty<Axis>();

    public ProgressViewModel()
    {
        LoadExercises();
    }

    private void LoadExercises()
    {
        var list = App.DbContext.Exercises.ToList();

        foreach (var ex in list)
            Exercises.Add(ex);
    }

    private void LoadChart()
    {
        if (SelectedExercise is null)
            return;



        var sessions = App.DbContext.WorkoutSessions
            .Include(s => s.Exercises)
                .ThenInclude(e => e.Sets)
            .ToList();

        var data = sessions
            .Select(s => new
            {
                s.Date,
                MaxWeight = s.Exercises
                    .Where(e => e.ExerciseId == SelectedExercise.Id)
                    .SelectMany(e => e.Sets)
                    .Select(set => set.Weight)
                    .DefaultIfEmpty(0)
                    .Max()
            })
            .Where(x => x.MaxWeight > 0)
            .OrderBy(x => x.Date)
            .ToList();

        var values = data
            .Select(x => new ObservablePoint(x.Date.Ticks, x.MaxWeight))
            .ToList();


        if (!values.Any())
        {
            Series = new ISeries[0];
            OnPropertyChanged(nameof(Series));
            return;
        }

        Series = new ISeries[]
        {
        new LineSeries<ObservablePoint>
        {
            Values = values
        }
        };

        XAxes = new Axis[]
        {
        new Axis
        {
            Labeler = value => new DateTime((long)value).ToString("dd/MM")
        }
        };

        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(XAxes));
    }
}