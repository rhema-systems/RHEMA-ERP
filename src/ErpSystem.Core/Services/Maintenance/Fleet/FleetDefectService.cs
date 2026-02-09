using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetDefectService : IFleetDefectService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkOrderService _workOrderService;
    private readonly ILogger<FleetDefectService> _logger;

    public FleetDefectService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkOrderService workOrderService,
        ILogger<FleetDefectService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workOrderService = workOrderService;
        _logger = logger;
    }

    public async Task<PagedResult<FleetDefectDto>> GetDefectsPagedAsync(int page, int pageSize, Guid? vehicleAssetId = null, string? status = null, string? searchTerm = null)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetDefect>();

        IQueryable<FleetDefect> q = repo.GetQueryable(d => d.TenantId == tenantId && !d.IsDeleted)
            .Include(d => d.VehicleAsset)
            .Include(d => d.ReportedByEmployee);

        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
            q = q.Where(d => d.VehicleAssetId == vehicleAssetId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(d => d.Status == status);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            q = q.Where(d => d.Title.Contains(term) || (d.Description != null && d.Description.Contains(term)));
        }

        var total = await q.CountAsync();

        var items = await q.OrderByDescending(d => d.ReportedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new FleetDefectDto
            {
                Id = d.Id,
                VehicleAssetId = d.VehicleAssetId,
                VehicleName = d.VehicleAsset.Name,
                FleetTripId = d.FleetTripId,
                FleetTripInspectionId = d.FleetTripInspectionId,
                Title = d.Title,
                Description = d.Description,
                Severity = d.Severity,
                Status = d.Status,
                ReportedAtUtc = d.ReportedAtUtc,
                ReportedByEmployeeId = d.ReportedByEmployeeId,
                ReportedByEmployeeName = d.ReportedByEmployee != null ? (d.ReportedByEmployee.FirstName + " " + d.ReportedByEmployee.LastName) : null,
                WorkOrderId = d.WorkOrderId
            })
            .ToListAsync();

        return new PagedResult<FleetDefectDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FleetDefectDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        var tenantId = _currentUserProvider.TenantId;

        var repo = _unitOfWork.Repository<FleetDefect>();
        var d = await repo.GetQueryable(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted)
            .Include(x => x.VehicleAsset)
            .Include(x => x.ReportedByEmployee)
            .FirstOrDefaultAsync();

        if (d == null) return null;

        return new FleetDefectDto
        {
            Id = d.Id,
            VehicleAssetId = d.VehicleAssetId,
            VehicleName = d.VehicleAsset.Name,
            FleetTripId = d.FleetTripId,
            FleetTripInspectionId = d.FleetTripInspectionId,
            Title = d.Title,
            Description = d.Description,
            Severity = d.Severity,
            Status = d.Status,
            ReportedAtUtc = d.ReportedAtUtc,
            ReportedByEmployeeId = d.ReportedByEmployeeId,
            ReportedByEmployeeName = d.ReportedByEmployee != null ? (d.ReportedByEmployee.FirstName + " " + d.ReportedByEmployee.LastName) : null,
            WorkOrderId = d.WorkOrderId
        };
    }

    public async Task<FleetDefectDto> CreateAsync(CreateFleetDefectDto dto)
    {
        dto ??= new CreateFleetDefectDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new ArgumentException("Title is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var vehicle = await _unitOfWork.Repository<MaintenanceAsset>()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == dto.VehicleAssetId && !a.IsDeleted, a => a.AssetCategory);

        if (vehicle == null) throw new ArgumentException("Vehicle not found.");
        if (!string.Equals(vehicle.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selected asset is not a vehicle.");

        var entity = new FleetDefect
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = dto.VehicleAssetId,
            FleetTripId = dto.FleetTripId,
            FleetTripInspectionId = dto.FleetTripInspectionId,
            Title = dto.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            Severity = string.IsNullOrWhiteSpace(dto.Severity) ? "Medium" : dto.Severity.Trim(),
            Status = "Open",
            ReportedAtUtc = now,
            ReportedByEmployeeId = null,
            CreatedAt = now,
            CreatedById = userId
        };

        await _unitOfWork.Repository<FleetDefect>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetDefectDto> UpdateStatusAsync(Guid id, UpdateFleetDefectStatusDto dto)
    {
        if (id == Guid.Empty) throw new ArgumentException("DefectId is required.");
        dto ??= new UpdateFleetDefectStatusDto();

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetDefect>();
        var entity = await repo.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == id && !d.IsDeleted)
            ?? throw new ArgumentException("Defect not found.");

        entity.Status = dto.Status.Trim();
        entity.UpdatedAt = now;
        entity.LastModifiedById = userId;

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            entity.AdditionalData = string.IsNullOrWhiteSpace(entity.AdditionalData)
                ? $"{{\"Notes\":\"{dto.Notes.Trim()}\"}}"
                : entity.AdditionalData;
        }

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<Guid> CreateWorkOrderAsync(CreateWorkOrderFromFleetDefectDto dto)
    {
        dto ??= new CreateWorkOrderFromFleetDefectDto();
        if (dto.DefectId == Guid.Empty) throw new ArgumentException("DefectId is required.");

        var tenantId = _currentUserProvider.TenantId;

        var defectRepo = _unitOfWork.Repository<FleetDefect>();
        var defect = await defectRepo.GetQueryable(d => d.TenantId == tenantId && d.Id == dto.DefectId && !d.IsDeleted)
            .Include(d => d.VehicleAsset)
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException("Defect not found.");

        if (defect.WorkOrderId.HasValue) return defect.WorkOrderId.Value;

        var create = new CreateWorkOrderDto
        {
            Title = string.IsNullOrWhiteSpace(dto.TitleOverride) ? defect.Title : dto.TitleOverride.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.DescriptionOverride) ? defect.Description : dto.DescriptionOverride.Trim(),
            AssetId = defect.VehicleAssetId,
            WorkOrderTypeId = dto.WorkOrderTypeId,
            MaintenanceTypeId = dto.MaintenanceTypeId,
            PriorityLevelId = dto.PriorityLevelId,
            MaintenanceLocation = "Internal",
            RequestedStartDate = DateTime.UtcNow,
            RequestedCompletionDate = DateTime.UtcNow.AddDays(1)
        };

        WorkOrderDto created;
        try
        {
            created = await _workOrderService.CreateWorkOrderAsync(create);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create work order from fleet defect {DefectId}", defect.Id);
            throw;
        }

        defect.WorkOrderId = created.Id;
        defect.Status = "InProgress";
        defect.UpdatedAt = DateTime.UtcNow;
        defect.LastModifiedById = _currentUserProvider.UserId;
        await defectRepo.UpdateAsync(defect);
        await _unitOfWork.SaveChangesAsync();

        return created.Id;
    }
}
