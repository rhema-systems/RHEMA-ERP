using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Repositories.Maintenance;

/// <summary>
/// Repository for asset condition inspection (pre-inspection at admission / post-inspection at discharge)
/// </summary>
public class AssetConditionRepository : GenericRepository<PreInspectionChecklistTemplate>, IAssetConditionRepository
{
    private readonly ILogger<AssetConditionRepository> _logger;

    public AssetConditionRepository(ApplicationDbContext context, ILogger<AssetConditionRepository> logger) : base(context)
    {
        _logger = logger;
    }

    #region Template Operations

    public async Task<PreInspectionChecklistTemplate?> GetTemplateByIdAsync(Guid id, Guid tenantId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted);
    }

    public async Task<PreInspectionChecklistTemplate?> GetTemplateWithItemsAsync(Guid id, Guid tenantId)
    {
        return await _dbSet
            .Include(t => t.ChecklistItems.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .Include(t => t.AssetCategory)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted);
    }

    public async Task<IEnumerable<PreInspectionChecklistTemplate>> GetAllTemplatesAsync(Guid tenantId, bool includeInactive = false)
    {
        var query = _dbSet
            .Include(t => t.AssetCategory)
            .Include(t => t.ChecklistItems.Where(i => !i.IsDeleted))
            .Where(t => t.TenantId == tenantId && !t.IsDeleted);

        if (!includeInactive)
            query = query.Where(t => t.IsActive);

        return await query.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync();
    }

    public async Task<IEnumerable<PreInspectionChecklistTemplate>> GetTemplatesByAssetCategoryAsync(Guid assetCategoryId, Guid tenantId)
    {
        return await _dbSet
            .Include(t => t.ChecklistItems.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .Where(t => t.AssetCategoryId == assetCategoryId && t.TenantId == tenantId && t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.SortOrder)
            .ToListAsync();
    }

    public async Task<PreInspectionChecklistTemplate?> GetDefaultTemplateForAssetCategoryAsync(Guid assetCategoryId, Guid tenantId)
    {
        return await _dbSet
            .Include(t => t.ChecklistItems.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(t => t.AssetCategoryId == assetCategoryId && t.TenantId == tenantId && t.IsDefault && t.IsActive && !t.IsDeleted);
    }

    public async Task<PreInspectionChecklistTemplate> AddTemplateAsync(PreInspectionChecklistTemplate template)
    {
        await _dbSet.AddAsync(template);
        return template;
    }

    public async Task UpdateTemplateAsync(PreInspectionChecklistTemplate template)
    {
        _dbSet.Update(template);
        await Task.CompletedTask;
    }

    public async Task DeleteTemplateAsync(Guid id, Guid tenantId)
    {
        var template = await GetTemplateByIdAsync(id, tenantId);
        if (template != null)
        {
            template.IsDeleted = true;
            template.DeletedAt = DateTime.UtcNow;
        }
    }

    #endregion

    #region Template Item Operations

    public async Task<PreInspectionChecklistItem?> GetTemplateItemByIdAsync(Guid id, Guid tenantId)
    {
        return await _context.Set<PreInspectionChecklistItem>()
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tenantId && !i.IsDeleted);
    }

    public async Task<IEnumerable<PreInspectionChecklistItem>> GetTemplateItemsAsync(Guid templateId, Guid tenantId)
    {
        return await _context.Set<PreInspectionChecklistItem>()
            .Where(i => i.TemplateId == templateId && i.TenantId == tenantId && !i.IsDeleted)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();
    }

    public async Task<PreInspectionChecklistItem> AddTemplateItemAsync(PreInspectionChecklistItem item)
    {
        await _context.Set<PreInspectionChecklistItem>().AddAsync(item);
        return item;
    }

    public async Task UpdateTemplateItemAsync(PreInspectionChecklistItem item)
    {
        _context.Set<PreInspectionChecklistItem>().Update(item);
        await Task.CompletedTask;
    }

    public async Task DeleteTemplateItemAsync(Guid id, Guid tenantId)
    {
        var item = await GetTemplateItemByIdAsync(id, tenantId);
        if (item != null)
        {
            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
        }
    }

    public async Task DeleteTemplateItemsByTemplateIdAsync(Guid templateId, Guid tenantId)
    {
        var items = await _context.Set<PreInspectionChecklistItem>()
            .Where(i => i.TemplateId == templateId && i.TenantId == tenantId && !i.IsDeleted)
            .ToListAsync();

        foreach (var item in items)
        {
            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
        }
    }

    #endregion

    #region Condition Record Operations

    public async Task<AssetConditionRecord?> GetRecordByIdAsync(Guid id, Guid tenantId)
    {
        return await _context.Set<AssetConditionRecord>()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted);
    }

    public async Task<AssetConditionRecord?> GetRecordWithDetailsAsync(Guid id, Guid tenantId)
    {
        return await _context.Set<AssetConditionRecord>()
            .Include(r => r.Asset)
            .Include(r => r.Template)
                .ThenInclude(t => t.ChecklistItems.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .Include(r => r.ItemResults.Where(ir => !ir.IsDeleted))
                .ThenInclude(ir => ir.ChecklistItem)
            .Include(r => r.Admission)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted);
    }

    public async Task<IEnumerable<AssetConditionRecord>> GetAllRecordsAsync(Guid tenantId, int page = 1, int pageSize = 20)
    {
        return await _context.Set<AssetConditionRecord>()
            .Include(r => r.Asset)
            .Include(r => r.Template)
            .Include(r => r.ItemResults.Where(ir => !ir.IsDeleted))
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.InspectionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetConditionRecord>> GetRecordsByAssetAsync(Guid assetId, Guid tenantId)
    {
        return await _context.Set<AssetConditionRecord>()
            .Include(r => r.Template)
            .Include(r => r.ItemResults.Where(ir => !ir.IsDeleted))
            .Where(r => r.AssetId == assetId && r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.InspectionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetConditionRecord>> GetRecordsByAdmissionAsync(Guid admissionId, Guid tenantId)
    {
        return await _context.Set<AssetConditionRecord>()
            .Include(r => r.Template)
            .Include(r => r.ItemResults.Where(ir => !ir.IsDeleted))
            .Where(r => r.AdmissionId == admissionId && r.TenantId == tenantId && !r.IsDeleted)
            .OrderByDescending(r => r.InspectionDate)
            .ToListAsync();
    }

    public async Task<AssetConditionRecord?> GetAdmissionRecordForAdmissionAsync(Guid admissionId, Guid tenantId)
    {
        return await _context.Set<AssetConditionRecord>()
            .Include(r => r.Asset)
            .Include(r => r.Template)
                .ThenInclude(t => t.ChecklistItems.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .Include(r => r.ItemResults.Where(ir => !ir.IsDeleted))
                .ThenInclude(ir => ir.ChecklistItem)
            .FirstOrDefaultAsync(r => r.AdmissionId == admissionId && r.InspectionType == "Admission" && r.TenantId == tenantId && !r.IsDeleted);
    }

    public async Task<AssetConditionRecord?> GetDischargeRecordForAdmissionAsync(Guid admissionId, Guid tenantId)
    {
        return await _context.Set<AssetConditionRecord>()
            .Include(r => r.Asset)
            .Include(r => r.Template)
                .ThenInclude(t => t.ChecklistItems.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .Include(r => r.ItemResults.Where(ir => !ir.IsDeleted))
                .ThenInclude(ir => ir.ChecklistItem)
            .FirstOrDefaultAsync(r => r.AdmissionId == admissionId && r.InspectionType == "Discharge" && r.TenantId == tenantId && !r.IsDeleted);
    }

    public async Task<AssetConditionRecord?> GetAdmissionRecordForJobCardAsync(Guid jobCardId, Guid tenantId)
    {
        _logger.LogInformation("🔍 GetAdmissionRecordForJobCardAsync called with JobCardId: {JobCardId}, TenantId: {TenantId}", jobCardId, tenantId);

        // Step 1: Find the admission for this job card
        var admission = await _context.Set<AssetAdmission>()
            .FirstOrDefaultAsync(a => a.JobCardId == jobCardId && a.TenantId == tenantId && !a.IsDeleted);

        if (admission == null)
        {
            // Debug: Check if there's an admission with this job card but different tenant or deleted
            var anyAdmission = await _context.Set<AssetAdmission>()
                .FirstOrDefaultAsync(a => a.JobCardId == jobCardId);
            if (anyAdmission != null)
            {
                _logger.LogWarning("❌ Found admission {AdmissionId} for JobCard {JobCardId}, but TenantId={AdmissionTenantId} (expected {TenantId}), IsDeleted={IsDeleted}",
                    anyAdmission.Id, jobCardId, anyAdmission.TenantId, tenantId, anyAdmission.IsDeleted);
            }
            else
            {
                _logger.LogWarning("❌ No admission found for JobCardId: {JobCardId}", jobCardId);
            }
            return null;
        }

        _logger.LogInformation("✅ Found admission {AdmissionId} for JobCard {JobCardId}", admission.Id, jobCardId);

        // Step 2: Find the condition record for this admission with all navigation properties
        var record = await _context.Set<AssetConditionRecord>()
            .Include(r => r.Asset)
            .Include(r => r.Template)
                .ThenInclude(t => t.ChecklistItems.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .Include(r => r.ItemResults.Where(ir => !ir.IsDeleted))
                .ThenInclude(ir => ir.ChecklistItem)
            .FirstOrDefaultAsync(r => r.AdmissionId == admission.Id
                && r.InspectionType == "Admission"
                && r.TenantId == tenantId
                && !r.IsDeleted);

        if (record == null)
        {
            // Debug: Check if there's a condition record but with different criteria
            var anyRecord = await _context.Set<AssetConditionRecord>()
                .FirstOrDefaultAsync(r => r.AdmissionId == admission.Id);
            if (anyRecord != null)
            {
                _logger.LogWarning("❌ Found condition record {RecordId} for Admission {AdmissionId}, but InspectionType={InspectionType}, TenantId={RecordTenantId}, IsDeleted={IsDeleted}",
                    anyRecord.Id, admission.Id, anyRecord.InspectionType, anyRecord.TenantId, anyRecord.IsDeleted);
            }
            else
            {
                _logger.LogWarning("❌ No condition record found for AdmissionId: {AdmissionId}", admission.Id);
            }
        }
        else
        {
            _logger.LogInformation("✅ Found condition record {RecordId} for Admission {AdmissionId} with {ItemCount} item results",
                record.Id, admission.Id, record.ItemResults?.Count ?? 0);
        }

        return record;
    }

    public async Task<AssetConditionRecord?> GetDischargeRecordForJobCardAsync(Guid jobCardId, Guid tenantId)
    {
        _logger.LogInformation("🔍 GetDischargeRecordForJobCardAsync called with JobCardId: {JobCardId}, TenantId: {TenantId}", jobCardId, tenantId);

        // Step 1: Find the admission for this job card
        var admission = await _context.Set<AssetAdmission>()
            .FirstOrDefaultAsync(a => a.JobCardId == jobCardId && a.TenantId == tenantId && !a.IsDeleted);

        if (admission == null)
        {
            _logger.LogWarning("❌ No admission found for JobCardId: {JobCardId} (discharge lookup)", jobCardId);
            return null;
        }

        _logger.LogInformation("✅ Found admission {AdmissionId} for JobCard {JobCardId} (discharge lookup)", admission.Id, jobCardId);

        // Step 2: Find the discharge condition record for this admission with all navigation properties
        var record = await _context.Set<AssetConditionRecord>()
            .Include(r => r.Asset)
            .Include(r => r.Template)
                .ThenInclude(t => t.ChecklistItems.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder))
            .Include(r => r.ItemResults.Where(ir => !ir.IsDeleted))
                .ThenInclude(ir => ir.ChecklistItem)
            .FirstOrDefaultAsync(r => r.AdmissionId == admission.Id
                && r.InspectionType == "Discharge"
                && r.TenantId == tenantId
                && !r.IsDeleted);

        if (record != null)
        {
            _logger.LogInformation("✅ Found discharge record {RecordId} for Admission {AdmissionId}", record.Id, admission.Id);
        }
        else
        {
            _logger.LogInformation("ℹ️ No discharge record found for Admission {AdmissionId} (this is normal if not yet discharged)", admission.Id);
        }

        return record;
    }

    public async Task<int> GetRecordsCountAsync(Guid tenantId)
    {
        return await _context.Set<AssetConditionRecord>()
            .CountAsync(r => r.TenantId == tenantId && !r.IsDeleted);
    }

    public async Task<AssetConditionRecord> AddRecordAsync(AssetConditionRecord record)
    {
        await _context.Set<AssetConditionRecord>().AddAsync(record);
        return record;
    }

    public async Task UpdateRecordAsync(AssetConditionRecord record)
    {
        _context.Set<AssetConditionRecord>().Update(record);
        await Task.CompletedTask;
    }

    public async Task DeleteRecordAsync(Guid id, Guid tenantId)
    {
        var record = await GetRecordByIdAsync(id, tenantId);
        if (record != null)
        {
            record.IsDeleted = true;
            record.DeletedAt = DateTime.UtcNow;
        }
    }

    #endregion

    #region Item Result Operations

    public async Task<AssetConditionItemResult?> GetItemResultByIdAsync(Guid id, Guid tenantId)
    {
        return await _context.Set<AssetConditionItemResult>()
            .Include(ir => ir.ChecklistItem)
            .FirstOrDefaultAsync(ir => ir.Id == id && ir.TenantId == tenantId && !ir.IsDeleted);
    }

    public async Task<IEnumerable<AssetConditionItemResult>> GetItemResultsByRecordAsync(Guid recordId, Guid tenantId)
    {
        return await _context.Set<AssetConditionItemResult>()
            .Include(ir => ir.ChecklistItem)
            .Where(ir => ir.ConditionRecordId == recordId && ir.TenantId == tenantId && !ir.IsDeleted)
            .OrderBy(ir => ir.ChecklistItem.SortOrder)
            .ToListAsync();
    }

    public async Task<AssetConditionItemResult> AddItemResultAsync(AssetConditionItemResult result)
    {
        await _context.Set<AssetConditionItemResult>().AddAsync(result);
        return result;
    }

    public async Task UpdateItemResultAsync(AssetConditionItemResult result)
    {
        _context.Set<AssetConditionItemResult>().Update(result);
        await Task.CompletedTask;
    }

    public async Task DeleteItemResultAsync(Guid id, Guid tenantId)
    {
        var result = await GetItemResultByIdAsync(id, tenantId);
        if (result != null)
        {
            result.IsDeleted = true;
            result.DeletedAt = DateTime.UtcNow;
        }
    }

    public async Task<AssetConditionItemResult?> GetItemResultByRecordAndItemAsync(Guid recordId, Guid checklistItemId, Guid tenantId)
    {
        return await _context.Set<AssetConditionItemResult>()
            .Include(ir => ir.ChecklistItem)
            .FirstOrDefaultAsync(ir => ir.ConditionRecordId == recordId && ir.ChecklistItemId == checklistItemId && ir.TenantId == tenantId && !ir.IsDeleted);
    }

    #endregion
}
