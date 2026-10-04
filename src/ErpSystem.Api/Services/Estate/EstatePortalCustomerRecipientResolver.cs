using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Estate;

public static class EstatePortalCustomerRecipientResolver
{
    public static async Task<IReadOnlyCollection<Guid>> ResolveForCaseAsync(
        ApplicationDbContext db,
        ProcedureCase procedureCase,
        CancellationToken cancellationToken = default)
    {
        var sourceReference = procedureCase.Fields
            .FirstOrDefault(field => !field.IsDeleted
                && string.Equals(field.Key, "sourceReference", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        if (string.IsNullOrWhiteSpace(sourceReference))
        {
            sourceReference = await db.ProcedureCaseFields
                .AsNoTracking()
                .Where(field => field.TenantId == procedureCase.TenantId
                    && field.ProcedureCaseId == procedureCase.Id
                    && !field.IsDeleted
                    && field.Key == "sourceReference")
                .Select(field => field.Value)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await ResolveAsync(
            db,
            procedureCase.TenantId,
            procedureCase.OpenedById,
            procedureCase.SourceDepartment,
            Guid.TryParse(sourceReference, out var businessPartnerId) ? businessPartnerId : null,
            cancellationToken);
    }

    public static async Task<IReadOnlyCollection<Guid>> ResolveAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Guid? openedById,
        string? sourceDepartment,
        Guid? businessPartnerId,
        CancellationToken cancellationToken = default)
    {
        var recipientIds = new HashSet<Guid>();
        if (openedById.HasValue
            && openedById != Guid.Empty
            && sourceDepartment?.StartsWith("External Portal", StringComparison.OrdinalIgnoreCase) == true)
        {
            recipientIds.Add(openedById.Value);
        }

        if (!businessPartnerId.HasValue || businessPartnerId == Guid.Empty)
        {
            return recipientIds;
        }

        var customer = await db.BusinessPartners
            .AsNoTracking()
            .Where(item => item.Id == businessPartnerId.Value
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.IsActive
                && item.ApprovalStatus == "Approved"
                && item.CustomerAccountNumber != null
                && BusinessPartnerRoles.CustomerTypes.Contains(item.PartnerType))
            .Select(item => new { item.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (customer is null)
        {
            return recipientIds;
        }

        if (customer.UserId.HasValue)
        {
            recipientIds.Add(customer.UserId.Value);
        }

        var linkedUserIds = await db.BusinessPartnerUsers
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId
                && link.BusinessPartnerId == businessPartnerId.Value
                && !link.IsDeleted
                && link.IsActive)
            .Select(link => link.UserId)
            .ToListAsync(cancellationToken);
        recipientIds.UnionWith(linkedUserIds);
        return recipientIds;
    }
}
