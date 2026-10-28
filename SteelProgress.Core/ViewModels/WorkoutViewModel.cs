using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

/// <summary>
/// Pantalla de entreno: preparar (elegir rutina y día), registrar series con
/// números grandes, descanso con cuenta atrás y resumen final.
/// Cada serie se guarda al marcarla, así un cierre de la app no pierde el entreno.
/// </summary>
public partial class WorkoutViewModel : ObservableObject
{
    public const int DefaultRestSeconds = 90;
    public const double WeightStep = 2.5;

    private static readonly CultureInfo Es = WorkoutExerciseState.Es;

    private readonly WorkoutRepository _workoutRepository;
    private readonly RoutineRepository _routineRepository;
    private readonly TimeProvider _clock;

    public WorkoutViewModel(WorkoutRepository workoutRepository, RoutineRepository routineRepository, TimeProvider? clock = null)
    {
        _workoutRepository = workoutRepository;
        _routineRepository = routineRepository;
        _clock = clock ?? TimeProvider.System;
    }

    // Las cargas de preparar comparten DbContext: una cada vez
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Task _dayOptionsLoad = Task.CompletedTask;

    /// <summary>La página lo llama al aparecer.</summary>
    public async Task RefreshAsync()
    {
        if (IsSessionActive) return;
        await LoadRoutinesAsync();
        await _dayOptionsLoad;
    }

    // ════ Preparar ════════════════════════════════════════════════════════

    public ObservableCollection<Routine> Routines { get; } = new();
    public ObservableCollection<WorkoutDayOption> DayOptions { get; } = new();
    public ObservableCollection<RoutineDayExercise> DayPreview { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoutines))]
    private bool _routinesLoaded;

    public bool HasRoutines => Routines.Count > 0;

    [ObservableProperty] private Routine? _selectedRoutine;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDay), nameof(StartButtonText))]
    private RoutineDay? _selectedRoutineDay;

    [ObservableProperty] private string _dayPreviewSummary = "";

    public bool HasSelectedDay => SelectedRoutineDay is not null;
    public string StartButtonText => SelectedRoutineDay is null ? "Elige un día" : $"▶ Empezar {SelectedRoutineDay.Name}";

    private async Task LoadRoutinesAsync()
    {
        var previous = SelectedRoutine?.Id;
        List<Routine> routines;
        await _loadLock.WaitAsync();
        try { routines = await _routineRepository.GetAllAsync(); }
        finally { _loadLock.Release(); }

        Routines.Clear();
        foreach (var routine in routines)
            Routines.Add(routine);

        RoutinesLoaded = true;
        OnPropertyChanged(nameof(HasRoutines));
        var selected = Routines.FirstOrDefault(r => r.Id == previous) ?? Routines.FirstOrDefault();
        if (ReferenceEquals(selected, SelectedRoutine) || selected?.Id == SelectedRoutine?.Id)
        {
            _selectedRoutine = selected; // misma rutina: solo refrescar sus días
            OnPropertyChanged(nameof(SelectedRoutine));
            _dayOptionsLoad = LoadDayOptionsAsync();
        }
        else
            SelectedRoutine = selected;
    }

    partial void OnSelectedRoutineChanged(Routine? value) => _dayOptionsLoad = LoadDayOptionsAsync();

    private async Task LoadDayOptionsAsync()
    {
        var routine = SelectedRoutine;
        var previousDay = SelectedRoutineDay?.Id;
        DayOptions.Clear();
        SelectedRoutineDay = null;
        if (routine is null) return;

        List<RoutineDay> days;
        Dictionary<int, DateTime> lastDone;
        await _loadLock.WaitAsync();
        try
        {
            days = await _routineRepository.GetDaysByRoutineIdAsync(routine.Id);
            lastDone = await _workoutRepository.GetLastSessionDatesAsync(days.Select(d => d.Id));
        }
        finally { _loadLock.Release(); }
        if (!ReferenceEquals(routine, SelectedRoutine)) return; // se eligió otra mientras tanto

        var today = _clock.GetLocalNow().Date;
        foreach (var day in days)
            DayOptions.Add(new WorkoutDayOption(day, lastDone.TryGetValue(day.Id, out var d) ? LastDoneText(d, today) : "Sin hacer"));

        SelectDay(DayOptions.FirstOrDefault(o => o.Day.Id == previousDay) ?? DayOptions.FirstOrDefault());
    }

    public void SelectDay(WorkoutDayOption? option)
    {
        foreach (var o in DayOptions)
            o.IsSelected = ReferenceEquals(o, option);
        SelectedRoutineDay = option?.Day;
    }

    partial void OnSelectedRoutineDayChanged(RoutineDay? value) => _ = LoadDayPreviewAsync();

    private async Task LoadDayPreviewAsync()
    {
        var day = SelectedRoutineDay;
        DayPreview.Clear();
        DayPreviewSummary = "";
        if (day is null) return;

        List<RoutineDayExercise> exercises;
        await _loadLock.WaitAsync();
        try { exercises = await _routineRepository.GetExercisesByDayIdAsync(day.Id); }
        finally { _loadLock.Release(); }
        if (!ReferenceEquals(day, SelectedRoutineDay)) return;

        foreach (var e in exercises)
            DayPreview.Add(e);
        DayPreviewSummary = exercises.Count == 1 ? "1 ejercicio" : $"{exercises.Count} ejercicios";
    }

    internal static string LastDoneText(DateTime date, DateTime today)
    {
        var days = (today - date.Date).Days;
        return days switch
        {
            <= 0 => "✓ Hoy",
            1 => "Ayer",
            _ => $"Hace {days} días"
        };
    }

    // ════ Sesión en curso ═════════════════════════════════════════════════

    public ObservableCollection<WorkoutExerciseState> ExerciseStates { get; } = new();

    [ObservableProperty] private bool _isSessionActive;
    [ObservableProperty] private WorkoutSession? _currentSession;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionText), nameof(PrescriptionText), nameof(PrescriptionNotes),
        nameof(HasPrescription), nameof(HasPrescriptionNotes), nameof(CurrentSetLabel))]
    private WorkoutExerciseState? _currentExercise;

    [ObservableProperty] private string _elapsedText = "0:00";

    public string PositionText => CurrentExercise is null || CurrentSession is null
        ? ""
        : $"{CurrentSession.RoutineDay?.Name} · ejercicio {CurrentExercise.Number} de {ExerciseStates.Count}";

    // Lo que pide la rutina para el ejercicio actual ("4 × 6–8 · RPE 8 · 1:30") y su nota
    public string PrescriptionText => CurrentExercise?.PrescriptionText ?? "";
    public string PrescriptionNotes => CurrentExercise?.PrescriptionNotes ?? "";
    public bool HasPrescription => PrescriptionText.Length > 0;
    public bool HasPrescriptionNotes => PrescriptionNotes.Length > 0;

    // Se mantiene por compatibilidad: la prescripción ya se carga con la sesión
    public Task LoadPrescriptionAsync() => Task.CompletedTask;

    /// <summary>Se dispara al marcar una serie; el argumento indica si es récord.</summary>
    public event Action<bool>? SetCompleted;
    /// <summary>Se dispara cuando el descanso llega a cero.</summary>
    public event Action? RestFinished;

    [RelayCommand]
    private async Task StartWorkout()
    {
        if (SelectedRoutineDay is null)
        {
            NotificationService.Error("Elige una rutina y un día antes de empezar.");
            return;
        }

        var session = await _workoutRepository.CreateSessionFromRoutineDayAsync(SelectedRoutineDay.Id);
        await LoadSessionAsync(session.Id);
    }

    public async Task LoadSessionAsync(int sessionId)
    {
        var session = await _workoutRepository.GetSessionByIdAsync(sessionId);
        if (session is null)
            return;

        // Include no garantiza orden: las filas se crearon en el orden del día
        var entries = session.Exercises.OrderBy(e => e.Id).ToList();
        // Por posición y no por ExerciseId: un ejercicio puede repetirse en el día
        var prescriptions = await _routineRepository.GetExercisesByDayIdAsync(session.RoutineDayId);

        ExerciseStates.Clear();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var prescription = i < prescriptions.Count && prescriptions[i].ExerciseId == entry.ExerciseId ? prescriptions[i] : null;
            var history = await _workoutRepository.GetExerciseHistoryAsync(entry.ExerciseId, session.Id);
            var state = new WorkoutExerciseState(entry, prescription, history, i + 1);

            foreach (var set in entry.Sets.OrderBy(s => s.Id))
                state.Sets.Add(new DoneSet(set, state.Sets.Count + 1, false));
            RecomputeRecords(state);

            ExerciseStates.Add(state);
        }

        _previousVolume = await _workoutRepository.GetPreviousVolumeAsync(session.RoutineDayId, session.Id);

        CurrentSession = session;
        IsSessionActive = true;
        IsResting = false;
        IsFinishing = false;
        WorkoutStateService.IsWorkoutActive = true;

        SelectExercise(ExerciseStates.FirstOrDefault(e => !e.IsDone) ?? ExerciseStates.FirstOrDefault());
        Tick();
    }

    public void SelectExercise(WorkoutExerciseState? state)
    {
        foreach (var e in ExerciseStates)
            e.IsCurrent = ReferenceEquals(e, state);
        CurrentExercise = state;
        LoadSuggestion();
    }

    [RelayCommand]
    private void NextExercise()
    {
        if (CurrentExercise is null) return;
        var i = ExerciseStates.IndexOf(CurrentExercise);
        if (i < ExerciseStates.Count - 1) SelectExercise(ExerciseStates[i + 1]);
    }

    [RelayCommand]
    private void PreviousExercise()
    {
        if (CurrentExercise is null) return;
        var i = ExerciseStates.IndexOf(CurrentExercise);
        if (i > 0) SelectExercise(ExerciseStates[i - 1]);
    }

    /// <summary>Lo llama la página cada segundo: cronómetro y descanso.</summary>
    public void Tick()
    {
        var now = _clock.GetLocalNow();
        if (CurrentSession is not null)
            ElapsedText = FormatDuration(now.DateTime - CurrentSession.Date);

        if (IsResting)
            UpdateRest(now);
    }

    // ── Serie actual ──────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WeightText))]
    private double _weight;

    [ObservableProperty] private int _reps;

    public string WeightText => WorkoutExerciseState.FormatKg(Weight);

    public string CurrentSetLabel
    {
        get
        {
            if (CurrentExercise is null) return "";
            var n = CurrentExercise.DoneCount + 1;
            return CurrentExercise.TargetSets is int t && n <= t ? $"Serie {n} de {t}" : $"Serie {n}";
        }
    }

    private void LoadSuggestion()
    {
        if (CurrentExercise is null)
        {
            Weight = 0;
            Reps = 0;
            return;
        }
        (Weight, Reps) = CurrentExercise.Suggestion(CurrentExercise.DoneCount + 1);
        OnPropertyChanged(nameof(CurrentSetLabel));
    }

    [RelayCommand] private void IncreaseWeight() => Weight = Math.Min(1000, Weight + WeightStep);
    [RelayCommand] private void DecreaseWeight() => Weight = Math.Max(0, Weight - WeightStep);
    [RelayCommand] private void IncreaseReps() => Reps = Math.Min(999, Reps + 1);
    [RelayCommand] private void DecreaseReps() => Reps = Math.Max(0, Reps - 1);

    /// <summary>Peso tecleado a mano ("72,5" o "72.5"). Devuelve false si no es válido.</summary>
    public bool TrySetWeight(string text)
    {
        if (!PrescriptionEditorViewModel.TryDouble(text, 0, 1000, out var value) || value is null) return false;
        Weight = value.Value;
        return true;
    }

    public bool TrySetReps(string text)
    {
        if (!PrescriptionEditorViewModel.TryInt(text, 0, 999, out var value) || value is null) return false;
        Reps = value.Value;
        return true;
    }

    [RelayCommand]
    private async Task CompleteSet()
    {
        var state = CurrentExercise;
        if (state is null) return;

        if (Reps <= 0)
        {
            NotificationService.Error("Indica cuántas repeticiones has hecho.");
            return;
        }

        var set = new WorkoutSet { WorkoutExerciseId = state.Entry.Id, Reps = Reps, Weight = Weight };
        await _workoutRepository.AddSetAsync(set);

        var done = new DoneSet(set, state.Sets.Count + 1, false);
        state.Sets.Add(done);
        RecomputeRecords(state);
        state.NotifyProgress();
        OnPropertyChanged(nameof(TodayVolume));

        SetCompleted?.Invoke(done.IsRecord);

        if (ExerciseStates.All(e => e.IsDone))
        {
            // Última serie del último ejercicio: directo al resumen
            OpenFinish();
            return;
        }

        if (state.IsDone)
            SelectExercise(NextPending(state) ?? state);
        else
            LoadSuggestion();

        StartRest(state.Prescription?.RestSeconds ?? DefaultRestSeconds);
    }

    [RelayCommand]
    private async Task DeleteSet(DoneSet? done)
    {
        if (done is null) return;
        var state = ExerciseStates.FirstOrDefault(e => e.Sets.Contains(done));
        if (state is null) return;

        await _workoutRepository.DeleteSetAsync(done.Set);
        state.Sets.Remove(done);

        // Renumerar: las series se muestran como 1, 2, 3...
        var renumbered = state.Sets.Select((d, i) => new DoneSet(d.Set, i + 1, false)).ToList();
        state.Sets.Clear();
        foreach (var d in renumbered) state.Sets.Add(d);

        RecomputeRecords(state);
        state.NotifyProgress();
        OnPropertyChanged(nameof(TodayVolume));
        if (ReferenceEquals(state, CurrentExercise)) LoadSuggestion();
    }

    private WorkoutExerciseState? NextPending(WorkoutExerciseState from)
    {
        var start = ExerciseStates.IndexOf(from);
        for (var k = 1; k <= ExerciseStates.Count; k++)
        {
            var candidate = ExerciseStates[(start + k) % ExerciseStates.Count];
            if (!candidate.IsDone) return candidate;
        }
        return null;
    }

    // Récord: supera el 1RM estimado de todo el historial previo y de las series anteriores de hoy.
    // Sin historial no hay récord (el primer entreno marcaría todas las series).
    internal static void RecomputeRecords(WorkoutExerciseState state)
    {
        var best = state.History.BestEstimatedOneRepMax;
        foreach (var done in state.Sets)
        {
            var e1rm = done.EstimatedOneRepMax;
            done.IsRecord = best is double b && done.Set.Weight > 0 && e1rm > b + 0.001;
            if (best is not null && e1rm > best) best = e1rm;
        }
    }

    // ── Descanso ──────────────────────────────────────────────────────────

    private DateTimeOffset _restEndsAt;

    [ObservableProperty] private bool _isResting;
    [ObservableProperty] private int _restTotalSeconds;
    [ObservableProperty] private int _restRemainingSeconds;

    /// <summary>Fracción que queda del descanso (1 = recién empezado, 0 = terminado).</summary>
    [ObservableProperty] private double _restProgress;

    public string RestRemainingText => FormatDuration(TimeSpan.FromSeconds(RestRemainingSeconds));
    public string RestTotalText => $"de {FormatDuration(TimeSpan.FromSeconds(RestTotalSeconds))}";

    partial void OnRestRemainingSecondsChanged(int value) => OnPropertyChanged(nameof(RestRemainingText));
    partial void OnRestTotalSecondsChanged(int value) => OnPropertyChanged(nameof(RestTotalText));

    public string NextTitle => CurrentExercise is null ? "" : $"{CurrentExercise.Name} · serie {CurrentExercise.DoneCount + 1}";
    public string NextDetail
    {
        get
        {
            if (CurrentExercise is null) return "";
            var parts = new List<string>();
            if (Weight > 0) parts.Add($"{WorkoutExerciseState.FormatKg(Weight)} kg");
            var p = CurrentExercise.Prescription;
            var reps = p is null ? null : PrescriptionFormatter.FormatReps(p.TargetReps, p.TargetRepsMax);
            if (reps is not null) parts.Add($"× {reps}");
            else if (Reps > 0) parts.Add($"× {Reps}");
            var text = string.Join(" ", parts);
            var intensity = CurrentExercise.IntensityText;
            return intensity.Length > 0 ? (text.Length > 0 ? $"{text} · {intensity}" : intensity) : text;
        }
    }

    public void StartRest(int seconds)
    {
        if (seconds <= 0) return;
        RestTotalSeconds = seconds;
        _restEndsAt = _clock.GetLocalNow().AddSeconds(seconds);
        IsResting = true;
        OnPropertyChanged(nameof(NextTitle));
        OnPropertyChanged(nameof(NextDetail));
        UpdateRest(_clock.GetLocalNow());
    }

    [RelayCommand]
    private void AddRestTime() => AdjustRest(15);

    [RelayCommand]
    private void SubtractRestTime() => AdjustRest(-15);

    private void AdjustRest(int seconds)
    {
        if (!IsResting) return;
        _restEndsAt = _restEndsAt.AddSeconds(seconds);
        if (seconds > 0) RestTotalSeconds += seconds;
        UpdateRest(_clock.GetLocalNow());
    }

    [RelayCommand]
    private void SkipRest() => IsResting = false;

    private void UpdateRest(DateTimeOffset now)
    {
        var remaining = (int)Math.Ceiling((_restEndsAt - now).TotalSeconds);
        if (remaining <= 0)
        {
            RestRemainingSeconds = 0;
            RestProgress = 0;
            IsResting = false;
            RestFinished?.Invoke();
            return;
        }

        RestRemainingSeconds = remaining;
        RestProgress = RestTotalSeconds > 0 ? Math.Clamp((_restEndsAt - now).TotalSeconds / RestTotalSeconds, 0, 1) : 0;
    }

    // ── Volumen ───────────────────────────────────────────────────────────

    private double? _previousVolume;

    public double TodayVolume => ExerciseStates.SelectMany(e => e.Sets).Where(d => !d.Set.IsWarmup).Sum(d => d.Set.Weight * d.Set.Reps);
    public string TodayVolumeText => $"{TodayVolume.ToString("#,0", Es)} kg";
    public bool HasVolumeComparison => _previousVolume is > 0;

    public string VolumeDeltaText
    {
        get
        {
            if (_previousVolume is not double prev || prev <= 0) return "";
            var pct = (TodayVolume - prev) / prev * 100;
            return $"{(pct >= 0 ? "+" : "−")}{Math.Abs(pct):0} %";
        }
    }

    public bool IsVolumeUp => _previousVolume is double prev && TodayVolume >= prev;

    partial void OnIsRestingChanged(bool value)
    {
        if (!value) return;
        OnPropertyChanged(nameof(TodayVolumeText));
        OnPropertyChanged(nameof(VolumeDeltaText));
        OnPropertyChanged(nameof(HasVolumeComparison));
        OnPropertyChanged(nameof(IsVolumeUp));
    }

    // ════ Finalizar ═══════════════════════════════════════════════════════

    [ObservableProperty] private bool _isFinishing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private int? _feeling;

    [ObservableProperty] private string _finishNotes = "";

    [ObservableProperty] private string _summaryDuration = "";
    [ObservableProperty] private string _summaryVolume = "";
    [ObservableProperty] private int _summarySets;
    [ObservableProperty] private int _recordCount;
    [ObservableProperty] private string _recordsText = "";
    [ObservableProperty] private string _missingText = "";
    [ObservableProperty] private string _summaryTitle = "";

    public bool HasRecords => RecordCount > 0;
    public bool IsIncomplete => MissingText.Length > 0;
    public string RecordBadgeText => RecordCount == 1 ? "★ 1 récord" : $"★ {RecordCount} récords";
    public bool CanSave => SummarySets > 0;

    partial void OnRecordCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasRecords));
        OnPropertyChanged(nameof(RecordBadgeText));
    }

    partial void OnMissingTextChanged(string value) => OnPropertyChanged(nameof(IsIncomplete));
    partial void OnSummarySetsChanged(int value) => OnPropertyChanged(nameof(CanSave));

    /// <summary>"Terminar": abre el resumen (o, sin ninguna serie, ofrece descartar).</summary>
    [RelayCommand]
    private async Task Finish()
    {
        if (!ExerciseStates.Any(e => e.Sets.Count > 0))
        {
            var discard = await ConfirmDialogService.ConfirmAsync("Terminar entreno", "No has registrado ninguna serie. ¿Descartar el entreno?");
            if (discard) await DiscardAsync();
            return;
        }
        OpenFinish();
    }

    public void OpenFinish()
    {
        if (CurrentSession is null) return;
        IsResting = false;

        var all = ExerciseStates.SelectMany(e => e.Sets).ToList();
        SummaryTitle = CurrentSession.RoutineDay?.Name ?? "Entreno";
        SummaryDuration = FormatDuration(_clock.GetLocalNow().DateTime - CurrentSession.Date);
        var volume = TodayVolume;
        SummaryVolume = volume >= 1000 ? $"{(volume / 1000).ToString("0.#", Es)} t" : $"{volume.ToString("0", Es)} kg";
        SummarySets = all.Count;

        var records = ExerciseStates
            .SelectMany(e => e.Sets.Where(d => d.IsRecord).Select(d => (e.Name, d)))
            .GroupBy(x => x.Name)
            .Select(g => g.OrderByDescending(x => x.d.EstimatedOneRepMax).First())
            .ToList();
        RecordCount = records.Count;
        RecordsText = string.Join(" · ", records.Select(r => $"{r.Name} {WorkoutExerciseState.FormatKg(r.d.Set.Weight)} × {r.d.Set.Reps}"));

        var missing = ExerciseStates.Where(e => e.MissingSets > 0).ToList();
        MissingText = missing.Count switch
        {
            0 => "",
            1 => $"Te {(missing[0].MissingSets == 1 ? "falta 1 serie" : $"faltan {missing[0].MissingSets} series")} de {missing[0].Name}. Se guardará como incompleto.",
            _ => $"Te faltan series en {missing.Count} ejercicios ({string.Join(", ", missing.Select(m => m.Name))}). Se guardará como incompleto."
        };

        IsFinishing = true;
    }

    [RelayCommand]
    private void SelectFeeling(string? value)
    {
        if (int.TryParse(value, out var f) && f is >= 1 and <= 5)
            Feeling = Feeling == f ? null : f;
    }

    [RelayCommand]
    private void ContinueWorkout() => IsFinishing = false;

    [RelayCommand]
    private async Task SaveWorkout()
    {
        if (CurrentSession is not { } session || !CanSave) return;

        var minutes = (int)Math.Round((_clock.GetLocalNow().DateTime - session.Date).TotalMinutes);
        session.DurationMinutes = Math.Max(1, minutes);
        session.Feeling = Feeling;
        session.Notes = string.IsNullOrWhiteSpace(FinishNotes) ? null : FinishNotes.Trim();
        session.IsComplete = !IsIncomplete;
        await _workoutRepository.UpdateSessionAsync(session);

        ResetSession();
        NotificationService.Success("Entreno guardado.");
    }

    [RelayCommand]
    private async Task DiscardWorkout()
    {
        var confirmed = await ConfirmDialogService.ConfirmAsync("Descartar entreno", "Se borrarán las series de este entreno. ¿Seguro?");
        if (confirmed) await DiscardAsync();
    }

    public async Task DiscardAsync()
    {
        if (CurrentSession is not null)
            await _workoutRepository.DeleteSessionAsync(CurrentSession);
        ResetSession();
        NotificationService.Info("Entreno descartado.");
    }

    private void ResetSession()
    {
        ExerciseStates.Clear();
        CurrentExercise = null;
        CurrentSession = null;
        IsResting = false;
        IsFinishing = false;
        IsSessionActive = false;
        Feeling = null;
        FinishNotes = "";
        _previousVolume = null;
        WorkoutStateService.IsWorkoutActive = false;
        _ = LoadDayOptionsAsync(); // "Hace N días" cambia tras entrenar
    }

    internal static string FormatDuration(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }
}

/// <summary>Tarjeta de un día en "¿Qué entrenas hoy?".</summary>
public partial class WorkoutDayOption : ObservableObject
{
    public RoutineDay Day { get; }
    public string LastDoneText { get; }

    [ObservableProperty] private bool _isSelected;

    public WorkoutDayOption(RoutineDay day, string lastDoneText)
    {
        Day = day;
        LastDoneText = lastDoneText;
    }

    public string Name => Day.Name;
}
