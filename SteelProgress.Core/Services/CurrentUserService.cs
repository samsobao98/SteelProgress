using SteelProgress.Domain.Services;

namespace SteelProgress.Core.Services;

public class CurrentUserService : ICurrentUserService
{
    private int? _currentUserId;

    public int? CurrentUserId => _currentUserId;
    public bool HasUser => _currentUserId.HasValue;

    public void SetCurrentUser(int userId) => _currentUserId = userId;
    public void ClearCurrentUser() => _currentUserId = null;
}
