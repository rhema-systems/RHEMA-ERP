using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;

namespace ErpSystem.Core.Services.Maintenance;

public class AssetAdmissionService : IAssetAdmissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMaintenanceAssetService _assetService;
    private readonly IAssetDowntimeService _downtimeService;

    public AssetAdmissionService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IMaintenanceAssetService assetService,
        IAssetDowntimeService downtimeService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _assetService = assetService;
        _downtimeService = downtimeService;
    }

    public async Task<PagedResult<AssetAdmissionDto>> GetAdmissionsAsync(AdmissionQueryParameters query)
    {
        var repo = _unitOfWork.Repository<AssetAdmission>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;

        var admissions = await repo.FindAsync(
            a => a.TenantId == tenantId,
            a => a.AdmittedBy);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            admissions = admissions.Where(a => a.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.AdmissionType))
        {
            admissions = admissions.Where(a => a.AdmissionType == query.AdmissionType);
        }

        if (query.AssetId.HasValue)
        {
            admissions = admissions.Where(a => a.AssetId == query.AssetId.Value);
        }

        if (query.WorkOrderId.HasValue)
        {
            admissions = admissions.Where(a => a.WorkOrderId == query.WorkOrderId.Value);
        }

        if (query.JobCardId.HasValue)
        {
            admissions = admissions.Where(a => a.JobCardId == query.JobCardId.Value);
        }

        if (query.FromDate.HasValue)
        {
            admissions = admissions.Where(a => a.AdmissionDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            admissions = admissions.Where(a => a.AdmissionDate <= query.ToDate.Value);
        }

        var totalCount = admissions.Count();
        var items = admissions
            .OrderByDescending(a => a.AdmissionDate)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var assetIds = items.Select(a => a.AssetId).Distinct().ToList();
        var assets = await _assetService.GetAssetsByIdsAsync(assetIds);
        var assetMap = assets.ToDictionary(a => a.Id, a => a);

        var dtos = items.Select(a => MapToDto(a, assetMap.GetValueOrDefault(a.AssetId))).ToList();

        return new PagedResult<AssetAdmissionDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task<AssetAdmissionDto?> GetAdmissionByIdAsync(Guid id)
    {
        var repo = _unitOfWork.Repository<AssetAdmission>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var admission = await repo.FirstOrDefaultAsync(
            a => a.Id == id && a.TenantId == tenantId,
            a => a.AdmittedBy);
        if (admission == null)
        {
            return null;
        }

        var asset = await _assetService.GetAssetByIdAsync(admission.AssetId);
        return MapToDto(admission, asset);
    }

    public async Task<AssetAdmissionDto> CreateAdmissionAsync(CreateAssetAdmissionDto dto)
    {
        var asset = await _assetService.GetAssetByIdAsync(dto.AssetId)
                    ?? throw new KeyNotFoundException($"Asset {dto.AssetId} not found");

        var repo = _unitOfWork.Repository<AssetAdmission>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;

        var admission = new AssetAdmission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AdmissionNumber = $"ADM-{DateTime.UtcNow:yyyyMMddHHmmss}",
            AssetId = dto.AssetId,
            JobCardId = dto.JobCardId,
            WorkOrderId = dto.WorkOrderId,
            AdmissionDate = DateTime.UtcNow,
            AdmittedById = Guid.TryParse(_currentUserService.UserId, out var admittedById) ? admittedById : Guid.Empty,
            AdmissionType = dto.AdmissionType,
            AssetConditionOnAdmission = dto.AssetConditionOnAdmission,
            AdmissionNotes = dto.AdmissionNotes,
            ObservedProblems = dto.ObservedProblems,
            MileageReading = dto.MileageReading,
            HoursReading = dto.HoursReading,
            FuelLevel = dto.FuelLevel,
            AdmissionChecklist = dto.AdmissionChecklistJson,
            AdmissionLocation = dto.AdmissionLocation,
            BayOrStation = dto.BayOrStation,
            EstimatedCompletionDate = dto.EstimatedCompletionDate,
            EstimatedDischargeDate = dto.EstimatedDischargeDate,
            Status = "Active"
        };

        await repo.AddAsync(admission);
        await _unitOfWork.SaveChangesAsync();

        // Start downtime if asset is effectively out of service
        if (dto.AdmissionType is "Emergency" or "Breakdown")
        {
            await _downtimeService.StartDowntimeAsync(new CreateAssetDowntimeDto
            {
                AssetId = dto.AssetId,
                StartTime = DateTime.UtcNow,
                Reason = $"Admission: {admission.AdmissionNumber}",
                Description = dto.ObservedProblems ?? dto.AdmissionNotes,
                RelatedWorkOrderId = dto.WorkOrderId,
                DowntimeType = dto.AdmissionType
            });
        }

        return MapToDto(admission, asset);
    }

    public async Task<AssetAdmissionDto> UpdateAdmissionAsync(Guid id, UpdateAssetAdmissionDto dto)
    {
        var repo = _unitOfWork.Repository<AssetAdmission>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var admission = await repo.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId)
                       ?? throw new KeyNotFoundException($"Admission {id} not found");

        admission.AdmissionType = dto.AdmissionType ?? admission.AdmissionType;
        admission.AssetConditionOnAdmission = dto.AssetConditionOnAdmission ?? admission.AssetConditionOnAdmission;
        admission.AdmissionNotes = dto.AdmissionNotes ?? admission.AdmissionNotes;
        admission.ObservedProblems = dto.ObservedProblems ?? admission.ObservedProblems;
        admission.MileageReading = dto.MileageReading ?? admission.MileageReading;
        admission.HoursReading = dto.HoursReading ?? admission.HoursReading;
        admission.FuelLevel = dto.FuelLevel ?? admission.FuelLevel;
        admission.AdmissionChecklist = dto.AdmissionChecklistJson ?? admission.AdmissionChecklist;
        admission.AdmissionLocation = dto.AdmissionLocation ?? admission.AdmissionLocation;
        admission.BayOrStation = dto.BayOrStation ?? admission.BayOrStation;
        admission.EstimatedCompletionDate = dto.EstimatedCompletionDate ?? admission.EstimatedCompletionDate;
        admission.EstimatedDischargeDate = dto.EstimatedDischargeDate ?? admission.EstimatedDischargeDate;

        await repo.UpdateAsync(admission);
        await _unitOfWork.SaveChangesAsync();

        var asset = await _assetService.GetAssetByIdAsync(admission.AssetId);
        return MapToDto(admission, asset);
    }

    public async Task CancelAdmissionAsync(Guid id, string? reason = null)
    {
        var repo = _unitOfWork.Repository<AssetAdmission>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var admission = await repo.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId)
                       ?? throw new KeyNotFoundException($"Admission {id} not found");

        admission.Status = "Cancelled";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            admission.AdmissionNotes = string.IsNullOrWhiteSpace(admission.AdmissionNotes)
                ? reason
                : admission.AdmissionNotes + "\n" + reason;
        }

        await repo.UpdateAsync(admission);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<AssetAdmissionDto>> GetActiveAdmissionsAsync()
    {
        var repo = _unitOfWork.Repository<AssetAdmission>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var admissions = await repo.FindAsync(
            a => a.TenantId == tenantId && a.Status == "Active",
            a => a.AdmittedBy);

        var assetIds = admissions.Select(a => a.AssetId).Distinct().ToList();
        var assets = await _assetService.GetAssetsByIdsAsync(assetIds);
        var assetMap = assets.ToDictionary(a => a.Id, a => a);

        return admissions
            .OrderByDescending(a => a.AdmissionDate)
            .Select(a => MapToDto(a, assetMap.GetValueOrDefault(a.AssetId)))
            .ToList();
    }

    public async Task<IEnumerable<AssetAdmissionDto>> GetAdmissionsByAssetAsync(Guid assetId)
    {
        var repo = _unitOfWork.Repository<AssetAdmission>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var admissions = await repo.FindAsync(
            a => a.TenantId == tenantId && a.AssetId == assetId,
            a => a.AdmittedBy);
        var asset = await _assetService.GetAssetByIdAsync(assetId);
        return admissions
            .OrderByDescending(a => a.AdmissionDate)
            .Select(a => MapToDto(a, asset))
            .ToList();
    }

    public async Task<IEnumerable<AssetAdmissionDto>> GetAdmissionsByWorkOrderAsync(Guid workOrderId)
    {
        var repo = _unitOfWork.Repository<AssetAdmission>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var admissions = await repo.FindAsync(
            a => a.TenantId == tenantId && a.WorkOrderId == workOrderId,
            a => a.AdmittedBy);

        var assetIds = admissions.Select(a => a.AssetId).Distinct().ToList();
        var assets = await _assetService.GetAssetsByIdsAsync(assetIds);
        var assetMap = assets.ToDictionary(a => a.Id, a => a);

        return admissions
            .OrderByDescending(a => a.AdmissionDate)
            .Select(a => MapToDto(a, assetMap.GetValueOrDefault(a.AssetId)))
            .ToList();
    }

    public async Task<AdmissionStatsDto> GetAdmissionStatsAsync(AdmissionStatsQuery query)
    {
        var repo = _unitOfWork.Repository<AssetAdmission>();
        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var admissions = await repo.FindAsync(a => a.TenantId == tenantId);

        if (query.AssetId.HasValue)
        {
            admissions = admissions.Where(a => a.AssetId == query.AssetId.Value);
        }

        if (query.StartDate.HasValue)
        {
            admissions = admissions.Where(a => a.AdmissionDate >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            admissions = admissions.Where(a => a.AdmissionDate <= query.EndDate.Value);
        }

        var list = admissions.ToList();
        var stats = new AdmissionStatsDto
        {
            TotalActive = list.Count(a => a.Status == "Active"),
            TotalCompleted = list.Count(a => a.Status == "Completed")
        };

        var completed = list.Where(a => a.Status == "Completed" && a.DischargeId.HasValue).ToList();
        if (completed.Count > 0)
        {
            var totalDays = completed.Sum(a =>
            {
                var end = a.EstimatedDischargeDate ?? a.AdmissionDate;
                return (end - a.AdmissionDate).TotalDays;
            });
            stats.AverageStayDays = totalDays / completed.Count;
        }

        stats.ByCondition = list
            .GroupBy(a => a.AssetConditionOnAdmission ?? "Unknown")
            .Select(g => new AdmissionConditionStatDto
            {
                Condition = g.Key,
                Count = g.Count()
            })
            .ToList();

        stats.ByType = list
            .GroupBy(a => a.AdmissionType ?? "Unknown")
            .Select(g => new AdmissionTypeStatDto
            {
                Type = g.Key,
                Count = g.Count()
            })
            .ToList();

        return stats;
    }

    public async Task<DowntimeSummaryDto> GetAdmissionDowntimeReportAsync(DowntimeReportQuery query)
    {
        var from = query.FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var to = query.ToDate ?? DateTime.UtcNow;

        var reportData = await _downtimeService.GetDowntimeReportAsync(from, to);
        var report = reportData.ToList();

        if (query.AssetId.HasValue)
        {
            report = report.Where(r => r.AssetId == query.AssetId.Value).ToList();
        }

        var summary = new DowntimeSummaryDto();

        foreach (var item in report)
        {
            summary.TotalDowntimeHours += item.TotalDowntimeHours;
        }

        var count = report.Count;
        if (count > 0)
        {
            summary.AverageDowntimeHours = summary.TotalDowntimeHours / count;
        }

        summary.DowntimeByAsset = report.Select(r => new DowntimeByAssetDto
        {
            AssetId = r.AssetId,
            AssetName = r.AssetName,
            AssetNumber = r.AssetNumber,
            TotalDowntimeHours = r.TotalDowntimeHours,
            AverageDowntimeHours = r.AverageIncidentDuration,
            IncidentCount = r.IncidentCount
        }).ToList();

        return summary;
    }

    private static AssetAdmissionDto MapToDto(AssetAdmission admission, MaintenanceAssetDto? asset)
    {
        // Get the user name from the AdmittedBy navigation property if loaded
        var admittedByName = admission.AdmittedBy != null
            ? $"{admission.AdmittedBy.FirstName} {admission.AdmittedBy.LastName}".Trim()
            : string.Empty;

        return new AssetAdmissionDto
        {
            Id = admission.Id,
            AdmissionNumber = admission.AdmissionNumber,
            AssetId = admission.AssetId,
            AssetName = asset?.Name ?? string.Empty,
            AssetNumber = asset?.AssetNumber ?? string.Empty,
            JobCardId = admission.JobCardId,
            WorkOrderId = admission.WorkOrderId,
            AdmissionDate = admission.AdmissionDate,
            AdmittedById = admission.AdmittedById,
            AdmittedBy = admittedByName,
            AdmissionType = admission.AdmissionType,
            AssetConditionOnAdmission = admission.AssetConditionOnAdmission,
            AdmissionNotes = admission.AdmissionNotes,
            ObservedProblems = admission.ObservedProblems,
            MileageReading = admission.MileageReading,
            HoursReading = admission.HoursReading,
            FuelLevel = admission.FuelLevel,
            AdmissionChecklistJson = admission.AdmissionChecklist,
            AdmissionLocation = admission.AdmissionLocation,
            BayOrStation = admission.BayOrStation,
            EstimatedCompletionDate = admission.EstimatedCompletionDate,
            EstimatedDischargeDate = admission.EstimatedDischargeDate,
            Status = admission.Status,
            DischargeId = admission.DischargeId,
            CreatedAt = admission.CreatedAt
        };
    }
}

