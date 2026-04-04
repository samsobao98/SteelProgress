using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using SteelProgress.App.Commands;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;


namespace SteelProgress.App.ViewModels;

public class RoutineViewModel : BaseViewModel
{

    public ObservableCollection<RoutineDay> RoutineDays { get; set; }
    private string _dayName = string.Empty;
    public string DayName
    {
        get => _dayName;
        set
        {
            _dayName = value;
            OnPropertyChanged();
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
            LoadDayExercises();
        }
    }

    public ICommand AddDayCommand { get; }
    public ICommand DeleteDayCommand { get; }

    private readonly RoutineRepository _repository;

    public ObservableCollection<Routine> Routines { get; set; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    private string _notes = string.Empty;
    public string Notes
    {
        get => _notes;
        set { _notes = value; OnPropertyChanged(); }
    }

    private Routine? _selectedRoutine;
    public Routine? SelectedRoutine
    {
        get => _selectedRoutine;
        set
        {
            _selectedRoutine = value;
            OnPropertyChanged();
            LoadRoutineDays();
        }

    }
    public ObservableCollection<RoutineDayExercise> DayExercises { get; set; }

    public ObservableCollection<Exercise> AllExercises { get; set; }

    private Exercise? _selectedExerciseToAdd;
    public Exercise? SelectedExerciseToAdd
    {
        get => _selectedExerciseToAdd;
        set { _selectedExerciseToAdd = value; OnPropertyChanged(); }
    }

    private RoutineDayExercise? _selectedDayExercise;
    public RoutineDayExercise? SelectedDayExercise
    {
        get => _selectedDayExercise;
        set { _selectedDayExercise = value; OnPropertyChanged(); }
    }


    public ICommand AddExerciseToDayCommand { get; }
    public ICommand DeleteExerciseFromDayCommand { get; }
    public ICommand AddRoutineCommand { get; }
    public ICommand DeleteRoutineCommand { get; }

    public RoutineViewModel(RoutineRepository repository)
    {
        _repository = repository;

        Routines = new ObservableCollection<Routine>();

        AddRoutineCommand = new RelayCommand(AddRoutine);
        DeleteRoutineCommand = new RelayCommand(DeleteRoutine);

        RoutineDays = new ObservableCollection<RoutineDay>();

        AddDayCommand = new RelayCommand(AddDay);
        DeleteDayCommand = new RelayCommand(DeleteDay);

        DayExercises = new ObservableCollection<RoutineDayExercise>();
        AllExercises = new ObservableCollection<Exercise>();

        AddExerciseToDayCommand = new RelayCommand(AddExerciseToDay);
        DeleteExerciseFromDayCommand = new RelayCommand(DeleteExerciseFromDay);

        LoadAllExercises();

        LoadRoutines();
    }

    public void LoadDayExercises()
    {
        DayExercises.Clear();

        if (SelectedRoutineDay is null)
            return;

        var list = _repository
            .GetExercisesByDayIdAsync(SelectedRoutineDay.Id)
            .Result;

        foreach (var item in list)
            DayExercises.Add(item);
    }

    private void AddExerciseToDay()
    {
        if (SelectedRoutineDay is null || SelectedExerciseToAdd is null)
        {
            MessageBox.Show("Selecciona un día y un ejercicio.");
            return;
        }

        int order = DayExercises.Count + 1;

        var rde = new RoutineDayExercise
        {
            RoutineDayId = SelectedRoutineDay.Id,
            ExerciseId = SelectedExerciseToAdd.Id,
            Order = order
        };

        _repository.AddExerciseToDayAsync(rde).Wait();

        LoadDayExercises();
    }

    private void DeleteExerciseFromDay()
    {
        if (SelectedDayExercise is null)
        {
            MessageBox.Show("Selecciona un ejercicio.");
            return;
        }

        _repository.DeleteExerciseFromDayAsync(SelectedDayExercise).Wait();

        LoadDayExercises();
    }

    private void LoadAllExercises()
    {
        var exercises = App.DbContext.Exercises.ToList();

        AllExercises.Clear();

        foreach (var ex in exercises)
            AllExercises.Add(ex);
    }

    public void LoadRoutineDays()
    {
        RoutineDays.Clear();

        if (SelectedRoutine is null)
        {
            return;
        }

        var days = _repository.GetDaysByRoutineIdAsync(SelectedRoutine.Id).Result;

        foreach (var day in days)
        {
            RoutineDays.Add(day);
        }
    }

    private void AddDay()
    {
        if (SelectedRoutine is null)
        {
            MessageBox.Show("Selecciona una rutina antes de añadir un día");
            return;
        }

        if (string.IsNullOrWhiteSpace(DayName))
        {
            MessageBox.Show("El nombre del día es obligatorio");
            return;
        }

        int nextOrder = RoutineDays.Count + 1;

        var routineDay = new RoutineDay
        {
            RoutineId = SelectedRoutine.Id,
            Name = DayName.Trim(),
            Order = nextOrder
        };

        _repository.AddDayAsync(routineDay).Wait();
        LoadRoutineDays();
        DayName = string.Empty;
    }

    private void DeleteDay()
    {
        if (SelectedRoutineDay is null)
        {
            MessageBox.Show("Selecciona un día para eliminar.");
            return;
        }

        var result = MessageBox.Show($"¿Seguro que quieres eliminar el día {SelectedRoutineDay.Name}?", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _repository.DeleteDayAsync(SelectedRoutineDay).Wait();

        LoadRoutineDays();
        SelectedRoutineDay = null;
    }

    public void LoadRoutines()
    {
        var routines = _repository.GetAllAsync().Result;

        Routines.Clear();

        foreach (var r in routines)
        {
            Routines.Add(r);
        }
    }

    private void AddRoutine()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show("El nombre es obligatorio");
            return;
        }

        var routine = new Routine
        {
            Name = Name,
            Notes = Notes
        };

        _repository.AddAsync(routine).Wait();

        LoadRoutines();
        ClearForm();
    }

    private void DeleteRoutine()
    {
        if (SelectedRoutine is null)
        {
            MessageBox.Show("Selecciona una rutina");
            return;
        }

        _repository.DeleteAsync(SelectedRoutine).Wait();

        LoadRoutines();
        ClearForm();
    }

    private void ClearForm()
    {
        Name = "";
        Notes = "";
        SelectedRoutine = null;
        SelectedRoutineDay = null;
        RoutineDays.Clear();
    }
}
