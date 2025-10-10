using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Data.Repositories;

namespace ErpSystem.Data.Repositories.Maintenance;

#region Inspection Management Repository Implementations

public class InspectionTemplateRepository : GenericRepository<InspectionTemplate>, IInspectionTemplateRepository
{
    public InspectionTemplateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InspectionTemplate>> GetActiveAsync()
    {
        return await _dbSet
            .Where(it => it.IsActive && !it.IsDeleted)
            .OrderBy(it => it.Category)
            .ThenBy(it => it.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<InspectionTemplate>> GetByCategoryAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be null or empty", nameof(category));
            
        return await _dbSet
            .Where(it => it.Category == category && !it.IsDeleted)
            .OrderBy(it => it.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<InspectionTemplate>> GetByInspectionTypeAsync(string inspectionType)
    {
        if (string.IsNullOrWhiteSpace(inspectionType))
            throw new ArgumentException("Inspection type cannot be null or empty", nameof(inspectionType));
            
        return await _dbSet
            .Where(it => it.InspectionType == inspectionType && !it.IsDeleted)
            .OrderBy(it => it.Name)
            .ToListAsync();
    }

    public async Task<int> GetInspectionCountByTemplateAsync(Guid templateId)
    {
        if (templateId == Guid.Empty)
            return 0;
            
        return await _context.Set<AssetInspection>()
            .Where(ai => ai.InspectionTemplateId == templateId && !ai.IsDeleted)
            .CountAsync();
    }
}

public class AssetInspectionRepository : GenericRepository<AssetInspection>, IAssetInspectionRepository
{
    public AssetInspectionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AssetInspection>> GetByAssetIdAsync(Guid assetId)
    {
        if (assetId == Guid.Empty)
            throw new ArgumentException("Asset ID cannot be empty", nameof(assetId));
            
        return await _dbSet
            .Where(ai => ai.AssetId == assetId && !ai.IsDeleted)
            .Include(ai => ai.Asset)
            .Include(ai => ai.InspectionTemplate)
            .OrderByDescending(ai => ai.InspectionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetInspection>> GetByInspectorIdAsync(Guid inspectorId)
    {
        if (inspectorId == Guid.Empty)
            throw new ArgumentException("Inspector ID cannot be empty", nameof(inspectorId));
            
        return await _dbSet
            .Where(ai => ai.InspectorId == inspectorId && !ai.IsDeleted)
            .Include(ai => ai.Asset)
            .Include(ai => ai.InspectionTemplate)
            .OrderByDescending(ai => ai.InspectionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetInspection>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
            
        return await _dbSet
            .Where(ai => ai.Status == status && !ai.IsDeleted)
            .Include(ai => ai.Asset)
            .Include(ai => ai.InspectionTemplate)
            .OrderByDescending(ai => ai.InspectionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetInspection>> GetByOverallResultAsync(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
            throw new ArgumentException("Result cannot be null or empty", nameof(result));
            
        return await _dbSet
            .Where(ai => ai.OverallResult == result && !ai.IsDeleted)
            .Include(ai => ai.Asset)
            .Include(ai => ai.InspectionTemplate)
            .OrderByDescending(ai => ai.InspectionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetInspection>> GetInspectionsDueAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _dbSet
            .Where(ai => !ai.IsDeleted &&
                        ai.Status == "Scheduled" &&
                        ai.InspectionDate <= today)
            .Include(ai => ai.Asset)
            .Include(ai => ai.InspectionTemplate)
            .OrderBy(ai => ai.InspectionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetInspection>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
            throw new ArgumentException("Start date must be before end date");
            
        return await _dbSet
            .Where(ai => !ai.IsDeleted &&
                        ai.InspectionDate >= startDate &&
                        ai.InspectionDate <= endDate)
            .Include(ai => ai.Asset)
            .Include(ai => ai.InspectionTemplate)
            .OrderBy(ai => ai.InspectionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetInspection>> GetRegulatoryInspectionsAsync()
    {
        return await _dbSet
            .Where(ai => ai.IsRegulatoryRequired && !ai.IsDeleted)
            .Include(ai => ai.Asset)
            .Include(ai => ai.InspectionTemplate)
            .OrderByDescending(ai => ai.InspectionDate)
            .ToListAsync();
    }

    public async Task<AssetInspection?> GetLatestInspectionByAssetAsync(Guid assetId)
    {
        if (assetId == Guid.Empty)
            throw new ArgumentException("Asset ID cannot be empty", nameof(assetId));
            
        return await _dbSet
            .Where(ai => ai.AssetId == assetId && !ai.IsDeleted && ai.Status == "Completed")
            .Include(ai => ai.Asset)
            .Include(ai => ai.InspectionTemplate)
            .OrderByDescending(ai => ai.InspectionDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<AssetInspection>> GetInspectionHistoryByAssetAsync(Guid assetId)
    {
        if (assetId == Guid.Empty)
            throw new ArgumentException("Asset ID cannot be empty", nameof(assetId));
            
        return await _dbSet
            .Where(ai => ai.AssetId == assetId && !ai.IsDeleted)
            .Include(ai => ai.Asset)
            .Include(ai => ai.InspectionTemplate)
            .OrderByDescending(ai => ai.InspectionDate)
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets overdue inspections that should have been completed by now
    /// </summary>
    public async Task<IEnumerable<AssetInspection>> GetOverdueInspectionsAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _dbSet
            .Where(ai => !ai.IsDeleted &&
                        ai.Status == "Scheduled" &&
                        ai.InspectionDate < today)
            .Include(ai => ai.Asset)
                .ThenInclude(a => a!.AssetCategory)
            .Include(ai => ai.InspectionTemplate)
            .OrderBy(ai => ai.InspectionDate)
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets inspections due within a specified number of days
    /// </summary>
    public async Task<IEnumerable<AssetInspection>> GetInspectionsDueInDaysAsync(int days)
    {
        var targetDate = DateTime.UtcNow.Date.AddDays(days);
        return await _dbSet
            .Where(ai => !ai.IsDeleted &&
                        ai.Status == "Scheduled" &&
                        ai.InspectionDate <= targetDate)
            .Include(ai => ai.Asset)
            .Include(ai => ai.InspectionTemplate)
            .OrderBy(ai => ai.InspectionDate)
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets inspections by asset criticality level
    /// </summary>
    public async Task<IEnumerable<AssetInspection>> GetByCriticalityAsync(string criticality)
    {
        if (string.IsNullOrWhiteSpace(criticality))
            throw new ArgumentException("Criticality cannot be null or empty", nameof(criticality));
            
        return await _dbSet
            .Where(ai => !ai.IsDeleted && 
                        ai.Asset != null && 
                        ai.Asset.Criticality.ToString() == criticality)
            .Include(ai => ai.Asset)
            .Include(ai => ai.InspectionTemplate)
            .OrderByDescending(ai => ai.InspectionDate)
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets failed inspections that require attention
    /// </summary>
    public async Task<IEnumerable<AssetInspection>> GetFailedInspectionsAsync()
    {
        return await _dbSet
            .Where(ai => !ai.IsDeleted &&
                        ai.Status == "Completed" &&
                        (ai.OverallResult == "Failed" || ai.OverallResult == "Critical"))
            .Include(ai => ai.Asset)
                .ThenInclude(a => a!.AssetCategory)
            .Include(ai => ai.InspectionTemplate)
            .OrderByDescending(ai => ai.InspectionDate)
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets inspection statistics for a given period
    /// </summary>
    public async Task<(int Total, int Completed, int Failed, int Overdue)> GetInspectionStatsAsync(DateTime startDate, DateTime endDate)
    {
        var inspections = await _dbSet
            .Where(ai => !ai.IsDeleted &&
                        ai.InspectionDate >= startDate &&
                        ai.InspectionDate <= endDate)
            .ToListAsync();
            
        var total = inspections.Count;
        var completed = inspections.Count(i => i.Status == "Completed");
        var failed = inspections.Count(i => i.Status == "Completed" && 
                                            (i.OverallResult == "Failed" || i.OverallResult == "Critical"));
        var overdue = inspections.Count(i => i.Status == "Scheduled" && i.InspectionDate < DateTime.UtcNow.Date);
        
        return (total, completed, failed, overdue);
    }
}

public class InspectionDocumentRepository : GenericRepository<InspectionDocument>, IInspectionDocumentRepository
{
    public InspectionDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InspectionDocument>> GetByInspectionIdAsync(Guid inspectionId)
    {
        if (inspectionId == Guid.Empty)
            throw new ArgumentException("Inspection ID cannot be empty", nameof(inspectionId));
            
        return await _dbSet
            .Where(id => id.InspectionId == inspectionId && !id.IsDeleted)
            .Include(id => id.Inspection)
            .OrderByDescending(id => id.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<InspectionDocument>> GetByDocumentTypeAsync(string documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            throw new ArgumentException("Document type cannot be null or empty", nameof(documentType));
            
        return await _dbSet
            .Where(id => id.DocumentType == documentType && !id.IsDeleted)
            .Include(id => id.Inspection)
            .OrderByDescending(id => id.CreatedAt)
            .ToListAsync();
    }

    public async Task<long> GetTotalFileSizeByInspectionAsync(Guid inspectionId)
    {
        if (inspectionId == Guid.Empty)
            return 0;
            
        return await _dbSet
            .Where(id => id.InspectionId == inspectionId && !id.IsDeleted)
            .SumAsync(id => id.FileSize);
    }
    
    /// <summary>
    /// Gets documents by uploaded user
    /// </summary>
    public async Task<IEnumerable<InspectionDocument>> GetByUploadedByAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
            
        return await _dbSet
            .Where(id => id.CreatedBy == userId.ToString() && !id.IsDeleted)
            .Include(id => id.Inspection)
            .OrderByDescending(id => id.CreatedAt)
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets recent documents within a specified number of days
    /// </summary>
    public async Task<IEnumerable<InspectionDocument>> GetRecentDocumentsAsync(int days)
    {
        var cutoffDate = DateTime.UtcNow.Date.AddDays(-days);
        return await _dbSet
            .Where(id => !id.IsDeleted && id.CreatedAt >= cutoffDate)
            .Include(id => id.Inspection)
                .ThenInclude(i => i!.Asset)
            .OrderByDescending(id => id.CreatedAt)
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets documents by file extension
    /// </summary>
    public async Task<IEnumerable<InspectionDocument>> GetByFileExtensionAsync(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Extension cannot be null or empty", nameof(extension));
            
        var normalizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        
        return await _dbSet
            .Where(id => !id.IsDeleted && 
                        id.FileName != null && 
                        id.FileName.EndsWith(normalizedExtension))
            .Include(id => id.Inspection)
            .OrderByDescending(id => id.CreatedAt)
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets total storage usage statistics
    /// </summary>
    public async Task<(long TotalSize, int TotalFiles, double AverageFileSize)> GetStorageStatsAsync()
    {
        var documents = await _dbSet
            .Where(id => !id.IsDeleted)
            .Select(id => id.FileSize)
            .ToListAsync();
            
        var totalSize = documents.Sum();
        var totalFiles = documents.Count;
        var averageSize = totalFiles > 0 ? (double)totalSize / totalFiles : 0;
        
        return (totalSize, totalFiles, averageSize);
    }
}

#endregion