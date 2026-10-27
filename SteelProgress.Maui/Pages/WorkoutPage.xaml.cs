using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Pages;

public partial class WorkoutPage : ContentPage
{
    private readonly WorkoutViewModel _vm;

    public WorkoutPage(WorkoutViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.RefreshAsync();
    }

    // Abre el primer vídeo o imagen del ejercicio con el visor del móvil
    private async void OnShowMediaTapped(object? sender, TappedEventArgs e)
    {
        var media = _vm.SelectedWorkoutExercise?.Exercise?.Media.OrderBy(m => m.Order).FirstOrDefault();
        var storage = Handler?.MauiContext?.Services.GetService<IMediaStorage>();
        if (media is null || storage is null) return;

        var path = storage.GetFullPath(media.FileName);
        if (!File.Exists(path)) return;

        if (sender is VisualElement element)
        {
            await element.ScaleToAsync(0.95, 70, Easing.CubicIn);
            await element.ScaleToAsync(1.0, 120, Easing.SpringOut);
        }
        await Launcher.Default.OpenAsync(new OpenFileRequest(media.Kind == MediaKind.Video ? "Vídeo" : "Imagen", new ReadOnlyFile(path)));
    }
}
