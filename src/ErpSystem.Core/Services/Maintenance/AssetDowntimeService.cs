using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for tracking and analysing asset downtime.
/// This implementation uses the generic IUnitOfWork repository API rather than a dedicated IAssetDowntimeRepository,
/// so it does not require any additional repository registrations.
/// </summary>
public class AssetDowntimeService : IAssetDowntimeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMaintenanceAssetService _assetService;
    private readonly ILogger<AssetDowntimeService> _logger;

    public AssetDowntimeService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IMaintenanceAssetService assetService,
        ILogger<AssetDowntimeService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _assetService = assetService;
        _logger = logger;
    }

    #region CRUD operations

    public async Task<AssetDowntimeDto> StartDowntimeAsync(CreateAssetDowntimeDto createDto)
    {
        try
        {
            // Validate asset exists
            var asset = await _assetService.GetAssetByIdAsync(createDto.AssetId) ?? throw new KeyNotFoundException($"Asset with ID {createDto.AssetId} not found");
            var downtime = new AssetDowntime
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserService.TenantId ?? Guid.Empty,
                AssetId = createDto.AssetId,
                WorkOrderId = createDto.WorkOrderId,
                StartTime = createDto.StartTime,
                EndTime = null,
                DowntimeHours = null,
                Reason = createDto.Reason,
                Description = createDto.Description,
                EstimatedCostImpact = createDto.EstimatedCostImpact,
                Status = "Active"
            };

            await _unitOfWork.Repository<AssetDowntime>().AddAsync(downtime);
            await _unitOfWork.SaveChangesAsync();

            return await MapToDtoAsync(downtime, asset.Name, asset.AssetNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting downtime for asset {AssetId}", createDto.AssetId);
            throw;
        }
    }

    public async Task<AssetDowntimeDto> EndDowntimeAsync(Guid id, DateTime endTime, string? resolutionNotes = null)
    {
        try
        {
            var repo = _unitOfWork.Repository<AssetDowntime>();
            var downtime = await repo.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == _currentUserService.TenantId) ?? throw new KeyNotFoundException($"Asset downtime with ID {id} not found");
            downtime.EndTime = endTime;
            if (endTime < downtime.StartTime)
            {
                endTime = downtime.StartTime;
            }
            downtime.DowntimeHours = (endTime - downtime.StartTime).TotalHours;
            downtime.Status = "Resolved";

            if (!string.IsNullOrWhiteSpace(resolutionNotes))
            {
                downtime.Description = string.IsNullOrWhiteSpace(downtime.Description)
                    ? resolutionNotes
                    : downtime.Description + "\n" + resolutionNotes;
            }

            await repo.UpdateAsync(downtime);
            await _unitOfWork.SaveChangesAsync();

            var asset = await _assetService.GetAssetByIdAsync(downtime.AssetId);
            return await MapToDtoAsync(downtime, asset?.Name, asset?.AssetNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending downtime {DowntimeId}", id);
            throw;
        }
    }

    public async Task<AssetDowntimeDto> UpdateDowntimeAsync(Guid id, UpdateAssetDowntimeDto updateDto)
    {
        try
        {
            var repo = _unitOfWork.Repository<AssetDowntime>();
            var downtime = await repo.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == _currentUserService.TenantId) ?? throw new KeyNotFoundException($"Asset downtime with ID {id} not found");
            downtime.StartTime = updateDto.StartTime;
            downtime.EndTime = updateDto.EndTime;
            if (downtime.EndTime.HasValue && downtime.EndTime.Value >= downtime.StartTime)
            {
                downtime.DowntimeHours = (downtime.EndTime.Value - downtime.StartTime).TotalHours;
            }

            downtime.Reason = updateDto.Reason;
            downtime.Description = updateDto.Description;
            downtime.EstimatedCostImpact = updateDto.EstimatedCostImpact;
            downtime.Status = string.IsNullOrWhiteSpace(updateDto.Status) ? downtime.Status : updateDto.Status;

            await repo.UpdateAsync(downtime);
            await _unitOfWork.SaveChangesAsync();

            var asset = await _assetService.GetAssetByIdAsync(downtime.AssetId);
            return await MapToDtoAsync(downtime, asset?.Name, asset?.AssetNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating downtime {DowntimeId}", id);
            throw;
        }
    }

    public async Task DeleteDowntimeAsync(Guid id)
    {
        try
        {
            var repo = _unitOfWork.Repository<AssetDowntime>();
            var downtime = await repo.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == _currentUserService.TenantId);

            if (downtime == null)
            {
                return;
            }

            await repo.DeleteAsync(downtime);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting downtime {DowntimeId}", id);
            throw;
        }
    }

    public async Task<AssetDowntimeDto?> GetDowntimeByIdAsync(Guid id)
    {
        var repo = _unitOfWork.Repository<AssetDowntime>();
        var downtime = await repo.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == _currentUserService.TenantId);

        if (downtime == null)
        {
            return null;
        }

        var asset = await _assetService.GetAssetByIdAsync(downtime.AssetId);
        return await MapToDtoAsync(downtime, asset?.Name, asset?.AssetNumber);
    }

    public async Task<IEnumerable<AssetDowntimeDto>> GetDowntimeByAssetAsync(Guid assetId)
    {
        var repo = _unitOfWork.Repository<AssetDowntime>();
        var records = await repo.FindAsync(d => d.AssetId == assetId && d.TenantId == _currentUserService.TenantId);
        var asset = await _assetService.GetAssetByIdAsync(assetId);

        var result = new List<AssetDowntimeDto>();
        var assetName = asset?.Name;
        var assetNumber = asset?.AssetNumber;

        foreach (var record in records.OrderByDescending(d => d.StartTime))
        {
            result.Add(await MapToDtoAsync(record, assetName, assetNumber));
        }

        return result;
    }

    public async Task<IEnumerable<AssetDowntimeDto>> GetActiveDowntimeAsync()
    {
        var repo = _unitOfWork.Repository<AssetDowntime>();
        var records = await repo.FindAsync(d => d.TenantId == _currentUserService.TenantId && (d.Status == "Active" || d.EndTime == null));

        var result = new List<AssetDowntimeDto>();
        foreach (var record in records.OrderByDescending(d => d.StartTime))
        {
            var asset = await _assetService.GetAssetByIdAsync(record.AssetId);
            result.Add(await MapToDtoAsync(record, asset?.Name, asset?.AssetNumber));
        }

        return result;
    }

    #endregion

    #region Analytics

    public async Task<DowntimeAnalyticsDto> GetDowntimeAnalyticsAsync(DateTime startDate, DateTime endDate, Guid? assetId = null)
    {
        var repo = _unitOfWork.Repository<AssetDowntime>();
        var records = await repo.FindAsync(d =>
            d.TenantId == _currentUserService.TenantId &&
            d.StartTime >= startDate &&
            d.StartTime <= endDate &&
            (!assetId.HasValue || d.AssetId == assetId.Value));

        var list = records.ToList();
        var analytics = new DowntimeAnalyticsDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalIncidents = list.Count
        };

        if (list.Count == 0)
        {
            return analytics;
        }

        foreach (var record in list)
        {
            var hours = record.DowntimeHours;
            if (!hours.HasValue && record.EndTime.HasValue)
            {
                hours = (record.EndTime.Value - record.StartTime).TotalHours;
            }

            if (!hours.HasValue)
            {
                continue;
            }

            analytics.TotalDowntimeHours += hours.Value;
            analytics.TotalCostImpact += record.EstimatedCostImpact;

            // By reason
            if (!string.IsNullOrWhiteSpace(record.Reason))
            {
                analytics.DowntimeByReason.TryGetValue(record.Reason, out var existing);
                analytics.DowntimeByReason[record.Reason] = existing + hours.Value;

                analytics.IncidentsByReason.TryGetValue(record.Reason, out var count);
                analytics.IncidentsByReason[record.Reason] = count + 1;
            }

            // By asset (using asset id as key; name will be filled separately if needed)
            var assetKey = record.AssetId.ToString();
            analytics.DowntimeByAsset.TryGetValue(assetKey, out var existingByAsset);
            analytics.DowntimeByAsset[assetKey] = existingByAsset + hours.Value;
        }

        analytics.AverageDowntimePerIncident = analytics.TotalIncidents > 0
            ? analytics.TotalDowntimeHours / analytics.TotalIncidents
            : 0;

        // Overall availability percentage is non-trivial to compute accurately; provide a simple approximation
        var totalPeriodHours = (endDate - startDate).TotalHours;
        analytics.OverallAvailabilityPercentage = totalPeriodHours > 0
            ? Math.Max(0, 100.0 - (analytics.TotalDowntimeHours / totalPeriodHours * 100.0))
            : 100.0;

        return analytics;
    }

    public async Task<AssetDowntimeDto?> GetActiveDowntimeByAssetAsync(Guid assetId)
    {
        var repo = _unitOfWork.Repository<AssetDowntime>();
        var record = (await repo.FindAsync(d =>
                d.TenantId == _currentUserService.TenantId &&
                d.AssetId == assetId &&
                (d.Status == "Active" || d.EndTime == null)))
            .OrderByDescending(d => d.StartTime)
            .FirstOrDefault();

        if (record == null)
        {
            return null;
        }

        var asset = await _assetService.GetAssetByIdAsync(assetId);
        return await MapToDtoAsync(record, asset?.Name, asset?.AssetNumber);
    }

    public async Task<double> GetAssetAvailabilityPercentageAsync(Guid assetId, DateTime startDate, DateTime endDate)
    {
        var analytics = await GetDowntimeAnalyticsAsync(startDate, endDate, assetId);
        return analytics.OverallAvailabilityPercentage;
    }

    public async Task<IEnumerable<AssetDowntimeReportDto>> GetDowntimeReportAsync(DateTime startDate, DateTime endDate)
    {
        var repo = _unitOfWork.Repository<AssetDowntime>();
        var records = await repo.FindAsync(d =>
            d.TenantId == _currentUserService.TenantId &&
            d.StartTime >= startDate &&
            d.StartTime <= endDate);

        var list = records.ToList();
        var groups = list.GroupBy(d => d.AssetId);
        var result = new List<AssetDowntimeReportDto>();

        foreach (var group in groups)
        {
            var assetId = group.Key;
            var asset = await _assetService.GetAssetByIdAsync(assetId);
            var assetName = asset?.Name ?? "Unknown";
            var assetNumber = asset?.AssetNumber ?? string.Empty;

            double totalHours = 0;
            decimal totalCost = 0;
            int incidentCount = 0;

            foreach (var record in group)
            {
                var hours = record.DowntimeHours;
                if (!hours.HasValue && record.EndTime.HasValue)
                {
                    hours = (record.EndTime.Value - record.StartTime).TotalHours;
                }

                if (!hours.HasValue)
                {
                    continue;
                }

                totalHours += hours.Value;
                totalCost += record.EstimatedCostImpact;
                incidentCount++;
            }

            if (incidentCount == 0)
            {
                continue;
            }

            var avgDuration = incidentCount > 0 ? totalHours / incidentCount : 0;

            result.Add(new AssetDowntimeReportDto
            {
                AssetId = assetId,
                AssetName = assetName,
                AssetNumber = assetNumber,
                TotalDowntimeHours = totalHours,
                TotalCostImpact = totalCost,
                IncidentCount = incidentCount,
                AverageIncidentDuration = avgDuration,
                // AvailabilityPercentage can be approximated per asset using the same period
                AvailabilityPercentage = await GetAssetAvailabilityPercentageAsync(assetId, startDate, endDate),
                MostCommonFailureReason = group
                    .Where(r => !string.IsNullOrWhiteSpace(r.Reason))
                    .GroupBy(r => r.Reason)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? string.Empty
            });
        }

        return result;
    }

    #endregion

    #region Helper Methods

    private static async Task<AssetDowntimeDto> MapToDtoAsync(AssetDowntime record, string? assetName, string? assetNumber)
    {
        // Compute downtime hours on the fly if not stored but we have EndTime
        double? hours = record.DowntimeHours;
        if (!hours.HasValue && record.EndTime.HasValue)
        {
            hours = (record.EndTime.Value - record.StartTime).TotalHours;
        }

        return new AssetDowntimeDto
        {
            Id = record.Id,
            AssetId = record.AssetId,
            WorkOrderId = record.WorkOrderId,
            StartTime = record.StartTime,
            EndTime = record.EndTime,
            DowntimeHours = hours,
            DowntimeType = record.Reason,
            Priority = string.Empty,
            Duration = (decimal)(hours ?? 0),
            RelatedWorkOrderId = record.WorkOrderId,
            AssetName = assetName ?? string.Empty,
            Reason = record.Reason,
            Description = record.Description,
            EstimatedCostImpact = record.EstimatedCostImpact,
            Status = record.Status,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }

    #endregion
}
