using System.Windows.Input;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Pages;

public partial class LauncherPage : ContentPage
{
    private readonly LauncherViewModel _vm;
    private bool _hasLoaded;

    public ICommand ConfirmDeleteCommand { get; }

    public LauncherPage(LauncherViewModel vm, AppShell shell)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        vm.ProfileChosen += () => Application.Current!.Windows[0].Page = shell;
        ConfirmDeleteCommand = new Command<UserProfile>(async p => await ConfirmAndDeleteAsync(p));
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

    private async Task ConfirmAndDeleteAsync(UserProfile profile)
    {
        bool confirmed = await DisplayAlertAsync(
            "Eliminar perfil",
            $"¿Eliminar \"{profile.Name}\"? Se borrarán permanentemente todas sus rutinas y sesiones de entrenamiento.",
            "Eliminar",
            "Cancelar");

        if (confirmed)
            await _vm.DeleteProfileAsync(profile);
    }
}
