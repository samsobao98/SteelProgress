using SteelProgress.App.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SteelProgress.App.Services;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using SteelProgress.App.Animations;
namespace SteelProgress.App;

public partial class MainWindow : Window
{
    private bool _isSidebarCollapsed = false;
    private readonly DispatcherTimer _toastTimer = new();
    private TaskCompletionSource<bool>? _confirmTcs;

    public MainWindow()
    {
        InitializeComponent();
        MainContent.Content = new WelcomeView();
        HideSidebar();
        ConfirmDialogService.OnConfirmationRequested += ShowConfirmDialogAsync;
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void AnimateSidebarWidth(double targetWidth)
{
    var animation = new DoubleAnimation
    {
        To = targetWidth,
        Duration = TimeSpan.FromMilliseconds(220),
        EasingFunction = new QuadraticEase
        {
            EasingMode = EasingMode.EaseInOut
        }
    };

    SidebarPanel.BeginAnimation(WidthProperty, animation);
}

    public async void GoToHome()
    {
        await Task.Delay(250);

        ShowSidebar();

        MainContent.Opacity = 0;
        MainContent.Content = new HomeView();
        SetActiveButton(BtnInicio);

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(450)
        };

        MainContent.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void HideSidebar()
    {
        AnimateSidebarWidth(0);
    }

    private void ShowSidebar()
    {
        AnimateSidebarWidth(220);
    }

    private Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        ConfirmTitle.Text = title;
        ConfirmMessage.Text = message;

        ConfirmOverlay.Visibility = Visibility.Visible;

        _confirmTcs = new TaskCompletionSource<bool>();
        return _confirmTcs.Task;
    }

    private void AcceptConfirm_Click(object sender, RoutedEventArgs e)
    {
        ConfirmOverlay.Visibility = Visibility.Collapsed;
        _confirmTcs?.SetResult(true);
    }

    private void CancelConfirm_Click(object sender, RoutedEventArgs e)
    {
        ConfirmOverlay.Visibility = Visibility.Collapsed;
        _confirmTcs?.SetResult(false);
    }

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;

        if (_isSidebarCollapsed)
        {
            AnimateSidebarWidth(70);

            BtnInicioText.Visibility = Visibility.Collapsed;
            BtnEjerciciosText.Visibility = Visibility.Collapsed;
            BtnRutinasText.Visibility = Visibility.Collapsed;
            BtnEntrenamientoText.Visibility = Visibility.Collapsed;
            BtnHistorialText.Visibility = Visibility.Collapsed;


            SidebarBottomText.Visibility = Visibility.Collapsed;
            BtnSalir.Visibility = Visibility.Collapsed;

            SidebarBottomLogo.Visibility = Visibility.Visible;
            SidebarBottomLogo.Width = 32;
            SidebarBottomLogo.Height = 32;
            SidebarBottomLogo.Margin = new Thickness(0);
        }
        else
        {
            AnimateSidebarWidth(220);

            BtnInicioText.Visibility = Visibility.Visible;
            BtnEjerciciosText.Visibility = Visibility.Visible;
            BtnRutinasText.Visibility = Visibility.Visible;
            BtnEntrenamientoText.Visibility = Visibility.Visible;
            BtnHistorialText.Visibility = Visibility.Visible;


            SidebarBottomText.Visibility = Visibility.Visible;
            BtnSalir.Visibility = Visibility.Visible;

            SidebarBottomLogo.Visibility = Visibility.Visible;
            SidebarBottomLogo.Width = 36;
            SidebarBottomLogo.Height = 36;
            SidebarBottomLogo.Margin = new Thickness(0, 0, 8, 0);
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

    private void ShowToast(string message, NotificationType type)
    {
        ToastMessage.Text = message;

        switch (type)
        {
            case NotificationType.Success:
                ToastTitle.Text = "Correcto";
                ToastBorder.BorderBrush = (Brush)FindResource("Accent");
                break;

            case NotificationType.Error:
                ToastTitle.Text = "Error";
                ToastBorder.BorderBrush = Brushes.IndianRed;
                break;

            default:
                ToastTitle.Text = "Aviso";
                ToastBorder.BorderBrush = (Brush)FindResource("BorderColor");
                break;
        }

        ToastBorder.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180)
        };

        ToastBorder.BeginAnimation(OpacityProperty, fadeIn);

        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void HideToast()
    {
        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180)
        };

        fadeOut.Completed += (_, _) =>
        {
            ToastBorder.Visibility = Visibility.Collapsed;
        };

        ToastBorder.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void SetActiveButton(Button activeButton)
    {
        ResetButton(BtnInicio);

        NotificationService.OnNotificationRequested += ShowToast;

        _toastTimer.Interval = TimeSpan.FromSeconds(3);
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            HideToast();
        };

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