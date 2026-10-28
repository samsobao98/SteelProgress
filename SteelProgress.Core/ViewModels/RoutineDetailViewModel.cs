using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

/// <summary>
/// Detalle de una rutina: días como chips seleccionables y la lista
/// reordenable de ejercicios del día activo, con lo prescrito para cada uno.
/// </summary>
public partial class RoutineDetailViewModel : ObservableObject
{
    private readonly RoutineRepository _repository;
    private readonly ExerciseRepository _exerciseRepository;

    // En la app todos los repositorios comparten un DbContext: una operación cada vez
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public Routine Routine { get; private set; } = null!;

    public ObservableCollection<RoutineDay> Days { get; } = new();
    public ObservableCollection<RoutineDayExercise> DayExercises { get; } = new();

    /// <summary>Panel «Añadir ejercicio» con buscador y selección múltiple.</summary>
    public ExercisePickerViewModel Picker { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDay), nameof(AddSheetTitle))]
    private RoutineDay? _selectedDay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private string _routineName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubtitleText))]
    private string? _routineNotes;

    // ── Overlay "nuevo día" ────────────────────────────────────────────────
    [ObservableProperty] private string _newDayName = string.Empty;

    public bool HasNoDays => Days.Count == 0;
    public bool HasSelectedDay => SelectedDay is not null;
    public bool HasNoExercises => DayExercises.Count == 0;
    public string AddSheetTitle => SelectedDay is null ? "Añadir ejercicio" : $"Añadir ejercicio a {SelectedDay.Name}";

    /// <summary>"Hipertrofia · 4 días".</summary>
    public string SubtitleText
    {
        get
        {
            var days = Days.Count == 1 ? "1 día" : $"{Days.Count} días";
            return string.IsNullOrWhiteSpace(RoutineNotes) ? days : $"{RoutineNotes} · {days}";
        }
    }

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
        Editor.Saved += async () => await LockedAsync(LoadDayExercisesAsync);
    }

    public void EditPrescription(RoutineDayExercise rde) => Editor.Open(rde);

    private async Task LockedAsync(Func<Task> action)
    {
        await _dbLock.WaitAsync();
        try { await action(); }
        finally { _dbLock.Release(); }
    }

    private async Task<T> LockedAsync<T>(Func<Task<T>> action)
    {
        await _dbLock.WaitAsync();
        try { return await action(); }
        finally { _dbLock.Release(); }
    }

    public Task InitializeAsync(Routine routine) => LockedAsync(async () =>
    {
        Routine = routine;
        RoutineName = routine.Name;
        RoutineNotes = routine.Notes;
        Picker.Load(await _exerciseRepository.GetAllAsync());
        await LoadDaysAsync();
    });

    // Sin cerrojo: quien llama ya lo tiene
    private async Task LoadDaysAsync()
    {
        var previousId = SelectedDay?.Id;

        var days = await _repository.GetDaysByRoutineIdAsync(Routine.Id);
        Days.Clear();
        foreach (var d in days)
            Days.Add(d);

        OnPropertyChanged(nameof(HasNoDays));
        OnPropertyChanged(nameof(SubtitleText));
        DaysChanged?.Invoke();

        // Mantiene el día activo si sigue existiendo; si no, el primero.
        _suppressDayLoad = true;
        SelectedDay = Days.FirstOrDefault(d => d.Id == previousId) ?? Days.FirstOrDefault();
        _suppressDayLoad = false;
        await LoadDayExercisesAsync();
    }

    private bool _suppressDayLoad;

    partial void OnSelectedDayChanged(RoutineDay? value)
    {
        if (!_suppressDayLoad)
            _ = LockedAsync(LoadDayExercisesAsync);
    }

    private async Task LoadDayExercisesAsync()
    {
        var day = SelectedDay;
        var list = day is null ? new List<RoutineDayExercise>() : await _repository.GetExercisesByDayIdAsync(day.Id);
        if (!ReferenceEquals(day, SelectedDay)) return; // se eligió otro día mientras tanto

        DayExercises.Clear();
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

        await LockedAsync(async () =>
        {
            var day = new RoutineDay
            {
                RoutineId = Routine.Id,
                Name = name,
                Order = Days.Count + 1
            };

            await _repository.AddDayAsync(day);
            NewDayName = string.Empty;
            _suppressDayLoad = true;
            SelectedDay = day;
            _suppressDayLoad = false;
            await LoadDaysAsync();
        });
    }

    public Task RenameDayAsync(RoutineDay day, string newName) => LockedAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        day.Name = newName.Trim();
        await _repository.UpdateDayAsync(day);
        await LoadDaysAsync();
    });

    /// <summary>Entrenos de ese día que se borrarían con él (para avisar antes).</summary>
    public Task<int> CountSessionsOfDayAsync(RoutineDay day) => LockedAsync(() => _repository.CountSessionsOfDayAsync(day.Id));

    public Task DeleteDayAsync(RoutineDay day) => LockedAsync(async () =>
    {
        await _repository.DeleteDayAsync(day);
        await LoadDaysAsync();
    });

    // ── Ejercicios del día ────────────────────────────────────────────────

    /// <summary>Añade al día los ejercicios elegidos en el panel, en el orden en que se tocaron.</summary>
    [RelayCommand]
    private async Task AddSelectedExercises()
    {
        if (SelectedDay is not { } day)
        {
            NotificationService.Error("Selecciona un día");
            return;
        }

        var chosen = Picker.Selected;
        if (chosen.Count == 0) return;

        await LockedAsync(async () =>
        {
            var nextOrder = DayExercises.Any() ? DayExercises.Max(e => e.Order) + 1 : 1;
            foreach (var exercise in chosen)
            {
                await _repository.AddExerciseToDayAsync(new RoutineDayExercise
                {
                    RoutineDayId = day.Id,
                    ExerciseId = exercise.Id,
                    Order = nextOrder++
                });
            }
            await LoadDayExercisesAsync();
        });

        Picker.Reset();
        NotificationService.Success(chosen.Count == 1 ? "Ejercicio añadido." : $"{chosen.Count} ejercicios añadidos.");
    }

    public Task DeleteExerciseAsync(RoutineDayExercise rde) => LockedAsync(async () =>
    {
        await _repository.DeleteExerciseFromDayAsync(rde);
        await LoadDayExercisesAsync();
        if (DayExercises.Count > 0)
            await _repository.ReorderDayExercisesAsync(DayExercises.ToList());
    });

    /// <summary>Guarda el orden actual de <see cref="DayExercises"/> tras un drag.</summary>
    public Task PersistReorderAsync() => LockedAsync(async () =>
    {
        if (DayExercises.Count == 0) return;
        await _repository.ReorderDayExercisesAsync(DayExercises.ToList());
        ExercisesChanged?.Invoke(); // las etiquetas 1, 2, 3… cambian
    });

    // ── Rutina ────────────────────────────────────────────────────────────

    public Task RenameRoutineAsync(string newName, string? notes) => LockedAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        Routine.Name = newName.Trim();
        Routine.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes!.Trim();
        await _repository.UpdateAsync(Routine);
        RoutineName = Routine.Name;
        RoutineNotes = Routine.Notes;
    });

    /// <summary>Entrenos de la rutina que se borrarían con ella (para avisar antes).</summary>
    public Task<int> CountSessionsOfRoutineAsync() => LockedAsync(() => _repository.CountSessionsOfRoutineAsync(Routine.Id));

    public async Task DeleteRoutineAsync()
    {
        await LockedAsync(() => _repository.DeleteAsync(Routine));
        RoutineDeleted?.Invoke();
    }
}
