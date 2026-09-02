using System.Security.Claims;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

public class CurrentUserService : ICurrentUserService, ICurrentUserProvider
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    public string? UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim?.Value;
        }
    }

    // ICurrentUserProvider implementation
    Guid ICurrentUserProvider.UserId
    {
        get
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }
            return Guid.Empty; // or throw exception based on requirements
        }
    }

    Guid ICurrentUserProvider.TenantId
    {
        get
        {
            var tenantIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id");
            if (tenantIdClaim != null && Guid.TryParse(tenantIdClaim.Value, out var tenantId))
            {
                return tenantId;
            }

            // Fallback: many parts of the system assume a tenant, and the DB seeds a default tenant.
            // If the user is authenticated but no tenant claim is present, use the default tenant.
            var isAuthenticated = _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
            return isAuthenticated ? DefaultTenantId : Guid.Empty;
        }
    }

    public string Username
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        }
    }

    public string FullName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var fullName = user?.FindFirst("full_name")?.Value;
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName.Trim();
            }

            var givenName = user?.FindFirst(ClaimTypes.GivenName)?.Value;
            var surname = user?.FindFirst(ClaimTypes.Surname)?.Value;
            var displayName = string.Join(" ", new[] { givenName, surname }
                .Where(part => !string.IsNullOrWhiteSpace(part))).Trim();

            return string.IsNullOrWhiteSpace(displayName) ? Username : displayName;
        }
    }

    public bool HasRole(string role)
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
    }

    public IDictionary<string, string> Claims
    {
        get
        {
            var claims = new Dictionary<string, string>();
            var user = _httpContextAccessor.HttpContext?.User;
            if (user != null)
            {
                foreach (var claim in user.Claims)
                {
                    claims[claim.Type] = claim.Value;
                }
            }
            return claims;
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

            var isAuthenticated = _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
            return isAuthenticated ? DefaultTenantId : null;
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

    public Guid? EmployeeId
    {
        get
        {
            var employeeIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("employee_id");
            if (employeeIdClaim != null && Guid.TryParse(employeeIdClaim.Value, out var employeeId))
            {
                return employeeId;
            }
            return null;
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

    public bool IsExternalUser
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User?.IsInRole(Constants.Roles.ExternalUser) == true;
        }
    }

    public string AuthenticationProvider
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst("auth_provider")?.Value ?? "Local";
        }
    }
}
