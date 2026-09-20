using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>Stages estimates in the caller's PO transaction. Never changes commercial totals.</summary>
public sealed class PurchaseOrderLandedCostPlanService(
    IPurchaseOrderLandedCostPlanRepository plans,
    IPurchaseOrderLandedCostPlanItemRepository costs,
    IBusinessPartnerRepository partners)
{
    public static Guid? ResolveTarget(UpsertPurchaseOrderLandedCostPlanItemDto cost,
        IReadOnlyList<PurchaseOrderItem> lines, Guid purchaseOrderId, Guid tenantId, bool allowLineIndex)
    {
        var target = cost.PurchaseOrderItemId;
        if (cost.PurchaseOrderLineIndex is int index)
        {
            if (!allowLineIndex || index < 0 || index >= lines.Count)
                throw new ArgumentException("The planned cost references an invalid PO line. Reload the purchase order.");
            if (target.HasValue && target != lines[index].Id)
                throw new ArgumentException("The planned cost's PO line references disagree.");
            target = lines[index].Id;
        }
        if (target.HasValue && !lines.Any(l => l.Id == target && l.PurchaseOrderId == purchaseOrderId &&
            l.TenantId == tenantId && !l.IsDeleted))
            throw new ArgumentException("The planned cost must reference an active line of this purchase order.");
        return target;
    }

    public async Task StageAsync(PurchaseOrder po, IReadOnlyList<PurchaseOrderItem> lines,
        UpsertPurchaseOrderLandedCostPlanDto? dto, bool allowLineIndex = false)
    {
        var plan = await plans.GetWithItemsByPurchaseOrderIdAsync(po.Id);
        if (plan != null && plan.TenantId != po.TenantId)
            throw new ArgumentException("The landed cost plan does not belong to this tenant.");
        if (dto == null)
        {
            if (plan?.Items.Any(c => !c.IsDeleted && c.PurchaseOrderItemId.HasValue &&
                !lines.Any(l => !l.IsDeleted && l.Id == c.PurchaseOrderItemId)) == true)
                throw new ArgumentException("A removed PO line has planned landed costs. Reload and remove its costs with the line before saving.");
            return; // An omitted plan leaves existing estimates untouched.
        }
        if (!string.Equals(po.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Planned landed costs can only be changed on a Draft purchase order.");
        if (dto.Items == null) throw new ArgumentException("Planned cost items are required.");
        var currency = string.IsNullOrWhiteSpace(dto.Currency) ? po.Currency : dto.Currency.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(currency) || currency.Length > 10)
            throw new ArgumentException("Enter a valid plan currency.");
        var staged = new List<PurchaseOrderLandedCostPlanItem>();
        foreach (var cost in dto.Items)
        {
            var context = new System.ComponentModel.DataAnnotations.ValidationContext(cost);
            System.ComponentModel.DataAnnotations.Validator.ValidateObject(cost, context, true);
            if (!Enum.IsDefined(cost.CostType) || !new[] { "ByValue", "ByQuantity", "ByWeight", "ByVolume", "Equal", "Manual" }
                .Contains(cost.AllocationMethod, StringComparer.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(cost.Currency))
                throw new ArgumentException("Enter a valid planned cost type, allocation method and currency.");
            var target = ResolveTarget(cost, lines, po.Id, po.TenantId, allowLineIndex);
            string? supplierName = null;
            if (cost.SupplierId.HasValue)
            {
                var partner = await partners.GetByIdAsync(cost.SupplierId.Value);
                if (partner == null || partner.TenantId != po.TenantId || partner.IsDeleted)
                    throw new ArgumentException("Select a saved landed-cost supplier in the current company.");
                supplierName = partner.PartnerName;
            }
            staged.Add(new PurchaseOrderLandedCostPlanItem
            {
                TenantId = po.TenantId, PurchaseOrderItemId = target,
                CostType = cost.CostType, Description = cost.Description.Trim(), Amount = cost.Amount,
                Currency = cost.Currency.Trim().ToUpperInvariant(), ExchangeRate = cost.ExchangeRate,
                AmountInPlanCurrency = Math.Round(cost.Amount * cost.ExchangeRate, 2, MidpointRounding.AwayFromZero),
                AllocationMethod = target.HasValue ? "ByQuantity" : cost.AllocationMethod,
                SupplierId = cost.SupplierId, SupplierName = supplierName,
                ReferenceNumber = cost.ReferenceNumber, Notes = cost.Notes
            });
        }
        // Validate the entire replacement before changing any tracked data.
        var isNew = plan == null;
        if (isNew && staged.Count == 0) return;
        plan ??= new PurchaseOrderLandedCostPlan { PurchaseOrderId = po.Id, TenantId = po.TenantId };
        plan.Currency = currency;
        plan.Notes = dto.Notes;
        plan.TotalPlannedCost = staged.Sum(c => c.AmountInPlanCurrency);
        if (isNew) await plans.AddAsync(plan);
        else
        {
            await costs.DeleteRangeAsync(plan.Items.Where(c => !c.IsDeleted).ToList());
            await plans.UpdateAsync(plan);
        }
        foreach (var cost in staged)
        {
            cost.PurchaseOrderLandedCostPlanId = plan.Id;
            await costs.AddAsync(cost);
        }
    }
}
