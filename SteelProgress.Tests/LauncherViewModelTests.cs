using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

public class LauncherViewModelTests
{
    // Un fallo dentro de MigrateAsync dejaba el launcher girando para siempre sin
    // decir nada, y con "Añadir perfil" todavía pulsable encima de una BD rota.
    [Fact]
    public async Task LoadAsync_CuandoFallaLaMigracion_ApagaElSpinnerYMuestraElError()
    {
        var vm = NuevoViewModel();
        var migracionFallida = Task.FromException(
            new InvalidOperationException("no se pudo abrir la base de datos"));

        await vm.LoadAsync(migracionFallida);

        Assert.True(vm.IsLoaded);
        Assert.False(vm.IsLoading);
        Assert.True(vm.HasError);
        Assert.Equal("no se pudo abrir la base de datos", vm.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_CuandoFallaLaMigracion_NoOfreceCrearPerfil()
    {
        var vm = NuevoViewModel();

        await vm.LoadAsync(Task.FromException(new InvalidOperationException("fallo")));

        Assert.False(vm.CanShowProfiles);
        Assert.False(vm.IsCreating);
    }

    // La línea bajo cada perfil en «¿Quién entrena hoy?»
    private static readonly DateTime Hoy = new(2026, 10, 30);

    [Fact]
    public void Resumen_VistaSimple_DiceCuandoYQueDiaFueElUltimoEntreno()
    {
        var perfil = new UserProfile { Name = "Álex" };

        Assert.Equal("Último entreno ayer · Pierna",
            LauncherViewModel.ProfileSummaryText(perfil, Hoy.AddDays(-1).AddHours(18), "Pierna", 0, Hoy));
        Assert.Equal("Último entreno hace 4 días",
            LauncherViewModel.ProfileSummaryText(perfil, Hoy.AddDays(-4), null, 0, Hoy));
    }

    [Fact]
    public void Resumen_VistaSimple_SinEntrenos()
    {
        Assert.Equal("Sin entrenar aún",
            LauncherViewModel.ProfileSummaryText(new UserProfile(), null, null, 0, Hoy));
    }

    [Theory]
    [InlineData(0, "Vista entrenador · sin clientes aún")]
    [InlineData(1, "Vista entrenador · 1 cliente")]
    [InlineData(6, "Vista entrenador · 6 clientes")]
    public void Resumen_VistaEntrenador_CuentaLosClientes(int clientes, string esperado)
    {
        var perfil = new UserProfile { ViewMode = ProfileViewMode.Trainer };

        Assert.Equal(esperado, LauncherViewModel.ProfileSummaryText(perfil, Hoy, "Pierna", clientes, Hoy));
    }

    private static LauncherViewModel NuevoViewModel() =>
        // LoadAsync revienta en el primer await, antes de resolver nada del contenedor.
        new(new ServiceProviderVacio(), new CurrentUserService(), new ViewOptionsService());

    private sealed class ServiceProviderVacio : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
