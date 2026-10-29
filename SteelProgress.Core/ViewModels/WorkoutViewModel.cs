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
    private readonly ViewOptionsService? _viewOptions;

    public WorkoutViewModel(WorkoutRepository workoutRepository, RoutineRepository routineRepository, TimeProvider? clock = null,
        ViewOptionsService? viewOptions = null)
    {
        _workoutRepository = workoutRepository;
        _routineRepository = routineRepository;
        _clock = clock ?? TimeProvider.System;
        _viewOptions = viewOptions;
    }

    /// <summary>Apuntar notas en cada serie: vista entrenador, o «Notas detalladas» en vista simple.
    /// Las notas que ya hay se ven siempre.</summary>
    public bool CanNoteSets => _viewOptions?.Current.EditDetailedNotes ?? false;

    // En la app todos los repositorios comparten un DbContext, que no admite dos operaciones
    // a la vez: toda operación de base de datos de esta pantalla pasa por este cerrojo
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private Task _dayOptionsLoad = Task.CompletedTask;

    /// <summary>La página lo llama al aparecer.</summary>
    public async Task RefreshAsync()
    {
        OnPropertyChanged(nameof(CanNoteSets)); // pudo cambiar en Ajustes
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
    public bool CanStart => SelectedRoutineDay is not null && DayPreview.Count > 0;
    public string StartButtonText => SelectedRoutineDay is null ? "Elige un día" : $"▶ Empezar {SelectedRoutineDay.Name}";

    // Lo elegido a mano se respeta mientras se está en la pantalla; sin elección, se propone
    // la rutina activa (la última entrenada) y el día que toca. Al empezar un entreno se olvida.
    private int? _pickedRoutineId;
    private int? _pickedDayId;

    /// <summary>El usuario toca una rutina.</summary>
    public void PickRoutine(Routine routine)
    {
        _pickedRoutineId = routine.Id;
        _pickedDayId = null;
        SelectedRoutine = routine;
    }

    private async Task LoadRoutinesAsync()
    {
        List<Routine> routines;
        int? activeId;
        await _dbLock.WaitAsync();
        try
        {
            // Nombres frescos si se renombró algo en Rutinas (ver RoutineRepository.ForgetLoaded)
            _routineRepository.ForgetLoaded();
            routines = await _routineRepository.GetAllAsync();
            // La marcada a mano; si no hay, la última entrenada
            activeId = await _routineRepository.GetMarkedActiveRoutineIdAsync()
                       ?? await _workoutRepository.GetLastTrainedRoutineIdAsync();
        }
        finally { _dbLock.Release(); }

        Routines.Clear();
        foreach (var routine in routines)
            Routines.Add(routine);

        RoutinesLoaded = true;
        OnPropertyChanged(nameof(HasRoutines));
        var selected = Routines.FirstOrDefault(r => r.Id == _pickedRoutineId)
                       ?? Routines.FirstOrDefault(r => r.Id == activeId)
                       ?? Routines.FirstOrDefault();
        // Una sola carga de días aunque la rutina sea la misma (los días o «Toca» pueden haber cambiado)
        _suppressDayOptionsLoad = true;
        SelectedRoutine = selected;
        _suppressDayOptionsLoad = false;
        _dayOptionsLoad = LoadDayOptionsAsync();
    }

    private bool _suppressDayOptionsLoad;

    partial void OnSelectedRoutineChanged(Routine? value)
    {
        if (!_suppressDayOptionsLoad)
            _dayOptionsLoad = LoadDayOptionsAsync();
    }

    private async Task LoadDayOptionsAsync()
    {
        var routine = SelectedRoutine;
        DayOptions.Clear();
        SelectedRoutineDay = null;
        if (routine is null) return;

        var today = _clock.GetLocalNow().Date;
        RoutineWeek week;
        Dictionary<int, DateTime> lastDone;
        await _dbLock.WaitAsync();
        try
        {
            week = await _routineRepository.GetRoutineWeekAsync(routine.Id, RoutineSchedule.WeekStart(today));
            lastDone = await _workoutRepository.GetLastSessionDatesAsync(week.Days.Select(d => d.Id));
        }
        finally { _dbLock.Release(); }
        if (!ReferenceEquals(routine, SelectedRoutine)) return; // se eligió otra mientras tanto

        var next = RoutineSchedule.NextDay(week);
        foreach (var day in week.Days)
            DayOptions.Add(new WorkoutDayOption(day,
                lastDone.TryGetValue(day.Id, out var d) ? LastDoneText(d, today) : "Sin hacer",
                isNext: day.Id == next?.Id));

        ApplyDaySelection(DayOptions.FirstOrDefault(o => o.Day.Id == _pickedDayId)
                          ?? DayOptions.FirstOrDefault(o => o.IsNext)
                          ?? DayOptions.FirstOrDefault());
    }

    /// <summary>El usuario toca un día.</summary>
    public void SelectDay(WorkoutDayOption? option)
    {
        _pickedDayId = option?.Day.Id;
        ApplyDaySelection(option);
    }

    private void ApplyDaySelection(WorkoutDayOption? option)
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
        OnPropertyChanged(nameof(CanStart));
        if (day is null) return;

        List<RoutineDayExercise> exercises;
        await _dbLock.WaitAsync();
        try { exercises = await _routineRepository.GetExercisesByDayIdAsync(day.Id); }
        finally { _dbLock.Release(); }
        if (!ReferenceEquals(day, SelectedRoutineDay)) return;

        foreach (var e in exercises)
            DayPreview.Add(e);
        DayPreviewSummary = exercises.Count == 1 ? "1 ejercicio" : $"{exercises.Count} ejercicios";
        OnPropertyChanged(nameof(CanStart));
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
        nameof(HasPrescription), nameof(HasPrescriptionNotes), nameof(CurrentSetLabel),
        nameof(Load), nameof(ShowsPlates), nameof(LoadText), nameof(IsPlateMachine))]
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

        await StartDayAsync(SelectedRoutineDay.Id);
    }

    /// <summary>
    /// Empieza un día de rutina. Lo usan el botón de esta pantalla y, a través de
    /// <see cref="WorkoutLaunch"/>, «Hoy toca» de Inicio y la vista previa de Rutinas.
    /// Con un entreno ya en curso no hace nada.
    /// </summary>
    public async Task StartDayAsync(int routineDayId)
    {
        if (IsSessionActive) return; // doble toque

        await _dbLock.WaitAsync();
        try
        {
            if (IsSessionActive) return;
            var exercises = await _routineRepository.GetExercisesByDayIdAsync(routineDayId);
            if (exercises.Count == 0)
            {
                NotificationService.Error("Este día no tiene ejercicios. Añádelos en Rutinas.");
                return;
            }

            var session = await _workoutRepository.CreateSessionFromRoutineDayAsync(routineDayId);
            await LoadSessionCoreAsync(session.Id);
            // Al terminar se vuelve a proponer la activa y el día que toca
            _pickedRoutineId = null;
            _pickedDayId = null;
        }
        finally { _dbLock.Release(); }
    }

    /// <summary>
    /// Un entreno que la app dejó a medias (se cerró sin guardar), para ofrecer continuarlo o
    /// descartarlo. Los que no llegaron a tener ninguna serie se borran sin preguntar.
    /// </summary>
    public async Task<UnsavedSession?> FindUnsavedAsync()
    {
        if (IsSessionActive) return null;
        await _dbLock.WaitAsync();
        try
        {
            var unsaved = (await _workoutRepository.GetUnsavedSessionsAsync())
                .Where(u => u.Id != WorkoutStateService.ActiveSessionId)
                .ToList();
            foreach (var empty in unsaved.Where(u => u.SetCount == 0))
                await _workoutRepository.DeleteSessionAsync(new WorkoutSession { Id = empty.Id });
            return unsaved.FirstOrDefault(u => u.SetCount > 0);
        }
        finally { _dbLock.Release(); }
    }

    public async Task DiscardUnsavedAsync(int sessionId)
    {
        await _dbLock.WaitAsync();
        try { await _workoutRepository.DeleteSessionAsync(new WorkoutSession { Id = sessionId }); }
        finally { _dbLock.Release(); }
        _dayOptionsLoad = LoadRoutinesAsync(); // cambian «Hace N días» y lo que toca
    }

    public async Task LoadSessionAsync(int sessionId)
    {
        await _dbLock.WaitAsync();
        try { await LoadSessionCoreAsync(sessionId); }
        finally { _dbLock.Release(); }
    }

    private async Task LoadSessionCoreAsync(int sessionId)
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
                state.Sets.Add(new DoneSet(set, state.Sets.Count + 1));
            RecomputeRecords(state);

            ExerciseStates.Add(state);
        }

        _previousVolume = await _workoutRepository.GetPreviousVolumeAsync(session.RoutineDayId, session.Id);

        CurrentSession = session;
        IsSessionActive = true;
        IsResting = false;
        IsFinishing = false;
        WorkoutStateService.IsWorkoutActive = true;
        WorkoutStateService.ActiveSessionId = session.Id;

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
    [NotifyPropertyChangedFor(nameof(WeightText), nameof(Load), nameof(ShowsPlates), nameof(LoadText))]
    private double _weight;

    [ObservableProperty] private int _reps;

    public string WeightText => WorkoutExerciseState.FormatKg(Weight);

    // ── Discos: cómo cargar la barra o la máquina de discos con los de «Tu gimnasio» ──

    private GymSetup Gym => _viewOptions?.Current.Gym ?? GymSetup.Default;
    private EquipmentType? CurrentEquipment => CurrentExercise?.Exercise?.EquipmentType;
    public bool IsPlateMachine => CurrentEquipment == EquipmentType.PlateMachine;

    /// <summary>
    /// Discos por lado para el peso actual; null si el ejercicio no se carga con discos.
    /// En la máquina de discos se cuentan solo los discos (aún no se guarda lo que pesa el carro).
    /// </summary>
    public PlateLoad? Load => CurrentEquipment switch
    {
        EquipmentType.Barbell => PlateLoading.For(Weight, Gym.BarWeight, Gym.Plates),
        EquipmentType.PlateMachine when Weight > 0 => PlateLoading.For(Weight, 0, Gym.Plates),
        _ => null
    };

    public bool ShowsPlates => Load is not null;

    /// <summary>«Por lado: 25 + 1,25, en barra de 20»; si no sale justo, cuánto sale con tus discos.</summary>
    public string LoadText
    {
        get
        {
            if (Load is not { } load) return "";
            var bar = WorkoutExerciseState.FormatKg(load.Base);
            if (load.BelowBase) return $"Menos que la barra sola ({bar} kg)";
            if (load.PerSide.Count == 0) return "Solo la barra";
            var text = $"Por lado: {string.Join(" + ", load.PerSide.Select(WorkoutExerciseState.FormatKg))}"
                + (IsPlateMachine ? "" : $", en barra de {bar}");
            return load.IsExact ? text : $"{text} · con tus discos sale {WorkoutExerciseState.FormatKg(load.Achieved)} kg";
        }
    }

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

    private bool _completingSet;

    [RelayCommand]
    private async Task CompleteSet()
    {
        var state = CurrentExercise;
        if (state is null || _completingSet) return; // un doble toque no registra dos series

        if (Reps <= 0)
        {
            NotificationService.Error("Indica cuántas repeticiones has hecho.");
            return;
        }

        var wasAllDone = ExerciseStates.All(e => e.IsDone);
        var set = new WorkoutSet { WorkoutExerciseId = state.Entry.Id, Reps = Reps, Weight = Weight };
        _completingSet = true;
        await _dbLock.WaitAsync();
        try { await _workoutRepository.AddSetAsync(set); }
        finally
        {
            _dbLock.Release();
            _completingSet = false;
        }

        var done = new DoneSet(set, state.Sets.Count + 1);
        state.Sets.Add(done);
        RecomputeRecords(state);
        state.NotifyProgress();
        OnPropertyChanged(nameof(TodayVolume));

        SetCompleted?.Invoke(done.IsRecord);

        if (!wasAllDone && ExerciseStates.All(e => e.IsDone))
        {
            // Esta serie completa el entreno: directo al resumen (no si ya estaba todo y se sigue entrenando)
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

        await _dbLock.WaitAsync();
        try { await _workoutRepository.DeleteSetAsync(done.Set); }
        finally { _dbLock.Release(); }
        state.Sets.Remove(done);

        // Renumerar: las series se muestran como 1, 2, 3...
        var renumbered = state.Sets.Select((d, i) => new DoneSet(d.Set, i + 1)).ToList();
        state.Sets.Clear();
        foreach (var d in renumbered) state.Sets.Add(d);

        RecomputeRecords(state);
        state.NotifyProgress();
        OnPropertyChanged(nameof(TodayVolume));
        if (ReferenceEquals(state, CurrentExercise)) LoadSuggestion();
    }

    /// <summary>Guarda la nota de una serie ya hecha; vacía la borra.</summary>
    public async Task SetNoteAsync(DoneSet done, string? text)
    {
        var note = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (note == done.Set.Notes) return;

        var before = done.Set.Notes;
        done.Set.Notes = note;
        await _dbLock.WaitAsync();
        try { await _workoutRepository.UpdateSetAsync(done.Set); }
        catch
        {
            done.Set.Notes = before;
            throw;
        }
        finally { _dbLock.Release(); }
        done.NotifyNotes();
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

    // Récord frente a todo el historial previo y a las series anteriores de hoy:
    // de peso si se levantan más kilos que nunca, de reps si se hacen más repeticiones
    // que nunca con ese peso o más. Sin historial no hay récord (el primer entreno
    // marcaría todas las series).
    internal static void RecomputeRecords(WorkoutExerciseState state)
    {
        var seen = state.History.BestRepsByWeight.ToList();

        foreach (var done in state.Sets)
        {
            var (weight, reps) = (done.Set.Weight, done.Set.Reps);
            done.Record = TrainingStats.RecordOf(seen, weight, reps);

            if (state.History.HasData)
                seen.Add(new PreviousSet(weight, reps));
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
            var discard = await ConfirmDialogService.ConfirmAsync("Terminar entreno", "No has registrado ninguna serie. ¿Descartar el entreno?", accept: "Descartar", destructive: true);
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
            .Select(g => g.OrderByDescending(x => x.d.Record == RecordKind.Weight).ThenByDescending(x => x.d.Set.Weight).ThenByDescending(x => x.d.Set.Reps).First())
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
        if (CurrentSession is not { } session || !CanSave || !IsFinishing) return;
        IsFinishing = false; // un doble toque no guarda dos veces

        var minutes = (int)Math.Round((_clock.GetLocalNow().DateTime - session.Date).TotalMinutes);
        session.DurationMinutes = Math.Max(1, minutes);
        session.Feeling = Feeling;
        session.Notes = string.IsNullOrWhiteSpace(FinishNotes) ? null : FinishNotes.Trim();
        session.IsComplete = !IsIncomplete;
        await _dbLock.WaitAsync();
        try { await _workoutRepository.UpdateSessionAsync(session); }
        finally { _dbLock.Release(); }

        ResetSession();
        NotificationService.Success("Entreno guardado.");
    }

    [RelayCommand]
    private async Task DiscardWorkout()
    {
        var confirmed = await ConfirmDialogService.ConfirmAsync("Descartar entreno", "Se borrarán las series de este entreno.", accept: "Descartar", destructive: true);
        if (confirmed) await DiscardAsync();
    }

    public async Task DiscardAsync()
    {
        if (CurrentSession is not { } session) return;
        CurrentSession = null; // un doble toque no borra dos veces
        await _dbLock.WaitAsync();
        try { await _workoutRepository.DeleteSessionAsync(session); }
        finally { _dbLock.Release(); }
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
        WorkoutStateService.ActiveSessionId = null;
        _dayOptionsLoad = LoadRoutinesAsync(); // cambian la activa, «Toca» y «Hace N días»
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

    /// <summary>Es el día que toca según la semana de la rutina.</summary>
    public bool IsNext { get; }

    [ObservableProperty] private bool _isSelected;

    public WorkoutDayOption(RoutineDay day, string lastDoneText, bool isNext = false)
    {
        Day = day;
        LastDoneText = lastDoneText;
        IsNext = isNext;
    }

    public string Name => Day.Name;
}
