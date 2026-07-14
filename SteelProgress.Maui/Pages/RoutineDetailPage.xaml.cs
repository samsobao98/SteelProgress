using System.ComponentModel;
using MauiIcons.Core;
using Microsoft.Maui.Controls.Shapes;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Pages;

public partial class RoutineDetailPage : ContentPage
{
    private readonly RoutineDetailViewModel _vm;
    private readonly List<(RoutineDay day, Border chip)> _dayChips = new();
    private Border? _activeSheet;

    public RoutineDetailPage(RoutineDetailViewModel vm)
    {
        InitializeComponent();
        _ = new MauiIcon();
        BindingContext = _vm = vm;

        _vm.DaysChanged += BuildDayChips;
        _vm.RoutineDeleted += OnRoutineDeleted;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    public Task LoadAsync(Routine routine) => _vm.InitializeAsync(routine);

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoutineDetailViewModel.SelectedDay))
            MainThread.BeginInvokeOnMainThread(StyleDayChips);
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Navigation.PopAsync();

    // ── Chips de días ──────────────────────────────────────────────────────

    private void BuildDayChips()
    {
        DayChipsContainer.Children.Clear();
        _dayChips.Clear();

        foreach (var day in _vm.Days)
        {
            var captured = day;
            var label = new Label
            {
                Text = day.Name,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#CDD6F4"),
                VerticalOptions = LayoutOptions.Center
            };
            var chip = new Border
            {
                BackgroundColor = Color.FromArgb("#313244"),
                Padding = new Thickness(16, 9),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = label
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => OnDayChipTapped(captured);
            chip.GestureRecognizers.Add(tap);

            _dayChips.Add((captured, chip));
            DayChipsContainer.Children.Add(chip);
        }

        // Chip "+" para añadir un día
        var addLabel = new Label
        {
            Text = "+",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#A6E3A1"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        var addChip = new Border
        {
            BackgroundColor = Color.FromArgb("#252536"),
            Padding = new Thickness(16, 9),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = addLabel
        };
        var addTap = new TapGestureRecognizer();
        addTap.Tapped += OnAddDayChipTapped;
        addChip.GestureRecognizers.Add(addTap);
        DayChipsContainer.Children.Add(addChip);

        StyleDayChips();
    }

    private void StyleDayChips()
    {
        foreach (var (day, chip) in _dayChips)
        {
            var selected = _vm.SelectedDay is not null && day.Id == _vm.SelectedDay.Id;
            chip.BackgroundColor = Color.FromArgb(selected ? "#CBA6F7" : "#313244");
            if (chip.Content is Label l)
                l.TextColor = Color.FromArgb(selected ? "#1E1E2E" : "#CDD6F4");
        }
    }

    private async void OnDayChipTapped(RoutineDay day)
    {
        // Tocar el día ya activo abre su menú de gestión.
        if (_vm.SelectedDay is not null && day.Id == _vm.SelectedDay.Id)
        {
            await ShowDayMenuAsync(day);
            return;
        }
        _vm.SelectDay(day);
    }

    private async Task ShowDayMenuAsync(RoutineDay day)
    {
        var action = await DisplayActionSheetAsync(day.Name, "Cancelar", "Eliminar día", "Renombrar");

        if (action == "Renombrar")
        {
            var name = await DisplayPromptAsync("Renombrar día", "Nuevo nombre", initialValue: day.Name);
            if (!string.IsNullOrWhiteSpace(name))
                await _vm.RenameDayAsync(day, name);
        }
        else if (action == "Eliminar día")
        {
            var ok = await DisplayAlertAsync("Eliminar día",
                $"¿Eliminar \"{day.Name}\" y todos sus ejercicios?", "Eliminar", "Cancelar");
            if (ok)
                await _vm.DeleteDayAsync(day);
        }
    }

    // ── Reordenar ejercicios ───────────────────────────────────────────────

    private async void OnReorderCompleted(object? sender, EventArgs e)
        => await _vm.PersistReorderAsync();

    // ── Menú de ejercicio ──────────────────────────────────────────────────

    private async void OnExerciseMenuTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not RoutineDayExercise rde)
            return;

        var title = rde.Exercise?.Name ?? "Ejercicio";
        var action = await DisplayActionSheetAsync(title, "Cancelar", "Eliminar", "Editar series y reps");

        if (action == "Editar series y reps")
        {
            var setsStr = await DisplayPromptAsync("Series", "Número de series objetivo",
                initialValue: rde.TargetSets?.ToString() ?? string.Empty, keyboard: Keyboard.Numeric);
            if (setsStr is null) return; // cancelado

            var repsStr = await DisplayPromptAsync("Repeticiones", "Repeticiones objetivo",
                initialValue: rde.TargetReps?.ToString() ?? string.Empty, keyboard: Keyboard.Numeric);
            if (repsStr is null) return;

            int? sets = int.TryParse(setsStr, out var s) && s > 0 ? s : null;
            int? reps = int.TryParse(repsStr, out var r) && r > 0 ? r : null;
            await _vm.UpdateExerciseTargetsAsync(rde, sets, reps);
        }
        else if (action == "Eliminar")
        {
            var ok = await DisplayAlertAsync("Eliminar ejercicio",
                $"¿Quitar \"{title}\" de este día?", "Eliminar", "Cancelar");
            if (ok)
                await _vm.DeleteExerciseAsync(rde);
        }
    }

    // ── Sheets: abrir ──────────────────────────────────────────────────────

    private async void OnAddExerciseTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.SelectedDay is null)
        {
            await DisplayAlertAsync("Sin día", "Crea o selecciona un día antes de añadir ejercicios.", "Entendido");
            return;
        }
        await ShowSheetAsync(AddExerciseSheet);
    }

    private void OnAddDayChipTapped(object? sender, TappedEventArgs e)
    {
        _vm.NewDayName = string.Empty;
        _ = ShowSheetAsync(AddDaySheet);
    }

    private void OnEditRoutineTapped(object? sender, TappedEventArgs e)
        => _ = ShowSheetAsync(EditRoutineSheet);

    // ── Sheets: confirmar ──────────────────────────────────────────────────

    private async void OnConfirmAddExerciseTapped(object? sender, TappedEventArgs e)
    {
        await _vm.AddExerciseToDayCommand.ExecuteAsync(null);
        if (_vm.SelectedExerciseToAdd is null) // éxito → VM limpió la selección
            await HideSheetAsync();
    }

    private async void OnConfirmAddDayTapped(object? sender, TappedEventArgs e)
    {
        DayNameEntry.Unfocus();
        await _vm.AddDayCommand.ExecuteAsync(null);
        if (string.IsNullOrEmpty(_vm.NewDayName)) // éxito → VM limpió el nombre
            await HideSheetAsync();
    }

    private async void OnSaveRoutineTapped(object? sender, TappedEventArgs e)
    {
        EditNameEntry.Unfocus();
        EditNotesEntry.Unfocus();
        if (string.IsNullOrWhiteSpace(EditNameEntry.Text))
            return;
        await _vm.RenameRoutineAsync(EditNameEntry.Text, EditNotesEntry.Text);
        await HideSheetAsync();
    }

    private async void OnDeleteRoutineTapped(object? sender, TappedEventArgs e)
    {
        var ok = await DisplayAlertAsync("Eliminar rutina",
            $"¿Eliminar \"{_vm.RoutineName}\" con todos sus días y ejercicios?", "Eliminar", "Cancelar");
        if (ok)
            await _vm.DeleteRoutineAsync();
    }

    private async void OnRoutineDeleted()
    {
        await HideSheetAsync();
        await Navigation.PopAsync();
    }

    // ── Sheets: animación ──────────────────────────────────────────────────

    private void OnBackdropTapped(object? sender, TappedEventArgs e)
        => _ = HideSheetAsync();

    private void OnCloseSheetTapped(object? sender, TappedEventArgs e)
        => _ = HideSheetAsync();

    private async Task ShowSheetAsync(Border sheet)
    {
        _activeSheet = sheet;
        sheet.TranslationY = 700;
        sheet.IsVisible = true;
        OverlayBackdrop.Opacity = 0;
        OverlayBackdrop.IsVisible = true;

        if (IsReduceMotionEnabled())
        {
            sheet.TranslationY = 0;
            OverlayBackdrop.Opacity = 0.6;
            return;
        }

        await Task.WhenAll(
            sheet.TranslateToAsync(0, 0, 320, Easing.CubicOut),
            OverlayBackdrop.FadeToAsync(0.6, 280, Easing.CubicOut));
    }

    private async Task HideSheetAsync()
    {
        var sheet = _activeSheet;
        if (sheet is null) return;

        if (IsReduceMotionEnabled())
        {
            sheet.IsVisible = false;
            OverlayBackdrop.IsVisible = false;
            _activeSheet = null;
            return;
        }

        await Task.WhenAll(
            sheet.TranslateToAsync(0, 700, 260, Easing.CubicIn),
            OverlayBackdrop.FadeToAsync(0, 220, Easing.CubicIn));

        sheet.IsVisible = false;
        OverlayBackdrop.IsVisible = false;
        _activeSheet = null;
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
