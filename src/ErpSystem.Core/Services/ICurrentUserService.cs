namespace ErpSystem.Core.Services;

public interface ICurrentUserService
{
    Guid? GetUserId();
    Guid? GetTenantId();
    string? GetUsername();
    bool IsAuthenticated();
}