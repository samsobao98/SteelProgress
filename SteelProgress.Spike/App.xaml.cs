namespace SteelProgress.Spike;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    // Resolvemos MainPage desde DI en lugar de crearla directamente
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var mainPage = IPlatformApplication.Current!.Services.GetRequiredService<MainPage>();
        return new Window(mainPage);
    }
}
