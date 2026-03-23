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

    private void LoadExercises()
    {
        _viewModel.LoadExercises();
        DgExercises.ItemsSource = _viewModel.Exercises;
    }
}