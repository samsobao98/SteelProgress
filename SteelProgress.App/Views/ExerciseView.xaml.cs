using System.Windows.Controls;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Repositories;

namespace SteelProgress.App.Views;

public partial class ExerciseView : UserControl
{
    public ExerciseView()
    {
        InitializeComponent();

        var repository = new ExerciseRepository(App.DbContext);
        DataContext = new ExerciseViewModel(repository);
    }
}
