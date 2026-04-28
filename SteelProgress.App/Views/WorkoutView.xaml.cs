using System.Windows.Controls;
using SteelProgress.App.ViewModels;
using SteelProgress.Data.Repositories;

namespace SteelProgress.App.Views;

public partial class WorkoutView : UserControl
{
    public WorkoutView()
    {
        InitializeComponent();

        var workoutRepository = new WorkoutRepository(App.DbContext);
        var routineRepository = new RoutineRepository(App.DbContext);

        DataContext = new WorkoutViewModel(workoutRepository, routineRepository);
    }
}