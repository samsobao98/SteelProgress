using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// Peso corporal: tendencia (Hacker's Diet), medias semanales, fuerza relativa y quién puede apuntarlo
public class BodyWeightTests : IDisposable
{
    private static readonly DateTime Today = new(2026, 10, 30);

    private sealed class FixedClock(DateTime local) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private readonly FixedClock _clock = new(Today.AddHours(8));
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();
    private readonly ClientContextService _clientContext;
    private readonly ViewOptionsService _viewOptions = new();
    private readonly Client _client;

    public BodyWeightTests()
    {
        _connection.Open();
        _clientContext = new ClientContextService(_user);
        _user.SetCurrentUser(1);
        using var db = Db();
        db.Database.Migrate();
        db.UserProfiles.Add(new UserProfile { Id = 1, Name = "Ana" });
        db.UserProfiles.Add(new UserProfile { Id = 2, Name = "Laura", Kind = ProfileKind.Coach });
        _client = new Client { Name = "Felipe", UserId = 2 };
        db.Clients.Add(_client);
        db.SaveChanges();
    }

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    public void Dispose() => _connection.Dispose();

    private BodyWeightViewModel Vm() => new(new BodyWeightRepository(Db()), new WorkoutRepository(Db()), _clientContext, _viewOptions, _clock);

    private static WeightPoint P(int daysAgo, double kg) => new(Today.AddDays(-daysAgo), kg);

    // ── Cálculos ──

    [Fact]
    public void TrendFollowsTheHackersDiet()
    {
        // Peso estable: la tendencia no se mueve
        var flat = BodyWeightStats.Trend(new[] { P(10, 80), P(5, 80), P(0, 80) });
        Assert.All(flat, p => Assert.Equal(80, p.Kg, 6));
        Assert.Equal(11, flat.Count); // un punto por día, del primero al último

        // Un salto de un día solo mueve la tendencia un 10 %
        var jump = BodyWeightStats.Trend(new[] { P(1, 80), P(0, 81) });
        Assert.Equal(80.1, jump[^1].Kg, 6);

        // Sin pesarse en medio, los días se rellenan en línea recta: 80 → 80,5 → 81
        var gap = BodyWeightStats.Trend(new[] { P(2, 80), P(0, 81) });
        Assert.Equal(80 + 0.1 * 0.5, gap[1].Kg, 6);
        Assert.Equal(80.05 + 0.1 * (81 - 80.05), gap[2].Kg, 6);
    }

    [Fact]
    public void ChangeLooksFourWeeksBackOrSinceTheFirst()
    {
        var month = BodyWeightStats.Trend(new[] { P(40, 80), P(0, 80) });
        Assert.Equal("Sin cambios en 4 semanas", BodyWeightStats.ChangeText(month, Today));

        var down = BodyWeightStats.Trend(Enumerable.Range(0, 40).Select(i => P(39 - i, 80 - i * 0.05)).ToList());
        Assert.StartsWith("−", BodyWeightStats.ChangeText(down, Today));
        Assert.EndsWith("kg en 4 semanas", BodyWeightStats.ChangeText(down, Today));

        var recent = BodyWeightStats.Trend(new[] { P(10, 80), P(0, 81) });
        Assert.Equal("+0,4 kg desde el 20/10", BodyWeightStats.ChangeText(recent, Today));

        Assert.StartsWith("Primer pesaje", BodyWeightStats.ChangeText(BodyWeightStats.Trend(new[] { P(0, 80) }), Today));
    }

    [Fact]
    public void WeeklyAveragesGoFromMondayToSunday()
    {
        // Hoy es viernes 30/10: la semana empieza el lunes 26
        var weeks = BodyWeightStats.WeeklyAverages(new[] { P(0, 79), P(4, 80), P(5, 81), P(11, 82) }, 2);
        Assert.Equal(2, weeks.Count);
        Assert.Equal((new DateTime(2026, 10, 26), 79.5, 2), (weeks[0].WeekStart, weeks[0].Kg, weeks[0].Count));
        Assert.Equal((new DateTime(2026, 10, 19), 81.5, 2), (weeks[1].WeekStart, weeks[1].Kg, weeks[1].Count));
    }

    [Fact]
    public void StrengthIsAgainstOwnTrendAndNeedsAWeighInBefore()
    {
        var trend = BodyWeightStats.Trend(new[] { P(80, 80), P(0, 80) });
        var squat = new ExerciseProgress(1, "Sentadilla", 100, 5, 116.7, Today, new[]
        {
            new ExercisePoint(Today.AddDays(-90), 90, 105),   // antes del primer pesaje: no cuenta
            new ExercisePoint(Today.AddDays(-60), 95, 112),
            new ExercisePoint(Today.AddDays(-1), 100, 120)
        });
        var onlyHighReps = new ExerciseProgress(2, "Zancadas", 20, 15, 0, Today, new[] { new ExercisePoint(Today, 20, 0) });

        var s = Assert.Single(BodyWeightStats.Strength(new[] { squat, onlyHighReps }, trend, Today, 5));
        Assert.Equal(("Sentadilla", 1.5, 1.4), (s.Exercise, s.Ratio, s.PreviousRatio!.Value));
        Assert.Equal("1,50 ×", BodyWeightStats.Ratio(s.Ratio));
    }

    [Fact]
    public void ParsesBothDecimalSeparators()
    {
        Assert.Equal(78.9, BodyWeightViewModel.ParseKg("78,9"));
        Assert.Equal(78.9, BodyWeightViewModel.ParseKg(" 78.9 "));
        Assert.Null(BodyWeightViewModel.ParseKg("abc"));
    }

    // ── Apuntar y quién puede ──

    [Fact]
    public async Task PersonalProfileSavesOnePerDay()
    {
        var vm = Vm();
        await vm.LoadAsync();
        Assert.True(vm.IsAllowed);
        Assert.True(vm.IsEmpty);

        vm.StartEntryCommand.Execute(null);
        vm.EntryText = "81";
        await vm.SaveEntryCommand.ExecuteAsync(null);
        vm.StartEntryCommand.Execute(null);
        Assert.Equal("81,0", vm.EntryText); // viene el último
        Assert.True(vm.HasEntryOnDate);
        vm.EntryMinusCommand.Execute(null);
        await vm.SaveEntryCommand.ExecuteAsync(null);

        using var db = Db();
        var only = Assert.Single(db.BodyWeights);
        Assert.Equal((80.9, Today), (only.Kg, only.Date));
        Assert.Equal("80,9", vm.TrendText);
        Assert.Equal("Último: 80,9 kg · hoy", vm.LastText);
    }

    [Fact]
    public async Task RejectsImpossibleWeightsAndFutureDays()
    {
        var vm = Vm();
        await vm.LoadAsync();
        vm.StartEntryCommand.Execute(null);
        vm.EntryText = "8";
        await vm.SaveEntryCommand.ExecuteAsync(null);
        Assert.True(vm.IsEntering);
        Assert.Contains("entre 20 y 400", vm.EntryError);

        vm.EntryText = "80";
        vm.EntryDate = Today.AddDays(1);
        await vm.SaveEntryCommand.ExecuteAsync(null);
        Assert.True(vm.IsEntering);
        using var db = Db();
        Assert.Empty(db.BodyWeights);
    }

    [Fact]
    public async Task CoachNeedsAClientAndItsConsent()
    {
        _user.SetCurrentUser(2);
        _viewOptions.Apply(new UserProfile { Id = 2, Kind = ProfileKind.Coach });
        var vm = Vm();
        await vm.LoadAsync();
        Assert.False(vm.IsVisible);

        using (var db = Db()) _clientContext.Activate(db.Clients.Single());
        await vm.LoadAsync();
        Assert.Equal(("Peso de Felipe", true), (vm.Title, vm.NeedsConsent));
        vm.StartEntryCommand.Execute(null);
        Assert.False(vm.IsEntering);

        // Con su consentimiento (lo da la primera limitación guardada), sí
        using (var db = Db())
        {
            db.Clients.Single().HealthConsentAt = Today;
            await db.SaveChangesAsync();
        }
        await vm.LoadAsync();
        Assert.True(vm.IsAllowed);
        vm.StartEntryCommand.Execute(null);
        vm.EntryText = "72,5";
        await vm.SaveEntryCommand.ExecuteAsync(null);

        // Es suyo: sin él activo no se ve, y retirar el consentimiento lo borra
        _clientContext.Clear();
        using (var db = Db()) Assert.Empty(db.BodyWeights);
        await new ClientRepository(Db()).WithdrawHealthConsentAsync(_client.Id);
        using (var db = Db()) Assert.Empty(db.BodyWeights.IgnoreQueryFilters());
    }
}
