using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetAssignmentService : IFleetAssignmentService
{
    private const string EntityType = "FleetVehicle";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAppEventBus _appEventBus;

    public FleetAssignmentService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider, IAppEventBus appEventBus)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _appEventBus = appEventBus;
    }

    public async Task<IReadOnlyList<FleetVehicleAssignmentDto>> GetAssignmentsAsync(Guid vehicleAssetId)
    {
        if (vehicleAssetId == Guid.Empty) return Array.Empty<FleetVehicleAssignmentDto>();

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetVehicleAssignment>();

        var items = await repo.GetQueryable(a => a.TenantId == tenantId && a.VehicleAssetId == vehicleAssetId && !a.IsDeleted)
            .Include(a => a.Employee)
            .OrderByDescending(a => a.AssignedFromUtc)
            .Select(a => new FleetVehicleAssignmentDto
            {
                Id = a.Id,
                VehicleAssetId = a.VehicleAssetId,
                EmployeeId = a.EmployeeId,
                EmployeeName = a.Employee.FirstName + " " + a.Employee.LastName,
                AssignedFromUtc = a.AssignedFromUtc,
                AssignedToUtc = a.AssignedToUtc,
                AssignmentType = a.AssignmentType,
                IsActive = a.IsActive,
                Notes = a.Notes
            })
            .ToListAsync();

        return items;
    }

    public async Task<FleetVehicleAssignmentDto?> GetCurrentAssignmentAsync(Guid vehicleAssetId)
    {
        if (vehicleAssetId == Guid.Empty) return null;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetVehicleAssignment>();

        var a = await repo.GetQueryable(x => x.TenantId == tenantId && x.VehicleAssetId == vehicleAssetId && x.IsActive && !x.IsDeleted)
            .Include(x => x.Employee)
            .OrderByDescending(x => x.AssignedFromUtc)
            .FirstOrDefaultAsync();

        if (a == null) return null;

        return new FleetVehicleAssignmentDto
        {
            Id = a.Id,
            VehicleAssetId = a.VehicleAssetId,
            EmployeeId = a.EmployeeId,
            EmployeeName = a.Employee.FirstName + " " + a.Employee.LastName,
            AssignedFromUtc = a.AssignedFromUtc,
            AssignedToUtc = a.AssignedToUtc,
            AssignmentType = a.AssignmentType,
            IsActive = a.IsActive,
            Notes = a.Notes
        };
    }

    public async Task<FleetVehicleAssignmentDto> AssignDriverAsync(AssignFleetDriverDto dto)
    {
        dto ??= new AssignFleetDriverDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        if (dto.EmployeeId == Guid.Empty) throw new ArgumentException("EmployeeId is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var vehicle = await _unitOfWork.Repository<MaintenanceAsset>()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == dto.VehicleAssetId && !a.IsDeleted, a => a.AssetCategory);

        if (vehicle == null) throw new ArgumentException("Vehicle not found.");
        if (!string.Equals(vehicle.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selected asset is not a vehicle.");

        var employee = await _unitOfWork.Repository<Employee>()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == dto.EmployeeId && !e.IsDeleted);

        if (employee == null) throw new ArgumentException("Employee not found.");

        // Block assignment if driver's license record exists and is expired.
        // Missing license records are allowed at assignment time (dispatch is still blocked by FleetTripService).
        var license = await _unitOfWork.Repository<EmployeeIdentificationCard>()
            .GetQueryable(c =>
                c.TenantId == tenantId &&
                c.EmployeeId == dto.EmployeeId &&
                !c.IsDeleted &&
                // [HR-MODULE-PORT] `EmployeeIdentificationCard` no longer has a free-text `DocumentType` string.
                // The HR module port replaced it with `IdentificationTypeId` + the `IdentificationType` lookup entity,
                // so driver-licence detection now matches on `IdentificationType.Name`. Behaviour is preserved as long
                // as the licence identification type is named with "driver" (e.g. "Driver's License"). If you referenced
                // a `DocumentType` string elsewhere, use the `IdentificationType` relationship instead.
                c.IdentificationType.Name.ToLower().Contains("driver"))
            .OrderByDescending(c => c.ExpiryDate)
            .FirstOrDefaultAsync();

        if (license?.ExpiryDate != null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            if (license.ExpiryDate.Value < today)
                throw new InvalidOperationException($"Cannot assign driver: driver's license for '{employee.FirstName} {employee.LastName}' expired on {license.ExpiryDate.Value:yyyy-MM-dd}.");
        }

        var repo = _unitOfWork.Repository<FleetVehicleAssignment>();

        // Vehicle must not currently be in use (shared lock with work orders and fleet trips).
        if (vehicle.Status != ErpSystem.Core.Enums.AssetStatus.Active)
            throw new InvalidOperationException($"Cannot assign driver: vehicle '{vehicle.Name}' ({vehicle.AssetNumber}) is currently {vehicle.Status}.");

        // Enforce: a driver can only have one active vehicle assignment at a time.
        // Do not auto-transfer implicitly; require explicitly ending the existing assignment first.
        var existingEmployeeActive = await repo.GetQueryable(a =>
                a.TenantId == tenantId &&
                a.EmployeeId == dto.EmployeeId &&
                a.IsActive &&
                !a.IsDeleted)
            .OrderByDescending(a => a.AssignedFromUtc)
            .FirstOrDefaultAsync();

        if (existingEmployeeActive != null && existingEmployeeActive.VehicleAssetId != dto.VehicleAssetId)
        {
            var existingVehicle = await _unitOfWork.Repository<MaintenanceAsset>()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == existingEmployeeActive.VehicleAssetId && !a.IsDeleted);

            var existingVehicleLabel = existingVehicle != null
                ? $"'{existingVehicle.Name}' ({existingVehicle.AssetNumber})"
                : existingEmployeeActive.VehicleAssetId.ToString();

            throw new InvalidOperationException(
                $"Cannot assign driver: '{employee.FirstName} {employee.LastName}' is already assigned to vehicle {existingVehicleLabel}. End the current assignment first.");
        }

        var existingActives = await repo.FindAsync(a =>
            a.TenantId == tenantId &&
            a.VehicleAssetId == dto.VehicleAssetId &&
            a.IsActive &&
            !a.IsDeleted);

        foreach (var existing in existingActives)
        {
            existing.IsActive = false;
            existing.AssignedToUtc ??= now;
            existing.UpdatedAt = now;
            existing.LastModifiedById = userId;
            await repo.UpdateAsync(existing);
        }

        var assignedFrom = (dto.AssignedFromUtc ?? now).ToUniversalTime();

        var entity = new FleetVehicleAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = dto.VehicleAssetId,
            EmployeeId = dto.EmployeeId,
            AssignedFromUtc = assignedFrom,
            AssignmentType = string.IsNullOrWhiteSpace(dto.AssignmentType) ? "Primary" : dto.AssignmentType.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            IsActive = true,
            CreatedAt = now,
            CreatedById = userId
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        // Publish event for admin-configurable notification topics (best-effort).
        try
        {
            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = EntityType,
                Activity = "DriverAssigned",
                Audience = "Internal",
                EntityId = vehicle.Id,
                TriggeredByUserId = userId,
                Data = new Dictionary<string, object>
                {
                    ["VehicleAssetId"] = vehicle.Id,
                    ["VehicleName"] = vehicle.Name,
                    ["VehicleAssetNumber"] = vehicle.AssetNumber ?? string.Empty,
                    ["VehicleLicensePlate"] = vehicle.LicensePlate ?? string.Empty,
                    ["EmployeeId"] = employee.Id,
                    ["EmployeeName"] = $"{employee.FirstName} {employee.LastName}",
                    ["AssignedFromUtc"] = assignedFrom.ToString("o"),
                    ["AssignedByUserId"] = userId
                }
            });
        }
        catch
        {
            // Best-effort.
        }

        return new FleetVehicleAssignmentDto
        {
            Id = entity.Id,
            VehicleAssetId = entity.VehicleAssetId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = employee.FirstName + " " + employee.LastName,
            AssignedFromUtc = entity.AssignedFromUtc,
            AssignedToUtc = entity.AssignedToUtc,
            AssignmentType = entity.AssignmentType,
            IsActive = entity.IsActive,
            Notes = entity.Notes
        };
    }

    public async Task<bool> EndAssignmentAsync(Guid assignmentId, EndFleetDriverAssignmentDto dto)
    {
        if (assignmentId == Guid.Empty) return false;
        dto ??= new EndFleetDriverAssignmentDto();

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;
        var now = DateTime.UtcNow;

        var repo = _unitOfWork.Repository<FleetVehicleAssignment>();
        var existing = await repo.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == assignmentId && !a.IsDeleted);
        if (existing == null) return false;

        existing.IsActive = false;
        existing.AssignedToUtc = (dto.AssignedToUtc ?? now).ToUniversalTime();
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            existing.Notes = dto.Notes.Trim();

        existing.UpdatedAt = now;
        existing.LastModifiedById = userId;

        await repo.UpdateAsync(existing);
        await _unitOfWork.SaveChangesAsync();

        // Publish event for admin-configurable notification topics (best-effort).
        try
        {
            var vehicle = await _unitOfWork.Repository<MaintenanceAsset>()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == existing.VehicleAssetId && !a.IsDeleted);

            var employee = await _unitOfWork.Repository<Employee>()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == existing.EmployeeId && !e.IsDeleted);

            if (vehicle != null && employee != null)
            {
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = tenantId,
                    EntityType = EntityType,
                    Activity = "DriverUnassigned",
                    Audience = "Internal",
                    EntityId = vehicle.Id,
                    TriggeredByUserId = userId,
                    Data = new Dictionary<string, object>
                    {
                        ["VehicleAssetId"] = vehicle.Id,
                        ["VehicleName"] = vehicle.Name,
                        ["VehicleAssetNumber"] = vehicle.AssetNumber ?? string.Empty,
                        ["VehicleLicensePlate"] = vehicle.LicensePlate ?? string.Empty,
                        ["EmployeeId"] = employee.Id,
                        ["EmployeeName"] = $"{employee.FirstName} {employee.LastName}",
                        ["UnassignedAtUtc"] = existing.AssignedToUtc?.ToString("o") ?? DateTime.UtcNow.ToString("o"),
                        ["UnassignedByUserId"] = userId
                    }
                });
            }
        }
        catch
        {
            // Best-effort.
        }

        return true;
    }
}

