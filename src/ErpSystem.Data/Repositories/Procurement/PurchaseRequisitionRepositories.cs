using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

#region Purchase Requisition Repository Implementations

public class PurchaseRequisitionRepository : GenericRepository<PurchaseRequisition>, IPurchaseRequisitionRepository
{
    private readonly IProcurementSettingsRepository _settingsRepository;
    private readonly ITenantContext _tenantContext;

    public PurchaseRequisitionRepository(
        ApplicationDbContext context,
        IProcurementSettingsRepository settingsRepository,
        ITenantContext tenantContext) : base(context)
    {
        _settingsRepository = settingsRepository;
        _tenantContext = tenantContext;
    }

    public async Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return new List<PurchaseRequisition>();
        }

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
        {
            return new List<PurchaseRequisition>();
        }

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
        {
            return null;
        }

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
        {
            return null;
        }

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
        {
            return new List<PurchaseRequisition>();
        }

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
        {
            return new List<PurchaseRequisition>();
        }

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
        // Get the format from settings
        var settings = await _context.Set<ProcurementSettings>()
            .FirstOrDefaultAsync(s => !s.IsDeleted);
        
        var format = settings?.PurchaseRequisitionNumberFormat ?? "PR-{YYYY}-{####}";
        
        return await GenerateNumberFromFormatAsync(format, "PR");
    }

    private async Task<string> GenerateNumberFromFormatAsync(string format, string prefix)
    {
        var now = DateTime.UtcNow;
        
        // Replace date placeholders
        var formattedNumber = format
            .Replace("{YYYY}", now.Year.ToString())
            .Replace("{YY}", now.Year.ToString().Substring(2))
            .Replace("{MM}", now.Month.ToString("D2"))
            .Replace("{DD}", now.Day.ToString("D2"));
        
        // Find sequence placeholder pattern
        var sequenceMatch = System.Text.RegularExpressions.Regex.Match(format, @"\{(#+)\}");
        if (sequenceMatch.Success)
        {
            var sequenceLength = sequenceMatch.Groups[1].Value.Length;
            
            // Extract the prefix pattern (everything before the sequence placeholder)
            var prefixPattern = format.Substring(0, sequenceMatch.Index)
                .Replace("{YYYY}", now.Year.ToString())
                .Replace("{YY}", now.Year.ToString().Substring(2))
                .Replace("{MM}", now.Month.ToString("D2"))
                .Replace("{DD}", now.Day.ToString("D2"));
            
            // Get all requisitions matching the prefix pattern
            var tenantId = _tenantContext.GetCurrentTenantId();
            var existingNumbers = await _dbSet
                .IgnoreQueryFilters()
                .Where(pr => pr.TenantId == tenantId && pr.RequisitionNumber.StartsWith(prefixPattern))
                .Select(pr => pr.RequisitionNumber)
                .ToListAsync();
            
            int nextSequence = 1;
            if (existingNumbers.Any())
            {
                // Extract sequence numbers and find max
                var maxSequence = existingNumbers
                    .Select(rn =>
                    {
                        var sequencePart = rn.Substring(prefixPattern.Length);
                        // Remove any suffix after the sequence
                        var suffixStart = format.IndexOf(sequenceMatch.Value) + sequenceMatch.Value.Length;
                        if (suffixStart < format.Length)
                        {
                            var suffix = format.Substring(suffixStart)
                                .Replace("{YYYY}", now.Year.ToString())
                                .Replace("{YY}", now.Year.ToString().Substring(2))
                                .Replace("{MM}", now.Month.ToString("D2"))
                                .Replace("{DD}", now.Day.ToString("D2"));
                            if (sequencePart.EndsWith(suffix))
                            {
                                sequencePart = sequencePart.Substring(0, sequencePart.Length - suffix.Length);
                            }
                        }
                        
                        if (int.TryParse(sequencePart, out int seq))
                        {
                            return seq;
                        }
                        return 0;
                    })
                    .Max();
                
                nextSequence = maxSequence + 1;
            }
            
            // Replace sequence placeholder with formatted number
            formattedNumber = formattedNumber.Replace(sequenceMatch.Value, nextSequence.ToString($"D{sequenceLength}"));
        }
        
        return formattedNumber;
    }

    // Missing methods referenced in controller
    public async Task<ErpSystem.Core.DTOs.Common.PagedResult<PurchaseRequisition>> GetRequisitionsAsync(int page, int pageSize, string? search = null, string? status = null, string? priority = null, DateTime? startDate = null, DateTime? endDate = null, string? department = null, Guid? sourcePlanId = null)
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
            query = query.Where(pr => pr.RequisitionNumber.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                                    (pr.RequestedBy != null && (pr.RequestedBy.FirstName.Contains(search, StringComparison.CurrentCultureIgnoreCase) || pr.RequestedBy.LastName.Contains(search, StringComparison.CurrentCultureIgnoreCase))));
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

        if (sourcePlanId.HasValue)
        {
            query = query.Where(pr => pr.SourcePlanId == sourcePlanId.Value);
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
        {
            return new List<PurchaseRequisitionItem>();
        }

        return await _dbSet
            .Where(pri => pri.RequisitionId == requisitionId && !pri.IsDeleted)
            .Include(pri => pri.Requisition)
            .Include(pri => pri.PreferredBusinessPartner)
            .Include(pri => pri.PurchaseOrder)
            .OrderBy(pri => pri.ItemDescription)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseRequisitionItem>> GetPendingItemsAsync()
    {
        return await _dbSet
            .Where(pri => pri.Status == "Pending" && !pri.IsDeleted)
            .Include(pri => pri.Requisition)
            .Include(pri => pri.PreferredBusinessPartner)
            .OrderBy(pri => pri.RequiredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseRequisitionItem>> GetItemsByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
        {
            return new List<PurchaseRequisitionItem>();
        }

        return await _dbSet
            .Where(pri => pri.InventoryItemId == inventoryItemId && !pri.IsDeleted)
            .Include(pri => pri.Requisition)
            .Include(pri => pri.PreferredBusinessPartner)
            .OrderByDescending(pri => pri.Requisition.RequisitionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseRequisitionItem>> GetItemsBySupplierAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty)
        {
            return new List<PurchaseRequisitionItem>();
        }

        return await _dbSet
            .Where(pri => pri.PreferredBusinessPartnerId == supplierId && !pri.IsDeleted)
            .Include(pri => pri.Requisition)
            .Include(pri => pri.PreferredBusinessPartner)
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
            .Include(pri => pri.PreferredBusinessPartner)
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
