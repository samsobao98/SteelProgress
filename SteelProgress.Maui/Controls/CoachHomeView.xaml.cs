using MauiIcons.Core;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Controls;

public partial class CoachHomeView : ContentView
{
    /// <summary>Se tocó un cliente (su fila o algo pendiente suyo): la página lo activa.</summary>
    public event EventHandler<Client>? ClientTapped;

    /// <summary>«Todos ›» o «Ir a Clientes».</summary>
    public event EventHandler? GoToClients;

    public CoachHomeView()
    {
        InitializeComponent();
        // Workaround for MAUI bug with URL-style xmlns (dotnet/maui#7503)
        _ = new MauiIcon();
    }

    private async void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not CoachClientRow row) return;
        await PressAsync(sender as VisualElement);
        ClientTapped?.Invoke(this, row.Client);
    }

    private async void OnPendingTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not CoachPendingItem item) return;
        await PressAsync(sender as VisualElement);
        ClientTapped?.Invoke(this, item.Client);
    }

    private void OnAllTapped(object? sender, TappedEventArgs e) => GoToClients?.Invoke(this, EventArgs.Empty);

    private static async Task PressAsync(VisualElement? element)
    {
        if (element is null) return;
        await element.ScaleToAsync(0.97, 70, Easing.CubicIn);
        await element.ScaleToAsync(1.0, 120, Easing.SpringOut);
    }
}
