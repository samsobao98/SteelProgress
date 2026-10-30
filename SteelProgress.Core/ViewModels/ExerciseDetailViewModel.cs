using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SteelProgress.Core.Services;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.ViewModels;

/// <summary>
/// Ficha de un ejercicio (Rediseño 4): su vídeo o imagen arriba (varios, con cámara lenta), sus
/// chips, tu mejor serie, 1RM estimado, sesiones y evolución, y «Cómo hacerlo». Solo lectura;
/// editar lo hace la pantalla de Ejercicios.
/// </summary>
public partial class ExerciseDetailViewModel : ObservableObject
{
    public Exercise Exercise { get; }
    public ExerciseProgress? Progress { get; }

    public string Name => Exercise.Name;
    /// <summary>Grupo, músculos secundarios, material y etiquetas, en ese orden y sin repetir.</summary>
    public IReadOnlyList<string> Chips { get; }
    public string OriginText { get; }
    public bool HasOrigin => OriginText.Length > 0;
    /// <summary>Solo los propios o importados: los de la biblioteca no se editan.</summary>
    // Desde una rutina (vista simple) la ficha se abre solo para ver: editar es del apartado Ejercicios
    public bool CanEdit => !Exercise.IsSystem && !ReadOnly;
    public bool ReadOnly { get; init; }

    public bool HasProgress => Progress is not null;
    public string BestSetText => Progress is null ? "" : $"{WorkoutExerciseState.FormatKg(Progress.BestWeight)}×{Progress.BestReps}";
    public string E1rmText => Progress is { BestE1rm: > 0 } p ? $"{WorkoutExerciseState.FormatKg(Math.Round(p.BestE1rm))} kg" : "—";
    public string SessionsText => Progress is null ? "" : Progress.Points.Count.ToString();
    public string SessionsCaption => Progress?.Points.Count == 1 ? "Sesión" : "Sesiones";
    /// <summary>La gráfica, con dos sesiones o más.</summary>
    public bool HasChart => Progress?.Points.Count >= 2;
    public string ChartCaption { get; }

    public IReadOnlyList<string> Steps { get; }
    public bool HasSteps => Steps.Count > 0;
    public string Notes => Exercise.Notes?.Trim() ?? "";
    public bool HasNotes => Notes.Length > 0;

    // ── Vídeos e imágenes ───────────────────────────────────────────────────

    public IReadOnlyList<ExerciseMedia> Media { get; }
    public bool HasMedia => Media.Count > 0;
    public bool HasManyMedia => Media.Count > 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentMedia), nameof(IndexText), nameof(IsVideo))]
    private int _mediaIndex;

    public ExerciseMedia? CurrentMedia => HasMedia ? Media[MediaIndex] : null;
    public bool IsVideo => CurrentMedia?.Kind == MediaKind.Video;
    public string IndexText => $"{MediaIndex + 1} / {Media.Count}";

    /// <summary>Cámara lenta (0,5×) para ver la técnica.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Speed), nameof(SpeedText))]
    private bool _slowMotion;

    public double Speed => SlowMotion ? 0.5 : 1.0;
    public string SpeedText => SlowMotion ? "0,5×" : "1×";

    [RelayCommand] private void NextMedia() { if (HasMedia) MediaIndex = (MediaIndex + 1) % Media.Count; }
    [RelayCommand] private void PreviousMedia() { if (HasMedia) MediaIndex = (MediaIndex - 1 + Media.Count) % Media.Count; }
    [RelayCommand] private void ToggleSlowMotion() => SlowMotion = !SlowMotion;

    // ── Gráfica: 1RM estimado por sesión, los últimos tres meses (o las últimas sesiones) ──

    public ISeries[] Series { get; } = Array.Empty<ISeries>();
    public Axis[] XAxes { get; } = Array.Empty<Axis>();
    public Axis[] YAxes { get; } = Array.Empty<Axis>();

    public ExerciseDetailViewModel(Exercise exercise, ExerciseProgress? progress, DateTime? today = null)
    {
        Exercise = exercise;
        Progress = progress;
        OriginText = ExerciseListItem.OriginOf(exercise);
        Media = exercise.Media.OrderBy(m => m.Order).ToList();

        var chips = new List<string> { exercise.MuscleGroup };
        chips.AddRange(ExerciseCatalog.TagsOf(exercise.SecondaryMuscles));
        if (EquipmentCatalog.DisplayText(exercise) is { Length: > 0 } equipment) chips.Add(equipment);
        chips.AddRange(ExerciseCatalog.TagsOf(exercise.Tags));
        Chips = chips.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();

        Steps = StepsOf(exercise.Instructions);

        var points = ChartPoints(progress, today ?? DateTime.Today, out var caption);
        ChartCaption = caption;
        if (points.Count >= 2)
            (Series, XAxes, YAxes) = BuildChart(points);
    }

    /// <summary>
    /// «Cómo hacerlo» en pasos: una línea por paso (sin la numeración que traiga, que se pone
    /// al mostrarlos); un texto de un solo párrafo se parte por frases.
    /// </summary>
    public static IReadOnlyList<string> StepsOf(string? instructions)
    {
        var text = instructions?.Trim() ?? "";
        if (text.Length == 0) return Array.Empty<string>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (lines.Count == 1)
            lines = Regex.Split(text, @"(?<=[.!?])\s+").Where(l => l.Length > 0).ToList();
        return lines
            .Select(l => Regex.Replace(l, @"^(\d+[.)]|[-•*])\s*", "").Trim())
            .Where(l => l.Length > 0)
            .Select((l, i) => $"{i + 1}. {l}")
            .ToList();
    }

    /// <summary>Las sesiones de los últimos 90 días; si son menos de dos, las últimas ocho.</summary>
    public static List<ExercisePoint> ChartPoints(ExerciseProgress? progress, DateTime today, out string caption)
    {
        caption = "1RM est.";
        if (progress is null) return new();
        var recent = progress.Points.Where(p => p.Date >= today.AddDays(-90)).ToList();
        if (recent.Count >= 2)
        {
            caption = "1RM est. · 3 meses";
            return recent;
        }
        caption = "1RM est. · últimas sesiones";
        return progress.Points.TakeLast(8).ToList();
    }

    private static (ISeries[], Axis[], Axis[]) BuildChart(IReadOnlyList<ExercisePoint> points)
    {
        // Colores del sistema de diseño (Tokens.xaml): lila del 1RM estimado y degradado hacia el fondo
        var line = SKColor.Parse("#C9B9F2");
        var series = new ISeries[]
        {
            new LineSeries<ObservablePoint>
            {
                Name = "1RM estimado",
                Values = points.Select(p => new ObservablePoint(p.Date.Ticks, Math.Round(p.BestE1rm, 1))).ToList(),
                Stroke = new SolidColorPaint(line) { StrokeThickness = 3 },
                GeometrySize = 0,
                GeometryStroke = null,
                GeometryFill = null,
                LineSmoothness = 0.4,
                Fill = new LinearGradientPaint(new[] { line.WithAlpha(90), line.WithAlpha(0) }, new SKPoint(0.5f, 0), new SKPoint(0.5f, 1))
            }
        };
        var labels = new SolidColorPaint(SKColor.Parse("#8A91A8")) { SKTypeface = SKTypeface.FromFamilyName("sans-serif") };
        var x = new[] { new Axis { Labeler = v => new DateTime((long)Math.Max(0, v)).ToString("dd/MM"), LabelsPaint = labels, TextSize = 10, SeparatorsPaint = null } };
        var y = new[] { new Axis { LabelsPaint = labels, TextSize = 10, SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#2A2E3C")) { StrokeThickness = 1 } } };
        return (series, x, y);
    }
}
