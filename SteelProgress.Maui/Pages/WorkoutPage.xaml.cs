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
    // Ver AppDialogs.IsShowing: las ventanas propias son páginas modales
    private bool _coveredByPopup;

    private static readonly string[] Feelings = ["😫", "😕", "😐", "💪", "🔥"];

    private readonly WorkoutViewModel _vm;
    private readonly RestRingDrawable _ring = new();
    private readonly List<(Routine routine, Border chip, Label label)> _routineChips = new();
    private readonly List<Border> _feelingButtons = new();
    private IDispatcherTimer? _timer;

    // Por constructor: en el primer OnAppearing de la pestaña aún no hay Handler del que sacarlos
    private readonly Core.Sharing.RoutineSharingService _sharing;

    public WorkoutPage(WorkoutViewModel vm, ClientContextService clientContext, ViewOptionsService viewOptions,
        Core.Sharing.RoutineSharingService sharing)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _clientContext = clientContext;
        _viewOptions = viewOptions;
        _sharing = sharing;
        _vm.SendToCoachRequested += OnSendToCoachRequested;

        RestRing.Drawable = _ring;
        BuildFeelingButtons();

        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.Routines.CollectionChanged += OnRoutinesChanged;
        _vm.SetCompleted += OnSetCompleted;
        _vm.RestFinished += OnRestFinished;
    }

    private readonly ClientContextService _clientContext;

    private readonly ViewOptionsService _viewOptions;

    // Al entrar o salir de un cliente, la pantalla vuelve a cargar sus datos
    private void OnClientChanged() => MainThread.BeginInvokeOnMainThread(async () =>
    {
        UpdateNeedsClient();
        await _vm.RefreshAsync();
    });

    // Un perfil de entrenador solo entrena con un cliente: sin uno activo, se pide elegirlo
    private void UpdateNeedsClient() =>
        ChooseClientState.IsVisible = _viewOptions.Current.IsCoach
                                      && !_clientContext.HasActiveClient
                                      && !_vm.IsSessionActive;

    private void OnGoToClientsTapped(object? sender, TappedEventArgs e) =>
        _ = Shell.Current.GoToAsync("//ClientsPage");

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (AppDialogs.IsShowing) { _coveredByPopup = true; return; } // solo se abrió una ventana encima
        _clientContext.Changed -= OnClientChanged;
        _timer?.Stop();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_coveredByPopup) { _coveredByPopup = false; return; } // vuelve de una ventana propia, no de otra pantalla
        _clientContext.Changed -= OnClientChanged;
        _clientContext.Changed += OnClientChanged;
        UpdateNeedsClient();

        // El cronómetro y el descanso se calculan con la hora: al volver a la pestaña siguen bien
        _timer ??= CreateTimer();
        _timer.Start();

        // Si la app se cerró en mitad de un entreno, antes de nada: ¿continuarlo o descartarlo?
        if (await AskAboutUnsavedAsync())
        {
            WorkoutLaunch.Take(); // se sigue con el que quedó a medias, no se empieza otro
            return;
        }

        // Inicio o Rutinas han pedido empezar un día: se empieza directamente, sin pasar por preparar
        // (Salvo en un perfil de entrenador sin cliente: esa sesión no sería de nadie)
        if (WorkoutLaunch.Take() is int dayId && !ChooseClientState.IsVisible)
            await _vm.StartDayAsync(dayId);
        await _vm.RefreshAsync();
    }

    // Devuelve true si se ha retomado el entreno a medias
    private async Task<bool> AskAboutUnsavedAsync()
    {
        if (await _vm.FindUnsavedAsync() is not { } unsaved) return false;

        const string resume = "Continuar entreno";
        const string discard = "Descartarlo";
        var when = RoutineSchedule.AgoText(unsaved.Date, DateTime.Today);
        var sets = unsaved.SetCount == 1 ? "1 serie" : $"{unsaved.SetCount} series";
        var choice = await AppDialogs.ChooseAsync("Tienes un entreno sin guardar", [resume], destructive: discard,
            subtitle: $"{unsaved.DayName} · {when} a las {unsaved.Date:HH:mm} · {sets}. La app se cerró antes de guardarlo.");

        if (choice == resume)
        {
            await _vm.LoadSessionAsync(unsaved.Id);
            return true;
        }
        if (choice == discard &&
            await AppDialogs.ConfirmAsync("Descartar entreno", unsaved.SetCount == 1 ? $"Se borrará la serie de {unsaved.DayName}." : $"Se borrarán las {sets} de {unsaved.DayName}.", accept: "Descartar", destructive: true))
        {
            await _vm.DiscardUnsavedAsync(unsaved.Id);
            NotificationService.Info("Entreno descartado.");
        }
        return false;
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
            case nameof(WorkoutViewModel.IsSessionActive):
                UpdateNeedsClient();
                break;
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
            var label = new Label { Text = routine.Name, FontFamily = "SpaceGroteskMedium", FontSize = 14, VerticalOptions = LayoutOptions.Center };
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
                : new SolidColorBrush(Color.FromArgb(selected ? "#B3B7EC" : "#1B1E28"));
            label.TextColor = Color.FromArgb(selected ? "#14161E" : "#A0A7BD");
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

        // La nota existente se puede corregir siempre; añadir una nueva, solo si está permitido
        var noteAction = done.HasNotes ? "Editar nota" : _vm.CanNoteSets ? "Añadir nota" : null;
        var action = await AppDialogs.ChooseAsync($"Serie {done.Number} · {done.Text}", noteAction is null ? [] : [noteAction],
            destructive: "Borrar serie", subtitle: done.HasNotes ? done.Notes : null);
        if (action == "Borrar serie" && !_vm.DeleteSetCommand.IsRunning)
            await _vm.DeleteSetCommand.ExecuteAsync(done);
        else if (action == noteAction && noteAction is not null)
            await EditNoteAsync(done);
    }

    private async Task EditNoteAsync(DoneSet done)
    {
        var text = await AppDialogs.PromptAsync($"Nota de la serie {done.Number}", "Se verá en el historial y la próxima vez que hagas este ejercicio. Déjala vacía para borrarla.",
            done.Notes, "ej. molestia en el hombro, última con ayuda…", maxLength: 200, accept: "Guardar");
        if (text is null) return;
        try { await _vm.SetNoteAsync(done, text); }
        catch (Exception ex)
        {
            NotificationService.Error("No se pudo guardar la nota.");
            System.Diagnostics.Debug.WriteLine(ex);
        }
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
            _feelingButtons[i].BackgroundColor = selected ? Color.FromArgb("#2EB3B7EC") : Colors.Transparent;
            _feelingButtons[i].Opacity = _vm.Feeling is null || selected ? 1 : 0.45;
        }
    }

    private async void OnSaveTapped(object? sender, TappedEventArgs e)
    {
        if (_vm.SaveWorkoutCommand.IsRunning) return;
        await PressAsync(sender as VisualElement);
        await _vm.SaveWorkoutCommand.ExecuteAsync(null);
    }

    // ── Enviar el entreno a tu entrenador ─────────────────────────────────

    private void OnSendToCoachTapped(object? sender, TappedEventArgs e) => _vm.SendToCoach = !_vm.SendToCoach;

    // Ya guardado: se crea el archivo y se abre el menú de compartir (WhatsApp, Telegram, correo…)
    private async void OnSendToCoachRequested(int sessionId, string coach, string dayName)
    {
        try
        {
            var folder = System.IO.Path.Combine(FileSystem.CacheDirectory, "compartir");
            var name = $"Entreno {dayName} {DateTime.Now:dd-MM}";
            var path = await _sharing.ExportWorkoutToFileAsync(sessionId, name, folder);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"Enviar a {coach}",
                File = new ShareFile(path, "application/octet-stream")
            });
        }
        catch (Exception ex) when (ex is Core.Sharing.SteelPackageException or IOException)
        {
            NotificationService.Error($"No se pudo crear el archivo: {ex.Message}");
        }
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
