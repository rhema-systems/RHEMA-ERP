using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Sales;

internal static class SalesBusinessPartnerNames
{
    public static async Task<IReadOnlyDictionary<Guid, string>> LoadAsync(
        IUnitOfWork unitOfWork, Guid tenantId, IEnumerable<Guid?> customerIds)
    {
        var ids = customerIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, string>();

        // CRM/Sales retain CustomerId as a domain label for BusinessPartner.Id.
        // Their legacy Customer navigations are deliberately excluded from the EF model.
        var partners = await unitOfWork.Repository<BusinessPartner>().FindAsync(
            partner => partner.TenantId == tenantId && !partner.IsDeleted && ids.Contains(partner.Id));
        return partners.ToDictionary(partner => partner.Id, partner => partner.PartnerName);
    }
}
