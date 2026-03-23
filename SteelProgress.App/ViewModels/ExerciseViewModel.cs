using System.Collections.ObjectModel;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;
using System.Windows;
using System.Windows.Input;
using SteelProgress.App.Commands;

namespace SteelProgress.App.ViewModels;
//Carga los ejercicios, los guarda en una ObservableCollection para enlazarla en una interfaz posteriormente
public class ExerciseViewModel : BaseViewModel
{
    private readonly ExerciseRepository _repository;
    public ICommand AddExerciseCommand { get; }
    public ICommand UpdateExerciseCommand { get; }
    public ICommand DeleteExerciseCommand { get; }
    public ObservableCollection<Exercise> Exercises { get; set; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    private string _muscleGroup = string.Empty;
    public string MuscleGroup
    {
        get => _muscleGroup;
        set
        {
            _muscleGroup = value;
            OnPropertyChanged();
        }
    }

    private string _notes = string.Empty;
    public string Notes
    {
        get => _notes;
        set
        {
            _notes = value;
            OnPropertyChanged();
        }
    }

    private Exercise? _selectedExercise;
    public Exercise? SelectedExercise
    {
        get => _selectedExercise;
        set
        {
            _selectedExercise = value;
            OnPropertyChanged();
            LoadSelectedExerciseIntoForm();
        }
    }

    public ExerciseViewModel(ExerciseRepository repository)
    {
        _repository = repository;
        Exercises = new ObservableCollection<Exercise>();
        AddExerciseCommand = new RelayCommand(AddExercise);
        UpdateExerciseCommand = new RelayCommand(UpdateExercise);
        DeleteExerciseCommand = new RelayCommand(DeleteExercise);
    }

    public void LoadExercises()
    {
        var exercises = _repository.GetAllAsync().Result;

        Exercises.Clear();

        foreach (var exercise in exercises)
        {
            Exercises.Add(exercise);
        }
    }

    public void ClearForm()
    {
        Name = string.Empty;
        MuscleGroup = string.Empty;
        Notes = string.Empty;
        SelectedExercise = null;
    }

    public void LoadSelectedExerciseIntoForm()
    {
        if (SelectedExercise is null)
        {
            Name = string.Empty;
            MuscleGroup = string.Empty;
            Notes = string.Empty;
            return;
        }

        Name = SelectedExercise.Name;
        MuscleGroup = SelectedExercise.MuscleGroup;
        Notes = SelectedExercise.Notes ?? string.Empty;
    }

    private void AddExercise()
    {
        string name = Name.Trim();
        string muscleGroup = MuscleGroup.Trim();
        string notes = Notes.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("El nombre del ejercicio es obligatorio.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(muscleGroup))
        {
            MessageBox.Show("El grupo muscular es obligatorio.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool exists = _repository.ExistsByNameAsync(name).Result;

        if (exists)
        {
            MessageBox.Show("Ya existe un ejercicio con ese nombre.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var exercise = new Exercise
        {
            Name = name,
            MuscleGroup = muscleGroup,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
        };

        _repository.AddAsync(exercise).Wait();
        LoadExercises();
        ClearForm();

        MessageBox.Show("Ejercicio añadido correctamente.", "Información",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void UpdateExercise()
    {
        if (SelectedExercise is null)
        {
            MessageBox.Show("Selecciona un ejercicio para actualizar.", "Información",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string name = Name.Trim();
        string muscleGroup = MuscleGroup.Trim();
        string notes = Notes.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("El nombre del ejercicio es obligatorio.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(muscleGroup))
        {
            MessageBox.Show("El grupo muscular es obligatorio.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool duplicateExists = _repository
            .ExistsByNameExceptIdAsync(name, SelectedExercise.Id)
            .Result;

        if (duplicateExists)
        {
            MessageBox.Show("Ya existe otro ejercicio con ese nombre.", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedExercise.Name = name;
        SelectedExercise.MuscleGroup = muscleGroup;
        SelectedExercise.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;

        _repository.UpdateAsync(SelectedExercise).Wait();
        LoadExercises();
        ClearForm();

        MessageBox.Show("Ejercicio actualizado correctamente.", "Información",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteExercise()
    {
        if (SelectedExercise is null)
        {
            MessageBox.Show("Selecciona un ejercicio para eliminar.",
                "Información",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"¿Seguro que quieres eliminar '{SelectedExercise.Name}'?",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _repository.DeleteAsync(SelectedExercise).Wait();
        LoadExercises();
        ClearForm();
    }
}