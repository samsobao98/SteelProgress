using SteelProgress.Maui.Services;
using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Pages;

public partial class LauncherPage : ContentPage
{
    private readonly LauncherViewModel _vm;
    private readonly IServiceProvider _services;
    private AppShell? _shell; // el de la entrada actual en un perfil
    private bool _hasLoaded;
    private bool _isEntering;

    // ── Color picker ────────────────────────────────────────────────────
    private readonly Dictionary<string, (Border dot, Border halo)> _colorDots = new();
    private string? _currentSelectedColor;

    // ── Idle pulse (halo only) ─────────────────────────────────────────
    private CancellationTokenSource _idlePulseCts = new();

    // ── Floating labels ────────────────────────────────────────────────
    private bool _floatingLabelSetup;
    private string _lastInitial = "?";

    // ── Overlay edit ─────────────────────────────────────────────────────
    private readonly Dictionary<string, (Border dot, Border halo)> _overlayColorDots = new();
    private bool _isOverlayEditing;
    private bool _overlayEditEntriesSetup;

    public ICommand ConfirmDeleteCommand { get; }

    public LauncherPage(LauncherViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _services = services;
        vm.ProfileChosen += () =>
        {
            _shell?.Detach();
            var shell = _shell = _services.GetRequiredService<AppShell>();
            shell.Opacity = 0;
            Application.Current!.Windows[0].Page = shell;
            _ = shell.FadeToAsync(1, 420, Easing.CubicOut);
            // Si se abrió un .steel desde otra app antes de elegir perfil
            MainThread.BeginInvokeOnMainThread(() => _ = Services.PendingImport.TryOpenAsync());
        };
        vm.ProfileUpdated += OnProfileUpdated;
        ConfirmDeleteCommand = new Command<UserProfile>(async p => await ConfirmAndDeleteAsync(p));
        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.Profiles.CollectionChanged += (_, _) =>
            MainThread.BeginInvokeOnMainThread(() => { if (!_vm.IsCreating) BuildProfileList(); });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_hasLoaded)
        {
            // De vuelta desde «Cambiar de perfil»: la lista se recarga (nombres, colores y última línea al día)
            await _vm.LoadAsync(Task.CompletedTask);
            if (!_vm.IsCreating && _vm.HasProfiles) BuildProfileList();
            return;
        }
        _hasLoaded = true;

        var app = (App)Application.Current!;
        await _vm.LoadAsync(app.MigrationTask);

        // OnAppearing es async void: lo que escape de aquí se pierde sin rastro y
        // deja la página a medio montar. Nada debajo debe poder tumbar el arranque.
        try
        {
            if (!_vm.IsCreating && _vm.HasProfiles)
                BuildProfileList();

            // Entrada tranquila: cabecera, título y lista aparecen uno tras otro
            ProfilesSection.TranslationY = 12;
            await HeaderSection.FadeToAsync(1, 220, Easing.CubicOut);
            await SubtitleLabel.FadeToAsync(1, 220, Easing.CubicOut);
            await Task.WhenAll(
                ProfilesSection.FadeToAsync(1, 260, Easing.CubicOut),
                ProfilesSection.TranslateToAsync(0, 0, 260, Easing.CubicOut)
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LauncherPage.OnAppearing: {ex}");

            // Todo se anima desde Opacity=0; si el fallo llega antes de que
            // termine, hay que dejarlo visible o la pantalla se queda en blanco.
            HeaderSection.Opacity = 1;
            SubtitleLabel.Opacity = 1;
            ProfilesSection.Opacity = 1;
            ProfilesSection.TranslationY = 0;
        }
    }

    /// <summary>
    /// «Cambiar de perfil» (pantalla Perfil): la app se cierra sobre sí misma y vuelve esta pantalla.
    /// El Shell de este perfil se descarta: el siguiente perfil entra con uno nuevo, desde Inicio.
    /// </summary>
    public async Task ReturnToProfilesAsync()
    {
        var window = Application.Current!.Windows[0];
        if (_shell is { } shell)
        {
            await shell.FadeToAsync(0, 180, Easing.CubicIn);
            shell.Detach();
            _shell = null;
        }

        Opacity = 0;
        window.Page = this;
        await this.FadeToAsync(1, 260, Easing.CubicOut);
    }

    // ── Color picker ──────────────────────────────────────────────────────

    private void BuildColorPicker()
    {
        ColorPickerContainer.Children.Clear();
        _colorDots.Clear();

        foreach (var color in _vm.AvailableColors)
        {
            var capturedColor = color;

            var halo = new Border
            {
                BackgroundColor = Color.FromArgb(color),
                WidthRequest = 54,
                HeightRequest = 54,
                Opacity = 0,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 27 },
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var dot = new Border
            {
                BackgroundColor = Color.FromArgb(color),
                WidthRequest = 36,
                HeightRequest = 36,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var cell = new Grid
            {
                WidthRequest = 54,
                HeightRequest = 62,
                HorizontalOptions = LayoutOptions.Center
            };
            cell.Add(halo);
            cell.Add(dot);

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => OnColorDotTapped(capturedColor);
            cell.GestureRecognizers.Add(tap);

            _colorDots[color] = (dot, halo);
            ColorPickerContainer.Children.Add(cell);
        }
    }

    private void OnColorDotTapped(string color)
    {
        if (color == _currentSelectedColor) return;

        if (_currentSelectedColor != null && _colorDots.TryGetValue(_currentSelectedColor, out var old))
        {
            _ = old.dot.ScaleToAsync(1.0, 200, Easing.CubicOut);
            _ = old.dot.TranslateToAsync(0, 0, 200, Easing.CubicOut);
            _ = old.halo.FadeToAsync(0, 200, Easing.CubicOut);
        }

        _currentSelectedColor = color;
        _vm.SetColorCommand.Execute(color);

        if (_colorDots.TryGetValue(color, out var n))
            _ = AnimateDotSelect(n.dot, n.halo);

        _ = AnimateAvatarColorBounce();
    }

    private static async Task AnimateDotSelect(Border dot, Border halo)
    {
        _ = halo.FadeToAsync(0.35, 220, Easing.CubicOut);
        await dot.ScaleToAsync(1.45, 120, Easing.CubicOut);
        await Task.WhenAll(
            dot.ScaleToAsync(1.15, 100, Easing.CubicIn),
            dot.TranslateToAsync(0, -6, 100, Easing.CubicOut)
        );
    }

    private void SyncColorDotSelection(string color)
    {
        foreach (var (c, pair) in _colorDots)
        {
            if (c == color)
            {
                pair.dot.Scale = 1.15;
                pair.dot.TranslationY = -6;
                pair.halo.Opacity = 0.35;
            }
            else
            {
                pair.dot.Scale = 1.0;
                pair.dot.TranslationY = 0;
                pair.halo.Opacity = 0;
            }
        }
        _currentSelectedColor = color;
    }

    // ── Avatar animations ─────────────────────────────────────────────────

    private async Task AnimateAvatarColorBounce()
    {
        await AvatarBorder.ScaleToAsync(0.82, 80, Easing.CubicIn);
        await AvatarBorder.ScaleToAsync(1.18, 160, Easing.CubicOut);
        await AvatarBorder.ScaleToAsync(1.0, 120, Easing.CubicIn);
    }

    private async Task AnimateAvatarInitialBounce()
    {
        await AvatarInitialLabel.ScaleToAsync(0.0, 60, Easing.CubicIn);
        await AvatarInitialLabel.ScaleToAsync(1.3, 120, Easing.CubicOut);
        await AvatarInitialLabel.ScaleToAsync(1.0, 100, Easing.CubicIn);
    }

    // ── Idle pulse (halo breathes) ────────────────────────────────────────

    private void StartIdlePulse()
    {
        _idlePulseCts.Cancel();
        _idlePulseCts = new CancellationTokenSource();
        _ = IdlePulseLoop(_idlePulseCts.Token);
    }

    private void StopIdlePulse()
    {
        _idlePulseCts.Cancel();
        AvatarHalo.Scale = 1.0;
        AvatarHalo.Opacity = 0.18;
    }

    private async Task IdlePulseLoop(CancellationToken ct)
    {
        try
        {
            await Task.Delay(600, ct);
            while (!ct.IsCancellationRequested)
            {
                await Task.WhenAll(
                    AvatarHalo.ScaleToAsync(1.12, 1100, Easing.SinInOut),
                    AvatarHalo.FadeToAsync(0.27, 1100, Easing.SinInOut)
                );
                if (ct.IsCancellationRequested) break;
                await Task.WhenAll(
                    AvatarHalo.ScaleToAsync(1.0, 1100, Easing.SinInOut),
                    AvatarHalo.FadeToAsync(0.18, 1100, Easing.SinInOut)
                );
                if (ct.IsCancellationRequested) break;
                await Task.Delay(400, ct);
            }
        }
        catch (TaskCanceledException) { }
    }

    // ── Floating labels ───────────────────────────────────────────────────

    private void SetupFloatingLabel()
    {
        if (_floatingLabelSetup) return;
        _floatingLabelSetup = true;

        // Nombre
        NameEntry.Focused += OnNameFocused;
        NameEntry.Unfocused += OnNameUnfocused;
        NameEntry.TextChanged += OnNameTextChanged;
        NameEntry.Completed += (_, _) => FraseEntry.Focus();

        // Frase
        FraseEntry.Focused += OnFraseFocused;
        FraseEntry.Unfocused += OnFraseUnfocused;
    }

    private void OnNameFocused(object? sender, FocusEventArgs e)
    {
        StopIdlePulse();
        AnimateLabelUp(FloatingLabel, BottomLineActive);
    }

    private void OnNameUnfocused(object? sender, FocusEventArgs e)
    {
        AnimateUnderlineOut(BottomLineActive);
        if (string.IsNullOrWhiteSpace(NameEntry.Text))
            AnimateLabelDown(FloatingLabel);
        StartIdlePulse();
    }

    private void OnNameTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = e.NewTextValue ?? string.Empty;
        var newInitial = string.IsNullOrWhiteSpace(text)
            ? "?"
            : text.TrimStart()[0].ToString().ToUpperInvariant();

        if (newInitial != _lastInitial)
        {
            _lastInitial = newInitial;
            _ = AnimateAvatarInitialBounce();
        }
    }

    private void OnFraseFocused(object? sender, FocusEventArgs e)
    {
        StopIdlePulse();
        AnimateLabelUp(FraseLabel, FraseLineActive);
    }

    private void OnFraseUnfocused(object? sender, FocusEventArgs e)
    {
        AnimateUnderlineOut(FraseLineActive);
        if (string.IsNullOrWhiteSpace(FraseEntry.Text))
            AnimateLabelDown(FraseLabel);
        StartIdlePulse();
    }

    private static void AnimateLabelUp(Label label, BoxView activeLine)
    {
        _ = label.TranslateToAsync(0, 0, 180, Easing.CubicOut);
        _ = label.ScaleToAsync(0.75, 180, Easing.CubicOut);
        label.TextColor = Color.FromArgb("#A6E3A1");
        AnimateUnderlineIn(activeLine);
    }

    private static void AnimateLabelDown(Label label)
    {
        _ = label.TranslateToAsync(0, 22, 180, Easing.CubicOut);
        _ = label.ScaleToAsync(1.0, 180, Easing.CubicOut);
        label.TextColor = Color.FromArgb("#8A91A8");
    }

    private static void AnimateUnderlineIn(BoxView line)
    {
        new Animation(v => line.ScaleX = v, line.ScaleX, 1.0)
            .Commit(line, "underline", length: 220, easing: Easing.CubicOut);
    }

    private static void AnimateUnderlineOut(BoxView line)
    {
        new Animation(v => line.ScaleX = v, line.ScaleX, 0.0)
            .Commit(line, "underline", length: 180, easing: Easing.CubicOut);
    }

    // ── ViewModel property changes ─────────────────────────────────────────

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.IsCreating) && _vm.IsCreating)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _lastInitial = "?";
                ResetFormState();
                BuildColorPicker();
                SyncColorDotSelection(_vm.SelectedColor);
                SetupFloatingLabel();
                StartIdlePulse();
            });
        }
        else if (e.PropertyName == nameof(LauncherViewModel.IsCreating) && !_vm.IsCreating && _vm.HasProfiles)
        {
            MainThread.BeginInvokeOnMainThread(() => BuildProfileList());
        }
        else if (e.PropertyName == nameof(LauncherViewModel.SelectedColor)
                 && _vm.SelectedColor != _currentSelectedColor)
        {
            MainThread.BeginInvokeOnMainThread(() => SyncColorDotSelection(_vm.SelectedColor));
        }
        else if (e.PropertyName == nameof(LauncherViewModel.EditingColor) && _isOverlayEditing)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OverlayEditAvatar.BackgroundColor = Color.FromArgb(_vm.EditingColor);
                SyncOverlayColorPicker(_vm.EditingColor);
            });
        }
    }

    private void ResetFormState()
    {
        FloatingLabel.TranslationY = 22;
        FloatingLabel.Scale = 1.0;
        FloatingLabel.TextColor = Color.FromArgb("#8A91A8");
        BottomLineActive.ScaleX = 0;
        FraseLabel.TranslationY = 22;
        FraseLabel.Scale = 1.0;
        FraseLabel.TextColor = Color.FromArgb("#8A91A8");
        FraseLineActive.ScaleX = 0;
        AvatarBorder.Scale = 1.0;
        AvatarHalo.Scale = 1.0;
        AvatarHalo.Opacity = 0.18;
        AvatarInitialLabel.Scale = 1.0;
    }

    // ── Lista de perfiles ─────────────────────────────────────────────────

    private void BuildProfileList()
    {
        ProfileList.Children.Clear();
        foreach (var profile in _vm.Profiles)
            ProfileList.Children.Add(BuildProfileRow(profile));
    }

    // Fila: avatar, nombre, una línea de contexto y ⋯. Tocar la fila entra; ⋯ abre editar/eliminar.
    private View BuildProfileRow(UserProfile profile)
    {
        var avatar = new Border
        {
            BackgroundColor = Color.FromArgb(profile.AvatarColor),
            WidthRequest = 44,
            HeightRequest = 44,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            VerticalOptions = LayoutOptions.Center,
            Content = new Grid
            {
                new Label
                {
                    Text = InitialOf(profile.Name),
                    TextColor = (Color)Resources["SpOnAccent"],
                    FontFamily = "SpaceGroteskBold",
                    FontSize = 18,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };
        // Con foto, la foto tapa la inicial (el color queda de fondo mientras carga)
        if (_vm.PhotoPathOf(profile) is string photo && avatar.Content is Grid avatarGrid)
            avatarGrid.Add(new Image { Source = ImageSource.FromFile(photo), Aspect = Aspect.AspectFill });

        var text = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = profile.Name,
                    Style = (Style)Resources["SpHeading"],
                    MaxLines = 1,
                    LineBreakMode = LineBreakMode.TailTruncation
                },
                new Label
                {
                    Text = _vm.SummaryOf(profile),
                    Style = (Style)Resources["SpSecondary"],
                    MaxLines = 1,
                    LineBreakMode = LineBreakMode.TailTruncation
                }
            }
        };

        // ⋯ con su propia zona táctil de 48 dp: no debe disparar el toque de la fila
        var more = new Grid
        {
            WidthRequest = 48,
            HeightRequest = 48,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "⋯",
                    FontSize = 22,
                    TextColor = (Color)Resources["SpText3"],
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }
        };
        SemanticProperties.SetDescription(more, $"Opciones de {profile.Name}");
        var moreTap = new TapGestureRecognizer();
        moreTap.Tapped += (_, _) => _ = ShowProfileOverlay(profile);
        more.GestureRecognizers.Add(moreTap);

        // Zona de «entrar»: avatar y textos, hermana de ⋯ y no madre (en Android un toque
        // en un hijo también puede llegar a un reconocedor del padre). El fondo transparente
        // hace que reciba toques también en el hueco entre avatar y textos.
        var enter = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 14,
            BackgroundColor = Colors.Transparent
        };
        enter.Add(avatar, 0);
        enter.Add(text, 1);

        var content = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 4
        };
        content.Add(enter, 0);
        content.Add(more, 1);

        var row = new Border
        {
            Style = (Style)Resources["SpCardStyle"],
            Padding = new Thickness(14, 10, 4, 10),
            MinimumHeightRequest = 68,
            Content = content
        };

        SemanticProperties.SetDescription(enter, $"Entrar como {profile.Name}");
        var enterTap = new TapGestureRecognizer();
        enterTap.Tapped += async (_, _) => await EnterProfileAsync(row, profile);
        enter.GestureRecognizers.Add(enterTap);
        return row;
    }

    private void ShowOverlayPhoto(UserProfile profile)
    {
        var photo = _vm.PhotoPathOf(profile);
        OverlayPhoto.Source = photo is null ? null : ImageSource.FromFile(photo);
        OverlayPhoto.IsVisible = photo is not null;
    }

    private static string InitialOf(string name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : name.TrimStart()[0].ToString().ToUpperInvariant();

    // Un toque corto en la fila y se entra; la app aparece con su propio fundido (ProfileChosen).
    private async Task EnterProfileAsync(View row, UserProfile profile)
    {
        if (_isEntering) return;
        _isEntering = true;
        try
        {
            await row.ScaleToAsync(0.97, 90, Easing.CubicOut);
            await row.ScaleToAsync(1.0, 120, Easing.CubicOut);
            _vm.SelectProfileCommand.Execute(profile);
        }
        finally
        {
            _isEntering = false;
        }
    }

    // ── Profile overlay ───────────────────────────────────────────────────

    private UserProfile? _overlayProfile;

    private async Task ShowProfileOverlay(UserProfile profile)
    {
        _overlayProfile = profile;

        // Always open in view mode, even if previously closed from edit mode
        OverlayViewContent.IsVisible = true;
        OverlayViewContent.Opacity = 1;
        OverlayEditContent.IsVisible = false;
        _isOverlayEditing = false;

        OverlayAvatar.BackgroundColor = Color.FromArgb(profile.AvatarColor);
        ShowOverlayPhoto(profile);
        OverlayInitial.Text = profile.Name.Length > 0
            ? profile.Name.TrimStart()[0].ToString().ToUpperInvariant()
            : "?";
        OverlayName.Text = profile.Name;

        if (!string.IsNullOrWhiteSpace(profile.Description))
        {
            OverlayDescription.Text = $"\"{profile.Description.Trim()}\" ";
            OverlayDescription.IsVisible = true;
        }
        else
        {
            OverlayDescription.IsVisible = false;
        }

        OverlayPanel.Scale = 0.85;
        OverlayPanel.Opacity = 0;
        OverlayBackdrop.Opacity = 0;
        ProfileOverlay.IsVisible = true;

        await Task.WhenAll(
            OverlayBackdrop.FadeToAsync(0.80, 280, Easing.CubicOut),
            OverlayPanel.FadeToAsync(1, 280, Easing.CubicOut),
            OverlayPanel.ScaleToAsync(1.0, 320, Easing.CubicOut)
        );
    }

    private async Task HideProfileOverlay()
    {
        _isOverlayEditing = false;
        await Task.WhenAll(
            OverlayBackdrop.FadeToAsync(0, 200, Easing.CubicIn),
            OverlayPanel.FadeToAsync(0, 180, Easing.CubicIn),
            OverlayPanel.ScaleToAsync(0.88, 180, Easing.CubicIn)
        );
        ProfileOverlay.IsVisible = false;
        _overlayProfile = null;
    }

    private void OnOverlayBackdropTapped(object? sender, TappedEventArgs e)
        => _ = HideProfileOverlay();

    private void OnOverlayPanelTapped(object? sender, TappedEventArgs e) { }

    private async void OnOverlayEliminarTapped(object? sender, TappedEventArgs e)
    {
        if (_overlayProfile is null) return;
        var profile = _overlayProfile;
        await HideProfileOverlay();
        await ConfirmAndDeleteAsync(profile);
    }

    // ── Overlay edit mode ──────────────────────────────────────────────────

    private async void OnOverlayEditarTapped(object? sender, TappedEventArgs e)
    {
        if (_overlayProfile is null) return;
        await SwitchOverlayToEditModeAsync();
    }

    private async void OnOverlayGuardarTapped(object? sender, TappedEventArgs e)
        => await _vm.UpdateProfileAsync();

    private async void OnOverlayCancelarTapped(object? sender, TappedEventArgs e)
        => await SwitchOverlayToViewModeAsync();

    private async Task SwitchOverlayToEditModeAsync()
    {
        _isOverlayEditing = true;
        _vm.StartEditing(_overlayProfile!);

        // Set up avatar before fading in (EditingColor PropertyChanged fires during
        // StartEditing but _isOverlayEditing was just set — handle explicitly here too)
        OverlayEditAvatar.BackgroundColor = Color.FromArgb(_vm.EditingColor);
        OverlayEditInitial.Text = _vm.EditingName.Length > 0
            ? _vm.EditingName.TrimStart()[0].ToString().ToUpperInvariant()
            : "?";

        BuildOverlayColorPicker();
        SyncOverlayColorPicker(_vm.EditingColor);
        SetupOverlayEditEntries();

        OverlayEditNameLineActive.ScaleX = 0;
        OverlayEditDescLineActive.ScaleX = 0;

        await OverlayViewContent.FadeToAsync(0, 140, Easing.CubicIn);
        OverlayEditContent.Opacity = 0;
        OverlayEditContent.IsVisible = true;
        OverlayViewContent.IsVisible = false;
        await OverlayEditContent.FadeToAsync(1, 180, Easing.CubicOut);
    }

    private async Task SwitchOverlayToViewModeAsync()
    {
        _isOverlayEditing = false;
        await OverlayEditContent.FadeToAsync(0, 140, Easing.CubicIn);
        OverlayViewContent.Opacity = 0;
        OverlayViewContent.IsVisible = true;
        OverlayEditContent.IsVisible = false;
        await OverlayViewContent.FadeToAsync(1, 180, Easing.CubicOut);
    }

    private void SetupOverlayEditEntries()
    {
        if (_overlayEditEntriesSetup) return;
        _overlayEditEntriesSetup = true;

        OverlayEditNameEntry.Focused += (_, _) => AnimateUnderlineIn(OverlayEditNameLineActive);
        OverlayEditNameEntry.Unfocused += (_, _) => AnimateUnderlineOut(OverlayEditNameLineActive);
        OverlayEditNameEntry.TextChanged += (_, e) =>
        {
            var text = e.NewTextValue ?? string.Empty;
            OverlayEditInitial.Text = string.IsNullOrWhiteSpace(text)
                ? "?"
                : text.TrimStart()[0].ToString().ToUpperInvariant();
        };
        OverlayEditNameEntry.Completed += (_, _) => OverlayEditDescEntry.Focus();

        OverlayEditDescEntry.Focused += (_, _) => AnimateUnderlineIn(OverlayEditDescLineActive);
        OverlayEditDescEntry.Unfocused += (_, _) => AnimateUnderlineOut(OverlayEditDescLineActive);
        OverlayEditDescEntry.Completed += async (_, _) => await _vm.UpdateProfileAsync();
    }

    private void BuildOverlayColorPicker()
    {
        OverlayEditColorPicker.Children.Clear();
        _overlayColorDots.Clear();

        foreach (var color in _vm.AvailableColors)
        {
            var capturedColor = color;

            var halo = new Border
            {
                BackgroundColor = Color.FromArgb(color),
                WidthRequest = 50,
                HeightRequest = 50,
                Opacity = 0,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 25 },
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var dot = new Border
            {
                BackgroundColor = Color.FromArgb(color),
                WidthRequest = 32,
                HeightRequest = 32,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            var cell = new Grid
            {
                WidthRequest = 50,
                HeightRequest = 56,
                HorizontalOptions = LayoutOptions.Center
            };
            cell.Add(halo);
            cell.Add(dot);

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => _vm.EditingColor = capturedColor;
            cell.GestureRecognizers.Add(tap);

            _overlayColorDots[color] = (dot, halo);
            OverlayEditColorPicker.Children.Add(cell);
        }
    }

    private void SyncOverlayColorPicker(string color)
    {
        foreach (var (c, pair) in _overlayColorDots)
        {
            if (c == color)
            {
                pair.dot.Scale = 1.2;
                pair.halo.Opacity = 0.35;
            }
            else
            {
                pair.dot.Scale = 1.0;
                pair.halo.Opacity = 0;
            }
        }
    }

    private void OnProfileUpdated()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            BuildProfileList();

            if (_overlayProfile is not null)
            {
                OverlayAvatar.BackgroundColor = Color.FromArgb(_overlayProfile.AvatarColor);
                ShowOverlayPhoto(_overlayProfile);
                OverlayInitial.Text = _overlayProfile.Name.Length > 0
                    ? _overlayProfile.Name.TrimStart()[0].ToString().ToUpperInvariant()
                    : "?";
                OverlayName.Text = _overlayProfile.Name;
                if (!string.IsNullOrWhiteSpace(_overlayProfile.Description))
                {
                    OverlayDescription.Text = $"\"{_overlayProfile.Description.Trim()}\" ";
                    OverlayDescription.IsVisible = true;
                }
                else
                {
                    OverlayDescription.IsVisible = false;
                }
            }

            await SwitchOverlayToViewModeAsync();
        });
    }

    // ── Delete confirmation ────────────────────────────────────────────────

    private async Task ConfirmAndDeleteAsync(UserProfile profile)
    {
        bool confirmed = await AppDialogs.ConfirmAsync(
            "Eliminar perfil",
            $"¿Eliminar «{profile.Name}»? Se borrarán para siempre todas sus rutinas y sesiones de entrenamiento.",
            "Eliminar",
            destructive: true);

        if (confirmed)
            await _vm.DeleteProfileAsync(profile);
    }
}
