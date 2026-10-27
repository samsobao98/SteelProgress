using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Pages;

public partial class LauncherPage : ContentPage
{
    private readonly LauncherViewModel _vm;
    private bool _hasLoaded;
    private bool _orbsStarted;

    // ── Color picker ────────────────────────────────────────────────────
    private readonly Dictionary<string, (Border dot, Border halo)> _colorDots = new();
    private string? _currentSelectedColor;

    // ── Idle pulse (halo only) ─────────────────────────────────────────
    private CancellationTokenSource _idlePulseCts = new();

    // ── Floating labels ────────────────────────────────────────────────
    private bool _floatingLabelSetup;
    private string _lastInitial = "?";

    // ── Carousel ────────────────────────────────────────────────────────
    private int _carouselIndex;
    private readonly List<View> _carouselCards = new();
    private bool _isCarouselAnimating;

    // ── Overlay edit ─────────────────────────────────────────────────────
    private readonly Dictionary<string, (Border dot, Border halo)> _overlayColorDots = new();
    private bool _isOverlayEditing;
    private bool _overlayEditEntriesSetup;

    public ICommand ConfirmDeleteCommand { get; }

    public LauncherPage(LauncherViewModel vm, AppShell shell)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        vm.ProfileChosen += () =>
        {
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
            MainThread.BeginInvokeOnMainThread(() => { if (!_vm.IsCreating) BuildCarousel(); });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_hasLoaded) return;
        _hasLoaded = true;

        if (!_orbsStarted)
        {
            _orbsStarted = true;
            StartOrbAnimations();
        }

        // Logo starts small for spring bounce
        LogoImage.Scale = 0.5;

        var app = (App)Application.Current!;
        await _vm.LoadAsync(app.MigrationTask);

        if (!_vm.IsCreating && _vm.HasProfiles)
            BuildCarousel();

        // Header section fades + slides in
        await Task.WhenAll(
            HeaderSection.FadeToAsync(1, 400, Easing.CubicOut),
            HeaderSection.TranslateToAsync(0, 0, 500, Easing.CubicOut)
        );

        // Logo spring bounce
        await LogoImage.ScaleToAsync(1.18, 320, Easing.CubicOut);
        await LogoImage.ScaleToAsync(0.94, 120, Easing.CubicIn);
        await LogoImage.ScaleToAsync(1.0, 100, Easing.CubicOut);

        // Start aura loop after bounce settles
        _ = LogoAuraLoop();

        await SubtitleLabel.FadeToAsync(1, 400, Easing.CubicOut);
    }

    // ── Logo aura ────────────────────────────────────────────────────────

    private async Task LogoAuraLoop()
    {
        while (true)
        {
            LogoAura.Scale = 1.0;
            LogoAura.Opacity = 0.15;
            await Task.WhenAll(
                LogoAura.ScaleToAsync(2.8, 2500, Easing.CubicOut),
                LogoAura.FadeToAsync(0, 2500, Easing.CubicOut)
            );
            await Task.Delay(1200);
        }
    }

    // ── Background orbs ──────────────────────────────────────────────────

    private void StartOrbAnimations()
    {
        _ = AnimateOrb(Orb1,  25,  35, 9000);
        _ = AnimateOrb(Orb2, -20, -28, 11500);
        _ = AnimateOrb(Orb3,  40, -20, 7800);
    }

    private static async Task AnimateOrb(View orb, double dx, double dy, int durationMs)
    {
        while (true)
        {
            await orb.TranslateToAsync(dx, dy, (uint)durationMs, Easing.SinInOut);
            await orb.TranslateToAsync(0, 0, (uint)durationMs, Easing.SinInOut);
        }
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
        label.TextColor = Color.FromArgb("#6C7086");
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
            MainThread.BeginInvokeOnMainThread(() => BuildCarousel());
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
        FloatingLabel.TextColor = Color.FromArgb("#6C7086");
        BottomLineActive.ScaleX = 0;
        FraseLabel.TranslationY = 22;
        FraseLabel.Scale = 1.0;
        FraseLabel.TextColor = Color.FromArgb("#6C7086");
        FraseLineActive.ScaleX = 0;
        AvatarBorder.Scale = 1.0;
        AvatarHalo.Scale = 1.0;
        AvatarHalo.Opacity = 0.18;
        AvatarInitialLabel.Scale = 1.0;
    }

    // ── Carousel ──────────────────────────────────────────────────────────

    private void BuildCarousel()
    {
        CarouselContainer.Children.Clear();
        _carouselCards.Clear();
        _isCarouselAnimating = false;
        _carouselIndex = Math.Clamp(_carouselIndex, 0, Math.Max(0, _vm.Profiles.Count - 1));

        for (int i = 0; i < _vm.Profiles.Count; i++)
        {
            int idx = i;
            var card = BuildProfileCard(_vm.Profiles[i], () => OnCarouselCardTapped(idx));
            _carouselCards.Add(card);
            CarouselContainer.Children.Add(card);
        }

        SyncCarouselPositions();
    }

    private View BuildProfileCard(UserProfile profile, Action onTap)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => onTap();

        var initial = new Label
        {
            Text = profile.Name.Length > 0
                ? profile.Name.TrimStart()[0].ToString().ToUpperInvariant()
                : "?",
            TextColor = Color.FromArgb("#1E1E2E"),
            FontSize = 40,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var avatar = new Border
        {
            BackgroundColor = Color.FromArgb(profile.AvatarColor),
            WidthRequest = 110,
            HeightRequest = 110,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 55 },
            Content = initial,
            HorizontalOptions = LayoutOptions.Center
        };

        var avatarGrid = new Grid
        {
            WidthRequest = 110,
            HeightRequest = 110,
            HorizontalOptions = LayoutOptions.Center
        };
        avatarGrid.Add(avatar);

        var name = new Label
        {
            Text = profile.Name,
            TextColor = Color.FromArgb("#CDD6F4"),
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var card = new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 130,
            Spacing = 0,
            Children = { avatarGrid, name }
        };
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private void SyncCarouselPositions()
    {
        for (int i = 0; i < _carouselCards.Count; i++)
        {
            var (tx, scale, opacity) = GetCardTransform(i - _carouselIndex);
            var card = _carouselCards[i];
            card.TranslationX = tx;
            card.Scale = scale;
            card.Opacity = opacity;
            card.ZIndex = i == _carouselIndex ? 10 : 10 - Math.Abs(i - _carouselIndex);
        }
    }

    private async Task AnimateCarouselTo(int newIndex)
    {
        if (_isCarouselAnimating) return;
        _isCarouselAnimating = true;
        _carouselIndex = newIndex;

        // Update ZIndex before animation so center card renders on top
        for (int i = 0; i < _carouselCards.Count; i++)
            _carouselCards[i].ZIndex = i == _carouselIndex ? 10 : 10 - Math.Abs(i - _carouselIndex);

        var tasks = new List<Task>();
        for (int i = 0; i < _carouselCards.Count; i++)
        {
            var (tx, scale, opacity) = GetCardTransform(i - _carouselIndex);
            var card = _carouselCards[i];
            tasks.Add(card.TranslateToAsync(tx, 0, 300, Easing.CubicOut));
            tasks.Add(card.ScaleToAsync(scale, 300, Easing.CubicOut));
            tasks.Add(card.FadeToAsync(opacity, 300, Easing.CubicOut));
        }
        await Task.WhenAll(tasks);

        _isCarouselAnimating = false;
    }

    private static (double tx, double scale, double opacity) GetCardTransform(int distance)
    {
        return Math.Abs(distance) switch
        {
            0 => (0,    1.00, 1.0),
            1 => (Math.Sign(distance) * 118.0, 0.75, 0.60),
            _ => (Math.Sign(distance) * 230.0, 0.55, 0.0)
        };
    }

    private void OnCarouselCardTapped(int index)
    {
        if (index == _carouselIndex)
            _ = ShowProfileOverlay(_vm.Profiles[index]);
        else
            _ = AnimateCarouselTo(index);
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
        OverlayInitial.Text = profile.Name.Length > 0
            ? profile.Name.TrimStart()[0].ToString().ToUpperInvariant()
            : "?";
        OverlayName.Text = profile.Name;

        if (!string.IsNullOrWhiteSpace(profile.Description))
        {
            OverlayDescription.Text = $"\"{profile.Description.Trim()}\"";
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

    private async void OnOverlayEntrarTapped(object? sender, TappedEventArgs e)
    {
        if (_overlayProfile is null) return;
        var profile = _overlayProfile;
        _overlayProfile = null;

        // Panel zooms out and dissolves while backdrop seals to solid dark
        await Task.WhenAll(
            OverlayBackdrop.FadeToAsync(1.0, 340, Easing.CubicIn),
            OverlayPanel.ScaleToAsync(1.10, 380, Easing.CubicOut),
            OverlayPanel.FadeToAsync(0, 300, Easing.CubicIn)
        );
        ProfileOverlay.IsVisible = false;

        _vm.SelectProfileCommand.Execute(profile);
    }

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
            BuildCarousel();

            if (_overlayProfile is not null)
            {
                OverlayAvatar.BackgroundColor = Color.FromArgb(_overlayProfile.AvatarColor);
                OverlayInitial.Text = _overlayProfile.Name.Length > 0
                    ? _overlayProfile.Name.TrimStart()[0].ToString().ToUpperInvariant()
                    : "?";
                OverlayName.Text = _overlayProfile.Name;
                if (!string.IsNullOrWhiteSpace(_overlayProfile.Description))
                {
                    OverlayDescription.Text = $"\"{_overlayProfile.Description.Trim()}\"";
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
        bool confirmed = await DisplayAlertAsync(
            "Eliminar perfil",
            $"¿Eliminar \"{profile.Name}\"? Se borrarán permanentemente todas sus rutinas y sesiones de entrenamiento.",
            "Eliminar",
            "Cancelar");

        if (confirmed)
            await _vm.DeleteProfileAsync(profile);
    }
}
