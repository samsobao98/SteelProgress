using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Context;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// Limitaciones de un cliente: consentimiento, superadas, retirar el consentimiento y el aviso del cliente activo
public class ClientLimitationsTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CurrentUserService _user = new();
    private readonly ClientContextService _clientContext;
    private readonly int _clientId;

    public ClientLimitationsTests()
    {
        _connection.Open();
        _clientContext = new ClientContextService(_user);
        _user.SetCurrentUser(1);
        using var db = Db();
        db.Database.Migrate();
        db.UserProfiles.Add(new UserProfile { Id = 1, Name = "Laura", Kind = ProfileKind.Coach });
        db.UserProfiles.Add(new UserProfile { Id = 2, Name = "Pablo", Kind = ProfileKind.Coach });
        db.SaveChanges();
        var client = new Client { Name = "Felipe", UserId = 1 };
        db.Clients.Add(client);
        db.SaveChanges();
        _clientId = client.Id;
    }

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, _user);

    public void Dispose() => _connection.Dispose();

    private ClientLimitationsViewModel Vm() => new(new ClientRepository(Db()), _clientContext);

    private static async Task AddAsync(ClientLimitationsViewModel vm, string zone, BodySide side, string text, GuidanceSource source)
    {
        vm.StartNewCommand.Execute(null);
        vm.EditZone = zone;
        vm.PickSide(side);
        vm.EditText = text;
        vm.EditSource = source;
        await vm.SaveCommand.ExecuteAsync(null);
    }

    [Fact]
    public void TitlesAgreeWithTheZone()
    {
        Assert.Equal("Rodilla derecha", LimitationText.Title(new ClientLimitation { Zone = "Rodilla", Side = BodySide.Right }));
        Assert.Equal("Hombro izquierdo", LimitationText.Title(new ClientLimitation { Zone = "Hombro", Side = BodySide.Left }));
        Assert.Equal("Cadera, ambos lados", LimitationText.Title(new ClientLimitation { Zone = "Cadera", Side = BodySide.Both }));
        Assert.Equal("Lumbar", LimitationText.Title(new ClientLimitation { Zone = "Lumbar" }));
    }

    [Fact]
    public async Task NothingIsSavedWithoutConsent()
    {
        var vm = Vm();
        await vm.LoadAsync(_clientId);
        Assert.Equal(("Limitaciones de Felipe", true, false), (vm.Title, vm.NeedsConsent, vm.CanAdd));

        vm.StartNewCommand.Execute(null);
        Assert.False(vm.IsEditing);

        vm.ConsentChecked = true;
        Assert.True(vm.CanAdd);
        vm.StartNewCommand.Execute(null);
        vm.EditZone = "Rodilla";
        Assert.False(vm.SaveCommand.CanExecute(null)); // falta el texto
        vm.CancelEditCommand.Execute(null);

        // Marcar la casilla y no guardar nada no registra el consentimiento
        using var db = Db();
        Assert.Null(db.Clients.Single().HealthConsentAt);
        Assert.Empty(db.ClientLimitations);
    }

    [Fact]
    public async Task FirstLimitationRecordsConsentAndCanBeResolved()
    {
        var vm = Vm();
        await vm.LoadAsync(_clientId);
        vm.ConsentChecked = true;
        await AddAsync(vm, "Rodilla", BodySide.Right, "  Sin saltos.  ", GuidanceSource.Physio);

        Assert.True(vm.HasConsent);
        var item = Assert.Single(vm.Current);
        Assert.Equal(("Rodilla derecha", "Sin saltos."), (item.Title, item.Text));
        Assert.StartsWith("Pautas del fisio · desde ", item.Meta);

        await vm.ToggleResolvedAsync(item);
        Assert.Empty(vm.Current);
        Assert.Equal("Superadas (1)", vm.ResolvedHeader);
        Assert.StartsWith("Superada el ", vm.Resolved[0].Meta);

        // Vuelve a estar vigente
        await vm.ToggleResolvedAsync(vm.Resolved[0]);
        Assert.Single(vm.Current);
        Assert.False(vm.HasResolved);
    }

    [Fact]
    public async Task WithdrawingConsentDeletesEverything()
    {
        var vm = Vm();
        await vm.LoadAsync(_clientId);
        vm.ConsentChecked = true;
        await AddAsync(vm, "Rodilla", BodySide.Right, "Sin saltos.", GuidanceSource.Physio);
        await AddAsync(vm, "Lumbar", BodySide.None, "Vigilar la técnica.", GuidanceSource.Own);
        await vm.ToggleResolvedAsync(vm.Current[1]);

        await new ClientRepository(Db()).WithdrawHealthConsentAsync(_clientId);
        var fresh = Vm();
        await fresh.LoadAsync(_clientId);

        Assert.True(fresh.NeedsConsent);
        Assert.Empty(fresh.Current);
        Assert.Empty(fresh.Resolved);
        using var db = Db();
        Assert.Empty(db.ClientLimitations);
    }

    [Fact]
    public async Task ActiveClientBannerShowsOnlyCurrentOnes()
    {
        var vm = Vm();
        await vm.LoadAsync(_clientId);
        vm.ConsentChecked = true;
        await AddAsync(vm, "Rodilla", BodySide.Right, "Sin saltos.", GuidanceSource.Physio);
        await AddAsync(vm, "Hombro", BodySide.Left, "Ya está bien.", GuidanceSource.Own);
        await vm.ToggleResolvedAsync(vm.Current[1]);

        // Como al trabajar con él desde Clientes o Inicio
        var summary = Assert.Single(await new ClientRepository(Db()).GetSummariesAsync(1, DateTime.Today));
        Assert.Equal("1 limitación", ClientsViewModel.ToItem(summary, DateTime.Today).LimitationsText);
        _clientContext.Activate(summary.Client);
        Assert.Equal("Rodilla", Assert.Single(_clientContext.ActiveLimitations).Zone);

        // Cambiarlas con él activo pone el aviso al día
        await vm.ToggleResolvedAsync(vm.Current[0]);
        Assert.Empty(_clientContext.ActiveLimitations);
    }

    [Fact]
    public async Task OtherTrainersCannotSeeThem()
    {
        var vm = Vm();
        await vm.LoadAsync(_clientId);
        vm.ConsentChecked = true;
        await AddAsync(vm, "Rodilla", BodySide.Right, "Sin saltos.", GuidanceSource.Physio);

        _user.SetCurrentUser(2);
        using var db = Db();
        Assert.Empty(db.ClientLimitations);
    }

    [Fact]
    public async Task EditingTheClientKeepsTheLimitations()
    {
        var vm = Vm();
        await vm.LoadAsync(_clientId);
        vm.ConsentChecked = true;
        await AddAsync(vm, "Rodilla", BodySide.Right, "Sin saltos.", GuidanceSource.Physio);

        var clients = new ClientsViewModel(new ClientRepository(Db()), _user, _clientContext);
        await clients.RefreshAsync();
        clients.StartEdit(clients.Clients[0].Client);
        clients.EditGoal = "Fuerza";
        await clients.SaveAsync();

        using var db = Db();
        Assert.Equal("Fuerza", db.Clients.Single().Goal);
        Assert.NotNull(db.Clients.Single().HealthConsentAt);
        Assert.Single(db.ClientLimitations);
    }
}
