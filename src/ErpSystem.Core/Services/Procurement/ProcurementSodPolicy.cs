using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Procurement-only SOD policy. Unknown sources, manual AP, mixed payment batches,
/// HR and unrelated inventory operations retain their existing separation rules.
/// </summary>
public sealed class ProcurementSodPolicy(IUnitOfWork unitOfWork) : IProcurementSodPolicy
{
    private static readonly HashSet<string> ProcurementWorkflowNames = new WorkflowEntityTypeCatalogService()
        .GetDefaultEntityTypes().Where(item => item.Module == "Procurement")
        .SelectMany(item => new[] { Normalize(item.Name), Normalize(item.Code) })
        .Concat(new[] { "purchaseorderreceipt", "goodsreceiptnote", "grn", "tenderaward", "tenderbid", "tenderevaluation", "tenderexception", "emergencypurchaseexception" })
        .ToHashSet(StringComparer.Ordinal);

    public async Task<bool> IsEnabledAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return true;
        var settings = unitOfWork.Repository<ProcurementSettings>()?
            .GetQueryable(settings => settings.TenantId == tenantId && !settings.IsDeleted);
        // Missing configuration retains the existing separation baseline.
        if (settings is null) return true;
        return await settings.AsNoTracking().Select(settings => (bool?)settings.EnforceSegregationOfDuties)
            .SingleOrDefaultAsync(cancellationToken) ?? true;
    }

    public async Task<bool> IsRequiredForSourceAsync(Guid tenantId, string? sourceType, Guid? sourceId,
        CancellationToken cancellationToken = default)
    {
        if (await IsEnabledAsync(tenantId, cancellationToken)) return true;
        if (IsProcurementSource(sourceType)) return false;
        if (!sourceId.HasValue || sourceId == Guid.Empty) return true;

        var normalized = Normalize(sourceType);
        List<Guid> invoiceIds;
        switch (normalized)
        {
            case "purchasereturn":
                var orderId = await unitOfWork.Repository<PurchaseReturn>().GetQueryable(item =>
                    item.TenantId == tenantId && !item.IsDeleted && item.Id == sourceId.Value)
                    .Select(item => item.PurchaseOrderId ?? (item.GoodsReceiptNote == null ? null : item.GoodsReceiptNote.PurchaseOrderId))
                    .SingleOrDefaultAsync(cancellationToken);
                return !orderId.HasValue || !await unitOfWork.Repository<PurchaseOrder>().GetQueryable(order =>
                    order.TenantId == tenantId && !order.IsDeleted && order.Id == orderId.Value).AnyAsync(cancellationToken);
            case "vendorinvoicematchexception":
                invoiceIds = await unitOfWork.Repository<VendorInvoiceMatchException>()
                    .GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted && item.Id == sourceId.Value)
                    .Select(item => item.VendorInvoiceId).ToListAsync(cancellationToken);
                break;
            case "vendorinvoice":
                invoiceIds = [sourceId.Value];
                break;
            case "vendorpayment":
                var allocations = await unitOfWork.Repository<VendorPaymentAllocation>()
                    .GetQueryable(allocation => allocation.TenantId == tenantId && !allocation.IsDeleted &&
                        allocation.VendorPaymentId == sourceId.Value)
                    .AsNoTracking().ToListAsync(cancellationToken);
                var reversedIds = allocations.Where(allocation => allocation.IsReversal && allocation.OriginalAllocationId.HasValue)
                    .Select(allocation => allocation.OriginalAllocationId!.Value).ToHashSet();
                invoiceIds = allocations.Where(allocation => !allocation.IsReversal && !reversedIds.Contains(allocation.Id))
                    .Select(allocation => allocation.VendorInvoiceId).Distinct().ToList();
                break;
            case "paymentbatch":
                invoiceIds = await unitOfWork.Repository<PaymentBatchInvoice>()
                    .GetQueryable(allocation => allocation.TenantId == tenantId && !allocation.IsDeleted &&
                        allocation.PaymentBatchId == sourceId.Value)
                    .Select(allocation => allocation.VendorInvoiceId).Distinct().ToListAsync(cancellationToken);
                break;
            default:
                return true;
        }
        if (invoiceIds.Count == 0) return true;
        var invoices = await unitOfWork.Repository<VendorInvoice>()
            .GetQueryable(invoice => invoice.TenantId == tenantId && !invoice.IsDeleted && invoiceIds.Contains(invoice.Id))
            .Include(invoice => invoice.LineItems).AsNoTracking().ToListAsync(cancellationToken);
        if (invoices.Count != invoiceIds.Count) return true;
        var poIds = invoices.Where(invoice => invoice.PurchaseOrderId.HasValue)
            .Select(invoice => invoice.PurchaseOrderId!.Value).Distinct().ToArray();
        var procurementPoIds = poIds.Length == 0 ? new List<Guid>() : await unitOfWork.Repository<PurchaseOrder>()
            .GetQueryable(order => order.TenantId == tenantId && !order.IsDeleted && poIds.Contains(order.Id))
            .Select(order => order.Id).ToListAsync(cancellationToken);
        return invoices.Any(invoice => !invoice.AcceptedSupplyKind.HasValue &&
            !(invoice.PurchaseOrderId.HasValue && procurementPoIds.Contains(invoice.PurchaseOrderId.Value)) &&
            !invoice.LineItems.Any(line => !line.IsDeleted && line.LandedCostItemId.HasValue));
    }

    public static bool IsProcurementSource(string? sourceType)
    {
        var normalized = Normalize(sourceType);
        return normalized.StartsWith("procurement", StringComparison.Ordinal) || ProcurementWorkflowNames.Contains(normalized);
    }

    private static string Normalize(string? value) => new((value ?? string.Empty)
        .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

public static class ProcurementSodPolicyExtensions
{
    public static Task<bool> IsProcurementSodEnabledAsync(this IUnitOfWork unitOfWork, Guid tenantId,
        CancellationToken cancellationToken = default) =>
        new ProcurementSodPolicy(unitOfWork).IsEnabledAsync(tenantId, cancellationToken);
}
