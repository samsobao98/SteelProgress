using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

public partial class RoutineViewModel : ObservableObject
{
    private readonly RoutineRepository _repository;
    private readonly ExerciseRepository _exerciseRepository;

    public ObservableCollection<Routine> Routines { get; } = new();
    public ObservableCollection<RoutineDay> RoutineDays { get; } = new();
    public ObservableCollection<RoutineDayExercise> DayExercises { get; } = new();
    public ObservableCollection<Exercise> AllExercises { get; } = new();

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _dayName = string.Empty;
    [ObservableProperty] private Exercise? _selectedExerciseToAdd;
    [ObservableProperty] private RoutineDayExercise? _selectedDayExercise;

    private Routine? _selectedRoutine;
    public Routine? SelectedRoutine
    {
        get => _selectedRoutine;
        set
        {
            _selectedRoutine = value;
            OnPropertyChanged();
            _ = LoadRoutineDaysAsync();
        }
    }

    private RoutineDay? _selectedRoutineDay;
    public RoutineDay? SelectedRoutineDay
    {
        get => _selectedRoutineDay;
        set
        {
            _selectedRoutineDay = value;
            OnPropertyChanged();
            _ = LoadDayExercisesAsync();
        }
    }

    public RoutineViewModel(RoutineRepository repository, ExerciseRepository exerciseRepository)
    {
        _repository = repository;
        _exerciseRepository = exerciseRepository;
        _ = InitializeAsync();
    }

    public Task RefreshAsync() => InitializeAsync();

    private async Task InitializeAsync()
    {
        await LoadAllExercisesAsync();
        await LoadRoutinesAsync();
    }

    private async Task LoadAllExercisesAsync()
    {
        var exercises = await _exerciseRepository.GetAllAsync();
        AllExercises.Clear();
        foreach (var ex in exercises)
            AllExercises.Add(ex);
    }

    private async Task LoadRoutinesAsync()
    {
        var routines = await _repository.GetAllAsync();
        Routines.Clear();
        foreach (var r in routines)
            Routines.Add(r);
    }

    private async Task LoadRoutineDaysAsync()
    {
        RoutineDays.Clear();

        if (SelectedRoutine is null)
            return;

        var days = await _repository.GetDaysByRoutineIdAsync(SelectedRoutine.Id);
        foreach (var day in days)
            RoutineDays.Add(day);
    }

    private async Task LoadDayExercisesAsync()
    {
        DayExercises.Clear();

        if (SelectedRoutineDay is null)
            return;

        var list = await _repository.GetExercisesByDayIdAsync(SelectedRoutineDay.Id);
        foreach (var item in list)
            DayExercises.Add(item);
    }

    [RelayCommand]
    private async Task AddRoutine()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            NotificationService.Error("El nombre es obligatorio");
            return;
        }

        var routine = new Routine { Name = Name, Notes = Notes };

        await _repository.AddAsync(routine);
        await LoadRoutinesAsync();
        ClearForm();
    }

    [RelayCommand]
    private async Task DeleteRoutine()
    {
        if (SelectedRoutine is null)
        {
            NotificationService.Error("Selecciona una rutina");
            return;
        }

        var confirmed = await ConfirmDialogService
            .ConfirmAsync("Eliminar rutina", $"¿Seguro que quieres eliminar la rutina {SelectedRoutine.Name}?");

        if (!confirmed)
            return;

        await _repository.DeleteAsync(SelectedRoutine);
        await LoadRoutinesAsync();
        ClearForm();
    }

    [RelayCommand]
    private async Task AddDay()
    {
        if (SelectedRoutine is null)
        {
            NotificationService.Error("Selecciona una rutina antes de añadir un día");
            return;
        }

        if (string.IsNullOrWhiteSpace(DayName))
        {
            NotificationService.Error("El nombre del día es obligatorio");
            return;
        }

        var routineDay = new RoutineDay
        {
            RoutineId = SelectedRoutine.Id,
            Name = DayName.Trim(),
            Order = RoutineDays.Count + 1
        };

        await _repository.AddDayAsync(routineDay);
        await LoadRoutineDaysAsync();
        DayName = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteDay()
    {
        if (SelectedRoutineDay is null)
        {
            NotificationService.Error("Selecciona un día para eliminar.");
            return;
        }

        var confirmed = await ConfirmDialogService
            .ConfirmAsync("Eliminar día", $"¿Seguro que quieres eliminar el día {SelectedRoutineDay.Name}?");

        if (!confirmed)
            return;

        await _repository.DeleteDayAsync(SelectedRoutineDay);
        await LoadRoutineDaysAsync();
        SelectedRoutineDay = null;
    }

    [RelayCommand]
    private async Task AddExerciseToDay()
    {
        if (SelectedRoutineDay is null || SelectedExerciseToAdd is null)
        {
            NotificationService.Error("Selecciona un día y un ejercicio.");
            return;
        }

        var nextOrder = DayExercises.Any() ? DayExercises.Max(e => e.Order) + 1 : 1;

        var dayExercise = new RoutineDayExercise
        {
            RoutineDayId = SelectedRoutineDay.Id,
            ExerciseId = SelectedExerciseToAdd.Id,
            Order = nextOrder
        };

        await _repository.AddExerciseToDayAsync(dayExercise);
        await LoadDayExercisesAsync();
    }

    [RelayCommand]
    private async Task DeleteExerciseFromDay()
    {
        if (SelectedDayExercise is null)
        {
            NotificationService.Error("Selecciona un ejercicio.");
            return;
        }

        var confirmed = await ConfirmDialogService
            .ConfirmAsync("Eliminar ejercicio", "¿Seguro que quieres eliminar este ejercicio?");

        if (!confirmed)
            return;

        await _repository.DeleteExerciseFromDayAsync(SelectedDayExercise);
        await ReorderDayExercisesAsync();
        await LoadDayExercisesAsync();
    }

    [RelayCommand]
    private async Task MoveExerciseUp()
    {
        if (SelectedDayExercise is null)
            return;

        var currentIndex = DayExercises.IndexOf(SelectedDayExercise);
        if (currentIndex <= 0)
            return;

        await SwapExerciseOrderAsync(SelectedDayExercise, DayExercises[currentIndex - 1]);
    }

    [RelayCommand]
    private async Task MoveExerciseDown()
    {
        if (SelectedDayExercise is null)
            return;

        var currentIndex = DayExercises.IndexOf(SelectedDayExercise);
        if (currentIndex < 0 || currentIndex >= DayExercises.Count - 1)
            return;

        await SwapExerciseOrderAsync(SelectedDayExercise, DayExercises[currentIndex + 1]);
    }

    private async Task SwapExerciseOrderAsync(RoutineDayExercise first, RoutineDayExercise second)
    {
        int tempOrder = first.Order;
        first.Order = second.Order;
        second.Order = tempOrder;

        _repository.UpdateRoutineDayExercise(first);
        _repository.UpdateRoutineDayExercise(second);

        var previousId = first.Id;
        await LoadDayExercisesAsync();
        SelectedDayExercise = DayExercises.FirstOrDefault(e => e.Id == previousId);
    }

    private async Task ReorderDayExercisesAsync()
    {
        if (SelectedRoutineDay is null)
            return;

        var exercises = (await _repository.GetExercisesByDayIdAsync(SelectedRoutineDay.Id))
            .OrderBy(e => e.Order)
            .ToList();

        for (int i = 0; i < exercises.Count; i++)
        {
            exercises[i].Order = i + 1;
            _repository.UpdateRoutineDayExercise(exercises[i]);
        }
    }

    private void ClearForm()
    {
        Name = string.Empty;
        Notes = string.Empty;
        SelectedRoutine = null;
        SelectedRoutineDay = null;
        RoutineDays.Clear();
    }
}
