using SteelProgress.Core.Services;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Controls;

public partial class ActiveClientBanner : ContentView
{
    private ClientContextService? _context;

    public ActiveClientBanner()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    private void Attach()
    {
        _context ??= Handler?.MauiContext?.Services.GetService<ClientContextService>();
        if (_context is null) return;
        _context.Changed -= Update;
        _context.Changed += Update;
        Update();
    }

    private void Detach()
    {
        if (_context is not null) _context.Changed -= Update;
    }

    private void Update() => MainThread.BeginInvokeOnMainThread(() =>
    {
        var client = _context?.ActiveClient;
        IsVisible = client is not null;
        if (client is null) return;

        NameLabel.Text = client.Name;
        InitialLabel.Text = string.IsNullOrEmpty(client.Name) ? "?" : client.Name[..1].ToUpperInvariant();
        Avatar.BackgroundColor = Color.FromArgb(client.AvatarColor);
        // Durante un entreno no se cambia de cliente
        ExitButton.IsVisible = !WorkoutStateService.IsWorkoutActive;
        ShowLimitations(_context!.ActiveLimitations);
    });

    // «Rodilla derecha · fisio» y debajo qué tener en cuenta, separadas del nombre por una línea
    private void ShowLimitations(IReadOnlyList<ClientLimitation> limitations)
    {
        LimitationList.Children.Clear();
        LimitationList.IsVisible = limitations.Count > 0;
        if (limitations.Count == 0) return;

        var warm = (Color)Application.Current!.Resources["SpIntensity"];
        LimitationList.Children.Add(new BoxView { HeightRequest = 1, Color = warm.WithAlpha(0.25f) });
        foreach (var l in limitations)
        {
            var title = new FormattedString();
            title.Spans.Add(new Span { Text = LimitationText.Title(l), FontFamily = "SpaceGroteskMedium" });
            if (LimitationText.ShortSource(l.Source) is string source)
                title.Spans.Add(new Span { Text = $" · {source}", TextColor = (Color)Application.Current.Resources["SpText2"] });

            var row = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 8 };
            row.Add(new Border
            {
                WidthRequest = 16, HeightRequest = 16, StrokeThickness = 0, BackgroundColor = warm,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 2, 0, 0),
                Content = new Label { Text = "!", FontFamily = "SpaceGroteskBold", FontSize = 11, TextColor = (Color)Application.Current.Resources["SpOnAccent"],
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
            });
            var text = new VerticalStackLayout { Spacing = 0 };
            text.Add(new Label { FormattedText = title, FontSize = 13, TextColor = (Color)Application.Current.Resources["SpText"] });
            text.Add(new Label { Text = l.Text, FontSize = 13, TextColor = (Color)Application.Current.Resources["SpText"], LineBreakMode = LineBreakMode.WordWrap });
            row.Add(text, 1);
            LimitationList.Children.Add(row);
        }
    }

    private void OnExitTapped(object? sender, TappedEventArgs e) => _context?.Clear();
}
