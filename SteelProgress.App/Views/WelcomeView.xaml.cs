using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SteelProgress.App.Views;

public partial class WelcomeView : UserControl
{
    public WelcomeView()
    {
        InitializeComponent();
        Loaded += WelcomeView_Loaded;
    }

    private void WelcomeView_Loaded(object sender, RoutedEventArgs e)
    {
        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(500)
        };

        var move = new DoubleAnimation
        {
            From = 30,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(500)
        };

        MainBorder.BeginAnimation(OpacityProperty, fade);
        ((TranslateTransform)MainBorder.RenderTransform).BeginAnimation(TranslateTransform.YProperty, move);
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        var main = Window.GetWindow(this) as MainWindow;
        main?.GoToHome();
    }
}