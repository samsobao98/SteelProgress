namespace SteelProgress.Domain.Services;

public interface ICurrentUserService
{
    int? CurrentUserId { get; }
    bool HasUser { get; }
    void SetCurrentUser(int userId);
    void ClearCurrentUser();

    // Cliente con el que trabaja el entrenador (null = sus propios datos).
    // Con un cliente activo, rutinas y sesiones son las de ese cliente.
    int? CurrentClientId => null;
    void SetCurrentClient(int? clientId) { }
}
