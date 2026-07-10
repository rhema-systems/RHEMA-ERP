using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;

namespace ErpSystem.Api.Services.Finance;

public static class FinanceTenantGuard
{
    public static Guid GetRequiredFinanceTenantId(this ICurrentUserService currentUserService)
    {
        var claims = currentUserService.Claims;
        if (claims is { Count: > 0 })
        {
            if (!claims.TryGetValue(Constants.Claims.TenantId, out var tenantClaim) ||
                !Guid.TryParse(tenantClaim, out var claimedTenantId) ||
                claimedTenantId == Guid.Empty)
            {
                throw new InvalidOperationException("Finance tenant context is required.");
            }

            return claimedTenantId;
        }

        var tenantId = currentUserService.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Finance tenant context is required.");
        }

        return tenantId.Value;
    }
}
