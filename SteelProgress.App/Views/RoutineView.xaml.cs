using System.Windows.Controls;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Repositories;

namespace SteelProgress.App.Views;

public partial class RoutineView : UserControl
{
    public RoutineView()
    {
        InitializeComponent();

        var routineRepository = new RoutineRepository(App.DbContext);
        var exerciseRepository = new ExerciseRepository(App.DbContext);
        DataContext = new RoutineViewModel(routineRepository, exerciseRepository);
    }
}
