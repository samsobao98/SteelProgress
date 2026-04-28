using System.Windows.Controls;
using SteelProgress.App.ViewModels;
using SteelProgress.Data.Repositories;

namespace SteelProgress.App.Views;

public partial class ExerciseView : UserControl
{
    public ExerciseView()
    {
        InitializeComponent();

        var repository = new ExerciseRepository(App.DbContext);
        var viewModel = new ExerciseViewModel(repository);

        viewModel.LoadExercises();

        DataContext = viewModel;
    }
}