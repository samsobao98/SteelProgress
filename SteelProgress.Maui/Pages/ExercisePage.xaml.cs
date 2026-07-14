using MauiIcons.Core;
using Microsoft.Maui.Controls.Shapes;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;
using SteelProgress.Maui.Converters;

namespace SteelProgress.Maui.Pages;

public partial class ExercisePage : ContentPage
{
    private readonly ExerciseViewModel _vm;
    private readonly List<(string? value, Border chip, Label label)> _groupChips = new();
    private bool _chipsBuilt;

    public ExercisePage(ExerciseViewModel vm)
    {
        InitializeComponent();
        _ = new MauiIcon();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_chipsBuilt)
        {
            _chipsBuilt = true;
            BuildGroupChips();
        }
        await _vm.RefreshAsync();
        await RunEntryAnimationsAsync();
    }

    // ── Chips de filtro por grupo muscular ───────────────────────────────

    private void BuildGroupChips()
    {
        GroupChipsContainer.Children.Clear();
        _groupChips.Clear();

        AddGroupChip(null, "Todos");
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
            chip.BackgroundColor = selected ? Color.FromArgb("#CBA6F7") : Color.FromArgb("#252536");
            label.TextColor = selected ? Color.FromArgb("#1E1E2E") : Color.FromArgb("#BAC2DE");
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

    // ── Item de la lista: abre la ficha de detalle ────────────────────────

    private Exercise? _detailExercise;

    private void OnExerciseTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is Exercise exercise)
            _ = ShowDetailAsync(exercise);
    }

    private async Task ShowDetailAsync(Exercise exercise)
    {
        _detailExercise = exercise;

        DetailName.Text = exercise.Name;
        DetailGroupLabel.Text = exercise.MuscleGroup;
        DetailGroupChip.BackgroundColor =
            (Color)new MuscleGroupColorConverter().Convert(exercise.MuscleGroup, typeof(Color), null, null!)!;

        DetailEquipmentChip.IsVisible = !string.IsNullOrWhiteSpace(exercise.Equipment);
        DetailEquipmentLabel.Text = exercise.Equipment ?? string.Empty;

        DetailSecondarySection.IsVisible = !string.IsNullOrWhiteSpace(exercise.SecondaryMuscles);
        DetailSecondaryLabel.Text = exercise.SecondaryMuscles ?? string.Empty;

        DetailInstructionsSection.IsVisible = !string.IsNullOrWhiteSpace(exercise.Instructions);
        DetailInstructionsLabel.Text = exercise.Instructions ?? string.Empty;

        DetailNotesSection.IsVisible = !string.IsNullOrWhiteSpace(exercise.Notes);
        DetailNotesLabel.Text = exercise.Notes ?? string.Empty;

        DetailEditButton.IsVisible = !exercise.IsSystem;

        DetailSheet.TranslationY = 800;
        DetailSheet.IsVisible = true;
        OverlayBackdrop.Opacity = 0;
        OverlayBackdrop.IsVisible = true;

        if (IsReduceMotionEnabled())
        {
            DetailSheet.TranslationY = 0;
            OverlayBackdrop.Opacity = 0.6;
            return;
        }

        await Task.WhenAll(
            DetailSheet.TranslateToAsync(0, 0, 320, Easing.CubicOut),
            OverlayBackdrop.FadeToAsync(0.6, 280, Easing.CubicOut));
    }

    private async Task HideDetailAsync()
    {
        if (!IsReduceMotionEnabled())
        {
            await Task.WhenAll(
                DetailSheet.TranslateToAsync(0, 800, 260, Easing.CubicIn),
                OverlayBackdrop.FadeToAsync(0, 220, Easing.CubicIn));
        }

        DetailSheet.IsVisible = false;
        OverlayBackdrop.IsVisible = false;
    }

    private void OnCloseDetailTapped(object? sender, TappedEventArgs e)
        => _ = HideDetailAsync();

    private async void OnDetailEditTapped(object? sender, TappedEventArgs e)
    {
        if (_detailExercise is null || _detailExercise.IsSystem) return;

        DetailSheet.IsVisible = false;
        _vm.SelectedExercise = _detailExercise;
        await ShowOverlayAsync();
    }

    // ── Cierre del overlay ────────────────────────────────────────────────

    private void OnBackdropTapped(object? sender, TappedEventArgs e)
    {
        if (DetailSheet.IsVisible)
            _ = HideDetailAsync();
        else
            _ = HideOverlayAsync();
    }

    private void OnCloseSheetTapped(object? sender, TappedEventArgs e)
        => _ = HideOverlayAsync();

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
