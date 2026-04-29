using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
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

    public Axis[] YAxes { get; set; } = Array.Empty<Axis>();
    public ISeries[] Series { get; set; } = Array.Empty<ISeries>();
    public Axis[] XAxes { get; set; } = Array.Empty<Axis>();

    public ProgressViewModel(int? selectedExerciseId = null)
    {
        LoadExercises();

        if (selectedExerciseId.HasValue)
        {
            SelectedExercise = Exercises
                .FirstOrDefault(e => e.Id == selectedExerciseId.Value);
        }
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
                Values = values,
                Stroke = new SolidColorPaint(SKColor.Parse("#00FF88")) { StrokeThickness = 3 },
                GeometryStroke = new SolidColorPaint(SKColor.Parse("#00FF88")) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColor.Parse("#121212")),
                GeometrySize = 9,
                Fill = null
            }
        };

        XAxes = new Axis[]
        {
            new Axis
            {
                Labeler = value => new DateTime((long)value).ToString("dd/MM"),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#B3B3B3")),
                SeparatorsPaint = null
            }
        };

        YAxes = new Axis[]
        {
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#B3B3B3")),
                SeparatorsPaint = null
            }
        };

        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(XAxes));
        OnPropertyChanged(nameof(YAxes));

        OnPropertyChanged(nameof(Series));
        OnPropertyChanged(nameof(XAxes));
    }
}