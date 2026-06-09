using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Spike;

public partial class MainPage : ContentPage
{
    private readonly AppDbContext _db;

    public MainPage(AppDbContext db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await TestDatabaseAsync();
        TestLiveCharts();
    }

    private async Task TestDatabaseAsync()
    {
        try
        {
            var count = await Task.Run(() => _db.Exercises.Count());
            DbStatusLabel.Text = "CORRECTO — Conexión a SQLite establecida";
            DbStatusLabel.TextColor = Color.FromArgb("#00FF88");
            ExerciseCountLabel.Text = $"Ejercicios en BD: {count}";
        }
        catch (Exception ex)
        {
            DbStatusLabel.Text = $"ERROR — {ex.Message}";
            DbStatusLabel.TextColor = Colors.Red;
        }
    }

    private async void OnInsertClicked(object? sender, EventArgs e)
    {
        InsertBtn.IsEnabled = false;
        try
        {
            var exercise = new Exercise
            {
                Name = $"Ejercicio Spike {DateTime.Now:HH:mm:ss}",
                MuscleGroup = "Test"
            };
            _db.Exercises.Add(exercise);
            await Task.Run(() => _db.SaveChanges());
            await TestDatabaseAsync();
        }
        catch (Exception ex)
        {
            DbStatusLabel.Text = $"ERROR al insertar — {ex.Message}";
            DbStatusLabel.TextColor = Colors.Red;
        }
        finally
        {
            InsertBtn.IsEnabled = true;
        }
    }

    private void TestLiveCharts()
    {
        try
        {
            // Datos de peso simulados — misma forma que usará ProgressView
            TestChart.Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = new double[] { 60, 62.5, 62.5, 65, 67.5, 70, 72.5 },
                    Name = "Peso levantado (kg)",
                    Stroke = new SolidColorPaint(SKColor.Parse("#00FF88")) { StrokeThickness = 3 },
                    Fill = null,
                    GeometrySize = 8,
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#00FF88")) { StrokeThickness = 2 },
                }
            };

            ChartStatusLabel.Text = "CORRECTO — LiveCharts renderizando";
            ChartStatusLabel.TextColor = Color.FromArgb("#00FF88");
        }
        catch (Exception ex)
        {
            ChartStatusLabel.Text = $"ERROR — {ex.Message}";
            ChartStatusLabel.TextColor = Colors.Red;
        }
    }
}
