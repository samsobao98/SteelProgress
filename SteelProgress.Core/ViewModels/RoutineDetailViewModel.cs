using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

/// <summary>
/// Detalle de una rutina: días como chips seleccionables y la lista
/// reordenable de ejercicios del día activo, con series/reps por ejercicio.
/// </summary>
public partial class RoutineDetailViewModel : ObservableObject
{
    private readonly RoutineRepository _repository;
    private readonly ExerciseRepository _exerciseRepository;

    public Routine Routine { get; private set; } = null!;

    public ObservableCollection<RoutineDay> Days { get; } = new();
    public ObservableCollection<RoutineDayExercise> DayExercises { get; } = new();
    public ObservableCollection<Exercise> AllExercises { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDay))]
    private RoutineDay? _selectedDay;

    [ObservableProperty] private string _routineName = string.Empty;
    [ObservableProperty] private string? _routineNotes;

    // ── Overlay "nuevo día" ────────────────────────────────────────────────
    [ObservableProperty] private string _newDayName = string.Empty;

    // ── Overlay "añadir ejercicio" ─────────────────────────────────────────
    [ObservableProperty] private Exercise? _selectedExerciseToAdd;
    [ObservableProperty] private string _newSets = string.Empty;
    [ObservableProperty] private string _newReps = string.Empty;

    public bool HasNoDays => Days.Count == 0;
    public bool HasSelectedDay => SelectedDay is not null;
    public bool HasNoExercises => DayExercises.Count == 0;

    /// <summary>Se dispara cuando la rutina se elimina (la página debe volver atrás).</summary>
    public event Action? RoutineDeleted;
    /// <summary>Se dispara cuando cambian los días (para reconstruir los chips).</summary>
    public event Action? DaysChanged;
    /// <summary>Se dispara cuando cambian los ejercicios del día (para refrescar la lista).</summary>
    public event Action? ExercisesChanged;

    /// <summary>Panel para editar lo prescrito (series, reps, intensidad, tempo...).</summary>
    public PrescriptionEditorViewModel Editor { get; }

    // viewOptions es opcional para poder crear el ViewModel a mano (tests)
    public RoutineDetailViewModel(RoutineRepository repository, ExerciseRepository exerciseRepository, ViewOptionsService? viewOptions = null)
    {
        _repository = repository;
        _exerciseRepository = exerciseRepository;
        Editor = new PrescriptionEditorViewModel(repository, viewOptions ?? new ViewOptionsService());
        Editor.Saved += async () => await LoadDayExercisesAsync();
    }

    public void EditPrescription(RoutineDayExercise rde) => Editor.Open(rde);

    public async Task InitializeAsync(Routine routine)
    {
        Routine = routine;
        RoutineName = routine.Name;
        RoutineNotes = routine.Notes;
        await LoadExercisePoolAsync();
        await LoadDaysAsync();
    }

    private async Task LoadExercisePoolAsync()
    {
        var pool = await _exerciseRepository.GetAllAsync();
        AllExercises.Clear();
        foreach (var ex in pool)
            AllExercises.Add(ex);
    }

    private async Task LoadDaysAsync()
    {
        var previousId = SelectedDay?.Id;

        var days = await _repository.GetDaysByRoutineIdAsync(Routine.Id);
        Days.Clear();
        foreach (var d in days)
            Days.Add(d);

        OnPropertyChanged(nameof(HasNoDays));
        DaysChanged?.Invoke();

        // Mantiene el día activo si sigue existiendo; si no, el primero.
        SelectedDay = Days.FirstOrDefault(d => d.Id == previousId) ?? Days.FirstOrDefault();
    }

    partial void OnSelectedDayChanged(RoutineDay? value) => _ = LoadDayExercisesAsync();

    private async Task LoadDayExercisesAsync()
    {
        DayExercises.Clear();
        OnPropertyChanged(nameof(HasNoExercises));

        if (SelectedDay is null)
        {
            ExercisesChanged?.Invoke();
            return;
        }

        var list = await _repository.GetExercisesByDayIdAsync(SelectedDay.Id);
        foreach (var item in list)
            DayExercises.Add(item);

        OnPropertyChanged(nameof(HasNoExercises));
        ExercisesChanged?.Invoke();
    }

    public void SelectDay(RoutineDay day) => SelectedDay = day;

    // ── Días ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddDay()
    {
        var name = NewDayName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            NotificationService.Error("El nombre del día es obligatorio");
            return;
        }

        var day = new RoutineDay
        {
            RoutineId = Routine.Id,
            Name = name,
            Order = Days.Count + 1
        };

        await _repository.AddDayAsync(day);
        NewDayName = string.Empty;
        await LoadDaysAsync();
        SelectedDay = Days.FirstOrDefault(d => d.Id == day.Id) ?? Days.LastOrDefault();
    }

    public async Task RenameDayAsync(RoutineDay day, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        day.Name = newName.Trim();
        await _repository.UpdateDayAsync(day);
        await LoadDaysAsync();
    }

    public async Task DeleteDayAsync(RoutineDay day)
    {
        await _repository.DeleteDayAsync(day);
        await LoadDaysAsync();
    }

    // ── Ejercicios del día ────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddExerciseToDay()
    {
        if (SelectedDay is null)
        {
            NotificationService.Error("Selecciona un día");
            return;
        }
        if (SelectedExerciseToAdd is null)
        {
            NotificationService.Error("Selecciona un ejercicio");
            return;
        }

        var nextOrder = DayExercises.Any() ? DayExercises.Max(e => e.Order) + 1 : 1;
        var rde = new RoutineDayExercise
        {
            RoutineDayId = SelectedDay.Id,
            ExerciseId = SelectedExerciseToAdd.Id,
            Order = nextOrder,
            TargetSets = ParseNullableInt(NewSets),
            TargetReps = ParseNullableInt(NewReps)
        };

        await _repository.AddExerciseToDayAsync(rde);
        SelectedExerciseToAdd = null;
        NewSets = string.Empty;
        NewReps = string.Empty;
        await LoadDayExercisesAsync();
    }

    public async Task DeleteExerciseAsync(RoutineDayExercise rde)
    {
        await _repository.DeleteExerciseFromDayAsync(rde);
        await LoadDayExercisesAsync();
        await PersistReorderAsync();
    }

    /// <summary>Guarda el orden actual de <see cref="DayExercises"/> tras un drag.</summary>
    public async Task PersistReorderAsync()
    {
        if (DayExercises.Count == 0) return;
        await _repository.ReorderDayExercisesAsync(DayExercises.ToList());
    }

    // ── Rutina ────────────────────────────────────────────────────────────

    public async Task RenameRoutineAsync(string newName, string? notes)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        Routine.Name = newName.Trim();
        Routine.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes!.Trim();
        await _repository.UpdateAsync(Routine);
        RoutineName = Routine.Name;
        RoutineNotes = Routine.Notes;
    }

    public async Task DeleteRoutineAsync()
    {
        await _repository.DeleteAsync(Routine);
        RoutineDeleted?.Invoke();
    }

    private static int? ParseNullableInt(string? s)
        => int.TryParse(s?.Trim(), out var v) && v > 0 ? v : null;
}
