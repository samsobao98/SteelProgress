using SteelProgress.Core.Services;

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
    });

    private void OnExitTapped(object? sender, TappedEventArgs e) => _context?.Clear();
}
