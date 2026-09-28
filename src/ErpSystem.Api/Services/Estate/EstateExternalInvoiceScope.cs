using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Api.Services.Estate;

public static class EstateExternalInvoiceScope
{
    public static IQueryable<Invoice> ForCustomerProperties(
        this IQueryable<Invoice> source,
        Guid tenantId,
        IReadOnlyCollection<Guid> customerIds)
    {
        var ids = customerIds.ToArray();
        return source.Where(invoice => invoice.TenantId == tenantId
            && !invoice.IsDeleted
            && ids.Contains(invoice.BusinessPartnerId)
            && (invoice.Status == InvoiceStatus.Sent
                || invoice.Status == InvoiceStatus.PartiallyPaid
                || invoice.Status == InvoiceStatus.Paid
                || invoice.Status == InvoiceStatus.Overdue)
            && ((invoice.Reference != null && invoice.Reference.StartsWith("RENT-"))
                || (invoice.Reference != null && invoice.Reference.StartsWith("LEGAL-TRANSFER-FEE-"))
                || (invoice.Notes != null
                    && (invoice.Notes.Contains("Estate / Property Management")
                        || invoice.Notes.Contains("Estate / Facilities")))));
    }
}
