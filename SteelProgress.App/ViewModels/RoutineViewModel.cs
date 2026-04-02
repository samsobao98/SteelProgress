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

        LoadRoutines();
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
