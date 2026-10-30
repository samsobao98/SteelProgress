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

    /// <summary>Las limitaciones vigentes del cliente activo, para el aviso «Trabajando con».</summary>
    public IReadOnlyList<ClientLimitation> ActiveLimitations { get; private set; } = Array.Empty<ClientLimitation>();

    public event Action? Changed;

    public void Activate(Client client)
    {
        ActiveClient = client;
        ActiveLimitations = Current(client.Limitations);
        _currentUser.SetCurrentClient(client.Id);
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (ActiveClient is null && _currentUser.CurrentClientId is null) return;
        ActiveClient = null;
        ActiveLimitations = Array.Empty<ClientLimitation>();
        _currentUser.SetCurrentClient(null);
        Changed?.Invoke();
    }

    /// <summary>Tras cambiar las limitaciones de un cliente: si es el activo, el aviso se pone al día.</summary>
    public void LimitationsChanged(int clientId, IEnumerable<ClientLimitation> limitations)
    {
        if (ActiveClient?.Id != clientId) return;
        ActiveLimitations = Current(limitations);
        Changed?.Invoke();
    }

    private static List<ClientLimitation> Current(IEnumerable<ClientLimitation> limitations) =>
        limitations.Where(l => l.ResolvedAt is null).OrderBy(l => l.Since).ToList();
}
