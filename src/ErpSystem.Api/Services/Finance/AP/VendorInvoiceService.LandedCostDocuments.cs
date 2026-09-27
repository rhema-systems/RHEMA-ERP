using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    private async Task EnsureLandedCostSupplierDocumentsAsync(LandedCost cost,
        IReadOnlyCollection<LandedCostItem> charges, CancellationToken token)
    {
        if (!_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException("Supplier documents must be generated in the invoice preparation transaction.");
        var receipt = await _unitOfWork.Repository<GoodsReceiptNote>().GetQueryable(r =>
            r.Id == cost.GoodsReceiptNoteId && r.TenantId == TenantId && !r.IsDeleted).AsNoTracking().SingleOrDefaultAsync(token)
            ?? throw new InvalidOperationException("The original landed-cost receipt is unavailable.");
        if (receipt.PurchaseOrderId.HasValue && !await _unitOfWork.Repository<PurchaseOrder>().GetQueryable(p =>
            p.Id == receipt.PurchaseOrderId && p.TenantId == TenantId && !p.IsDeleted).AnyAsync(token))
            throw new InvalidOperationException("The original receipt purchase order is unavailable in this company.");
        var repository = _unitOfWork.Repository<LandedCostSupplierDocument>();
        var existing = await repository.GetQueryable(d => d.TenantId == TenantId && d.LandedCostId == cost.Id && !d.IsDeleted).ToListAsync(token);
        foreach (var charge in charges)
        {
            if (charge.TenantId != TenantId || charge.LandedCostId != cost.Id || !charge.SupplierId.HasValue || charge.Amount <= 0)
                throw new InvalidOperationException("A supplier document requires a positive source charge and its confirmed supplier.");
            var old = existing.SingleOrDefault(d => d.LandedCostItemId == charge.Id);
            if (old != null)
            {
                if (old.BusinessPartnerId != charge.SupplierId || old.GoodsReceiptNoteId != receipt.Id ||
                    old.PurchaseOrderId != receipt.PurchaseOrderId || old.Amount != charge.Amount ||
                    old.Currency != charge.Currency || old.CostType != charge.CostType)
                    throw new InvalidOperationException("The charge no longer matches its supplier document. Cancel and replace the unposted source voucher through the governed process.");
                continue;
            }
            var document = new LandedCostSupplierDocument
            {
                TenantId = TenantId, LandedCostId = cost.Id, LandedCostItemId = charge.Id,
                GoodsReceiptNoteId = receipt.Id, PurchaseOrderId = receipt.PurchaseOrderId,
                BusinessPartnerId = charge.SupplierId.Value, CostType = charge.CostType,
                Amount = charge.Amount, Currency = charge.Currency, CreatedById = CurrentUserId, CreatedBy = UserName
            };
            await repository.AddAsync(document);
            await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
            {
                TenantId = TenantId, UserId = CurrentUserId, Username = UserName,
                Action = "LANDED_COST_SUPPLIER_DOCUMENT_CREATED", Resource = nameof(LandedCostSupplierDocument), ResourceId = document.Id.ToString(),
                NewValues = JsonSerializer.Serialize(new { document.LandedCostId, document.LandedCostItemId, document.GoodsReceiptNoteId,
                    document.PurchaseOrderId, document.BusinessPartnerId, document.Amount, document.Currency }),
                Timestamp = DateTime.UtcNow, IpAddress = "Unknown", CreatedById = CurrentUserId, CreatedBy = UserName
            });
        }
    }
}
