using System.Security.Claims;
using ErpSystem.Core.Services;

namespace ErpSystem.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }

    public Guid? GetTenantId()
    {
        var tenantIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id");
        if (tenantIdClaim != null && Guid.TryParse(tenantIdClaim.Value, out var tenantId))
        {
            return tenantId;
        }
        return null;
    }

    public string? GetUsername()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
    }

    public bool IsAuthenticated()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }
}