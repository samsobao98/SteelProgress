namespace SteelProgress.Domain.Services;

public interface ICurrentUserService
{
    int? CurrentUserId { get; }
    bool HasUser { get; }
    void SetCurrentUser(int userId);
    void ClearCurrentUser();
}
