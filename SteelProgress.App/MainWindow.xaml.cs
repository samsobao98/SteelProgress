using SteelProgress.App.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SteelProgress.App;

public partial class MainWindow : Window
{
    private bool _isSidebarCollapsed = false;

    public MainWindow()
    {
        InitializeComponent();
        MainContent.Content = new HomeView();
        SetActiveButton(BtnInicio);
    }

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;

        if (_isSidebarCollapsed)
        {
            SidebarColumn.Width = new GridLength(70);

            BtnInicioText.Visibility = Visibility.Collapsed;
            BtnEjerciciosText.Visibility = Visibility.Collapsed;
            BtnRutinasText.Visibility = Visibility.Collapsed;
            BtnEntrenamientoText.Visibility = Visibility.Collapsed;
            BtnHistorialText.Visibility = Visibility.Collapsed;
        }
        else
        {
            SidebarColumn.Width = new GridLength(220);

            BtnInicioText.Visibility = Visibility.Visible;
            BtnEjerciciosText.Visibility = Visibility.Visible;
            BtnRutinasText.Visibility = Visibility.Visible;
            BtnEntrenamientoText.Visibility = Visibility.Visible;
            BtnHistorialText.Visibility = Visibility.Visible;
        }
    }

    private void Navigate(UserControl view, Button activeButton)
    {
        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(120)
        };

        fadeOut.Completed += (_, _) =>
        {
            MainContent.Content = view;
            SetActiveButton(activeButton);

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(160)
            };

            MainContent.BeginAnimation(OpacityProperty, fadeIn);
        };

        MainContent.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void SetActiveButton(Button activeButton)
    {
        ResetButton(BtnInicio);
        ResetButton(BtnEjercicios);
        ResetButton(BtnRutinas);
        ResetButton(BtnEntrenamiento);
        ResetButton(BtnHistorial);

        activeButton.BorderBrush = (Brush)FindResource("Accent");
        activeButton.BorderThickness = new Thickness(4, 1, 1, 1);
    }

    private void ResetButton(Button button)
    {
        button.BorderBrush = (Brush)FindResource("BorderColor");
        button.BorderThickness = new Thickness(1);
    }

    private void OpenHomeView_Click(object sender, RoutedEventArgs e)
    {
        Navigate(new HomeView(), BtnInicio);
    }

    private void OpenExerciseView_Click(object sender, RoutedEventArgs e)
    {
        Navigate(new ExerciseView(), BtnEjercicios);
    }

    private void OpenRoutineWindow_Click(object sender, RoutedEventArgs e)
    {
        Navigate(new RoutineView(), BtnRutinas);
    }

    private void OpenWorkoutWindow_Click(object sender, RoutedEventArgs e)
    {
        Navigate(new WorkoutView(), BtnEntrenamiento);
    }

    private void OpenHistoryWindow_Click(object sender, RoutedEventArgs e)
    {
        Navigate(new HistoryView(), BtnHistorial);
    }

    public void NavigateToProgress(int exerciseId)
    {
        Navigate(new ProgressView(exerciseId), BtnHistorial);
    }
}