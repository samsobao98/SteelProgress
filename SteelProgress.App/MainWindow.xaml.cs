using System.Windows;
using SteelProgress.App.Views;

namespace SteelProgress.App;

public partial class MainWindow : Window
{
    private bool _isSidebarCollapsed = false;

    public MainWindow()
    {
        InitializeComponent();
        MainContent.Content = new HomeView();
    }

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;

        if (_isSidebarCollapsed)
        {
            SidebarColumn.Width = new GridLength(70);

            SidebarTitle.Visibility = Visibility.Collapsed;
            SidebarSubtitle.Visibility = Visibility.Collapsed;

            BtnInicioText.Visibility = Visibility.Collapsed;
            BtnRutinasText.Visibility = Visibility.Collapsed;
            BtnEntrenamientoText.Visibility = Visibility.Collapsed;
            BtnHistorialText.Visibility = Visibility.Collapsed;
            BtnEjerciciosText.Visibility = Visibility.Collapsed;      
        }
        else
        {
            SidebarColumn.Width = new GridLength(220);

            SidebarTitle.Visibility = Visibility.Visible;
            SidebarSubtitle.Visibility = Visibility.Visible;

            BtnInicioText.Visibility = Visibility.Visible;
            BtnRutinasText.Visibility = Visibility.Visible;
            BtnEntrenamientoText.Visibility = Visibility.Visible;
            BtnHistorialText.Visibility = Visibility.Visible;
            BtnEjerciciosText.Visibility = Visibility.Visible;
        }
    }

    public void NavigateToProgress(int exerciseId)
    {
        MainContent.Content = new ProgressView(exerciseId);
    }

    private void OpenExerciseView_Click(object sender, RoutedEventArgs e)
    {
        MainContent.Content = new ExerciseView();
    }

    private void OpenHomeView_Click(object sender, RoutedEventArgs e)
    {
        MainContent.Content = new HomeView();
    }

    private void OpenRoutineWindow_Click(object sender, RoutedEventArgs e)
    {
        MainContent.Content = new RoutineView();
    }

    private void OpenWorkoutWindow_Click(object sender, RoutedEventArgs e)
    {
        MainContent.Content = new WorkoutView();
    }

    private void OpenHistoryWindow_Click(object sender, RoutedEventArgs e)
    {
        MainContent.Content = new HistoryView();
    }

}