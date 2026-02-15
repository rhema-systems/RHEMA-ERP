using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetIncidentService : IFleetIncidentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkOrderService _workOrderService;
    private readonly ILogger<FleetIncidentService> _logger;

    public FleetIncidentService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkOrderService workOrderService,
        ILogger<FleetIncidentService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workOrderService = workOrderService;
        _logger = logger;
    }

    public async Task<PagedResult<FleetIncidentDto>> GetPagedAsync(int page, int pageSize, Guid? vehicleAssetId = null, string? status = null, string? searchTerm = null)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetIncident>();

        IQueryable<FleetIncident> q = repo.GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted)
            .Include(x => x.VehicleAsset)
            .Include(x => x.DriverEmployee);

        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
            q = q.Where(x => x.VehicleAssetId == vehicleAssetId.Value);

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            q = q.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            q = q.Where(x =>
                x.Title.Contains(term) ||
                (x.Description != null && x.Description.Contains(term)) ||
                (x.Location != null && x.Location.Contains(term)) ||
                (x.ClaimNumber != null && x.ClaimNumber.Contains(term)));
        }

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FleetIncidentDto
            {
                Id = x.Id,
                VehicleAssetId = x.VehicleAssetId,
                VehicleName = x.VehicleAsset.Name,
                FleetTripId = x.FleetTripId,
                DriverEmployeeId = x.DriverEmployeeId,
                DriverEmployeeName = x.DriverEmployee != null ? (x.DriverEmployee.FirstName + " " + x.DriverEmployee.LastName) : null,
                OccurredAtUtc = x.OccurredAtUtc,
                IncidentType = x.IncidentType,
                Title = x.Title,
                Description = x.Description,
                Location = x.Location,
                Severity = x.Severity,
                Status = x.Status,
                DamageAssessment = x.DamageAssessment,
                EstimatedRepairCost = x.EstimatedRepairCost,
                ActualRepairCost = x.ActualRepairCost,
                CurrencyCode = x.CurrencyCode,
                InsuranceCompany = x.InsuranceCompany,
                PolicyNumber = x.PolicyNumber,
                ClaimNumber = x.ClaimNumber,
                ClaimStatus = x.ClaimStatus,
                ClaimAmount = x.ClaimAmount,
                ClaimSubmittedAtUtc = x.ClaimSubmittedAtUtc,
                ClaimSettledAtUtc = x.ClaimSettledAtUtc,
                WorkOrderId = x.WorkOrderId,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<FleetIncidentDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FleetIncidentDto?> GetByIdAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        var tenantId = _currentUserProvider.TenantId;

        var repo = _unitOfWork.Repository<FleetIncident>();
        var x = await repo.GetQueryable(i => i.TenantId == tenantId && i.Id == id && !i.IsDeleted)
            .Include(i => i.VehicleAsset)
            .Include(i => i.DriverEmployee)
            .FirstOrDefaultAsync();

        if (x == null) return null;

        return new FleetIncidentDto
        {
            Id = x.Id,
            VehicleAssetId = x.VehicleAssetId,
            VehicleName = x.VehicleAsset.Name,
            FleetTripId = x.FleetTripId,
            DriverEmployeeId = x.DriverEmployeeId,
            DriverEmployeeName = x.DriverEmployee != null ? (x.DriverEmployee.FirstName + " " + x.DriverEmployee.LastName) : null,
            OccurredAtUtc = x.OccurredAtUtc,
            IncidentType = x.IncidentType,
            Title = x.Title,
            Description = x.Description,
            Location = x.Location,
            Severity = x.Severity,
            Status = x.Status,
            DamageAssessment = x.DamageAssessment,
            EstimatedRepairCost = x.EstimatedRepairCost,
            ActualRepairCost = x.ActualRepairCost,
            CurrencyCode = x.CurrencyCode,
            InsuranceCompany = x.InsuranceCompany,
            PolicyNumber = x.PolicyNumber,
            ClaimNumber = x.ClaimNumber,
            ClaimStatus = x.ClaimStatus,
            ClaimAmount = x.ClaimAmount,
            ClaimSubmittedAtUtc = x.ClaimSubmittedAtUtc,
            ClaimSettledAtUtc = x.ClaimSettledAtUtc,
            WorkOrderId = x.WorkOrderId,
            CreatedAt = x.CreatedAt
        };
    }

    public async Task<FleetIncidentDto> CreateAsync(CreateFleetIncidentDto dto)
    {
        dto ??= new CreateFleetIncidentDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new ArgumentException("Title is required.");
        if (string.IsNullOrWhiteSpace(dto.IncidentType)) dto.IncidentType = "Incident";
        if (string.IsNullOrWhiteSpace(dto.Status)) dto.Status = "Open";
        if (string.IsNullOrWhiteSpace(dto.Severity)) dto.Severity = "Medium";

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var vehicleRepo = _unitOfWork.Repository<MaintenanceAsset>();
        var vehicle = await vehicleRepo.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == dto.VehicleAssetId && !a.IsDeleted, a => a.AssetCategory);
        if (vehicle == null) throw new ArgumentException("Vehicle not found.");
        if (!string.Equals(vehicle.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selected asset is not a vehicle.");

        var entity = new FleetIncident
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = dto.VehicleAssetId,
            FleetTripId = dto.FleetTripId,
            DriverEmployeeId = dto.DriverEmployeeId,
            OccurredAtUtc = dto.OccurredAtUtc ?? now,
            IncidentType = dto.IncidentType.Trim(),
            Title = dto.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            Location = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim(),
            Severity = dto.Severity.Trim(),
            Status = dto.Status.Trim(),
            DamageAssessment = string.IsNullOrWhiteSpace(dto.DamageAssessment) ? null : dto.DamageAssessment.Trim(),
            EstimatedRepairCost = dto.EstimatedRepairCost,
            ActualRepairCost = dto.ActualRepairCost,
            CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? null : dto.CurrencyCode.Trim(),
            InsuranceCompany = string.IsNullOrWhiteSpace(dto.InsuranceCompany) ? null : dto.InsuranceCompany.Trim(),
            PolicyNumber = string.IsNullOrWhiteSpace(dto.PolicyNumber) ? null : dto.PolicyNumber.Trim(),
            ClaimNumber = string.IsNullOrWhiteSpace(dto.ClaimNumber) ? null : dto.ClaimNumber.Trim(),
            ClaimStatus = string.IsNullOrWhiteSpace(dto.ClaimStatus) ? null : dto.ClaimStatus.Trim(),
            ClaimAmount = dto.ClaimAmount,
            ClaimSubmittedAtUtc = dto.ClaimSubmittedAtUtc,
            ClaimSettledAtUtc = dto.ClaimSettledAtUtc,
            CreatedAt = now,
            CreatedById = userId
        };

        await _unitOfWork.Repository<FleetIncident>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        await UpsertIncidentCostEntryAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<FleetIncidentDto> UpdateAsync(Guid id, UpdateFleetIncidentDto dto)
    {
        if (id == Guid.Empty) throw new ArgumentException("IncidentId is required.");
        dto ??= new UpdateFleetIncidentDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var vehicleRepo = _unitOfWork.Repository<MaintenanceAsset>();
        var vehicle = await vehicleRepo.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == dto.VehicleAssetId && !a.IsDeleted, a => a.AssetCategory);
        if (vehicle == null) throw new ArgumentException("Vehicle not found.");
        if (!string.Equals(vehicle.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selected asset is not a vehicle.");

        var repo = _unitOfWork.Repository<FleetIncident>();
        var entity = await repo.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted)
            ?? throw new ArgumentException("Incident not found.");

        entity.VehicleAssetId = dto.VehicleAssetId;
        entity.FleetTripId = dto.FleetTripId;
        entity.DriverEmployeeId = dto.DriverEmployeeId;
        entity.OccurredAtUtc = dto.OccurredAtUtc ?? entity.OccurredAtUtc;
        entity.IncidentType = string.IsNullOrWhiteSpace(dto.IncidentType) ? entity.IncidentType : dto.IncidentType.Trim();
        entity.Title = string.IsNullOrWhiteSpace(dto.Title) ? entity.Title : dto.Title.Trim();
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.Location = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim();
        entity.Severity = string.IsNullOrWhiteSpace(dto.Severity) ? entity.Severity : dto.Severity.Trim();
        entity.Status = string.IsNullOrWhiteSpace(dto.Status) ? entity.Status : dto.Status.Trim();
        entity.DamageAssessment = string.IsNullOrWhiteSpace(dto.DamageAssessment) ? null : dto.DamageAssessment.Trim();
        entity.EstimatedRepairCost = dto.EstimatedRepairCost;
        entity.ActualRepairCost = dto.ActualRepairCost;
        entity.CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? null : dto.CurrencyCode.Trim();

        entity.InsuranceCompany = string.IsNullOrWhiteSpace(dto.InsuranceCompany) ? null : dto.InsuranceCompany.Trim();
        entity.PolicyNumber = string.IsNullOrWhiteSpace(dto.PolicyNumber) ? null : dto.PolicyNumber.Trim();
        entity.ClaimNumber = string.IsNullOrWhiteSpace(dto.ClaimNumber) ? null : dto.ClaimNumber.Trim();
        entity.ClaimStatus = string.IsNullOrWhiteSpace(dto.ClaimStatus) ? null : dto.ClaimStatus.Trim();
        entity.ClaimAmount = dto.ClaimAmount;
        entity.ClaimSubmittedAtUtc = dto.ClaimSubmittedAtUtc;
        entity.ClaimSettledAtUtc = dto.ClaimSettledAtUtc;

        entity.UpdatedAt = now;
        entity.LastModifiedById = userId;

        await repo.UpdateAsync(entity);
        await UpsertIncidentCostEntryAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public async Task<Guid> CreateWorkOrderAsync(CreateWorkOrderFromFleetIncidentDto dto)
    {
        dto ??= new CreateWorkOrderFromFleetIncidentDto();
        if (dto.IncidentId == Guid.Empty) throw new ArgumentException("IncidentId is required.");

        var tenantId = _currentUserProvider.TenantId;
        var incidentRepo = _unitOfWork.Repository<FleetIncident>();

        var incident = await incidentRepo.GetQueryable(x => x.TenantId == tenantId && x.Id == dto.IncidentId && !x.IsDeleted)
            .Include(x => x.VehicleAsset)
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException("Incident not found.");

        if (incident.WorkOrderId.HasValue) return incident.WorkOrderId.Value;

        var billingType = "Repairs";

        var create = new CreateWorkOrderDto
        {
            Title = string.IsNullOrWhiteSpace(dto.TitleOverride) ? incident.Title : dto.TitleOverride.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.DescriptionOverride) ? incident.Description : dto.DescriptionOverride.Trim(),
            AssetId = incident.VehicleAssetId,
            WorkOrderTypeId = dto.WorkOrderTypeId,
            MaintenanceTypeId = dto.MaintenanceTypeId,
            PriorityLevelId = dto.PriorityLevelId,
            MaintenanceLocation = "Internal",
            BillingType = billingType,
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
            _logger.LogError(ex, "Failed to create work order from fleet incident {IncidentId}", incident.Id);
            throw;
        }

        incident.WorkOrderId = created.Id;
        incident.Status = "InProgress";
        incident.UpdatedAt = DateTime.UtcNow;
        incident.LastModifiedById = _currentUserProvider.UserId;
        await incidentRepo.UpdateAsync(incident);

        await UpsertIncidentCostEntryAsync(incident);
        await _unitOfWork.SaveChangesAsync();

        return created.Id;
    }

    private async Task UpsertIncidentCostEntryAsync(FleetIncident incident)
    {
        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetCostEntry>();
        var existing = await repo.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId &&
            x.FleetIncidentId == incident.Id &&
            x.CostType == "Incident" &&
            !x.IsDeleted);

        var amount = incident.ActualRepairCost.GetValueOrDefault(0m);
        if (amount <= 0m)
        {
            if (existing != null)
            {
                existing.IsDeleted = true;
                existing.DeletedAt = now;
                existing.DeletedBy = userId.ToString();
                await repo.UpdateAsync(existing);
            }
            return;
        }

        var notes = $"Incident: {incident.Title}";
        if (existing == null)
        {
            var entity = new FleetCostEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                VehicleAssetId = incident.VehicleAssetId,
                FleetIncidentId = incident.Id,
                WorkOrderId = incident.WorkOrderId,
                CostDateUtc = incident.OccurredAtUtc,
                CostType = "Incident",
                Source = "Incident",
                Amount = amount,
                CurrencyCode = incident.CurrencyCode,
                Notes = notes,
                CreatedAt = now,
                CreatedById = userId
            };
            await repo.AddAsync(entity);
            return;
        }

        existing.VehicleAssetId = incident.VehicleAssetId;
        existing.WorkOrderId = incident.WorkOrderId;
        existing.CostDateUtc = incident.OccurredAtUtc;
        existing.Amount = amount;
        existing.CurrencyCode = incident.CurrencyCode;
        existing.Notes = notes;
        existing.UpdatedAt = now;
        existing.LastModifiedById = userId;
        await repo.UpdateAsync(existing);
    }
}
