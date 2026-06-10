namespace SteelProgress.Maui.Pages;

public partial class WelcomePage : ContentPage
{
    private readonly AppShell _shell;

    public WelcomePage(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunEntranceAnimation();
    }

    private async Task RunEntranceAnimation()
    {
        // Card principal: fade + slide desde abajo
        await Task.WhenAll(
            MainCard.FadeToAsync(1, 450),
            MainCard.TranslateToAsync(0, 0, 450, Easing.CubicOut)
        );

        // Elementos escalonados
        await AnimateIn(LogoImage, 80);
        await AnimateIn(HeroText, 60);
        await AnimateIn(FeatureCards, 60);
        await AnimateIn(StartButton, 60);
    }

    private static async Task AnimateIn(VisualElement el, uint delay)
    {
        await Task.Delay((int)delay);
        await Task.WhenAll(
            el.FadeToAsync(1, 320),
            el.TranslateToAsync(0, 0, 320, Easing.CubicOut)
        );
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        StartButton.IsEnabled = false;

        await MainCard.FadeToAsync(0, 350, Easing.CubicIn);

        Application.Current!.Windows[0].Page = _shell;
    }
}
