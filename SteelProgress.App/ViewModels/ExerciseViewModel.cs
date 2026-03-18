using System.Collections.ObjectModel;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.App.ViewModels;
//Carga los ejercicios, los guarda en una ObservableCollection para enlazarla en una interfaz posteriormente
public class ExerciseViewModel : BaseViewModel
{
    private readonly ExerciseRepository _repository;

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
        }
    }

    public ExerciseViewModel(ExerciseRepository repository)
    {
        _repository = repository;
        Exercises = new ObservableCollection<Exercise>();
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
            return;

        Name = SelectedExercise.Name;
        MuscleGroup = SelectedExercise.MuscleGroup;
        Notes = SelectedExercise.Notes ?? string.Empty;
    }
}