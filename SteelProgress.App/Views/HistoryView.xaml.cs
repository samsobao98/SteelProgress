using System.Windows;
using System.Windows.Controls;
using SteelProgress.App.ViewModels;

namespace SteelProgress.App.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
        DataContext = new HistoryViewModel();
    }

    private void OpenSelectedExerciseProgress_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HistoryViewModel viewModel ||
            viewModel.SelectedExerciseSummary is null)
        {
            MessageBox.Show("Selecciona un ejercicio del historial.");
            return;
        }

        var mainWindow = Window.GetWindow(this) as MainWindow;

        if (mainWindow is null)
            return;

        mainWindow.NavigateToProgress(viewModel.SelectedExerciseSummary.ExerciseId);
    }
}