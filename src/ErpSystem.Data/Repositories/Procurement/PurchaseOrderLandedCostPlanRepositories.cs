using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public class PurchaseOrderLandedCostPlanRepository
    : GenericRepository<PurchaseOrderLandedCostPlan>, IPurchaseOrderLandedCostPlanRepository
{
    public PurchaseOrderLandedCostPlanRepository(ApplicationDbContext context) : base(context) { }

    public async Task<PurchaseOrderLandedCostPlan?> GetByPurchaseOrderIdAsync(Guid purchaseOrderId)
    {
        if (purchaseOrderId == Guid.Empty) return null;
        return await _dbSet.FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId && !p.IsDeleted);
    }

    public async Task<PurchaseOrderLandedCostPlan?> GetWithItemsByPurchaseOrderIdAsync(Guid purchaseOrderId)
    {
        if (purchaseOrderId == Guid.Empty) return null;
        return await _dbSet
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId && !p.IsDeleted);
    }
}

public class PurchaseOrderLandedCostPlanItemRepository
    : GenericRepository<PurchaseOrderLandedCostPlanItem>, IPurchaseOrderLandedCostPlanItemRepository
{
    public PurchaseOrderLandedCostPlanItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PurchaseOrderLandedCostPlanItem>> GetByPlanIdAsync(Guid planId)
    {
        if (planId == Guid.Empty) return new List<PurchaseOrderLandedCostPlanItem>();
        return await _dbSet
            .Where(i => i.PurchaseOrderLandedCostPlanId == planId && !i.IsDeleted)
            .ToListAsync();
    }
}

