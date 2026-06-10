using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class LauncherPage : ContentPage
{
    private readonly LauncherViewModel _vm;
    private bool _hasLoaded;

    public LauncherPage(LauncherViewModel vm, AppShell shell)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        vm.ProfileChosen += () => Application.Current!.Windows[0].Page = shell;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasLoaded) return;
        _hasLoaded = true;

        var app = (App)Application.Current!;
        await _vm.LoadAsync(app.MigrationTask);

        // Animate after content is ready so the entrance is clearly visible
        await Task.WhenAll(
            HeaderSection.FadeToAsync(1, 700, Easing.CubicOut),
            HeaderSection.TranslateToAsync(0, 0, 700, Easing.CubicOut)
        );

        await SubtitleLabel.FadeToAsync(1, 400, Easing.CubicOut);
    }
}
