using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Data.Repositories;

namespace ErpSystem.Data.Repositories.Inventory;

#region Stock Adjustment Repository Implementation

public class StockAdjustmentRepository : GenericRepository<StockAdjustment>, IStockAdjustmentRepository
{
    public StockAdjustmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StockAdjustment>> GetAdjustmentsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
            return new List<StockAdjustment>();
            
        return await _dbSet
            .Where(sa => !sa.IsDeleted &&
                        sa.AdjustmentDate >= startDate &&
                        sa.AdjustmentDate <= endDate)
            .Include(sa => sa.Items)
                .ThenInclude(i => i.InventoryItem)
            .OrderByDescending(sa => sa.AdjustmentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockAdjustment>> GetPendingAdjustmentsAsync()
    {
        return await _dbSet
            .Where(sa => sa.Status == "Draft" && !sa.IsDeleted)
            .Include(sa => sa.Items)
                .ThenInclude(i => i.InventoryItem)
            .OrderByDescending(sa => sa.AdjustmentDate)
            .ToListAsync();
    }

    public async Task<StockAdjustment?> GetByAdjustmentNumberAsync(string adjustmentNumber)
    {
        if (string.IsNullOrWhiteSpace(adjustmentNumber))
            return null;
            
        return await _dbSet
            .Where(sa => sa.AdjustmentNumber == adjustmentNumber && !sa.IsDeleted)
            .Include(sa => sa.Items)
                .ThenInclude(i => i.InventoryItem)
                    .ThenInclude(ii => ii.Category)
            .FirstOrDefaultAsync();
    }

    public async Task<StockAdjustment?> GetWithItemsAsync(Guid adjustmentId)
    {
        if (adjustmentId == Guid.Empty)
            return null;
            
        return await _dbSet
            .Where(sa => sa.Id == adjustmentId && !sa.IsDeleted)
            .Include(sa => sa.Items)
                .ThenInclude(i => i.InventoryItem)
                    .ThenInclude(ii => ii.Category)
            .Include(sa => sa.Items)
                .ThenInclude(i => i.Location)
                    .ThenInclude(l => l!.Warehouse)
            .FirstOrDefaultAsync();
    }
    
    /// <summary>
    /// Gets stock adjustments by reason code
    /// </summary>
    public async Task<IEnumerable<StockAdjustment>> GetByReasonCodeAsync(string reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            return new List<StockAdjustment>();
            
        return await _dbSet
            .Where(sa => sa.ReasonCode == reasonCode && !sa.IsDeleted)
            .Include(sa => sa.Items)
            .OrderByDescending(sa => sa.AdjustmentDate)
            .ToListAsync();
    }
    
    /// <summary>
    /// Generates a new adjustment number
    /// </summary>
    public async Task<string> GenerateAdjustmentNumberAsync()
    {
        var currentYear = DateTime.UtcNow.Year;
        var yearPrefix = currentYear.ToString().Substring(2); // Last 2 digits of year
        
        // Get all adjustments for the current year to find max sequence
        var adjustmentsInYear = await _dbSet
            .Where(sa => sa.AdjustmentNumber.StartsWith($"ADJ{yearPrefix}") && !sa.IsDeleted)
            .Select(sa => sa.AdjustmentNumber)
            .ToListAsync();
            
        int nextSequence = 1;
        if (adjustmentsInYear.Any())
        {
            // Parse all sequence numbers and find the maximum
            var maxSequence = adjustmentsInYear
                .Select(an => {
                    var sequencePart = an.Substring(5); // Remove "ADJ" + year prefix
                    if (int.TryParse(sequencePart, out int seq))
                        return seq;
                    return 0;
                })
                .Max();
            
            nextSequence = maxSequence + 1;
        }
        
        return $"ADJ{yearPrefix}{nextSequence:D4}"; // Format as ADJ24NNNN
    }
    
    /// <summary>
    /// Gets adjustments requiring approval
    /// </summary>
    public async Task<IEnumerable<StockAdjustment>> GetAdjustmentsRequiringApprovalAsync()
    {
        return await _dbSet
            .Where(sa => sa.Status == "Draft" && 
                        !sa.IsDeleted &&
                        Math.Abs(sa.TotalAdjustmentValue) > 1000) // Adjustments over $1000 need approval
            .Include(sa => sa.Items)
                .ThenInclude(i => i.InventoryItem)
            .OrderByDescending(sa => sa.AdjustmentDate)
            .ToListAsync();
    }
}

/// <summary>
/// Repository for stock adjustment items
/// </summary>
public class StockAdjustmentItemRepository : GenericRepository<StockAdjustmentItem>
{
    public StockAdjustmentItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StockAdjustmentItem>> GetByAdjustmentIdAsync(Guid adjustmentId)
    {
        if (adjustmentId == Guid.Empty)
            return new List<StockAdjustmentItem>();
            
        return await _dbSet
            .Where(sai => sai.AdjustmentId == adjustmentId && !sai.IsDeleted)
            .Include(sai => sai.InventoryItem)
                .ThenInclude(ii => ii.Category)
            .Include(sai => sai.Location)
                .ThenInclude(l => l!.Warehouse)
            .OrderBy(sai => sai.InventoryItem.ItemCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockAdjustmentItem>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<StockAdjustmentItem>();
            
        return await _dbSet
            .Where(sai => sai.InventoryItemId == inventoryItemId && !sai.IsDeleted)
            .Include(sai => sai.Adjustment)
            .Include(sai => sai.InventoryItem)
            .Include(sai => sai.Location)
            .OrderByDescending(sai => sai.Adjustment.AdjustmentDate)
            .ToListAsync();
    }
}

#endregion
