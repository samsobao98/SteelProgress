using System;
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

    // Animación de entrada escalonada de los elementos de la pantalla
    private void WelcomeView_Loaded(object sender, RoutedEventArgs e)
    {
        AnimateMainBorder();
        AnimateElement(LogoImage, 120);
        AnimateElement(HeroTextPanel, 260);
        AnimateElement(FeaturesGrid, 400);
        AnimateElement(StartButton, 540);
    }

    private void AnimateMainBorder()
    {
        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(450)
        };

        var move = new DoubleAnimation
        {
            From = 30,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(450)
        };

        MainBorder.BeginAnimation(OpacityProperty, fade);

        if (MainBorder.RenderTransform is TranslateTransform translate)
            translate.BeginAnimation(TranslateTransform.YProperty, move);
    }

    private void AnimateElement(UIElement element, int delayMs)
    {
        element.Opacity = 0;

        var transform = new TranslateTransform
        {
            Y = 15
        };

        element.RenderTransform = transform;

        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            Duration = TimeSpan.FromMilliseconds(350)
        };

        var move = new DoubleAnimation
        {
            From = 15,
            To = 0,
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            Duration = TimeSpan.FromMilliseconds(350)
        };

        element.BeginAnimation(OpacityProperty, fade);
        transform.BeginAnimation(TranslateTransform.YProperty, move);
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        await AnimateExit();

        var main = Window.GetWindow(this) as MainWindow;
        main?.GoToHome();
    }

    // Animación de salida antes de navegar al Home
    private Task AnimateExit()
    {
        var tcs = new TaskCompletionSource<bool>();

        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(450)
        };

        fadeOut.Completed += (_, _) => tcs.SetResult(true);

        MainBorder.BeginAnimation(OpacityProperty, fadeOut);

        return tcs.Task;
    }
}