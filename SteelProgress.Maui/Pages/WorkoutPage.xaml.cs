using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;
using SteelProgress.Maui.Controls;
using SteelProgress.Maui.Services;

namespace SteelProgress.Maui.Pages;

public partial class WorkoutPage : ContentPage
{
    private static readonly string[] Feelings = ["😫", "😕", "😐", "💪", "🔥"];

    private readonly WorkoutViewModel _vm;
    private readonly RestRingDrawable _ring = new();
    private readonly List<(Routine routine, Border chip, Label label)> _routineChips = new();
    private readonly List<Border> _feelingButtons = new();
    private IDispatcherTimer? _timer;

    public WorkoutPage(WorkoutViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        RestRing.Drawable = _ring;
        BuildFeelingButtons();

        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.Routines.CollectionChanged += OnRoutinesChanged;
        _vm.SetCompleted += OnSetCompleted;
        _vm.RestFinished += OnRestFinished;
    }

    private Core.Services.ClientContextService? _clientContext;

    // Al entrar o salir de un cliente, la pantalla vuelve a cargar sus datos
    private void OnClientChanged() => MainThread.BeginInvokeOnMainThread(async () => await _vm.RefreshAsync());

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_clientContext is not null) _clientContext.Changed -= OnClientChanged;
        _timer?.Stop();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _clientContext ??= Handler?.MauiContext?.Services.GetService<Core.Services.ClientContextService>();
        if (_clientContext is not null)
        {
            _clientContext.Changed -= OnClientChanged;
            _clientContext.Changed += OnClientChanged;
        }

        // El cronómetro y el descanso se calculan con la hora: al volver a la pestaña siguen bien
        _timer ??= CreateTimer();
        _timer.Start();

        await _vm.RefreshAsync();
    }

    private IDispatcherTimer CreateTimer()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.Tick += (_, _) => { if (_vm.IsSessionActive) _vm.Tick(); };
        return timer;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WorkoutViewModel.SelectedRoutine):
                StyleRoutineChips();
                break;
            case nameof(WorkoutViewModel.RestProgress):
                _ring.Progress = _vm.RestProgress;
                RestRing.Invalidate();
                break;
            case nameof(WorkoutViewModel.Feeling):
                StyleFeelingButtons();
                break;
            case nameof(WorkoutViewModel.IsFinishing) when _vm.IsFinishing:
                _ = PopAsync(FinishCheck);
                break;
        }
    }

    // ── Preparar ──────────────────────────────────────────────────────────

    private void OnRoutinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RoutineChips.Children.Clear();
        _routineChips.Clear();

        foreach (var routine in _vm.Routines)
        {
            var captured = routine;
            var label = new Label { Text = routine.Name, FontFamily = "OpenSansSemibold", FontSize = 14, VerticalOptions = LayoutOptions.Center };
            var chip = new Border
            {
                Padding = new Thickness(16, 9),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = label
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => _vm.PickRoutine(captured);
            chip.GestureRecognizers.Add(tap);

            _routineChips.Add((captured, chip, label));
            RoutineChips.Children.Add(chip);
        }
        StyleRoutineChips();
    }

    private void StyleRoutineChips()
    {
        foreach (var (routine, chip, label) in _routineChips)
        {
            var selected = routine.Id == _vm.SelectedRoutine?.Id;
            chip.Background = selected && Application.Current?.Resources.TryGetValue("SpSteelBrush", out var steel) == true
                ? (Brush)steel
                : new SolidColorBrush(Color.FromArgb(selected ? "#B4BEFE" : "#252536"));
            label.TextColor = Color.FromArgb(selected ? "#1B1B2B" : "#A6ADC8");
        }
    }

    private void OnDayTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is WorkoutDayOption option)
        {
            _ = PressAsync(sender as VisualElement);
            _vm.SelectDay(option);
        }
    }

    private async void OnStartTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.StartWorkoutCommand.IsRunning) return;
        await PressAsync(sender as VisualElement);
        await _vm.StartWorkoutCommand.ExecuteAsync(null);
    }

    // ── Registrando ───────────────────────────────────────────────────────

    private void OnSegmentTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is WorkoutExerciseState state)
            _vm.SelectExercise(state);
    }

    private async void OnFinishTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.FinishCommand.IsRunning) return;
        await PressAsync(sender as VisualElement);
        await _vm.FinishCommand.ExecuteAsync(null);
    }

    private async void OnCompleteSetTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.CompleteSetCommand.IsRunning) return;
        await PressAsync(CompleteSetButton);
        await _vm.CompleteSetCommand.ExecuteAsync(null);
    }

    private void OnSetCompleted(bool isRecord)
    {
        if (isRecord)
        {
            Vibrate(TimeSpan.FromMilliseconds(120));
            AppToast.Show("★ ¡Nuevo récord!", ToastKind.Record);
        }
        else
            Haptic();
    }

    private void OnRestFinished() => MainThread.BeginInvokeOnMainThread(() => Vibrate(TimeSpan.FromMilliseconds(400)));

    private async void OnDoneSetTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not DoneSet done) return;

        var action = await AppDialogs.ChooseAsync($"Serie {done.Number} · {done.Text}", [], destructive: "Borrar serie");
        if (action == "Borrar serie" && !_vm.DeleteSetCommand.IsRunning)
            await _vm.DeleteSetCommand.ExecuteAsync(done);
    }

    private async void OnWeightTapped(object? sender, TappedEventArgs e)
    {
        // TrySetWeight aplica el valor si es válido; si no, el error sale en la misma ventana
        await AppDialogs.PromptAsync("Peso (kg)", "Kilos de esta serie", initialValue: _vm.WeightText,
            keyboard: Keyboard.Numeric, maxLength: 7,
            validate: t => _vm.TrySetWeight(t) ? null : "Un número entre 0 y 1000, p. ej. 72,5.");
    }

    private async void OnRepsTapped(object? sender, TappedEventArgs e)
    {
        await AppDialogs.PromptAsync("Repeticiones", "Repeticiones de esta serie", initialValue: _vm.Reps.ToString(),
            keyboard: Keyboard.Numeric, maxLength: 3,
            validate: t => _vm.TrySetReps(t) ? null : "Un número entre 0 y 999.");
    }

    // Abre el primer vídeo o imagen del ejercicio con el visor del móvil
    private async void OnShowMediaTapped(object? sender, TappedEventArgs e)
    {
        var media = _vm.CurrentExercise?.Exercise?.Media.OrderBy(m => m.Order).FirstOrDefault();
        var storage = Handler?.MauiContext?.Services.GetService<IMediaStorage>();
        if (media is null || storage is null) return;

        var path = storage.GetFullPath(media.FileName);
        if (!File.Exists(path)) return;

        await PressAsync(sender as VisualElement);
        await Launcher.Default.OpenAsync(new OpenFileRequest(media.Kind == MediaKind.Video ? "Vídeo" : "Imagen", new ReadOnlyFile(path)));
    }

    // ── Finalizar ─────────────────────────────────────────────────────────

    private void BuildFeelingButtons()
    {
        for (var i = 0; i < Feelings.Length; i++)
        {
            var value = (i + 1).ToString();
            var button = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(0, 6),
                HorizontalOptions = LayoutOptions.Center,
                WidthRequest = 48,
                Content = new Label { Text = Feelings[i], FontSize = 24, HorizontalOptions = LayoutOptions.Center }
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                _ = PressAsync(button);
                _vm.SelectFeelingCommand.Execute(value);
            };
            button.GestureRecognizers.Add(tap);

            Grid.SetColumn(button, i);
            FeelingRow.Children.Add(button);
            _feelingButtons.Add(button);
        }
        StyleFeelingButtons();
    }

    private void StyleFeelingButtons()
    {
        for (var i = 0; i < _feelingButtons.Count; i++)
        {
            var selected = _vm.Feeling == i + 1;
            _feelingButtons[i].BackgroundColor = selected ? Color.FromArgb("#2EB4BEFE") : Colors.Transparent;
            _feelingButtons[i].Opacity = _vm.Feeling is null || selected ? 1 : 0.45;
        }
    }

    private async void OnSaveTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.SaveWorkoutCommand.IsRunning) return;
        await PressAsync(sender as VisualElement);
        await _vm.SaveWorkoutCommand.ExecuteAsync(null);
    }

    // ── Respuesta al tocar ────────────────────────────────────────────────

    private void OnPressTapped(object? sender, TappedEventArgs e) => _ = PressAsync(sender as VisualElement);

    // Escala 0,96 con rebote (guía visual §12.1)
    private static async Task PressAsync(VisualElement? element)
    {
        if (element is null || IsReduceMotionEnabled()) return;
        await element.ScaleToAsync(0.96, 60, Easing.CubicIn);
        await element.ScaleToAsync(1.0, 150, Easing.SpringOut);
    }

    private static async Task PopAsync(VisualElement element)
    {
        if (IsReduceMotionEnabled()) return;
        element.Scale = 0.4;
        await element.ScaleToAsync(1.0, 450, Easing.SpringOut);
    }

    private static void Haptic()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); }
        catch (Exception) { /* sin vibración en este dispositivo (p. ej. escritorio) */ }
    }

    private static void Vibrate(TimeSpan duration)
    {
        try { Vibration.Default.Vibrate(duration); }
        catch (Exception) { /* sin vibración en este dispositivo (p. ej. escritorio) */ }
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
