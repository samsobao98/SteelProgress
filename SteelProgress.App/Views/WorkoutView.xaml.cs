using System.Linq;
using System.Windows.Controls;
using SteelProgress.App.ViewModels;
using SteelProgress.Data.Repositories;

namespace SteelProgress.App.Views;

public partial class WorkoutView : UserControl
{
    public WorkoutView()
    {
        InitializeComponent();

        var sessionId = App.DbContext.WorkoutSessions
            .OrderByDescending(ws => ws.Id)
            .Select(ws => ws.Id)
            .FirstOrDefault();

        if (sessionId == 0)
            return;

        var repo = new WorkoutRepository(App.DbContext);
        var vm = new WorkoutViewModel(repo);

        DataContext = vm;
        vm.LoadSession(sessionId);
    }
}