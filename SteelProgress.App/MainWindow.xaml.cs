using System.Windows;
using SteelProgress.App.ViewModels;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;


namespace SteelProgress.App;

public partial class MainWindow : Window
{

    private Exercise? _selectedExercise;
    private readonly ExerciseRepository _repository;
    private readonly ExerciseViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _repository = new ExerciseRepository(App.DbContext);
        _viewModel = new ExerciseViewModel(_repository);

        LoadExercises();
    }

    private void LoadExercises()
    {
        _viewModel.LoadExercises();
        DgExercises.ItemsSource = _viewModel.Exercises;
    }

    private void DgExercises_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DgExercises.SelectedItem is not Exercise selectedExercise)
            return;

        _viewModel.SelectedExercise = selectedExercise;
        _viewModel.LoadSelectedExerciseIntoForm();

        TxtName.Text = _viewModel.Name;
        TxtMuscleGroup.Text = _viewModel.MuscleGroup;
        TxtNotes.Text = _viewModel.Notes;
    }

    private void BtnUpdateExercise_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedExercise is null)
        {
            MessageBox.Show("Selecciona un ejercicio para actualizar.",
                "Información",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string name = TxtName.Text.Trim();
        string muscleGroup = TxtMuscleGroup.Text.Trim();
        string? notes = TxtNotes.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("El nombre del ejercicio es obligatorio.",
                "Validación",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(muscleGroup))
        {
            MessageBox.Show("El grupo muscular es obligatorio.",
                "Validación",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        bool duplicateExists = _repository
     .ExistsByNameExceptIdAsync(name, _viewModel.SelectedExercise.Id)
        .Result;

        if (duplicateExists)
        {
            MessageBox.Show("Ya existe otro ejercicio con ese nombre.",
                "Validación",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _viewModel.SelectedExercise.Name = name;
        _viewModel.SelectedExercise.MuscleGroup = muscleGroup;
        _viewModel.SelectedExercise.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;

        _repository.UpdateAsync(_viewModel.SelectedExercise).Wait();

        LoadExercises();
        ClearForm();
        _viewModel.SelectedExercise = null;

        MessageBox.Show("Ejercicio actualizado correctamente.",
            "Información",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void BtnAddExercise_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtName.Text.Trim();
        string muscleGroup = TxtMuscleGroup.Text.Trim();
        string? notes = TxtNotes.Text.Trim();

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

        ClearForm();
        LoadExercises();

        MessageBox.Show("Ejercicio añadido correctamente.", "Información",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnDeleteExercise_Click(object sender, RoutedEventArgs e)
    {
        if (DgExercises.SelectedItem is not Exercise selectedExercise)
        {
            MessageBox.Show("Selecciona un ejercicio para eliminar.",
                "Información",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"¿Seguro que quieres eliminar '{selectedExercise.Name}'?",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _repository.DeleteAsync(selectedExercise).Wait();

        LoadExercises();
    }
    private void ClearForm()
    {
        _viewModel.ClearForm();

        TxtName.Text = _viewModel.Name;
        TxtMuscleGroup.Text = _viewModel.MuscleGroup;
        TxtNotes.Text = _viewModel.Notes;
        DgExercises.SelectedItem = null;
    }
}