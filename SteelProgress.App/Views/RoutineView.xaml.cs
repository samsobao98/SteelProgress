using System.Windows.Controls;
using SteelProgress.App.ViewModels;
using SteelProgress.Data.Repositories;

namespace SteelProgress.App.Views;

public partial class RoutineView : UserControl
{
    public RoutineView()
    {
        InitializeComponent();

        var repository = new RoutineRepository(App.DbContext);
        DataContext = new RoutineViewModel(repository);
    }
}