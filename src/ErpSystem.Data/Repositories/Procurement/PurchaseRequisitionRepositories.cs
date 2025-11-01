using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Data.Repositories;

namespace ErpSystem.Data.Repositories.Procurement;

#region Purchase Requisition Repository Implementations

public class PurchaseRequisitionRepository : GenericRepository<PurchaseRequisition>, IPurchaseRequisitionRepository
{
    public PurchaseRequisitionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return new List<PurchaseRequisition>();
            
        return await _dbSet
            .Where(pr => pr.Status == status && !pr.IsDeleted)
            .Include(pr => pr.RequestedBy)
            .Include(pr => pr.ApprovedBy)
            .Include(pr => pr.Items)
            .OrderByDescending(pr => pr.RequisitionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByRequesterAsync(Guid requesterId)
    {
        if (requesterId == Guid.Empty)
            return new List<PurchaseRequisition>();
            
        return await _dbSet
            .Where(pr => pr.RequestedById == requesterId && !pr.IsDeleted)
            .Include(pr => pr.RequestedBy)
            .Include(pr => pr.ApprovedBy)
            .Include(pr => pr.Items)
            .OrderByDescending(pr => pr.RequisitionDate)
            .ToListAsync();
    }

    public async Task<PurchaseRequisition?> GetByRequisitionNumberAsync(string requisitionNumber)
    {
        if (string.IsNullOrWhiteSpace(requisitionNumber))
            return null;
            
        return await _dbSet
            .Where(pr => pr.RequisitionNumber == requisitionNumber && !pr.IsDeleted)
            .Include(pr => pr.RequestedBy)
            .Include(pr => pr.ApprovedBy)
            .Include(pr => pr.Items)
            .FirstOrDefaultAsync();
    }

    public async Task<PurchaseRequisition?> GetRequisitionWithItemsAsync(Guid requisitionId)
    {
        if (requisitionId == Guid.Empty)
            return null;
            
        return await _dbSet
            .Where(pr => pr.Id == requisitionId && !pr.IsDeleted)
            .Include(pr => pr.RequestedBy)
            .Include(pr => pr.ApprovedBy)
            .Include(pr => pr.Items)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PurchaseRequisition>> GetRequisitionsRequiringApprovalAsync()
    {
        return await _dbSet
            .Where(pr => pr.Status == "Submitted" && !pr.IsDeleted)
            .Include(pr => pr.RequestedBy)
            .Include(pr => pr.Items)
            .OrderBy(pr => pr.RequisitionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
            return new List<PurchaseRequisition>();
            
        return await _dbSet
            .Where(pr => !pr.IsDeleted &&
                        pr.RequisitionDate >= startDate &&
                        pr.RequisitionDate <= endDate)
            .Include(pr => pr.RequestedBy)
            .Include(pr => pr.ApprovedBy)
            .Include(pr => pr.Items)
            .OrderByDescending(pr => pr.RequisitionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByDepartmentAsync(string department)
    {
        if (string.IsNullOrWhiteSpace(department))
            return new List<PurchaseRequisition>();
            
        return await _dbSet
            .Where(pr => pr.Department == department && !pr.IsDeleted)
            .Include(pr => pr.RequestedBy)
            .Include(pr => pr.ApprovedBy)
            .Include(pr => pr.Items)
            .OrderByDescending(pr => pr.RequisitionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseRequisition>> GetUrgentRequisitionsAsync()
    {
        return await _dbSet
            .Where(pr => pr.Priority == "Urgent" && 
                        (pr.Status == "Submitted" || pr.Status == "Approved") && 
                        !pr.IsDeleted)
            .Include(pr => pr.RequestedBy)
            .Include(pr => pr.Items)
            .OrderBy(pr => pr.RequiredDate)
            .ToListAsync();
    }

    public async Task<string> GenerateRequisitionNumberAsync()
    {
        var currentYear = DateTime.UtcNow.Year;
        var yearPrefix = currentYear.ToString().Substring(2); // Last 2 digits of year
        
        // Get all requisitions for the current year to find max sequence
        var requisitionsInYear = await _dbSet
            .Where(pr => pr.RequisitionNumber.StartsWith($"REQ{yearPrefix}") && !pr.IsDeleted)
            .Select(pr => pr.RequisitionNumber)
            .ToListAsync();
            
        int nextSequence = 1;
        if (requisitionsInYear.Any())
        {
            // Parse all sequence numbers and find the maximum
            var maxSequence = requisitionsInYear
                .Select(rn => {
                    var sequencePart = rn.Substring(5); // Remove "REQ" + year prefix
                    if (int.TryParse(sequencePart, out int seq))
                        return seq;
                    return 0;
                })
                .Max();
            
            nextSequence = maxSequence + 1;
        }
        
        return $"REQ{yearPrefix}{nextSequence:D4}"; // Format as REQ24NNNN
    }
    
    // Missing methods referenced in controller
    public async Task<ErpSystem.Core.DTOs.Common.PagedResult<PurchaseRequisition>> GetRequisitionsAsync(int page, int pageSize, string? search = null, string? status = null, string? priority = null, DateTime? startDate = null, DateTime? endDate = null, string? department = null)
    {
        var query = _dbSet.Where(pr => !pr.IsDeleted)
            .Include(pr => pr.RequestedBy)
            .Include(pr => pr.ApprovedBy)
            .Include(pr => pr.Items)
            .AsQueryable();
            
        // Apply filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(pr => pr.RequisitionNumber.ToLower().Contains(search) ||
                                    (pr.RequestedBy != null && (pr.RequestedBy.FirstName.ToLower().Contains(search) || pr.RequestedBy.LastName.ToLower().Contains(search))));
        }
        
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(pr => pr.Status == status);
        }
        
        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(pr => pr.Priority == priority);
        }
        
        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(pr => pr.Department == department);
        }
        
        if (startDate.HasValue)
        {
            query = query.Where(pr => pr.RequisitionDate >= startDate.Value);
        }
        
        if (endDate.HasValue)
        {
            query = query.Where(pr => pr.RequisitionDate <= endDate.Value);
        }
        
        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(pr => pr.RequisitionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
            
        return new ErpSystem.Core.DTOs.Common.PagedResult<PurchaseRequisition>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
    
    public async Task<PurchaseRequisition?> GetRequisitionByIdAsync(Guid id)
    {
        return await GetRequisitionWithItemsAsync(id);
    }
    
    public async Task<PurchaseRequisition> CreateRequisitionAsync(PurchaseRequisition requisition)
    {
        return await AddAsync(requisition);
    }
    
    public async Task UpdateStatusAsync(Guid requisitionId, string status)
    {
        var requisition = await GetByIdAsync(requisitionId);
        if (requisition != null)
        {
            requisition.Status = status;
            await UpdateAsync(requisition);
        }
    }
    
    public async Task<PurchaseRequisition> UpdateRequisitionAsync(PurchaseRequisition requisition)
    {
        await UpdateAsync(requisition);
        return requisition;
    }
    
    public async Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByStatus(string status)
    {
        return await GetRequisitionsByStatusAsync(status);
    }
    
    public async Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByPriority(string priority)
    {
        return await _dbSet
            .Where(pr => pr.Priority == priority && !pr.IsDeleted)
            .Include(pr => pr.RequestedBy)
            .OrderByDescending(pr => pr.RequisitionDate)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByDepartment(string department)
    {
        return await GetRequisitionsByDepartmentAsync(department);
    }
    
    public async Task<IEnumerable<PurchaseRequisition>> GetPendingApprovalRequisitions()
    {
        return await GetRequisitionsRequiringApprovalAsync();
    }
}

public class PurchaseRequisitionItemRepository : GenericRepository<PurchaseRequisitionItem>, IPurchaseRequisitionItemRepository
{
    public PurchaseRequisitionItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PurchaseRequisitionItem>> GetItemsByRequisitionAsync(Guid requisitionId)
    {
        if (requisitionId == Guid.Empty)
            return new List<PurchaseRequisitionItem>();
            
        return await _dbSet
            .Where(pri => pri.RequisitionId == requisitionId && !pri.IsDeleted)
            .Include(pri => pri.Requisition)
            .Include(pri => pri.PreferredSupplier)
            .Include(pri => pri.PurchaseOrder)
            .OrderBy(pri => pri.ItemDescription)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseRequisitionItem>> GetPendingItemsAsync()
    {
        return await _dbSet
            .Where(pri => pri.Status == "Pending" && !pri.IsDeleted)
            .Include(pri => pri.Requisition)
            .Include(pri => pri.PreferredSupplier)
            .OrderBy(pri => pri.RequiredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseRequisitionItem>> GetItemsByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<PurchaseRequisitionItem>();
            
        return await _dbSet
            .Where(pri => pri.InventoryItemId == inventoryItemId && !pri.IsDeleted)
            .Include(pri => pri.Requisition)
            .Include(pri => pri.PreferredSupplier)
            .OrderByDescending(pri => pri.Requisition.RequisitionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseRequisitionItem>> GetItemsBySupplierAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty)
            return new List<PurchaseRequisitionItem>();
            
        return await _dbSet
            .Where(pri => pri.PreferredSupplierId == supplierId && !pri.IsDeleted)
            .Include(pri => pri.Requisition)
            .Include(pri => pri.PreferredSupplier)
            .OrderByDescending(pri => pri.Requisition.RequisitionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseRequisitionItem>> GetApprovedItemsNotOrderedAsync()
    {
        return await _dbSet
            .Where(pri => pri.Requisition.Status == "Approved" && 
                         pri.Status == "Approved" &&
                         pri.PurchaseOrderId == null &&
                         !pri.IsDeleted)
            .Include(pri => pri.Requisition)
            .Include(pri => pri.PreferredSupplier)
            .OrderBy(pri => pri.RequiredDate)
            .ToListAsync();
    }
    
    // Missing method referenced in controller
    public async Task<IEnumerable<PurchaseRequisitionItem>> GetItemsByRequisitionIdAsync(Guid requisitionId)
    {
        return await GetItemsByRequisitionAsync(requisitionId);
    }
    
    public async Task<PurchaseRequisitionItem> CreateItemAsync(PurchaseRequisitionItem item)
    {
        return await AddAsync(item);
    }
}

#endregion