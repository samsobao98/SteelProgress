using System.ComponentModel;
using CommunityToolkit.Maui.Views;
using MauiIcons.Core;
using MauiIcons.Material;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;
using SteelProgress.Maui.Converters;

namespace SteelProgress.Maui.Pages;

/// <summary>
/// Ficha de un ejercicio (Rediseño 4). La abre Ejercicios con <see cref="Show"/>; «Editar» la
/// cierra y le devuelve el ejercicio (<c>onEdit</c>), que abre su formulario.
/// </summary>
public partial class ExerciseDetailPage : ContentPage
{
    private readonly ExerciseDetailViewModel _vm;
    private readonly Func<Exercise, Task>? _onEdit;

    private ExerciseDetailPage(ExerciseDetailViewModel vm, Func<Exercise, Task>? onEdit)
    {
        InitializeComponent();
        _ = new MauiIcon();
        BindingContext = _vm = vm;
        _onEdit = onEdit;
        _vm.PropertyChanged += OnVmChanged;
        // El vídeo arriba, a lo ancho y en 16:10 (como mucho la mitad de la pantalla)
        SizeChanged += (_, _) =>
        {
            if (Width > 0) MediaArea.HeightRequest = Math.Min(Width * 0.625, Height * 0.5);
        };
        ShowCurrentMedia();
    }

    public static Task Show(INavigation navigation, ExerciseDetailViewModel vm, Func<Exercise, Task>? onEdit) =>
        navigation.PushAsync(new ExerciseDetailPage(vm, onEdit));

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExerciseDetailViewModel.CurrentMedia)) ShowCurrentMedia();
    }

    private void ShowCurrentMedia()
    {
        var media = _vm.CurrentMedia;
        var path = media is null ? null : MediaFiles.PathOf(media);
        if (media?.Kind == MediaKind.Video && path is not null && File.Exists(path))
        {
            MediaImage.Source = null;
            Player.Source = MediaSource.FromFile(path);
        }
        else
        {
            Player.Stop();
            Player.Source = null;
            MediaImage.Source = path is not null && File.Exists(path) ? ImageSource.FromFile(path) : null;
        }
    }

    private void OnSoundTapped(object? sender, TappedEventArgs e)
    {
        Player.ShouldMute = !Player.ShouldMute;
        SoundIcon.Icon = Player.ShouldMute ? MaterialIcons.VolumeOff : MaterialIcons.VolumeUp;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) => await Navigation.PopAsync();

    private async void OnEditTapped(object? sender, TappedEventArgs e)
    {
        if (!_vm.CanEdit) return;
        await Navigation.PopAsync();
        if (_onEdit is not null) await _onEdit(_vm.Exercise);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Player.Stop();
    }

    // Al salir de la pila: soltar el reproductor (si no, en Android sigue con el vídeo abierto)
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (Navigation.NavigationStack.Contains(this)) return;
        _vm.PropertyChanged -= OnVmChanged;
        Player.Handler?.DisconnectHandler();
    }
}
