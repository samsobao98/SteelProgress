using System.Windows;
using SteelProgress.App.ViewModels;
using SteelProgress.Data.Repositories;

namespace SteelProgress.App;

public partial class HistoryWindow : Window
{
    public HistoryWindow()
    {
        InitializeComponent();

        var repo = new WorkoutRepository(App.DbContext);
        DataContext = new HistoryViewModel(repo);
    }
}