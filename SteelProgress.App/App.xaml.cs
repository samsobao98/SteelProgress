using Microsoft.EntityFrameworkCore;
using SteelProgress.Data.Context;
using System.Configuration;
using System.Data;
using System.Windows;

namespace SteelProgress.App;

public partial class App : Application
{
    public static AppDbContext DbContext { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=steelprogress.db")
            .Options;

        DbContext = new AppDbContext(options);

        DbContext.Database.Migrate();

        base.OnStartup(e);
    }
}
