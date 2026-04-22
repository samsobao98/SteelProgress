using System.Windows.Controls;
using SteelProgress.App.ViewModels;
using SteelProgress.Data.Repositories;

namespace SteelProgress.App.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();

        var repo = new WorkoutRepository(App.DbContext);
        DataContext = new HistoryViewModel(repo);
    }
}