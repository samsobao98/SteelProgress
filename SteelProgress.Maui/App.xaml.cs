using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using SteelProgress.Maui.Pages;

namespace SteelProgress.Maui;

public partial class App : Application
{
    private readonly WelcomePage _welcomePage;
    private readonly IServiceProvider _services;

    public App(WelcomePage welcomePage, IServiceProvider services)
    {
        InitializeComponent();
        _welcomePage = welcomePage;
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_welcomePage);
    }

    protected override void OnStart()
    {
        base.OnStart();
        _ = MigrateAsync();
    }

    private async Task MigrateAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}