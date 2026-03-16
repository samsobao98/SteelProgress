using System.Windows;
using SteelProgress.Domain.Entities;

namespace SteelProgress.App;

public partial class MainWindow : Window
{

    private Exercise? _selectedExercise;

    public MainWindow()
    {
        InitializeComponent();
        LoadExercises();
    }

    private void LoadExercises()
    {
        var exercises = App.DbContext.Exercises
            .OrderBy(e => e.Name)
            .ToList();

        DgExercises.ItemsSource = exercises;
    }

    private void DgExercises_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DgExercises.SelectedItem is not Exercise selectedExercise)
            return;

        _selectedExercise = selectedExercise;

        TxtName.Text = selectedExercise.Name;
        TxtMuscleGroup.Text = selectedExercise.MuscleGroup;
        TxtNotes.Text = selectedExercise.Notes ?? string.Empty;
    }

    private void BtnUpdateExercise_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedExercise is null)
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

        bool duplicateExists = App.DbContext.Exercises
            .Any(e => e.Name == name && e.Id != _selectedExercise.Id);

        if (duplicateExists)
        {
            MessageBox.Show("Ya existe otro ejercicio con ese nombre.",
                "Validación",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _selectedExercise.Name = name;
        _selectedExercise.MuscleGroup = muscleGroup;
        _selectedExercise.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;

        App.DbContext.SaveChanges();

        LoadExercises();
        ClearForm();
        _selectedExercise = null;

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

        bool exists = App.DbContext.Exercises.Any(e => e.Name == name);

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

        App.DbContext.Exercises.Add(exercise);
        App.DbContext.SaveChanges();

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

        App.DbContext.Exercises.Remove(selectedExercise);
        App.DbContext.SaveChanges();

        LoadExercises();
    }
    private void ClearForm()
    {
        TxtName.Text = string.Empty;
        TxtMuscleGroup.Text = string.Empty;
        TxtNotes.Text = string.Empty;
        DgExercises.SelectedItem = null;
    }
}