using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.ViewModels;

/// <summary>
/// Inicio: qué toca hoy (y empezarlo con un toque), la semana, la racha, los récords del mes
/// y las últimas sesiones. Muestra los datos del perfil y cliente activos, como Entreno.
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly WorkoutRepository _workoutRepository;
    private readonly RoutineRepository _routineRepository;
    private readonly TimeProvider _clock;

    // Cada repositorio tiene su DbContext, que no admite dos operaciones a la vez:
    // dos recargas seguidas (aparecer + cambio de cliente) se ponen en cola
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    [ObservableProperty] private string _profileName = "";
    [ObservableProperty] private string _profileColor = "#89B4FA";
    [ObservableProperty] private string _profileInitial = "?";
    [ObservableProperty] private string _greeting = "Hola";

    [ObservableProperty] private IReadOnlyList<WeekDayCell> _week = Array.Empty<WeekDayCell>();
    [ObservableProperty] private int _sessionCount;
    [ObservableProperty] private int _weekStreak;
    [ObservableProperty] private int _monthRecords;

    // ── Hoy toca ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowToday), nameof(ShowActiveWorkout), nameof(ShowNoRoutines))]
    private bool _isLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowToday), nameof(ShowNoRoutines))]
    private bool _hasRoutines;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowToday), nameof(ShowActiveWorkout), nameof(ShowNoRoutines))]
    private bool _isWorkoutActive;
    [ObservableProperty] private string _todayRoutineText = "";
    [ObservableProperty] private string _todayDayName = "";
    [ObservableProperty] private string _todayDetail = "";
    [ObservableProperty] private IReadOnlyList<string> _todayExercises = Array.Empty<string>();
    [ObservableProperty] private string _todayMoreText = "";
    [ObservableProperty] private int? _todayDayId;
    [ObservableProperty] private bool _canStartToday;

    // Si hoy ya se entrenó, la tarjeta pasa a «Siguiente» y lo dice arriba
    [ObservableProperty] private string _todayLabel = "Hoy toca";
    [ObservableProperty] private string _todayDoneText = "";
    public bool HasTodayDone => TodayDoneText.Length > 0;
    partial void OnTodayDoneTextChanged(string value) => OnPropertyChanged(nameof(HasTodayDone));

    // La tarjeta de arriba: lo que toca, el entreno en curso o crear la primera rutina
    public bool ShowToday => IsLoaded && HasRoutines && !IsWorkoutActive;
    public bool ShowActiveWorkout => IsLoaded && IsWorkoutActive;
    public bool ShowNoRoutines => IsLoaded && !HasRoutines && !IsWorkoutActive;

    public bool HasTodayMore => TodayMoreText.Length > 0;
    public bool HasSessions => RecentSessions.Count > 0;
    public string StartTodayText => TodayDayName.Length > 0 ? $"▶ Empezar {TodayDayName}" : "▶ Empezar";

    public ObservableCollection<RecentSessionItem> RecentSessions { get; } = new();

    public HomeViewModel(AppDbContext dbContext, ICurrentUserService currentUser,
        WorkoutRepository workoutRepository, RoutineRepository routineRepository, TimeProvider? clock = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _workoutRepository = workoutRepository;
        _routineRepository = routineRepository;
        _clock = clock ?? TimeProvider.System;
    }

    partial void OnTodayMoreTextChanged(string value) => OnPropertyChanged(nameof(HasTodayMore));
    partial void OnTodayDayNameChanged(string value) => OnPropertyChanged(nameof(StartTodayText));

    /// <summary>La página lo llama al aparecer.</summary>
    public async Task RefreshAsync()
    {
        await _dbLock.WaitAsync();
        try { await LoadAsync(); }
        finally { _dbLock.Release(); }
    }

    /// <summary>
    /// Pide a Entreno que empiece el día que toca. Devuelve false si no hay nada que empezar;
    /// la página cambia de pestaña después.
    /// </summary>
    public bool RequestStartToday()
    {
        if (IsWorkoutActive) return true; // «Continuar»: basta con ir a Entreno
        if (!CanStartToday || TodayDayId is not int dayId) return false;
        WorkoutLaunch.Request(dayId);
        return true;
    }

    private async Task LoadAsync()
    {
        var now = _clock.GetLocalNow().DateTime;
        var today = now.Date;
        Greeting = now.Hour switch
        {
            >= 5 and < 12 => "Buenos días",
            >= 12 and < 20 => "Buenas tardes",
            _ => "Buenas noches"
        };

        // Sin seguimiento: un cambio de nombre o color en Ajustes (otro contexto) se ve al volver
        if (_currentUser.CurrentUserId is int userId
            && await _dbContext.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == userId) is UserProfile profile)
        {
            ProfileName = profile.Name;
            ProfileColor = profile.AvatarColor;
            ProfileInitial = string.IsNullOrEmpty(profile.Name) ? "?" : profile.Name[0].ToString().ToUpper();
        }

        // Los filtros globales del contexto ya limitan todo al perfil y cliente activos
        var log = await _workoutRepository.GetTrainingLogAsync();
        Week = TrainingStats.Week(log.Sessions, today);
        SessionCount = log.Sessions.Count;
        WeekStreak = TrainingStats.WeekStreak(log.Sessions, today);
        MonthRecords = TrainingStats.RecordsThisMonth(TrainingStats.Records(log.Sets), today);

        var volumes = TrainingStats.SessionVolumes(log.Sets);
        RecentSessions.Clear();
        foreach (var s in log.Sessions.Take(3))
            RecentSessions.Add(new RecentSessionItem(s, volumes.GetValueOrDefault(s.Id), today));
        OnPropertyChanged(nameof(HasSessions));

        await LoadTodayAsync(log, today);
        IsLoaded = true;
    }

    private async Task LoadTodayAsync(TrainingLog log, DateTime today)
    {
        IsWorkoutActive = WorkoutStateService.IsWorkoutActive;
        var doneToday = log.Sessions.Where(s => s.Date.Date == today).Select(s => s.DayName).Distinct().ToList();
        TodayLabel = doneToday.Count > 0 ? "Siguiente" : "Hoy toca";
        TodayDoneText = doneToday.Count > 0 ? $"✓ Hoy ya has entrenado {string.Join(" y ", doneToday)}" : "";

        // La rutina activa: la marcada a mano; si no hay, la última entrenada (como Entreno)
        _routineRepository.ForgetLoaded();
        var routines = await _routineRepository.GetAllAsync();
        HasRoutines = routines.Count > 0;
        var activeId = await _routineRepository.GetMarkedActiveRoutineIdAsync()
                       ?? await _workoutRepository.GetLastTrainedRoutineIdAsync();
        var routine = routines.FirstOrDefault(r => r.Id == activeId) ?? routines.FirstOrDefault();

        RoutineDay? next = null;
        List<RoutineDayExercise> exercises = new();
        if (routine is not null)
        {
            var week = await _routineRepository.GetRoutineWeekAsync(routine.Id, RoutineSchedule.WeekStart(today));
            next = RoutineSchedule.NextDay(week);
            if (next is not null)
                exercises = await _routineRepository.GetExercisesByDayIdAsync(next.Id);
        }

        TodayRoutineText = routine is null ? "" : routine.IsMarkedActive || routine.Id == activeId ? $"{routine.Name} · activa" : routine.Name;
        TodayDayName = next?.Name ?? "";
        TodayDayId = next?.Id;
        CanStartToday = next is not null && exercises.Count > 0;

        const int shown = 2;
        TodayExercises = exercises.Take(shown).Select(e => e.Exercise?.Name ?? "Ejercicio").ToList();
        TodayMoreText = exercises.Count > shown ? $"+{exercises.Count - shown}" : "";

        if (next is null)
        {
            TodayDetail = routine is null ? "" : "Esta rutina no tiene días todavía.";
            return;
        }

        var parts = new List<string> { exercises.Count == 1 ? "1 ejercicio" : $"{exercises.Count} ejercicios" };
        var daySessions = log.Sessions.Where(s => s.RoutineDayId == next.Id).ToList();
        var durations = daySessions.Where(s => s.DurationMinutes is > 0).Select(s => s.DurationMinutes!.Value).ToList();
        if (durations.Count > 0)
            parts.Add($"~{(int)Math.Round(durations.Average())} min");
        parts.Add(RoutineSchedule.LastTrainedText(daySessions.FirstOrDefault()?.Date, today));
        TodayDetail = string.Join(" · ", parts);
    }
}
