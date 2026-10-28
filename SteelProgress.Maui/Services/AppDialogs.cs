using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Shapes;

namespace SteelProgress.Maui.Services;

/// <summary>
/// Ventanas con el diseño de la app en lugar de los diálogos del sistema (docs/PLAN_VENTANAS.md):
/// menú de opciones (panel inferior), confirmación, aviso con un botón y petición de texto.
/// Se usan igual que DisplayActionSheetAsync / DisplayAlertAsync / DisplayPromptAsync.
/// </summary>
public static class AppDialogs
{
    // Una ventana cada vez: un doble toque no abre dos
    private static bool _isOpen;

    /// <summary>Menú de opciones. Devuelve la opción elegida o null si se cancela.</summary>
    public static Task<string?> ChooseAsync(string title, IEnumerable<string> options, string? destructive = null, string? subtitle = null) =>
        ShowAsync<string?>(new ActionSheetView(title, subtitle, options.ToList(), destructive), bottom: true, fallback: null);

    /// <summary>Confirmación con dos botones; destructive pinta el principal en rojo.</summary>
    public static async Task<bool> ConfirmAsync(string title, string message, string accept = "Aceptar", string cancel = "Cancelar", bool destructive = false) =>
        await ShowAsync<bool?>(new ConfirmView(title, message, accept, cancel, destructive), bottom: false, fallback: false) == true;

    /// <summary>Aviso con un solo botón.</summary>
    public static Task AlertAsync(string title, string message, string ok = "Entendido") =>
        ShowAsync<bool?>(new ConfirmView(title, message, ok, cancel: null, destructive: false), bottom: false, fallback: false);

    /// <summary>
    /// Pide un texto. validate devuelve el mensaje de error (o null si vale) y se muestra
    /// dentro de la misma ventana. Devuelve null si se cancela.
    /// </summary>
    public static Task<string?> PromptAsync(string title, string? message = null, string initialValue = "", string? placeholder = null,
        Keyboard? keyboard = null, int maxLength = 100, string accept = "Aceptar", string cancel = "Cancelar",
        Func<string, string?>? validate = null) =>
        ShowAsync<string?>(new PromptView(title, message, initialValue, placeholder, keyboard ?? Keyboard.Default, maxLength, accept, cancel, validate),
            bottom: false, fallback: null);

    /// <summary>Aviso breve como tarjeta abajo que se cierra sola (alternativa al Snackbar en Windows).</summary>
    internal static async Task ShowToastCardAsync(string message, Color background, Color text, string? actionText, Action? action, TimeSpan duration)
    {
        var page = CurrentPage();
        if (page is null || _isOpen) return;

        var view = new ToastCardView(message, background, text, actionText, action);
        var options = new PopupOptions
        {
            PageOverlayColor = Colors.Transparent,
            Shape = null,
            Shadow = null,
            CanBeDismissedByTappingOutsideOfPopup = true
        };
        var shown = page.ShowPopupAsync<bool?>(view, options);
        _ = Task.Delay(duration).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => view.Dismiss()), TaskScheduler.Default);
        await shown;
    }

    private static async Task<T> ShowAsync<T>(DialogView<T> view, bool bottom, T fallback)
    {
        var page = CurrentPage();
        if (page is null || _isOpen) return fallback;

        _isOpen = true;
        try
        {
            var options = new PopupOptions
            {
                PageOverlayColor = Color.FromArgb("#990A0A10"),
                Shape = null,
                Shadow = null,
                CanBeDismissedByTappingOutsideOfPopup = true
            };
            view.VerticalOptions = bottom ? LayoutOptions.End : LayoutOptions.Center;
            var result = await page.ShowPopupAsync<T>(view, options);
            return result.WasDismissedByTappingOutsideOfPopup ? fallback : result.Result ?? fallback;
        }
        finally { _isOpen = false; }
    }

    private static Page? CurrentPage()
    {
        var root = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (root is Shell shell) return shell.CurrentPage ?? shell;
        return root?.Navigation.ModalStack.LastOrDefault() ?? root?.Navigation.NavigationStack.LastOrDefault() ?? root;
    }

    // ── Piezas comunes ────────────────────────────────────────────────────

    internal static Color Token(string key, string fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c ? c : Color.FromArgb(fallback);

    internal static Brush SteelBrush =>
        Application.Current?.Resources.TryGetValue("SpSteelBrush", out var b) == true && b is Brush brush ? brush : new SolidColorBrush(Color.FromArgb("#B4BEFE"));

    internal static Border Button(string text, ButtonKind kind, Action onTap)
    {
        var label = new Label
        {
            Text = text,
            FontFamily = "OpenSansSemibold",
            FontSize = 15,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            TextColor = kind switch
            {
                ButtonKind.Primary => Token("SpOnAccent", "#1B1B2B"),
                ButtonKind.Danger => Color.FromArgb("#1B1B2B"),
                _ => Token("SpText", "#CDD6F4")
            }
        };
        var button = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(0, 14),
            Content = label,
            Background = kind switch
            {
                ButtonKind.Primary => SteelBrush,
                ButtonKind.Danger => new SolidColorBrush(Token("SpDanger", "#F38BA8")),
                _ => new SolidColorBrush(Token("SpCardHi", "#2C2D42"))
            }
        };
        SemanticProperties.SetDescription(button, text);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            await button.ScaleToAsync(0.96, 60, Easing.CubicIn);
            await button.ScaleToAsync(1.0, 120, Easing.SpringOut);
            onTap();
        };
        button.GestureRecognizers.Add(tap);
        return button;
    }

    internal static Label Title(string text) => new()
    {
        Text = text,
        FontFamily = "OpenSansSemibold",
        FontSize = 18,
        TextColor = Token("SpText", "#CDD6F4"),
        LineBreakMode = LineBreakMode.WordWrap
    };

    internal static Label Secondary(string text) => new()
    {
        Text = text,
        FontFamily = "OpenSansRegular",
        FontSize = 14,
        TextColor = Token("SpText2", "#A6ADC8"),
        LineBreakMode = LineBreakMode.WordWrap
    };

    internal enum ButtonKind { Primary, Secondary, Danger }
}

/// <summary>Base de las ventanas: un Popup que se cierra una sola vez con su resultado.</summary>
internal abstract class DialogView<T> : Popup<T>
{
    private bool _closing;

    protected DialogView()
    {
        BackgroundColor = Colors.Transparent;
        Padding = 0;
    }

    protected async void Finish(T result)
    {
        if (_closing) return;
        _closing = true;
        await CloseAsync(result);
    }
}

/// <summary>Aviso breve: mensaje y, si hay, botón de acción («Deshacer»).</summary>
internal sealed class ToastCardView : DialogView<bool?>
{
    public ToastCardView(string message, Color background, Color text, string? actionText, Action? action)
    {
        VerticalOptions = LayoutOptions.End;
        HorizontalOptions = LayoutOptions.Center;
        Margin = new Thickness(20, 0, 20, 32);

        var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 16 };
        grid.Add(new Label
        {
            Text = message,
            FontFamily = "OpenSansRegular",
            FontSize = 14,
            TextColor = text,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        }, 0);
        if (actionText is { Length: > 0 } && action is not null)
        {
            var button = new Label
            {
                Text = actionText,
                FontFamily = "OpenSansSemibold",
                FontSize = 14,
                TextColor = AppDialogs.Token("SpSteel2", "#B4BEFE"),
                VerticalOptions = LayoutOptions.Center
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => { action(); Finish(true); };
            button.GestureRecognizers.Add(tap);
            grid.Add(button, 1);
        }

        Content = new Border
        {
            BackgroundColor = background,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(16, 12),
            MaximumWidthRequest = 480,
            Content = grid
        };
    }

    public void Dismiss() => Finish(null);
}

/// <summary>Menú de opciones como panel inferior: filas, la peligrosa en rojo y «Cancelar» abajo.</summary>
internal sealed class ActionSheetView : DialogView<string?>
{
    public ActionSheetView(string title, string? subtitle, IReadOnlyList<string> options, string? destructive)
    {
        HorizontalOptions = LayoutOptions.Fill;
        Margin = 0;

        var stack = new VerticalStackLayout { Spacing = 0 };
        stack.Add(new BoxView
        {
            HeightRequest = 4, WidthRequest = 40, CornerRadius = 2,
            Color = AppDialogs.Token("SpText4", "#45475A"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 14)
        });
        stack.Add(AppDialogs.Title(title));
        if (!string.IsNullOrWhiteSpace(subtitle))
            stack.Add(AppDialogs.Secondary(subtitle));

        var list = new VerticalStackLayout { Spacing = 0, Margin = new Thickness(0, 10, 0, 0) };
        foreach (var option in options)
            list.Add(Row(option, danger: false));
        if (destructive is not null)
            list.Add(Row(destructive, danger: true));
        stack.Add(list);

        var cancel = AppDialogs.Button("Cancelar", AppDialogs.ButtonKind.Secondary, () => Finish(null));
        cancel.Margin = new Thickness(0, 14, 0, 0);
        stack.Add(cancel);

        Content = new Border
        {
            BackgroundColor = AppDialogs.Token("SpSheet", "#232334"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) },
            Padding = new Thickness(20, 12, 20, 28),
            Content = new ScrollView { Content = stack, MaximumHeightRequest = 560 }
        };
    }

    private View Row(string text, bool danger)
    {
        var label = new Label
        {
            Text = text,
            FontFamily = "OpenSansSemibold",
            FontSize = 16,
            TextColor = danger ? AppDialogs.Token("SpDanger", "#F38BA8") : AppDialogs.Token("SpText", "#CDD6F4"),
            VerticalOptions = LayoutOptions.Center
        };
        var row = new Border
        {
            BackgroundColor = Colors.Transparent,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(12, 15),
            Content = label
        };
        SemanticProperties.SetDescription(row, text);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            row.BackgroundColor = AppDialogs.Token("SpCardHi", "#2C2D42");
            await Task.Delay(70);
            Finish(text);
        };
        row.GestureRecognizers.Add(tap);
        return row;
    }
}

/// <summary>Confirmación (dos botones) o aviso (uno): tarjeta centrada.</summary>
internal sealed class ConfirmView : DialogView<bool?>
{
    public ConfirmView(string title, string message, string accept, string? cancel, bool destructive)
    {
        Margin = new Thickness(24, 0);

        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Add(AppDialogs.Title(title));
        if (!string.IsNullOrWhiteSpace(message))
            stack.Add(AppDialogs.Secondary(message));

        var primary = AppDialogs.Button(accept, destructive ? AppDialogs.ButtonKind.Danger : AppDialogs.ButtonKind.Primary, () => Finish(true));
        if (cancel is null)
        {
            primary.Margin = new Thickness(0, 12, 0, 0);
            stack.Add(primary);
        }
        else
        {
            var buttons = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8, Margin = new Thickness(0, 12, 0, 0) };
            buttons.Add(AppDialogs.Button(cancel, AppDialogs.ButtonKind.Secondary, () => Finish(false)), 0);
            buttons.Add(primary, 1);
            stack.Add(buttons);
        }

        Content = Card(stack);
    }

    internal static Border Card(View content) => new()
    {
        BackgroundColor = AppDialogs.Token("SpSheet", "#232334"),
        Stroke = new SolidColorBrush(AppDialogs.Token("SpLine", "#34354A")),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 22 },
        Padding = new Thickness(20),
        MaximumWidthRequest = 440,
        Content = content
    };
}

/// <summary>Pide un texto: campo con foco automático y error dentro de la misma tarjeta.</summary>
internal sealed class PromptView : DialogView<string?>
{
    private readonly Entry _entry;

    public PromptView(string title, string? message, string initialValue, string? placeholder, Keyboard keyboard,
        int maxLength, string accept, string cancel, Func<string, string?>? validate)
    {
        Margin = new Thickness(24, 0);

        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Add(AppDialogs.Title(title));
        if (!string.IsNullOrWhiteSpace(message))
            stack.Add(AppDialogs.Secondary(message));

        _entry = new Entry
        {
            Text = initialValue,
            Placeholder = placeholder,
            Keyboard = keyboard,
            MaxLength = maxLength,
            FontSize = 16,
            TextColor = AppDialogs.Token("SpText", "#CDD6F4"),
            PlaceholderColor = AppDialogs.Token("SpText4", "#45475A"),
            BackgroundColor = Colors.Transparent,
            ReturnType = ReturnType.Done
        };
        stack.Add(new Border
        {
            BackgroundColor = AppDialogs.Token("SpBg", "#1E1E2E"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(12, 2),
            Margin = new Thickness(0, 6, 0, 0),
            Content = _entry
        });

        var error = new Label
        {
            FontFamily = "OpenSansRegular",
            FontSize = 13,
            TextColor = AppDialogs.Token("SpDanger", "#F38BA8"),
            IsVisible = false
        };
        stack.Add(error);

        void Submit()
        {
            var text = _entry.Text ?? "";
            var problem = validate?.Invoke(text);
            if (problem is not null)
            {
                error.Text = problem;
                error.IsVisible = true;
                return;
            }
            _entry.Unfocus();
            Finish(text);
        }

        _entry.Completed += (_, _) => Submit();
        _entry.TextChanged += (_, _) => error.IsVisible = false;

        var buttons = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Add(AppDialogs.Button(cancel, AppDialogs.ButtonKind.Secondary, () => { _entry.Unfocus(); Finish(null); }), 0);
        buttons.Add(AppDialogs.Button(accept, AppDialogs.ButtonKind.Primary, Submit), 1);
        stack.Add(buttons);

        Content = ConfirmView.Card(stack);
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        await Task.Delay(120); // tras la animación de apertura, para que el teclado aparezca bien
        _entry.Focus();
        _entry.CursorPosition = 0;
        _entry.SelectionLength = _entry.Text?.Length ?? 0;
    }
}
