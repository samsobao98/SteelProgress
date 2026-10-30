using MauiIcons.Core;
using MauiIcons.Material;
using Microsoft.Maui.Controls.Shapes;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Controls;

/// <summary>
/// El menú de la app (maqueta «Abanico desde el logo», 2026-10-30): abajo solo está el botón del logo.
/// Un toque abre los apartados en abanico; mantener pulsado también lo abre, y deslizar el dedo
/// hasta un apartado y soltar va directo. Sustituye a la barra de pestañas de Shell, que sigue
/// por debajo (oculta) para navegar y para decidir qué apartados se ven (Clientes, Progreso).
/// </summary>
public static class FanMenuHost
{
    // Alto de la franja de abajo: deja sitio al botón sin tapar el final de las listas
    public const double BarHeight = 84;

    /// <summary>Envuelve el contenido de una página principal con la franja y el abanico.</summary>
    public static ContentPage Wrap(ContentPage page, string route)
    {
        // Soltar el contenido antes de moverlo: si se añade al Grid siendo aún el de la página,
        // al cambiar Content la página le quita el padre y los iconos del XAML salen como «?».
        var original = page.Content;
        page.Content = null;
        var host = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(BarHeight) }
        };
        host.Add(original, 0, 0);

        var menu = new FanMenu(route);
        host.Add(menu, 0, 0);
        Grid.SetRowSpan(menu, 2);

        page.Content = host;
        page.Appearing += (_, _) => menu.Refresh();
        page.Disappearing += (_, _) =>
        {
            menu.Close(animated: false);
            menu.StopLiveTime();
        };
        return page;
    }
}

public class FanMenu : Grid
{
    private sealed record Section(string Route, string Title, MaterialIcons Icon);

    // Orden del abanico, de izquierda a derecha
    private static readonly (string Route, MaterialIcons Icon)[] Order =
    {
        ("HomePage", MaterialIcons.Home),
        ("ClientsPage", MaterialIcons.Groups),
        ("ExercisePage", MaterialIcons.FitnessCenter),
        ("RoutinePage", MaterialIcons.EventNote),
        ("WorkoutPage", MaterialIcons.PlayArrow),
        ("ProgressPage", MaterialIcons.ShowChart)
    };

    private const double ButtonSize = 64;
    private const double ButtonBottom = 10;          // margen del botón sobre el borde
    private const double ItemSize = 52;
    private const double Radius = 160;
    private const double HitRadius = 44;             // distancia al centro de un apartado para «engancharlo» al deslizar
    private const double FromAngle = 160, ToAngle = 20;

    private static readonly Color Green = Color.FromArgb("#A6E3A1");
    private static readonly Color Bg = Color.FromArgb("#14161E");
    private static readonly Color Card = Color.FromArgb("#1B1E28");
    private static readonly Color Line = Color.FromArgb("#2A2E3C");
    private static readonly Color Text = Color.FromArgb("#E8EAF2");
    private static readonly Color Text2 = Color.FromArgb("#A0A7BD");
    private static readonly Color Text3 = Color.FromArgb("#8A91A8");

    private readonly string _route;
    private readonly BoxView _scrim;
    private readonly Border _button;
    private readonly Image _logo;
    private readonly Ellipse _liveRing;
    private readonly Border _liveTime;
    private readonly Label _liveTimeLabel;
    private IDispatcherTimer? _liveTicker;
    private readonly HorizontalStackLayout _current;
    private readonly Label _hint;
    private readonly List<(Section Section, Grid View, Border Circle, Label Label, double X, double Y)> _items = new();

    private bool _isOpen;
    private bool _openedByHold;     // el toque que suelta un «mantener pulsado» no debe cerrar
    private bool _isPanning;
    private string? _hot;
    private CancellationTokenSource? _holdCts;

    public FanMenu(string route)
    {
        _route = route;

        // Cerrado, la capa deja pasar los toques a la página (salvo la franja y el botón)
        InputTransparent = true;
        CascadeInputTransparent = false;
        RowDefinitions.Add(new RowDefinition(GridLength.Star));
        RowDefinitions.Add(new RowDefinition(FanMenuHost.BarHeight));

        // Franja de abajo: fondo de la app y, a la izquierda, dónde estás
        var bar = new BoxView { Color = Bg, InputTransparent = true };
        this.Add(bar, 0, 1);

        _current = new HorizontalStackLayout
        {
            Spacing = 6,
            Margin = new Thickness(20, 0, 0, ButtonBottom + ButtonSize / 2 - 9),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.End,
            InputTransparent = true
        };
        this.Add(_current, 0, 1);

        _scrim = new BoxView { Color = Color.FromArgb("#0A0B10"), Opacity = 0, IsVisible = false };
        var scrimTap = new TapGestureRecognizer();
        scrimTap.Tapped += (_, _) => Close();
        _scrim.GestureRecognizers.Add(scrimTap);
        this.Add(_scrim, 0, 0);
        this.SetRowSpan(_scrim, 2);

        _hint = new Label
        {
            Text = "Suelta sobre un apartado para ir",
            FontFamily = "SpaceGroteskRegular",
            FontSize = 12,
            TextColor = Text3,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, ButtonBottom + ButtonSize + Radius + 46),
            Opacity = 0,
            InputTransparent = true
        };
        this.Add(_hint, 0, 0);
        this.SetRowSpan(_hint, 2);

        // Botón del logo; el anillo verde avisa de un entreno en curso
        _liveRing = new Ellipse
        {
            Stroke = Green,
            StrokeThickness = 3,
            WidthRequest = ButtonSize + 10,
            HeightRequest = ButtonSize + 10,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, ButtonBottom - 5),
            IsVisible = false,
            InputTransparent = true
        };

        // Y encima del botón, el tiempo que lleva el entreno (el mismo cronómetro que Entreno)
        _liveTimeLabel = new Label
        {
            FontFamily = "SpaceGroteskBold",
            FontSize = 11,
            TextColor = Bg,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        _liveTime = new Border
        {
            BackgroundColor = Green,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 9 },
            Padding = new Thickness(7, 2),
            HeightRequest = 18,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, ButtonBottom + ButtonSize - 9),
            Content = _liveTimeLabel,
            IsVisible = false,
            InputTransparent = true
        };

        _logo = new Image { Source = "logo_marca.png", WidthRequest = 40, HeightRequest = 40 };
        _button = new Border
        {
            WidthRequest = ButtonSize,
            HeightRequest = ButtonSize,
            BackgroundColor = Card,
            Stroke = Line,
            StrokeThickness = 1,
            StrokeShape = new Ellipse(),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, ButtonBottom),
            Content = _logo
        };
        SemanticProperties.SetDescription(_button, "Menú de apartados");
        SemanticProperties.SetHint(_button, "Toca para ver los apartados");
        WireButtonGestures();
    }

    // ── Apartados ─────────────────────────────────────────────────────────

    /// <summary>Vuelve a leer qué apartados hay (cambian con la vista y el cliente activo) y el entreno en curso.</summary>
    public void Refresh()
    {
        BuildItems();
        _liveRing.IsVisible = WorkoutStateService.IsWorkoutActive;
        if (WorkoutStateService.IsWorkoutActive) StartLiveTime(); else StopLiveTime();

        var current = _items.FirstOrDefault(i => i.Section.Route == _route).Section;
        _current.Children.Clear();
        if (current is not null)
        {
            _current.Children.Add(new MauiIcon { Icon = current.Icon, IconColor = Text, IconSize = 18, VerticalOptions = LayoutOptions.Center });
            _current.Children.Add(new Label { Text = current.Title, FontFamily = "SpaceGroteskMedium", FontSize = 13, TextColor = Text3, VerticalOptions = LayoutOptions.Center });
        }
    }

    private void StartLiveTime()
    {
        UpdateLiveTime();
        if (_liveTicker is null)
        {
            _liveTicker = Dispatcher.CreateTimer();
            _liveTicker.Interval = TimeSpan.FromSeconds(1);
            _liveTicker.Tick += (_, _) => UpdateLiveTime();
        }
        _liveTicker.Start();
    }

    /// <summary>Para el cronómetro mientras la página no se ve.</summary>
    public void StopLiveTime()
    {
        _liveTicker?.Stop();
        _liveTime.IsVisible = false;
    }

    private void UpdateLiveTime()
    {
        // El entreno puede terminar mientras esta página sigue abierta debajo de otra
        if (!WorkoutStateService.IsWorkoutActive || WorkoutStateService.ActiveSessionStart is not DateTime start)
        {
            _liveTicker?.Stop();
            _liveTime.IsVisible = false;
            _liveRing.IsVisible = WorkoutStateService.IsWorkoutActive;
            return;
        }
        _liveTimeLabel.Text = WorkoutViewModel.FormatDuration(DateTime.Now - start);
        _liveTime.IsVisible = true;
    }

    private static List<Section> VisibleSections()
    {
        if (Shell.Current?.CurrentItem is not ShellItem tabBar) return new();
        var visible = tabBar.Items
            .Where(t => t.IsVisible && t.CurrentItem is not null)
            .ToDictionary(t => t.CurrentItem.Route, t => t.Title);
        return Order.Where(o => visible.ContainsKey(o.Route))
            .Select(o => new Section(o.Route, visible[o.Route], o.Icon))
            .ToList();
    }

    private void BuildItems()
    {
        foreach (var item in _items) Children.Remove(item.View);
        Children.Remove(_liveRing);
        Children.Remove(_button);
        Children.Remove(_liveTime);
        _items.Clear();

        var sections = VisibleSections();
        for (int i = 0; i < sections.Count; i++)
        {
            var s = sections[i];
            var t = sections.Count == 1 ? 0.5 : (double)i / (sections.Count - 1);
            var angle = (FromAngle - (FromAngle - ToAngle) * t) * Math.PI / 180;
            var x = Radius * Math.Cos(angle);
            var y = -Radius * Math.Sin(angle);

            var isCurrent = s.Route == _route;
            var isLiveWorkout = s.Route == "WorkoutPage" && WorkoutStateService.IsWorkoutActive;
            var circle = new Border
            {
                WidthRequest = ItemSize,
                HeightRequest = ItemSize,
                BackgroundColor = isLiveWorkout ? Green : Card,
                Stroke = isCurrent ? Green : Line,
                StrokeThickness = isCurrent ? 1.5 : 1,
                StrokeShape = new Ellipse(),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Start,
                Content = new MauiIcon
                {
                    Icon = s.Icon,
                    IconSize = 24,
                    IconColor = isLiveWorkout ? Bg : isCurrent ? Green : Text,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            var label = new Label
            {
                Text = isLiveWorkout ? "En curso" : s.Title,
                FontFamily = "SpaceGroteskMedium",
                FontSize = 11,
                TextColor = Text2,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End
            };
            // El círculo queda centrado sobre el botón; el texto cuelga debajo
            var view = new Grid
            {
                WidthRequest = 88,
                HeightRequest = ItemSize + 20,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 0, ButtonBottom + ButtonSize / 2 - (ItemSize + 20 - ItemSize / 2)),
                Opacity = 0,
                Scale = 0.3,
                IsVisible = false
            };
            view.Add(circle);
            view.Add(label);
            SemanticProperties.SetDescription(view, isLiveWorkout ? "Volver al entreno en curso" : s.Title);
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => Go(s.Route);
            view.GestureRecognizers.Add(tap);

            this.Add(view, 0, 0);
            this.SetRowSpan(view, 2);
            _items.Add((s, view, circle, label, x, y));
        }

        // El botón siempre encima de todo
        this.Add(_liveRing, 0, 0);
        this.SetRowSpan(_liveRing, 2);
        this.Add(_button, 0, 0);
        this.SetRowSpan(_button, 2);
        this.Add(_liveTime, 0, 0);
        this.SetRowSpan(_liveTime, 2);
    }

    // ── Abrir y cerrar ────────────────────────────────────────────────────

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        if (_items.Count == 0) Refresh();

        InputTransparent = false;
        _scrim.IsVisible = true;
        _ = _scrim.FadeToAsync(0.72, 180, Easing.CubicOut);
        _ = _logo.RotateToAsync(-31.5, 260, Easing.CubicOut);   // la barra del logo queda horizontal
        _ = _button.ScaleToAsync(1.06, 180, Easing.CubicOut);
        _button.Stroke = Green;
        _ = _current.FadeToAsync(0, 120, Easing.CubicOut);     // los apartados de abajo caen encima
        SemanticProperties.SetHint(_button, "Toca para cerrar");

        for (int i = 0; i < _items.Count; i++)
        {
            var (_, view, _, _, x, y) = _items[i];
            view.IsVisible = true;
            view.TranslationX = 0;
            view.TranslationY = 0;
            _ = AnimateItemIn(view, x, y, i * 22);
        }
    }

    private static async Task AnimateItemIn(View view, double x, double y, int delayMs)
    {
        if (delayMs > 0) await Task.Delay(delayMs);
        await Task.WhenAll(
            view.TranslateToAsync(x, y, 280, Easing.CubicOut),
            view.ScaleToAsync(1, 280, Easing.CubicOut),
            view.FadeToAsync(1, 180, Easing.CubicOut));
    }

    public void Close(bool animated = true)
    {
        _holdCts?.Cancel();
        SetHot(null);
        _hint.Opacity = 0;
        if (!_isOpen) return;
        _isOpen = false;
        _openedByHold = false;
        _button.Stroke = Line;
        SemanticProperties.SetHint(_button, "Toca para ver los apartados");

        if (!animated)
        {
            _current.Opacity = 1;
            _scrim.Opacity = 0;
            _scrim.IsVisible = false;
            _logo.Rotation = 0;
            _button.Scale = 1;
            foreach (var item in _items)
            {
                item.View.Opacity = 0;
                item.View.Scale = 0.3;
                item.View.TranslationX = item.View.TranslationY = 0;
                item.View.IsVisible = false;
            }
            InputTransparent = true;
            return;
        }

        _ = _logo.RotateToAsync(0, 200, Easing.CubicOut);
        _ = _button.ScaleToAsync(1, 160, Easing.CubicOut);
        _ = _current.FadeToAsync(1, 200, Easing.CubicOut);
        foreach (var item in _items)
        {
            var view = item.View;
            _ = Task.WhenAll(
                    view.TranslateToAsync(0, 0, 180, Easing.CubicIn),
                    view.ScaleToAsync(0.3, 180, Easing.CubicIn),
                    view.FadeToAsync(0, 140, Easing.CubicIn))
                .ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => { if (!_isOpen) view.IsVisible = false; }));
        }
        _ = _scrim.FadeToAsync(0, 180, Easing.CubicIn).ContinueWith(_ =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_isOpen) return;
                _scrim.IsVisible = false;
                InputTransparent = true;
            }));
    }

    private void Go(string route)
    {
        Close();
        if (route == _route) return;
        if (Shell.Current?.CurrentItem is not ShellItem tabBar) return;
        var tab = tabBar.Items.FirstOrDefault(t => t.CurrentItem?.Route == route);
        if (tab is not null) tabBar.CurrentItem = tab;
    }

    // ── Gestos del botón ──────────────────────────────────────────────────

    private void WireButtonGestures()
    {
        // Toque: abre o cierra. Si este mismo toque ya abrió el abanico al mantener pulsado, no lo cierra.
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (_openedByHold) { _openedByHold = false; return; }
            if (_isOpen) Close(); else Open();
        };
        _button.GestureRecognizers.Add(tap);

        // Mantener pulsado: abre sin soltar
        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += (_, _) => StartHold();
        pointer.PointerReleased += (_, _) =>
        {
            _holdCts?.Cancel();
            // Según el dispositivo, al soltar tras mantener pulsado llega un toque o no:
            // si no llega enseguida, se olvida para no tragarse el siguiente toque de verdad.
            if (_openedByHold) _ = ForgetHoldSoon();
        };
        _button.GestureRecognizers.Add(pointer);

        // Deslizar desde el botón: abre, resalta el apartado bajo el dedo y al soltar va a él
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPan;
        _button.GestureRecognizers.Add(pan);
    }

    private async Task ForgetHoldSoon()
    {
        await Task.Delay(350);
        _openedByHold = false;
    }

    private async void StartHold()
    {
        _holdCts?.Cancel();
        var cts = _holdCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(260, cts.Token);
        }
        catch (TaskCanceledException) { return; }
        if (_isOpen) return;
        _openedByHold = true;
        Open();
        _hint.Opacity = 1;
        Haptic();
    }

    private void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isPanning = true;
                _holdCts?.Cancel();
                if (!_isOpen) { _openedByHold = true; Open(); }
                _hint.Opacity = 1;
                break;
            case GestureStatus.Running:
                if (!_isPanning) return;
                // El dedo sale del centro del botón: el desplazamiento es la posición respecto a él
                SetHot(Nearest(e.TotalX, e.TotalY));
                break;
            case GestureStatus.Completed:
                _isPanning = false;
                _openedByHold = false;
                var target = _hot;
                _hint.Opacity = 0;
                if (target is not null) Go(target);   // soltar fuera de un apartado deja el abanico abierto
                break;
            case GestureStatus.Canceled:
                _isPanning = false;
                SetHot(null);
                break;
        }
    }

    private string? Nearest(double dx, double dy)
    {
        string? best = null;
        var bestDistance = HitRadius;
        foreach (var item in _items)
        {
            var d = Math.Sqrt(Math.Pow(dx - item.X, 2) + Math.Pow(dy - item.Y, 2));
            if (d < bestDistance) { bestDistance = d; best = item.Section.Route; }
        }
        return best;
    }

    private void SetHot(string? route)
    {
        if (route == _hot) return;
        _hot = route;
        foreach (var item in _items)
        {
            var hot = item.Section.Route == route;
            var isCurrent = item.Section.Route == _route;
            _ = item.View.ScaleToAsync(hot ? 1.14 : 1, 120, Easing.CubicOut);
            item.Circle.Stroke = hot || isCurrent ? Green : Line;
            item.Circle.StrokeThickness = hot ? 2.5 : isCurrent ? 1.5 : 1;
            item.Label.TextColor = hot ? Text : Text2;
        }
        if (route is not null) Haptic();
    }

    private static void Haptic()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); }
        catch { /* sin vibración en este dispositivo: no pasa nada */ }
    }
}
