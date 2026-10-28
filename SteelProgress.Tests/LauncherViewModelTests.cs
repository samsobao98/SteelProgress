using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;

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

    private static LauncherViewModel NuevoViewModel() =>
        // LoadAsync revienta en el primer await, antes de resolver nada del contenedor.
        new(new ServiceProviderVacio(), new CurrentUserService(), new ViewOptionsService());

    private sealed class ServiceProviderVacio : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
