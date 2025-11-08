using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using System.Text.Json;

namespace ErpSystem.Core.Services.Maintenance;

public class AssetUsageTrackingService : IAssetUsageTrackingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMaintenanceAssetService _assetService;

    public AssetUsageTrackingService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IMaintenanceAssetService assetService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _assetService = assetService;
    }

    public async Task<AssetUsageTrackingDto> CreateUsageRecordAsync(CreateAssetUsageTrackingDto createDto)
    {
        // Validate asset exists
        var asset = await _assetService.GetAssetByIdAsync(createDto.AssetId);
        if (asset == null)
            throw new KeyNotFoundException($"Asset with ID {createDto.AssetId} not found");

        var usageRecord = new AssetUsageTracking
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUserService.TenantId ?? Guid.Empty,
            AssetId = createDto.AssetId,
            RecordedAt = createDto.RecordedAt,
            Mileage = createDto.Mileage,
            MileageUnit = createDto.MileageUnit,
            OperatingHours = createDto.OperatingHours,
            Cycles = createDto.Cycles,
            FuelConsumed = createDto.FuelConsumed,
            FuelUnit = createDto.FuelUnit,
            DataSource = createDto.DataSource,
            ExternalReferenceId = createDto.ExternalReferenceId,
            AdditionalMetrics = createDto.AdditionalMetrics,
            Notes = createDto.Notes,
            RecordedById = _currentUserService.UserId != null ? Guid.Parse(_currentUserService.UserId) : (Guid?)null,
            IsValidated = createDto.IsValidated,
            TriggeredMaintenance = false
        };

        await _unitOfWork.Repository<AssetUsageTracking>().AddAsync(usageRecord);
        await _unitOfWork.SaveChangesAsync();

        return await MapToDto(usageRecord);
    }

    public async Task<IEnumerable<AssetUsageTrackingDto>> BulkCreateUsageRecordsAsync(BulkUsageImportDto bulkDto)
    {
        var results = new List<AssetUsageTrackingDto>();

        foreach (var recordDto in bulkDto.UsageRecords)
        {
            recordDto.DataSource = bulkDto.DataSource;
            recordDto.IsValidated = bulkDto.ValidateAll;
            
            var result = await CreateUsageRecordAsync(recordDto);
            results.Add(result);
        }

        return results;
    }

    public async Task<AssetUsageTrackingDto?> GetUsageRecordByIdAsync(Guid id)
    {
        var record = await _unitOfWork.Repository<AssetUsageTracking>()
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _currentUserService.TenantId);

        return record != null ? await MapToDto(record) : null;
    }

    public async Task<IEnumerable<AssetUsageTrackingDto>> GetUsageRecordsAsync(
        Guid assetId, 
        DateTime? startDate = null, 
        DateTime? endDate = null)
    {
        var query = await _unitOfWork.Repository<AssetUsageTracking>()
            .FindAsync(u => 
                u.AssetId == assetId && 
                u.TenantId == _currentUserService.TenantId &&
                (!startDate.HasValue || u.RecordedAt >= startDate.Value) &&
                (!endDate.HasValue || u.RecordedAt <= endDate.Value));

        var orderedRecords = query.OrderByDescending(u => u.RecordedAt).ToList();
        var dtos = new List<AssetUsageTrackingDto>();
        
        foreach (var record in orderedRecords)
        {
            dtos.Add(await MapToDto(record));
        }

        return dtos;
    }

    public async Task<PagedResult<AssetUsageTrackingDto>> GetUsageRecordsPagedAsync(
        Guid assetId, 
        int page, 
        int pageSize, 
        DateTime? startDate = null, 
        DateTime? endDate = null)
    {
        var query = await _unitOfWork.Repository<AssetUsageTracking>()
            .FindAsync(u => 
                u.AssetId == assetId && 
                u.TenantId == _currentUserService.TenantId &&
                (!startDate.HasValue || u.RecordedAt >= startDate.Value) &&
                (!endDate.HasValue || u.RecordedAt <= endDate.Value));

        var totalCount = query.Count();
        var records = query
            .OrderByDescending(u => u.RecordedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = new List<AssetUsageTrackingDto>();
        foreach (var record in records)
        {
            dtos.Add(await MapToDto(record));
        }

        return new PagedResult<AssetUsageTrackingDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task DeleteUsageRecordAsync(Guid id)
    {
        var record = await _unitOfWork.Repository<AssetUsageTracking>()
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _currentUserService.TenantId);

        if (record == null)
            throw new KeyNotFoundException($"Usage record with ID {id} not found");

        await _unitOfWork.Repository<AssetUsageTracking>().DeleteAsync(record);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<AssetUsageSummaryDto> GetAssetUsageSummaryAsync(Guid assetId)
    {
        return await GetAssetUsageSummaryAsync(assetId, null);
    }

    public async Task<AssetUsageSummaryDto> GetAssetUsageSummaryAsync(Guid assetId, Guid? tenantId)
    {
        var asset = await _assetService.GetAssetByIdAsync(assetId);
        if (asset == null)
            throw new KeyNotFoundException($"Asset with ID {assetId} not found");

        // If no tenant provided, use current user's tenant; for background service (null tenant), query all
        var effectiveTenantId = tenantId ?? _currentUserService.TenantId;
        
        var records = await _unitOfWork.Repository<AssetUsageTracking>()
            .FindAsync(u => u.AssetId == assetId && (effectiveTenantId == null || u.TenantId == effectiveTenantId));

        var recordsList = records.OrderByDescending(u => u.RecordedAt).ToList();
        var latestRecord = recordsList.FirstOrDefault();

        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var recentRecords = recordsList.Where(u => u.RecordedAt >= thirtyDaysAgo).ToList();

        decimal? avgDailyMileage = null;
        decimal? avgDailyHours = null;

        if (recentRecords.Any())
        {
            var daysSpan = (DateTime.UtcNow - recentRecords.Last().RecordedAt).TotalDays;
            if (daysSpan > 0)
            {
                var mileageRecords = recentRecords.Where(r => r.Mileage.HasValue).ToList();
                if (mileageRecords.Any())
                {
                    var mileageDiff = mileageRecords.First().Mileage - mileageRecords.Last().Mileage;
                    avgDailyMileage = mileageDiff / (decimal)daysSpan;
                }

                var hoursRecords = recentRecords.Where(r => r.OperatingHours.HasValue).ToList();
                if (hoursRecords.Any())
                {
                    var hoursDiff = hoursRecords.First().OperatingHours - hoursRecords.Last().OperatingHours;
                    avgDailyHours = hoursDiff / (decimal)daysSpan;
                }
            }
        }

        return new AssetUsageSummaryDto
        {
            AssetId = assetId,
            AssetName = asset.Name,
            AssetNumber = asset.AssetNumber,
            CurrentMileage = latestRecord?.Mileage,
            CurrentOperatingHours = latestRecord?.OperatingHours,
            CurrentCycles = latestRecord?.Cycles,
            AverageDailyMileage = avgDailyMileage,
            AverageDailyOperatingHours = avgDailyHours,
            TotalFuelConsumed = recordsList.Where(r => r.FuelConsumed.HasValue).Sum(r => r.FuelConsumed),
            LastRecordedAt = latestRecord?.RecordedAt,
            TotalRecords = recordsList.Count
        };
    }

    public async Task<IEnumerable<AssetUsageSummaryDto>> GetAllAssetUsageSummariesAsync()
    {
        var assets = await _assetService.GetAllAssetsAsync();
        var summaries = new List<AssetUsageSummaryDto>();

        foreach (var asset in assets)
        {
            summaries.Add(await GetAssetUsageSummaryAsync(asset.Id));
        }

        return summaries;
    }

    public async Task<decimal?> GetCurrentMileageAsync(Guid assetId)
    {
        var latest = await GetLatestUsageRecordAsync(assetId);
        return latest?.Mileage;
    }

    public async Task<decimal?> GetCurrentOperatingHoursAsync(Guid assetId)
    {
        var latest = await GetLatestUsageRecordAsync(assetId);
        return latest?.OperatingHours;
    }

    public async Task<int?> GetCurrentCyclesAsync(Guid assetId)
    {
        var latest = await GetLatestUsageRecordAsync(assetId);
        return latest?.Cycles;
    }

    public async Task<decimal?> GetAverageDailyMileageAsync(Guid assetId, int days = 30)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);
        var records = await _unitOfWork.Repository<AssetUsageTracking>()
            .FindAsync(u => 
                u.AssetId == assetId && 
                u.TenantId == _currentUserService.TenantId &&
                u.RecordedAt >= startDate &&
                u.Mileage.HasValue);

        var recordsList = records.OrderBy(u => u.RecordedAt).ToList();
        if (recordsList.Count < 2) return null;

        var firstRecord = recordsList.First();
        var lastRecord = recordsList.Last();
        var mileageDiff = lastRecord.Mileage!.Value - firstRecord.Mileage!.Value;
        var daysDiff = (lastRecord.RecordedAt - firstRecord.RecordedAt).TotalDays;

        return daysDiff > 0 ? mileageDiff / (decimal)daysDiff : null;
    }

    public async Task<decimal?> GetAverageDailyHoursAsync(Guid assetId, int days = 30)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);
        var records = await _unitOfWork.Repository<AssetUsageTracking>()
            .FindAsync(u => 
                u.AssetId == assetId && 
                u.TenantId == _currentUserService.TenantId &&
                u.RecordedAt >= startDate &&
                u.OperatingHours.HasValue);

        var recordsList = records.OrderBy(u => u.RecordedAt).ToList();
        if (recordsList.Count < 2) return null;

        var firstRecord = recordsList.First();
        var lastRecord = recordsList.Last();
        var hoursDiff = lastRecord.OperatingHours!.Value - firstRecord.OperatingHours!.Value;
        var daysDiff = (lastRecord.RecordedAt - firstRecord.RecordedAt).TotalDays;

        return daysDiff > 0 ? hoursDiff / (decimal)daysDiff : null;
    }

    public async Task<bool> ImportUsageDataFromSourceAsync(string dataSource, string externalData)
    {
        try
        {
            // This would be extended based on the data source type
            // For now, assume JSON format
            var records = JsonSerializer.Deserialize<List<CreateAssetUsageTrackingDto>>(externalData);
            if (records == null) return false;

            await BulkCreateUsageRecordsAsync(new BulkUsageImportDto
            {
                UsageRecords = records,
                DataSource = dataSource,
                ValidateAll = false // Require manual validation for imports
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<AssetUsageTrackingDto>> GetUnvalidatedRecordsAsync()
    {
        var records = await _unitOfWork.Repository<AssetUsageTracking>()
            .FindAsync(u => !u.IsValidated && u.TenantId == _currentUserService.TenantId);

        var dtos = new List<AssetUsageTrackingDto>();
        foreach (var record in records.OrderByDescending(u => u.RecordedAt))
        {
            dtos.Add(await MapToDto(record));
        }

        return dtos;
    }

    public async Task ValidateUsageRecordAsync(Guid id)
    {
        var record = await _unitOfWork.Repository<AssetUsageTracking>()
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == _currentUserService.TenantId);

        if (record == null)
            throw new KeyNotFoundException($"Usage record with ID {id} not found");

        record.IsValidated = true;
        await _unitOfWork.Repository<AssetUsageTracking>().UpdateAsync(record);
        await _unitOfWork.SaveChangesAsync();
    }

    #region Helper Methods

    private async Task<AssetUsageTracking?> GetLatestUsageRecordAsync(Guid assetId)
    {
        var records = await _unitOfWork.Repository<AssetUsageTracking>()
            .FindAsync(u => u.AssetId == assetId && u.TenantId == _currentUserService.TenantId);

        return records.OrderByDescending(u => u.RecordedAt).FirstOrDefault();
    }

    private async Task<AssetUsageTrackingDto> MapToDto(AssetUsageTracking record)
    {
        var asset = await _assetService.GetAssetByIdAsync(record.AssetId);

        return new AssetUsageTrackingDto
        {
            Id = record.Id,
            AssetId = record.AssetId,
            AssetName = asset?.Name,
            AssetNumber = asset?.AssetNumber,
            RecordedAt = record.RecordedAt,
            Mileage = record.Mileage,
            MileageUnit = record.MileageUnit,
            OperatingHours = record.OperatingHours,
            Cycles = record.Cycles,
            FuelConsumed = record.FuelConsumed,
            FuelUnit = record.FuelUnit,
            DataSource = record.DataSource,
            ExternalReferenceId = record.ExternalReferenceId,
            AdditionalMetrics = record.AdditionalMetrics,
            Notes = record.Notes,
            RecordedById = record.RecordedById,
            RecordedByName = null, // Could be populated from HR module if needed
            TriggeredMaintenance = record.TriggeredMaintenance,
            IsValidated = record.IsValidated,
            CreatedAt = record.CreatedAt
        };
    }

    #endregion
}
