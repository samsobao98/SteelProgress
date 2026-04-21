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

        DataContext = _viewModel;


        LoadExercises();
    }

    private void OpenHistoryWindow_Click(object sender, RoutedEventArgs e)
    {
        new HistoryWindow().Show();
    }

    private void OpenWorkoutWindow_Click(object sender, RoutedEventArgs e)
    {
        var sessionId = App.DbContext.WorkoutSessions
            .OrderByDescending(ws => ws.Id)
            .Select(ws => ws.Id)
            .FirstOrDefault();

        if (sessionId == 0)
        {
            MessageBox.Show("No hay sesiones de entrenamiento creadas.");
            return;
        }

        new WorkoutWindow(sessionId).Show();
    }

    private void OpenRoutineWindow_Click(object sender, RoutedEventArgs e)
    {
        new RoutineWindow().Show();
    }

    private void LoadExercises()
    {
        _viewModel.LoadExercises();
        DgExercises.ItemsSource = _viewModel.Exercises;
    }

    private void OpenProgressWindow_Click(object sender, RoutedEventArgs e)
    {
        new ProgressWindow().Show();
    }


}