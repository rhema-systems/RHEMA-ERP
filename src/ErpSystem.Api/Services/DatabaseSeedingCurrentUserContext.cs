using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;

namespace ErpSystem.Web.Services;

/// <summary>
/// Scoped system identity for explicit database-seeding command hosts. The baseline model
/// owns this stable default tenant identity. Normal web hosts retain their request-backed
/// current-user registrations because the seeding composition uses TryAdd.
/// </summary>
internal sealed class DatabaseSeedingCurrentUserContext : ICurrentUserService, ICurrentUserProvider
{
    internal static readonly Guid DefaultTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid SystemUserId = Guid.Empty;
    private static readonly string[] SystemRoles = [Constants.Roles.SuperAdmin];

    public string? UserId => SystemUserId.ToString();
    public string? UserName => "system";
    public string FullName => "System";
    public string? Email => "system@local";
    public Guid? TenantId => DefaultTenantId;
    public Guid? EmployeeId => null;
    public bool IsAuthenticated => true;
    public IEnumerable<string> Roles => SystemRoles;
    public IDictionary<string, string> Claims => new Dictionary<string, string>
    {
        [Constants.Claims.TenantId] = DefaultTenantId.ToString()
    };
    public string? IpAddress => null;
    public string? UserAgent => "DatabaseSeedingCommand";
    public bool IsInRole(string role) => HasRole(role);

    Guid ICurrentUserProvider.UserId => SystemUserId;
    Guid ICurrentUserProvider.TenantId => DefaultTenantId;
    public string Username => UserName!;
    public bool HasRole(string role) => SystemRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public bool IsExternalUser => false;
    public string AuthenticationProvider => "DatabaseSeedingCommand";
}
