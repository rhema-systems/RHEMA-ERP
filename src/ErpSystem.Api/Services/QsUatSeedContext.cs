using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;

namespace ErpSystem.Api.Services;

// Registered only by the explicit, target-checked CLI. This is a non-login
// system actor, never an impersonation of a business reviewer or approver.
public sealed class QsUatSeedContext : ICurrentUserService, ICurrentUserProvider, ITenantContext
{
    private Tenant? tenant;
    private Guid actorId;
    public void Initialize(Tenant selectedTenant, Guid systemActorId)
    {
        if (tenant is not null || selectedTenant.Code != "DEFAULT" || systemActorId == Guid.Empty)
            throw new InvalidOperationException("QS UAT context must be initialized once for DEFAULT.");
        tenant = selectedTenant;
        actorId = systemActorId;
    }
    public string? UserId => actorId.ToString();
    Guid ICurrentUserProvider.UserId => actorId;
    public Guid? TenantId => tenant?.Id;
    Guid ICurrentUserProvider.TenantId => tenant?.Id ?? Guid.Empty;
    public string? UserName => "qs.uat.bootstrap";
    public string Username => UserName!;
    public string FullName => "QS UAT bootstrap";
    public string? Email => null;
    public Guid? EmployeeId => null;
    public bool IsAuthenticated => tenant is not null && actorId != Guid.Empty;
    public IEnumerable<string> Roles => IsAuthenticated ? [Constants.Roles.SuperAdmin] : [];
    public IDictionary<string, string> Claims => new Dictionary<string, string>();
    public string? IpAddress => null;
    public string? UserAgent => "ExplicitQsUatSeedCommand";
    public bool IsInRole(string role) => HasRole(role);
    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public bool IsExternalUser => false;
    public string AuthenticationProvider => "ExplicitQsUatSeedCommand";
    public Guid GetCurrentTenantId() => tenant?.Id ?? Guid.Empty;
    public Task<Tenant?> GetCurrentTenantAsync() => Task.FromResult(tenant);
    public bool HasCurrentTenant() => tenant is not null;
    public void SetCurrentTenant(Guid tenantId)
    {
        if (tenant?.Id != tenantId) throw new InvalidOperationException("QS UAT cannot switch tenants.");
    }
}
