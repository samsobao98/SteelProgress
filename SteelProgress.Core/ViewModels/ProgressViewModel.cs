using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;

namespace SteelProgress.Core.ViewModels;

public enum ProgressSection { Summary, Exercises, History }

/// <summary>
/// Progreso (absorbe Historial): resumen con objetivo semanal, racha, volumen del mes, calendario
/// de constancia y récords recientes; la evolución de cada ejercicio; y el historial de sesiones.
/// Todo sale de una sola carga del historial (<see cref="WorkoutRepository.GetTrainingLogAsync"/>).
/// </summary>
public partial class ProgressViewModel : ObservableObject
{
    public const int CalendarWeeks = 15;
    private const int DefaultWeeklyGoal = 3;

    private readonly WorkoutRepository _workoutRepository;
    private readonly RoutineRepository _routineRepository;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    private TrainingLog _log = TrainingLog.Empty;
    private List<RecordEvent> _records = new();
    private Dictionary<int, double> _volumes = new();
    private int? _pendingExerciseId;

    public ProgressViewModel(WorkoutRepository workoutRepository, RoutineRepository routineRepository, TimeProvider? clock = null)
    {
        _workoutRepository = workoutRepository;
        _routineRepository = routineRepository;
        _clock = clock ?? TimeProvider.System;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSummary), nameof(IsExercises), nameof(IsHistory))]
    private ProgressSection _section = ProgressSection.Summary;

    public bool IsSummary => Section == ProgressSection.Summary;
    public bool IsExercises => Section == ProgressSection.Exercises;
    public bool IsHistory => Section == ProgressSection.History;

    [RelayCommand] private void ShowSummary() => Section = ProgressSection.Summary;
    [RelayCommand] private void ShowExercises() => Section = ProgressSection.Exercises;
    [RelayCommand] private void ShowHistory() => Section = ProgressSection.History;

    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _hasData;

    /// <summary>La página lo llama al aparecer.</summary>
    public async Task RefreshAsync()
    {
        var today = _clock.GetLocalNow().Date;
        int goal;
        await _dbLock.WaitAsync();
        try
        {
            _log = await _workoutRepository.GetTrainingLogAsync();
            _routineRepository.ForgetLoaded();
            goal = await WeeklyGoalAsync();
        }
        finally { _dbLock.Release(); }

        _records = TrainingStats.Records(_log.Sets);
        _volumes = TrainingStats.SessionVolumes(_log.Sets);
        HasData = _log.Sessions.Count > 0;

        LoadSummary(today, goal);
        LoadExercises(today);
        LoadHistory(today);
        IsLoaded = true;
    }

    // Objetivo semanal: los días de la rutina activa (marcada o última entrenada). No hay un campo
    // propio todavía; sin rutina, 3 días.
    private async Task<int> WeeklyGoalAsync()
    {
        var activeId = await _routineRepository.GetMarkedActiveRoutineIdAsync()
                       ?? await _workoutRepository.GetLastTrainedRoutineIdAsync();
        if (activeId is not int id) return DefaultWeeklyGoal;
        var days = await _routineRepository.GetDaysByRoutineIdAsync(id);
        return days.Count > 0 ? days.Count : DefaultWeeklyGoal;
    }

    // ════ Resumen ═════════════════════════════════════════════════════════

    [ObservableProperty] private int _weekDone;
    [ObservableProperty] private int _weekGoal = DefaultWeeklyGoal;
    [ObservableProperty] private double _weekProgress;
    [ObservableProperty] private string _streakText = "";
    [ObservableProperty] private bool _hasStreak;
    [ObservableProperty] private string _monthVolumeText = "";
    [ObservableProperty] private string _monthDeltaText = "";
    [ObservableProperty] private bool _isMonthUp;
    [ObservableProperty] private IReadOnlyList<CalendarDay> _calendar = Array.Empty<CalendarDay>();
    [ObservableProperty] private string _calendarCaption = "";

    public bool HasMonthDelta => MonthDeltaText.Length > 0;
    partial void OnMonthDeltaTextChanged(string value) => OnPropertyChanged(nameof(HasMonthDelta));

    public ObservableCollection<RecordItem> RecentRecords { get; } = new();
    public bool HasRecords => RecentRecords.Count > 0;

    private void LoadSummary(DateTime today, int goal)
    {
        WeekGoal = goal;
        WeekDone = TrainingStats.SessionsThisWeek(_log.Sessions, today);
        WeekProgress = Math.Clamp((double)WeekDone / goal, 0, 1);

        var streak = TrainingStats.WeekStreak(_log.Sessions, today);
        HasStreak = streak > 0;
        StreakText = streak == 1 ? "1 semana" : $"{streak} semanas";

        var (current, previous) = TrainingStats.MonthVolume(_log.Sets, today);
        MonthVolumeText = TrainingStats.FormatVolume(current);
        MonthDeltaText = TrainingStats.DeltaText(current, previous);
        IsMonthUp = current >= previous;

        Calendar = TrainingStats.Calendar(_log.Sets, today, CalendarWeeks);
        var trainedDays = Calendar.Count(d => d.Level > 0);
        CalendarCaption = trainedDays == 1 ? "1 día entrenado" : $"{trainedDays} días entrenados";

        RecentRecords.Clear();
        foreach (var r in _records.Take(5))
            RecentRecords.Add(new RecordItem(r, today));
        OnPropertyChanged(nameof(HasRecords));
    }

    // ════ Ejercicios ══════════════════════════════════════════════════════

    public ObservableCollection<ExerciseProgressItem> ExerciseItems { get; } = new();
    public bool HasExercises => ExerciseItems.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedExercise))]
    private ExerciseProgressItem? _selectedExercise;

    public bool HasSelectedExercise => SelectedExercise is not null;

    [ObservableProperty] private ISeries[] _series = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _xAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _yAxes = Array.Empty<Axis>();

    /// <summary>Abre la evolución de un ejercicio (desde otra pantalla o el historial).</summary>
    public void SelectExerciseById(int exerciseId)
    {
        Section = ProgressSection.Exercises;
        var item = ExerciseItems.FirstOrDefault(e => e.Progress.ExerciseId == exerciseId);
        if (item is null) { _pendingExerciseId = exerciseId; return; } // aún no se ha cargado
        SelectedExercise = item;
    }

    [RelayCommand]
    private void SelectExercise(ExerciseProgressItem? item) =>
        SelectedExercise = ReferenceEquals(item, SelectedExercise) ? null : item;

    private void LoadExercises(DateTime today)
    {
        var keep = _pendingExerciseId ?? SelectedExercise?.Progress.ExerciseId;
        _pendingExerciseId = null;

        ExerciseItems.Clear();
        foreach (var e in TrainingStats.Exercises(_log.Sets))
            ExerciseItems.Add(new ExerciseProgressItem(e, today));
        OnPropertyChanged(nameof(HasExercises));

        SelectedExercise = ExerciseItems.FirstOrDefault(e => e.Progress.ExerciseId == keep);
    }

    partial void OnSelectedExerciseChanged(ExerciseProgressItem? value)
    {
        foreach (var item in ExerciseItems)
            item.IsSelected = ReferenceEquals(item, value);
        BuildChart(value?.Progress);
    }

    private void BuildChart(ExerciseProgress? progress)
    {
        if (progress is null || progress.Points.Count == 0)
        {
            Series = Array.Empty<ISeries>();
            return;
        }

        // Colores del sistema de diseño (Tokens.xaml): acero para el peso, lila para el 1RM estimado
        var weight = SKColor.Parse("#89B4FA");
        var e1rm = SKColor.Parse("#CBA6F7");
        var card = SKColor.Parse("#252536");

        Series = new ISeries[]
        {
            new LineSeries<ObservablePoint>
            {
                Name = "Peso máximo",
                Values = progress.Points.Select(p => new ObservablePoint(p.Date.Ticks, p.MaxWeight)).ToList(),
                Stroke = new SolidColorPaint(weight) { StrokeThickness = 3 },
                GeometryStroke = new SolidColorPaint(weight) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(card),
                GeometrySize = 8,
                LineSmoothness = 0.3,
                Fill = null
            },
            new LineSeries<ObservablePoint>
            {
                Name = "1RM estimado",
                Values = progress.Points.Select(p => new ObservablePoint(p.Date.Ticks, Math.Round(p.BestE1rm, 1))).ToList(),
                Stroke = new SolidColorPaint(e1rm) { StrokeThickness = 2, PathEffect = new DashEffect([6, 6]) },
                GeometryStroke = null,
                GeometryFill = null,
                GeometrySize = 0,
                LineSmoothness = 0.3,
                Fill = null
            }
        };

        var labels = new SolidColorPaint(SKColor.Parse("#6C7086")) { SKTypeface = SKTypeface.FromFamilyName("sans-serif") };
        XAxes = new[]
        {
            new Axis
            {
                Labeler = value => new DateTime((long)Math.Max(0, value)).ToString("dd/MM"),
                LabelsPaint = labels,
                TextSize = 11,
                SeparatorsPaint = null
            }
        };
        YAxes = new[]
        {
            new Axis
            {
                LabelsPaint = labels,
                TextSize = 11,
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#34354A")) { StrokeThickness = 1 }
            }
        };
    }

    // ════ Historial ═══════════════════════════════════════════════════════

    public ObservableCollection<RecentSessionItem> Sessions { get; } = new();
    public bool HasSessions => Sessions.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSessionDetail))]
    private SessionDetail? _sessionDetail;

    public bool HasSessionDetail => SessionDetail is not null;

    private void LoadHistory(DateTime today)
    {
        var open = SessionDetail?.SessionId;
        Sessions.Clear();
        foreach (var s in _log.Sessions)
            Sessions.Add(new RecentSessionItem(s, _volumes.GetValueOrDefault(s.Id), today));
        OnPropertyChanged(nameof(HasSessions));

        SessionDetail = open is int id && Sessions.FirstOrDefault(s => s.SessionId == id) is { } item
            ? BuildDetail(item.Session)
            : null;
    }

    [RelayCommand]
    private void OpenSession(RecentSessionItem? item) =>
        SessionDetail = item is null ? null : BuildDetail(item.Session);

    /// <summary>Abre una sesión del historial por su id (p. ej. desde Inicio).</summary>
    public void OpenSessionById(int sessionId)
    {
        Section = ProgressSection.History;
        OpenSession(Sessions.FirstOrDefault(s => s.SessionId == sessionId));
    }

    [RelayCommand]
    private void CloseSession() => SessionDetail = null;

    private SessionDetail BuildDetail(LoggedSession session)
    {
        // La sesión anterior del mismo día de rutina, para comparar ejercicio a ejercicio
        var previous = _log.Sessions
            .Where(s => s.RoutineDayId == session.RoutineDayId && s.Date < session.Date)
            .OrderByDescending(s => s.Date)
            .FirstOrDefault();

        var exercises = _log.Sets.Where(s => s.SessionId == session.Id)
            .GroupBy(s => s.ExerciseId)
            .OrderBy(g => g.Min(s => s.SetId))
            .Select(g =>
            {
                var sets = g.OrderBy(s => s.SetId).ToList();
                var before = previous is null ? null
                    : _log.Sets.Where(s => s.SessionId == previous.Id && s.ExerciseId == g.Key).ToList();
                var record = _records.FirstOrDefault(r => r.SessionId == session.Id && r.ExerciseId == g.Key);
                return new SessionExerciseDetail(g.Key, sets[0].ExerciseName,
                    string.Join("   ", sets.Select(s => WorkoutExerciseState.FormatSet(s.Weight, s.Reps))),
                    ComparisonText(sets, before),
                    record is null ? "" : RecordItem.KindText(record.Kind));
            })
            .ToList();

        var volume = _volumes.GetValueOrDefault(session.Id);
        var parts = new List<string> { session.Date.ToString("dddd d 'de' MMMM", WorkoutExerciseState.Es) };
        if (session.DurationMinutes is > 0) parts.Add($"{session.DurationMinutes} min");
        if (volume > 0) parts.Add(TrainingStats.FormatVolume(volume));

        return new SessionDetail(session.Id, session.DayName, session.RoutineName, string.Join(" · ", parts),
            session.Notes ?? "", !session.IsComplete, exercises);
    }

    // Mejor serie de hoy frente a la de la sesión anterior del mismo día
    internal static string ComparisonText(IReadOnlyList<LoggedSet> current, IReadOnlyList<LoggedSet>? previous)
    {
        static LoggedSet? Best(IEnumerable<LoggedSet>? sets) =>
            sets?.OrderByDescending(s => s.Weight).ThenByDescending(s => s.Reps).FirstOrDefault();

        var now = Best(current);
        var before = Best(previous);
        if (now is null) return "";
        if (before is null) return "Primera vez en este día";

        var kg = now.Weight - before.Weight;
        if (kg > 0.001) return $"+{WorkoutExerciseState.FormatKg(kg)} kg respecto a la anterior";
        if (Math.Abs(kg) <= 0.001 && now.Reps > before.Reps) return $"+{now.Reps - before.Reps} reps con el mismo peso";
        if (Math.Abs(kg) <= 0.001 && now.Reps == before.Reps) return "Igual que la anterior";
        return "Por debajo de la anterior";
    }
}

/// <summary>Un récord en la lista de récords recientes.</summary>
public class RecordItem
{
    public RecordEvent Record { get; }
    public string ExerciseName => Record.ExerciseName;
    public string ValueText => $"{WorkoutExerciseState.FormatKg(Record.Weight)} × {Record.Reps}";
    public string WhenText { get; }
    public string Kind => KindText(Record.Kind);

    public RecordItem(RecordEvent record, DateTime today)
    {
        Record = record;
        var ago = RoutineSchedule.AgoText(record.Date, today);
        WhenText = char.ToUpper(ago[0]) + ago[1..];
    }

    public static string KindText(RecordKind kind) => kind switch
    {
        RecordKind.Weight => "★ Récord de peso",
        RecordKind.Reps => "★ Récord de reps",
        _ => ""
    };
}

/// <summary>Un ejercicio en la sección Ejercicios de Progreso.</summary>
public partial class ExerciseProgressItem : ObservableObject
{
    public ExerciseProgress Progress { get; }
    public string Name => Progress.Name;
    public string BestText => $"Mejor: {WorkoutExerciseState.FormatSet(Progress.BestWeight, Progress.BestReps)}";
    public string E1rmText => Progress.BestE1rm > 0 ? $"1RM est. {WorkoutExerciseState.FormatKg(Math.Round(Progress.BestE1rm))} kg" : "";
    public string DetailText { get; }

    [ObservableProperty] private bool _isSelected;

    public ExerciseProgressItem(ExerciseProgress progress, DateTime today)
    {
        Progress = progress;
        var sessions = progress.Points.Count == 1 ? "1 sesión" : $"{progress.Points.Count} sesiones";
        DetailText = $"{sessions} · {RoutineSchedule.LastTrainedText(progress.LastDate, today)}";
    }
}

public sealed record SessionExerciseDetail(int ExerciseId, string Name, string SetsText, string ComparisonText, string RecordText)
{
    public bool HasRecord => RecordText.Length > 0;
    public bool HasComparison => ComparisonText.Length > 0;
}

public sealed record SessionDetail(int SessionId, string Title, string RoutineName, string Subtitle, string Notes,
    bool IsIncomplete, IReadOnlyList<SessionExerciseDetail> Exercises)
{
    public bool HasNotes => Notes.Length > 0;
}
