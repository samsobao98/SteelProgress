using SteelProgress.Maui.Services;
using System.ComponentModel;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class ProfilePage : ContentPage
{
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

    private readonly ProfileViewModel _vm;

    public ProfilePage(ProfileViewModel vm, LauncherPage launcher)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnViewModelChanged;
        _vm.SwitchProfileRequested += () => _ = launcher.ReturnToProfilesAsync();
        _vm.GoToWorkoutRequested += () => _ = Shell.Current.GoToAsync("//WorkoutPage");
        BuildColorRow();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_coveredByPopup) { _coveredByPopup = false; return; } // vuelve de una ventana propia, no de otra pantalla
        await _vm.LoadAsync();
        UpdateModeSelector();

        PageContent.Opacity = 0;
        PageContent.TranslationY = 14;
        await Task.WhenAll(
            PageContent.FadeToAsync(1, 260, Easing.CubicOut),
            PageContent.TranslateToAsync(0, 0, 260, Easing.CubicOut));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (AppDialogs.IsShowing) _coveredByPopup = true; // solo se abrió una ventana encima
    }


    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProfileViewModel.IsDetailed) or nameof(ProfileViewModel.IsCoach))
            UpdateModeSelector();
        else if (e.PropertyName == nameof(ProfileViewModel.EditColor))
            UpdateColorSelection();
    }

    // ── Foto del avatar al editar ──

    // Reducida y girada por el selector: una foto de perfil no necesita más de 640 px
    private static MediaPickerOptions PhotoOptions => new()
    {
        Title = "Foto de perfil",
        SelectionLimit = 1,
        MaximumWidth = 640,
        MaximumHeight = 640,
        CompressionQuality = 85,
        RotateImage = true
    };

    private async void OnEditAvatarTapped(object? sender, TappedEventArgs e)
    {
        if (!_vm.CanChoosePhoto) return;
        await EditAvatar.ScaleToAsync(0.95, 70, Easing.CubicIn);
        await EditAvatar.ScaleToAsync(1.0, 120, Easing.SpringOut);

        const string gallery = "Elegir de la galería";
        const string camera = "Hacer una foto";
        const string remove = "Quitar foto";
        var options = MediaPicker.Default.IsCaptureSupported ? new[] { gallery, camera } : new[] { gallery };
        var choice = await AppDialogs.ChooseAsync("Foto de perfil", options, _vm.HasEditPhoto ? remove : null);
        if (choice == remove)
        {
            _vm.RemoveEditPhotoCommand.Execute(null);
            return;
        }

        try
        {
            var file = choice switch
            {
                gallery => (await MediaPicker.Default.PickPhotosAsync(PhotoOptions))?.FirstOrDefault(),
                camera => await MediaPicker.Default.CapturePhotoAsync(PhotoOptions),
                _ => null
            };
            if (file is null) return;
            await using var stream = await file.OpenReadAsync();
            await _vm.SetEditPhotoAsync(stream);
        }
        catch (PermissionException)
        {
            await AppDialogs.AlertAsync("Permiso necesario", "Da permiso a SteelProgress para usar la cámara o tus fotos.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ProfilePage.OnEditAvatarTapped: {ex}");
            await AppDialogs.AlertAsync("No se pudo usar la foto", "Prueba con otra.");
        }
    }

    // ── Colores del avatar al editar (como en la ficha de un cliente) ──

    private void BuildColorRow()
    {
        foreach (var hex in _vm.AvailableColors)
        {
            var swatch = new Border
            {
                WidthRequest = 34,
                HeightRequest = 34,
                Margin = new Thickness(0, 0, 10, 8),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 17 },
                BackgroundColor = Color.FromArgb(hex),
                StrokeThickness = 0,
                ClassId = hex
            };
            SemanticProperties.SetDescription(swatch, "Color del avatar");
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => _vm.SetEditColorCommand.Execute(hex);
            swatch.GestureRecognizers.Add(tap);
            ColorRow.Children.Add(swatch);
        }
        UpdateColorSelection();
    }

    private void UpdateColorSelection()
    {
        foreach (var child in ColorRow.Children.OfType<Border>())
        {
            var selected = child.ClassId == _vm.EditColor;
            child.StrokeThickness = selected ? 3 : 0;
            child.Stroke = selected ? Colors.White : null;
            child.Scale = selected ? 1.1 : 1.0;
        }
    }

    // El seleccionado lleva el degradado acero; el otro queda transparente
    private void UpdateModeSelector()
    {
        Paint(PersonalOption, !_vm.IsCoach);
        Paint(CoachOption, _vm.IsCoach);
        Paint(SimpleOption, !_vm.IsDetailed);
        Paint(DetailedOption, _vm.IsDetailed);
    }

    private static void Paint(Border option, bool selected)
    {
        option.Background = selected ? Resource<Brush>("SpSteelBrush") : Brush.Transparent;
        if (option.Content is Label label)
            label.TextColor = Resource<Color>(selected ? "SpOnAccent" : "SpText2");
    }

    // Busca también en los diccionarios combinados (Tokens.xaml)
    private static T Resource<T>(string key) =>
        Application.Current!.Resources.TryGetValue(key, out var value) ? (T)value : throw new KeyNotFoundException(key);

    // Respuesta al tocar: se encoge un poco y rebota
    private async void OnOptionTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not VisualElement element) return;
        await element.ScaleToAsync(0.96, 70, Easing.CubicIn);
        await element.ScaleToAsync(1.0, 120, Easing.SpringOut);
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("..");
}
