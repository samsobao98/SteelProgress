using SteelProgress.App.ViewModels;
using SteelProgress.Data.Repositories;
using System.Windows;

namespace SteelProgress.App;

public partial class WorkoutWindow : Window
{
    public WorkoutWindow(int sessionId)
    {
        InitializeComponent();

        var repo = new WorkoutRepository(App.DbContext);
        var vm = new WorkoutViewModel(repo);

        DataContext = vm;

        vm.LoadSession(sessionId);
      
    }
}