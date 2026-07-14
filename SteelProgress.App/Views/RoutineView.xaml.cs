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
        // NOTA: la gestión de rutinas se rediseñó para MAUI (lista + detalle).
        // Esta vista WPF es legacy y sus bindings quedaron obsoletos.
        DataContext = new RoutineViewModel(routineRepository);
    }
}
