using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Estate;

public sealed record FacilitiesProviderSelection(BusinessPartner Provider, Contract Contract);

public sealed class FacilitiesProviderSelectionService(ApplicationDbContext db)
{
    public async Task<FacilitiesProviderSelection?> ResolveAsync(
        Guid tenantId,
        string? providerIdValue,
        string? contractIdValue,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerIdValue) && string.IsNullOrWhiteSpace(contractIdValue))
            return null;
        if (!Guid.TryParse(providerIdValue, out var providerId)
            || !Guid.TryParse(contractIdValue, out var contractId))
            throw new InvalidOperationException("Select both an approved service provider and its active contract before Maintenance handoff.");

        var provider = await db.BusinessPartners.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == providerId && item.TenantId == tenantId && !item.IsDeleted,
            cancellationToken);
        if (provider is null || !BusinessPartnerLifecyclePolicy.IsOperationallyApproved(provider)
            || provider.PartnerType is not ("Supplier" or "Contractor" or "Both")
            || provider.ComplianceValidUntilUtc.HasValue && provider.ComplianceValidUntilUtc.Value < now)
            throw new InvalidOperationException("The selected service provider is not approved, active, or compliant in Procurement.");

        var today = now.Date;
        var contract = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id == contractId && item.TenantId == tenantId && !item.IsDeleted
            && item.BusinessPartnerId == providerId && item.Status == "Active"
            && (!item.StartDate.HasValue || item.StartDate.Value.Date <= today)
            && (!item.EndDate.HasValue || item.EndDate.Value.Date >= today),
            cancellationToken);
        if (contract is null)
            throw new InvalidOperationException("The selected Procurement contract is not active for this provider.");

        return new FacilitiesProviderSelection(provider, contract);
    }
}
