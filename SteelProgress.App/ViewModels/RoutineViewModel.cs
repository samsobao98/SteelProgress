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
        set { _selectedRoutine = value; OnPropertyChanged(); }

    }

    public ICommand AddRoutineCommand { get; }
    public ICommand DeleteRoutineCommand { get; }

    public RoutineViewModel(RoutineRepository repository)
    {
        _repository = repository;

        Routines = new ObservableCollection<Routine>();

        AddRoutineCommand = new RelayCommand(AddRoutine);
        DeleteRoutineCommand = new RelayCommand(DeleteRoutine);

        LoadRoutines();
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
    }
}
