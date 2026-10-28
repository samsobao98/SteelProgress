using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.Services;

// Cliente con el que está trabajando el entrenador. Mientras haya uno activo, Rutinas, Entreno,
// Historial y Progreso muestran y guardan los datos de ese cliente (filtros de AppDbContext).
public class ClientContextService
{
    private readonly ICurrentUserService _currentUser;

    public ClientContextService(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;

        // Cambiar de perfil deja de trabajar con el cliente: que el nombre activo no se quede atrás
        if (currentUser is CurrentUserService service)
            service.ClientChanged += () =>
            {
                if (_currentUser.CurrentClientId is null && ActiveClient is not null)
                {
                    ActiveClient = null;
                    Changed?.Invoke();
                }
            };
    }

    public Client? ActiveClient { get; private set; }
    public bool HasActiveClient => ActiveClient is not null;

    public event Action? Changed;

    public void Activate(Client client)
    {
        ActiveClient = client;
        _currentUser.SetCurrentClient(client.Id);
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (ActiveClient is null && _currentUser.CurrentClientId is null) return;
        ActiveClient = null;
        _currentUser.SetCurrentClient(null);
        Changed?.Invoke();
    }
}
