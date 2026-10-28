using System.ComponentModel;
using MauiIcons.Core;
using Microsoft.Maui.Controls.Shapes;
using SteelProgress.Core.Services;
using SteelProgress.Core.Sharing;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;
using SteelProgress.Maui.Services;

namespace SteelProgress.Maui.Pages;

public partial class RoutineDetailPage : ContentPage
{
    private readonly RoutineDetailViewModel _vm;
    private readonly RoutineSharingService _sharing;
    private readonly List<(RoutineDay day, Border chip, Label label)> _dayChips = new();
    private readonly List<(string? group, Border chip, Label label)> _groupChips = new();
    private Border? _activeSheet;

    public RoutineDetailPage(RoutineDetailViewModel vm, RoutineSharingService sharing)
    {
        InitializeComponent();
        _ = new MauiIcon();
        BindingContext = _vm = vm;
        _sharing = sharing;

        _vm.DaysChanged += BuildDayChips;
        _vm.ExercisesChanged += OnExercisesChanged;
        _vm.RoutineDeleted += OnRoutineDeleted;
        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.Picker.PropertyChanged += OnPickerPropertyChanged;

        BuildGroupChips();
    }

    public Task LoadAsync(Routine routine) => _vm.InitializeAsync(routine);

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoutineDetailViewModel.SelectedDay))
            MainThread.BeginInvokeOnMainThread(StyleDayChips);
    }

    private void OnPickerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExercisePickerViewModel.GroupFilter))
            StyleGroupChips();
    }

    // Las etiquetas 1, 2, 3… salen de Order, que cambia al reordenar sin avisar a la vista
    private void OnExercisesChanged() => MainThread.BeginInvokeOnMainThread(() =>
    {
        ExerciseList.ItemsSource = null;
        ExerciseList.ItemsSource = _vm.DayExercises;
    });

    private async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Navigation.PopAsync();

    // ── Chips de días ──────────────────────────────────────────────────────

    private static Brush SteelBrush =>
        Application.Current?.Resources.TryGetValue("SpSteelBrush", out var b) == true ? (Brush)b : new SolidColorBrush(Color.FromArgb("#B4BEFE"));

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
                FontFamily = "OpenSansSemibold",
                VerticalOptions = LayoutOptions.Center
            };
            var chip = new Border
            {
                Padding = new Thickness(16, 9),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = label
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => OnDayChipTapped(captured);
            chip.GestureRecognizers.Add(tap);

            _dayChips.Add((captured, chip, label));
            DayChipsContainer.Children.Add(chip);
        }

        // Chip "+" para añadir un día
        var addChip = new Border
        {
            BackgroundColor = Color.FromArgb("#252536"),
            Padding = new Thickness(16, 9),
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Color.FromArgb("#34354A")),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Label
            {
                Text = _vm.Days.Count == 0 ? "+ Añadir día" : "+",
                FontSize = _vm.Days.Count == 0 ? 14 : 18,
                FontFamily = "OpenSansSemibold",
                TextColor = Color.FromArgb("#B4BEFE"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
        var addTap = new TapGestureRecognizer();
        addTap.Tapped += OnAddDayChipTapped;
        addChip.GestureRecognizers.Add(addTap);
        DayChipsContainer.Children.Add(addChip);

        StyleDayChips();
    }

    private void StyleDayChips()
    {
        foreach (var (day, chip, label) in _dayChips)
        {
            var selected = _vm.SelectedDay is not null && day.Id == _vm.SelectedDay.Id;
            chip.Background = selected ? SteelBrush : new SolidColorBrush(Color.FromArgb("#252536"));
            label.TextColor = Color.FromArgb(selected ? "#1B1B2B" : "#CDD6F4");
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
        else if (action == "Eliminar día" && RoutineSchedule.CanDeleteNow())
        {
            // Borrar un día borra también sus entrenos del historial
            var sessions = await _vm.CountSessionsOfDayAsync(day);
            var history = sessions switch
            {
                0 => "",
                1 => " y su entreno del historial",
                _ => $" y sus {sessions} entrenos del historial"
            };
            var ok = await DisplayAlertAsync("Eliminar día",
                $"¿Eliminar «{day.Name}» con sus ejercicios{history}?", "Eliminar", "Cancelar");
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
        var action = await DisplayActionSheetAsync(title, "Cancelar", "Quitar del día", "Editar series, reps e intensidad");

        if (action == "Editar series, reps e intensidad")
        {
            _vm.EditPrescription(rde);
        }
        else if (action == "Quitar del día")
        {
            var ok = await DisplayAlertAsync("Quitar ejercicio",
                $"¿Quitar «{title}» de este día?", "Quitar", "Cancelar");
            if (ok)
                await _vm.DeleteExerciseAsync(rde);
        }
    }

    private void OnAddTargetsTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is RoutineDayExercise rde)
            _vm.EditPrescription(rde);
    }

    // ── Compartir (.steel) ─────────────────────────────────────────────────

    private async void OnShareTapped(object? sender, TappedEventArgs e)
    {
        await PressAsync(ShareButton);
        await RoutineShareFlow.ShareAsync(this, _sharing, _vm.Routine, ShareButton);
    }

    // ── Panel «Añadir ejercicio» ───────────────────────────────────────────

    private void BuildGroupChips()
    {
        AddGroupChip(null, "Todos");
        foreach (var group in _vm.Picker.MuscleGroups)
            AddGroupChip(group, group);
        StyleGroupChips();
    }

    private void AddGroupChip(string? group, string text)
    {
        var label = new Label { Text = text, FontSize = 12, FontFamily = "OpenSansSemibold", VerticalOptions = LayoutOptions.Center };
        var chip = new Border
        {
            Padding = new Thickness(11, 5),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = label
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => _vm.Picker.GroupFilter = group;
        chip.GestureRecognizers.Add(tap);

        _groupChips.Add((group, chip, label));
        GroupChipsContainer.Children.Add(chip);
    }

    private void StyleGroupChips()
    {
        foreach (var (group, chip, label) in _groupChips)
        {
            var selected = group == _vm.Picker.GroupFilter;
            chip.BackgroundColor = Color.FromArgb(selected ? "#2689B4FA" : "#2C2D42");
            label.TextColor = Color.FromArgb(selected ? "#89B4FA" : "#A6ADC8");
        }
    }

    private void OnPickerItemTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ExercisePickerItem item)
        {
            _ = PressAsync(sender as VisualElement);
            _vm.Picker.ToggleCommand.Execute(item);
        }
    }

    private async void OnAddExerciseTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.SelectedDay is null)
        {
            await DisplayAlertAsync("Sin día", "Crea o selecciona un día antes de añadir ejercicios.", "Entendido");
            return;
        }
        _vm.Picker.Reset();
        await ShowSheetAsync(AddExerciseSheet);
    }

    private async void OnConfirmAddExerciseTapped(object? sender, TappedEventArgs e)
    {
        if (!_vm.Picker.HasSelection || _vm.AddSelectedExercisesCommand.IsRunning) return;
        SearchEntry.Unfocus();
        await PressAsync(sender as VisualElement);
        await _vm.AddSelectedExercisesCommand.ExecuteAsync(null);
        await HideSheetAsync();
    }

    // ── Sheets: abrir ──────────────────────────────────────────────────────

    private void OnAddDayChipTapped(object? sender, TappedEventArgs e)
    {
        _vm.NewDayName = string.Empty;
        _ = ShowSheetAsync(AddDaySheet);
    }

    private async void OnMarkActiveTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.ToggleMarkedActiveCommand.IsRunning) return;
        await PressAsync(MarkActiveButton);
        await _vm.ToggleMarkedActiveCommand.ExecuteAsync(null);
    }

    private void OnEditRoutineTapped(object? sender, TappedEventArgs e)
        => _ = ShowSheetAsync(EditRoutineSheet);

    // ── Sheets: confirmar ──────────────────────────────────────────────────

    private async void OnConfirmAddDayTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.AddDayCommand.IsRunning) return;
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
        if (!RoutineSchedule.CanDeleteNow()) return;

        // Borrar una rutina borra también su historial de entrenos
        var sessions = await _vm.CountSessionsOfRoutineAsync();
        var history = sessions switch
        {
            0 => "",
            1 => " Se borrará también su entreno del historial.",
            _ => $" Se borrarán también sus {sessions} entrenos del historial."
        };
        var ok = await DisplayAlertAsync("Eliminar rutina",
            $"¿Eliminar «{_vm.RoutineName}» con todos sus días y ejercicios?{history}", "Eliminar", "Cancelar");
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
        sheet.TranslationY = 800;
        sheet.IsVisible = true;
        OverlayBackdrop.Opacity = 0;
        OverlayBackdrop.IsVisible = true;

        if (IsReduceMotionEnabled())
        {
            sheet.TranslationY = 0;
            OverlayBackdrop.Opacity = 0.55;
            return;
        }

        await Task.WhenAll(
            sheet.TranslateToAsync(0, 0, 320, Easing.CubicOut),
            OverlayBackdrop.FadeToAsync(0.55, 280, Easing.CubicOut));
    }

    private async Task HideSheetAsync()
    {
        var sheet = _activeSheet;
        if (sheet is null) return;
        SearchEntry.Unfocus();

        if (IsReduceMotionEnabled())
        {
            sheet.IsVisible = false;
            OverlayBackdrop.IsVisible = false;
            _activeSheet = null;
            return;
        }

        await Task.WhenAll(
            sheet.TranslateToAsync(0, 800, 260, Easing.CubicIn),
            OverlayBackdrop.FadeToAsync(0, 220, Easing.CubicIn));

        sheet.IsVisible = false;
        OverlayBackdrop.IsVisible = false;
        _activeSheet = null;
    }

    // Escala 0,96 con rebote (guía visual §12.1)
    private static async Task PressAsync(VisualElement? element)
    {
        if (element is null || IsReduceMotionEnabled()) return;
        await element.ScaleToAsync(0.96, 60, Easing.CubicIn);
        await element.ScaleToAsync(1.0, 150, Easing.SpringOut);
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
