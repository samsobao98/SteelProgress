using SteelProgress.Maui.Services;
using MauiIcons.Core;
using Microsoft.Maui.Controls.Shapes;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;
using SteelProgress.Maui.Controls;
using SteelProgress.Maui.Converters;

namespace SteelProgress.Maui.Pages;

public partial class ExercisePage : ContentPage
{
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

    private readonly ExerciseViewModel _vm;
    private readonly List<(string? value, Border chip, Label label)> _groupChips = new();
    private readonly List<(string value, Border chip, Label label)> _formChips = new();
    private readonly List<(EquipmentType value, Border chip, Label label)> _equipmentChips = new();
    private bool _chipsBuilt;

    public ExercisePage(ExerciseViewModel vm)
    {
        InitializeComponent();
        _ = new MauiIcon();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExerciseViewModel.MuscleGroup)) StyleFormChips();
            else if (e.PropertyName == nameof(ExerciseViewModel.SelectedEquipment)) StyleEquipmentChips();
        };
        SizeChanged += (_, _) => { if (Height > 0) SheetScroll.MaximumHeightRequest = Height * 0.92; };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_coveredByPopup) { _coveredByPopup = false; return; } // vuelve de una ventana propia, no de otra pantalla
        if (!_chipsBuilt)
        {
            _chipsBuilt = true;
            BuildGroupChips();
            BuildFormChips();
            BuildEquipmentChips();
        }
        await _vm.RefreshAsync();
        await RunEntryAnimationsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (AppDialogs.IsShowing) _coveredByPopup = true; // solo se abrió una ventana encima
    }


    // ── Chips de filtro por grupo muscular ───────────────────────────────

    private void BuildGroupChips()
    {
        GroupChipsContainer.Children.Clear();
        _groupChips.Clear();

        AddGroupChip(null, "Todos");
        AddGroupChip(ExerciseCatalog.OwnFilter, ExerciseCatalog.OwnFilter);
        foreach (var group in _vm.MuscleGroups)
            AddGroupChip(group, group);

        StyleGroupChips();
    }

    private void AddGroupChip(string? value, string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var chip = new Border
        {
            Padding = new Thickness(14, 8),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = label
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            _vm.SelectedGroupFilter = value;
            StyleGroupChips();
        };
        chip.GestureRecognizers.Add(tap);

        _groupChips.Add((value, chip, label));
        GroupChipsContainer.Children.Add(chip);
    }

    private void StyleGroupChips()
    {
        foreach (var (value, chip, label) in _groupChips)
        {
            var selected = value == _vm.SelectedGroupFilter;
            chip.BackgroundColor = selected ? Color.FromArgb("#C9B9F2") : Color.FromArgb("#1B1E28");
            label.TextColor = selected ? Color.FromArgb("#14161E") : Color.FromArgb("#C6CBDA");
        }
    }

    // Grupo muscular del formulario: chips en vez de desplegable (maqueta de crear ejercicio)
    private void BuildFormChips()
    {
        FormGroupChips.Children.Clear();
        _formChips.Clear();
        foreach (var group in _vm.MuscleGroups)
        {
            var label = new Label { Text = group, FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
            var chip = new Border
            {
                Padding = new Thickness(12, 7),
                Margin = new Thickness(0, 0, 6, 6),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = label
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => _vm.SelectMuscleGroupCommand.Execute(group);
            chip.GestureRecognizers.Add(tap);
            _formChips.Add((group, chip, label));
            FormGroupChips.Children.Add(chip);
        }
        StyleFormChips();
    }

    private void StyleFormChips()
    {
        foreach (var (value, chip, label) in _formChips)
        {
            var selected = value == _vm.MuscleGroup;
            chip.BackgroundColor = selected ? Color.FromArgb("#C9B9F2") : Color.FromArgb("#1B1E28");
            label.TextColor = selected ? Color.FromArgb("#14161E") : Color.FromArgb("#C6CBDA");
        }
    }

    // Con qué se hace: dos columnas de tarjetas con su icono (maqueta «Con qué se hace»)
    private void BuildEquipmentChips()
    {
        EquipmentChips.Children.Clear();
        EquipmentChips.RowDefinitions.Clear();
        _equipmentChips.Clear();
        var types = _vm.EquipmentTypes;
        for (var i = 0; i < types.Count; i++)
        {
            var type = types[i];
            if (i % 2 == 0) EquipmentChips.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = new Label
            {
                Text = type == EquipmentType.Other ? "Otro: gomas, TRX…" : EquipmentCatalog.Label(type),
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.WordWrap
            };
            var content = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10 };
            content.Add(new EquipmentIcon { Type = type, VerticalOptions = LayoutOptions.Center }, 0);
            content.Add(label, 1);
            var chip = new Border
            {
                Padding = new Thickness(12, 0),
                HeightRequest = 56,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Content = content
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => _vm.SelectEquipmentCommand.Execute(type);
            chip.GestureRecognizers.Add(tap);
            _equipmentChips.Add((type, chip, label));
            EquipmentChips.Add(chip, i % 2, i / 2);
        }
        StyleEquipmentChips();
    }

    private void StyleEquipmentChips()
    {
        foreach (var (value, chip, label) in _equipmentChips)
        {
            var selected = value == _vm.SelectedEquipment;
            chip.BackgroundColor = selected ? Color.FromArgb("#262A37") : Color.FromArgb("#1B1E28");
            chip.Stroke = selected ? Color.FromArgb("#C9B9F2") : Colors.Transparent;
            chip.StrokeThickness = selected ? 2 : 0;
            label.TextColor = selected ? Color.FromArgb("#E8EAF2") : Color.FromArgb("#C6CBDA");
            label.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    private async Task RunEntryAnimationsAsync()
    {
        if (IsReduceMotionEnabled())
        {
            ExerciseList.Opacity = 1;
            return;
        }

        ExerciseList.Opacity = 0;
        // El FAB no se anima: animar Scale/Opacity sobre un elemento con Shadow
        // deja la sombra rasterizada borrosa en Android (mismo fix que RoutinePage).
        await ExerciseList.FadeToAsync(1, 320, Easing.CubicOut);
    }

    // ── FAB: abre overlay en modo añadir ──────────────────────────────────

    private void OnFabTapped(object? sender, TappedEventArgs e)
    {
        _vm.ClearForm();
        _ = ShowOverlayAsync();
    }

    // ── Item de la lista: abre la ficha (página propia, con el vídeo) ──────

    private bool _opening;

    private async void OnExerciseTapped(object? sender, TappedEventArgs e)
    {
        if (_opening || (sender as BindableObject)?.BindingContext is not ExerciseListItem item) return;
        _opening = true;
        try
        {
            await ExerciseDetailPage.Show(Navigation, new ExerciseDetailViewModel(item.Exercise, item.Progress), async exercise =>
            {
                _vm.SelectedExercise = exercise;
                await ShowOverlayAsync();
            });
        }
        finally { _opening = false; }
    }

    // ── Cierre del overlay ────────────────────────────────────────────────

    // Cerrar sin guardar: ClearForm borra los vídeos elegidos para un ejercicio nuevo que no se guardó
    private void OnBackdropTapped(object? sender, TappedEventArgs e) => _ = CancelOverlayAsync();

    private void OnCloseSheetTapped(object? sender, TappedEventArgs e)
        => _ = CancelOverlayAsync();

    private async Task CancelOverlayAsync()
    {
        await HideOverlayAsync();
        _vm.ClearForm();
    }

    // ── Vídeos e imágenes ─────────────────────────────────────────────────

    private async void OnAddMediaClicked(object? sender, EventArgs e)
    {
        const string photo = "Foto o GIF de la galería";
        const string video = "Vídeo de la galería";
        const string record = "Grabar vídeo";

        var options = MediaPicker.Default.IsCaptureSupported ? new[] { photo, video, record } : new[] { photo, video };
        var choice = await AppDialogs.ChooseAsync("Añadir vídeo o imagen", options);

        try
        {
            // De la galería se pueden elegir varios a la vez
            IEnumerable<FileResult?> files = choice switch
            {
                photo => await MediaPicker.Default.PickPhotosAsync() ?? [],
                video => await MediaPicker.Default.PickVideosAsync() ?? [],
                record => await MediaPicker.Default.CaptureVideoAsync() is { } recorded ? [recorded] : [],
                _ => []
            };

            foreach (var file in files)
            {
                if (file is null) continue;
                await using var stream = await file.OpenReadAsync();
                await _vm.AddMediaAsync(stream, file.FileName);
            }
        }
        catch (PermissionException)
        {
            await AppDialogs.AlertAsync("Permiso necesario", "Da permiso a SteelProgress para acceder a tus fotos y vídeos.");
        }
    }

    private async void OnRemoveMediaTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ExerciseMedia media)
            await _vm.RemoveMediaCommand.ExecuteAsync(media);
    }

    // En el formulario, tocar un vídeo lo abre con el reproductor del móvil (en la ficha se ve dentro de la app)
    private async void OnMediaTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ExerciseMedia media) return;
        if (_vm.GetMediaPath(media) is not string path || !File.Exists(path)) return;

        if (sender is VisualElement element)
        {
            await element.ScaleToAsync(0.95, 70, Easing.CubicIn);
            await element.ScaleToAsync(1.0, 120, Easing.SpringOut);
        }
        await Launcher.Default.OpenAsync(new OpenFileRequest(media.Kind == MediaKind.Video ? "Vídeo" : "Imagen", new ReadOnlyFile(path)));
    }

    // ── Guardar (añadir o actualizar) ─────────────────────────────────────

    private async void OnSaveTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.IsEditMode)
            await _vm.UpdateExerciseCommand.ExecuteAsync(null);
        else
            await _vm.AddExerciseCommand.ExecuteAsync(null);

        // VM llama ClearForm() en éxito → Name queda vacío
        if (string.IsNullOrWhiteSpace(_vm.Name))
            await HideOverlayAsync();
    }

    // ── Eliminar ──────────────────────────────────────────────────────────

    private async void OnDeleteTapped(object? sender, TappedEventArgs e)
    {
        await _vm.DeleteExerciseCommand.ExecuteAsync(null);

        // VM llama ClearForm() en éxito → SelectedExercise queda null
        if (_vm.SelectedExercise is null)
            await HideOverlayAsync();
    }

    // ── Animaciones overlay ───────────────────────────────────────────────

    private async Task ShowOverlayAsync()
    {
        BottomSheet.TranslationY = 700;
        BottomSheet.IsVisible = true;
        OverlayBackdrop.Opacity = 0;
        OverlayBackdrop.IsVisible = true;

        if (IsReduceMotionEnabled())
        {
            BottomSheet.TranslationY = 0;
            OverlayBackdrop.Opacity = 0.6;
            return;
        }

        await Task.WhenAll(
            BottomSheet.TranslateToAsync(0, 0, 320, Easing.CubicOut),
            OverlayBackdrop.FadeToAsync(0.6, 280, Easing.CubicOut));
    }

    private async Task HideOverlayAsync()
    {
        if (IsReduceMotionEnabled())
        {
            BottomSheet.IsVisible = false;
            OverlayBackdrop.IsVisible = false;
            return;
        }

        await Task.WhenAll(
            BottomSheet.TranslateToAsync(0, 700, 260, Easing.CubicIn),
            OverlayBackdrop.FadeToAsync(0, 220, Easing.CubicIn));

        BottomSheet.IsVisible = false;
        OverlayBackdrop.IsVisible = false;
    }

    private static bool IsReduceMotionEnabled()
    {
#if IOS
        return UIKit.UIAccessibility.IsReduceMotionEnabled;
#else
        return false;
#endif
    }
}
