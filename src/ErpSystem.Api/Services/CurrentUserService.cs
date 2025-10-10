using System.Security.Claims;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim?.Value;
        }
    }

    public string? UserName
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
        }
    }

    public string? Email
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var tenantIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id");
            if (tenantIdClaim != null && Guid.TryParse(tenantIdClaim.Value, out var tenantId))
            {
                return tenantId;
            }
            return null;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
        }
    }

    public IEnumerable<string> Roles
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User?.FindAll(ClaimTypes.Role)?.Select(c => c.Value) ?? Enumerable.Empty<string>();
        }
    }

    public bool IsInRole(string role)
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
    }

    public string? IpAddress
    {
        get
        {
            return _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        }
    }

    public string? UserAgent
    {
        get
        {
            return _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].FirstOrDefault();
        }
    }
}
