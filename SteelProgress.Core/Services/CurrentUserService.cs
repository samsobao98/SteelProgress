using SteelProgress.Domain.Services;

namespace SteelProgress.Core.Services;

public class CurrentUserService : ICurrentUserService
{
    private int? _currentUserId;
    private int? _currentClientId;

    public int? CurrentUserId => _currentUserId;
    public bool HasUser => _currentUserId.HasValue;
    public int? CurrentClientId => _currentClientId;

    // Avisa a las pantallas para que recarguen al cambiar de cliente
    public event Action? ClientChanged;

    public void SetCurrentUser(int userId)
    {
        _currentUserId = userId;
        SetCurrentClient(null);
    }

    public void ClearCurrentUser()
    {
        _currentUserId = null;
        SetCurrentClient(null);
    }

    public void SetCurrentClient(int? clientId)
    {
        if (_currentClientId == clientId) return;
        _currentClientId = clientId;
        ClientChanged?.Invoke();
    }
}
