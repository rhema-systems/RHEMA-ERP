using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public class FleetTripService : IFleetTripService
{
    private const string EntityType = "FleetTrip";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IFleetComplianceService _fleetComplianceService;
    private readonly IMaintenanceAssetService _maintenanceAssetService;
    private readonly IAssetUsageTrackingService _assetUsageTrackingService;
    private readonly IAppEventBus _appEventBus;

    public FleetTripService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IFleetComplianceService fleetComplianceService,
        IMaintenanceAssetService maintenanceAssetService,
        IAssetUsageTrackingService assetUsageTrackingService,
        IAppEventBus appEventBus)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _fleetComplianceService = fleetComplianceService;
        _maintenanceAssetService = maintenanceAssetService;
        _assetUsageTrackingService = assetUsageTrackingService;
        _appEventBus = appEventBus;
    }

    public async Task<PagedResult<FleetTripDto>> GetTripsPagedAsync(
        int page,
        int pageSize,
        string? searchTerm = null,
        string? status = null,
        Guid? vehicleAssetId = null)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var repo = _unitOfWork.Repository<FleetTrip>();

        IQueryable<FleetTrip> q = repo.GetQueryable(t => t.TenantId == tenantId)
            .Include(t => t.VehicleAsset)
            .Include(t => t.DriverEmployee);

        if (vehicleAssetId.HasValue && vehicleAssetId.Value != Guid.Empty)
        {
            q = q.Where(t => t.VehicleAssetId == vehicleAssetId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            q = q.Where(t => t.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            q = q.Where(t =>
                (t.Purpose != null && t.Purpose.Contains(term)) ||
                (t.Origin != null && t.Origin.Contains(term)) ||
                (t.Destination != null && t.Destination.Contains(term)) ||
                (t.VehicleAsset != null && (t.VehicleAsset.Name.Contains(term) || t.VehicleAsset.AssetNumber.Contains(term))) ||
                (t.VehicleAsset != null && t.VehicleAsset.LicensePlate != null && t.VehicleAsset.LicensePlate.Contains(term)));
        }

        var total = await q.CountAsync();
        var trips = await q
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = trips.Select(Map).ToList();

        return new PagedResult<FleetTripDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FleetTripDto?> GetTripByIdAsync(Guid tripId)
    {
        if (tripId == Guid.Empty) return null;
        var tenantId = _currentUserProvider.TenantId;

        var trip = await _unitOfWork.Repository<FleetTrip>()
            .FirstOrDefaultAsync(t => t.Id == tripId && t.TenantId == tenantId, t => t.VehicleAsset, t => t.DriverEmployee);

        return trip == null ? null : Map(trip);
    }

    public async Task<FleetTripDto> CreateTripAsync(CreateFleetTripDto dto)
    {
        dto ??= new CreateFleetTripDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var vehicle = await RequireVehicleAsync(dto.VehicleAssetId);
        var driver = dto.DriverEmployeeId.HasValue && dto.DriverEmployeeId.Value != Guid.Empty
            ? await RequireEmployeeAsync(dto.DriverEmployeeId.Value)
            : null;

        var trip = new FleetTrip
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VehicleAssetId = vehicle.Id,
            RequestedByUserId = userId,
            DriverEmployeeId = driver?.Id,
            Purpose = string.IsNullOrWhiteSpace(dto.Purpose) ? null : dto.Purpose.Trim(),
            Origin = string.IsNullOrWhiteSpace(dto.Origin) ? null : dto.Origin.Trim(),
            Destination = string.IsNullOrWhiteSpace(dto.Destination) ? null : dto.Destination.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            PlannedStartAt = dto.PlannedStartAt,
            PlannedEndAt = dto.PlannedEndAt,
            Status = FleetTripStatuses.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedById = userId
        };

        await _unitOfWork.Repository<FleetTrip>().AddAsync(trip);
        await _unitOfWork.SaveChangesAsync();

        await _appEventBus.PublishAsync(new EntityActivityEvent
        {
            TenantId = tenantId,
            EntityType = EntityType,
            Activity = "TripRequested",
            Audience = "Internal",
            EntityId = trip.Id,
            TriggeredByUserId = userId,
            Data = new Dictionary<string, object>
            {
                ["VehicleAssetId"] = trip.VehicleAssetId,
                ["VehicleName"] = vehicle.Name,
                ["VehicleAssetNumber"] = vehicle.AssetNumber,
                ["VehicleLicensePlate"] = vehicle.LicensePlate ?? string.Empty,
                ["RequestedByUserId"] = trip.RequestedByUserId
            }
        });

        return (await GetTripByIdAsync(trip.Id))!;
    }

    public async Task<FleetTripDto> UpdateTripAsync(Guid tripId, UpdateFleetTripDto dto)
    {
        if (tripId == Guid.Empty) throw new ArgumentException("TripId is required.");
        dto ??= new UpdateFleetTripDto();

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var repo = _unitOfWork.Repository<FleetTrip>();
        var trip = await repo.FirstOrDefaultAsync(t => t.Id == tripId && t.TenantId == tenantId)
            ?? throw new ArgumentException($"Trip with ID {tripId} not found.");

        if (trip.Status != FleetTripStatuses.Draft && trip.Status != FleetTripStatuses.Rejected)
            throw new InvalidOperationException("Only Draft or Rejected trips can be edited.");

        var vehicle = await RequireVehicleAsync(dto.VehicleAssetId);
        var driver = dto.DriverEmployeeId.HasValue && dto.DriverEmployeeId.Value != Guid.Empty
            ? await RequireEmployeeAsync(dto.DriverEmployeeId.Value)
            : null;

        trip.VehicleAssetId = vehicle.Id;
        trip.DriverEmployeeId = driver?.Id;
        trip.Purpose = string.IsNullOrWhiteSpace(dto.Purpose) ? null : dto.Purpose.Trim();
        trip.Origin = string.IsNullOrWhiteSpace(dto.Origin) ? null : dto.Origin.Trim();
        trip.Destination = string.IsNullOrWhiteSpace(dto.Destination) ? null : dto.Destination.Trim();
        trip.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        trip.PlannedStartAt = dto.PlannedStartAt;
        trip.PlannedEndAt = dto.PlannedEndAt;

        if (trip.Status == FleetTripStatuses.Rejected)
        {
            // Allow resubmission after edits.
            trip.Status = FleetTripStatuses.Draft;
            trip.RejectedAt = null;
            trip.RejectedByUserId = null;
            trip.RejectionReason = null;
        }

        trip.UpdatedAt = DateTime.UtcNow;
        trip.LastModifiedById = userId;

        await repo.UpdateAsync(trip);
        await _unitOfWork.SaveChangesAsync();

        return (await GetTripByIdAsync(trip.Id))!;
    }

    public async Task<bool> SubmitForApprovalAsync(Guid tripId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var repo = _unitOfWork.Repository<FleetTrip>();
        var trip = await repo.FirstOrDefaultAsync(t => t.Id == tripId && t.TenantId == tenantId)
            ?? throw new ArgumentException($"Trip {tripId} not found");

        if (trip.Status != FleetTripStatuses.Draft)
            throw new InvalidOperationException("Trip must be in Draft status");

        var workflowResult = await _workflowIntegrationService.SubmitAsync(EntityType, tripId);
        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start workflow");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplySubmitOutcome(trip, workflowResult.Outcome, userId);

        trip.UpdatedAt = DateTime.UtcNow;
        trip.LastModifiedById = userId;
        await repo.UpdateAsync(trip);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ApproveAsync(Guid tripId, string? comments = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var repo = _unitOfWork.Repository<FleetTrip>();
        var trip = await repo.FirstOrDefaultAsync(t => t.Id == tripId && t.TenantId == tenantId)
            ?? throw new ArgumentException($"Trip {tripId} not found");

        if (trip.Status != FleetTripStatuses.Submitted)
            throw new InvalidOperationException("Trip must be in Submitted status");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(EntityType, tripId, userId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            EntityType,
            tripId,
            userId,
            "Approve",
            comments);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process approval");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(trip, workflowResult.Outcome, userId);

        trip.UpdatedAt = DateTime.UtcNow;
        trip.LastModifiedById = userId;
        await repo.UpdateAsync(trip);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RejectAsync(Guid tripId, string reason, string? comments = null)
    {
        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var repo = _unitOfWork.Repository<FleetTrip>();
        var trip = await repo.FirstOrDefaultAsync(t => t.Id == tripId && t.TenantId == tenantId)
            ?? throw new ArgumentException($"Trip {tripId} not found");

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(EntityType, tripId, userId);
        if (!canApprove)
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");

        var rejectionText = !string.IsNullOrWhiteSpace(comments) ? comments : reason;
        if (string.IsNullOrWhiteSpace(rejectionText))
            rejectionText = "Rejected";

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
            EntityType,
            tripId,
            userId,
            "Reject",
            rejectionText);

        if (!workflowResult.ExecutionResult.Success)
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process rejection");

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(EntityType);
        adapter.ApplyApprovalOutcome(trip, workflowResult.Outcome, userId, rejectionText);

        trip.UpdatedAt = DateTime.UtcNow;
        trip.LastModifiedById = userId;
        await repo.UpdateAsync(trip);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<FleetTripDto> DispatchAsync(Guid tripId, DispatchFleetTripDto dto)
    {
        if (tripId == Guid.Empty) throw new ArgumentException("TripId is required.");
        dto ??= new DispatchFleetTripDto();

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var repo = _unitOfWork.Repository<FleetTrip>();
        var trip = await repo.FirstOrDefaultAsync(t => t.Id == tripId && t.TenantId == tenantId)
            ?? throw new ArgumentException($"Trip {tripId} not found");

        if (trip.Status != FleetTripStatuses.Approved)
            throw new InvalidOperationException("Trip must be Approved before dispatch.");

        var blocking = await _fleetComplianceService.GetDispatchBlockingItemsAsync(trip.VehicleAssetId, DateTime.UtcNow);
        if (blocking.Count > 0)
        {
            var first = blocking.First();
            throw new InvalidOperationException(
                $"Dispatch blocked: '{first.ComplianceType}' is {(first.ExpiryDate.Date < DateTime.UtcNow.Date ? "overdue" : "due soon")} (expires {first.ExpiryDate:yyyy-MM-dd}).");
        }

        // Default driver from current assignment if not explicitly set on trip.
        if (!trip.DriverEmployeeId.HasValue || trip.DriverEmployeeId.Value == Guid.Empty)
        {
            var assignment = await _unitOfWork.Repository<FleetVehicleAssignment>()
                .GetQueryable(a => a.TenantId == tenantId && a.VehicleAssetId == trip.VehicleAssetId && a.IsActive && !a.IsDeleted)
                .OrderByDescending(a => a.AssignedFromUtc)
                .FirstOrDefaultAsync();

            if (assignment != null)
            {
                trip.DriverEmployeeId = assignment.EmployeeId;
            }
        }

        // Driver license expiry blocking (Driver's License in HR identification cards).
        if (trip.DriverEmployeeId.HasValue && trip.DriverEmployeeId.Value != Guid.Empty)
        {
            var driverId = trip.DriverEmployeeId.Value;
            var driver = await _unitOfWork.Repository<Employee>()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == driverId && !e.IsDeleted);

            var license = await _unitOfWork.Repository<EmployeeIdentificationCard>()
                .GetQueryable(c =>
                    c.TenantId == tenantId &&
                    c.EmployeeId == driverId &&
                    !c.IsDeleted &&
                    c.DocumentType.ToLower().Contains("driver"))
                .OrderByDescending(c => c.ExpiryDate)
                .FirstOrDefaultAsync();

            if (license?.ExpiryDate == null)
            {
                var name = driver != null ? $"{driver.FirstName} {driver.LastName}" : driverId.ToString();
                throw new InvalidOperationException($"Dispatch blocked: driver license record not found for '{name}'.");
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            if (license.ExpiryDate.Value < today)
            {
                var name = driver != null ? $"{driver.FirstName} {driver.LastName}" : driverId.ToString();
                throw new InvalidOperationException($"Dispatch blocked: driver's license for '{name}' expired on {license.ExpiryDate.Value:yyyy-MM-dd}.");
            }
        }

        var vehicle = await RequireVehicleAsync(trip.VehicleAssetId);

        var dispatchedAt = (dto.DispatchedAt ?? DateTime.UtcNow).ToUniversalTime();
        trip.DispatchedAt = dispatchedAt;
        trip.DispatchedByUserId = userId;
        trip.ActualStartAt = dispatchedAt;
        trip.Status = FleetTripStatuses.Dispatched;

        trip.StartMileage = dto.StartMileage ?? trip.StartMileage ?? vehicle.Mileage;
        trip.StartOperatingHours = dto.StartOperatingHours ?? trip.StartOperatingHours ?? vehicle.OperatingHours;

        trip.UpdatedAt = DateTime.UtcNow;
        trip.LastModifiedById = userId;

        await repo.UpdateAsync(trip);
        await _unitOfWork.SaveChangesAsync();

        await _appEventBus.PublishAsync(new EntityActivityEvent
        {
            TenantId = tenantId,
            EntityType = EntityType,
            Activity = "TripDispatched",
            Audience = "Internal",
            EntityId = trip.Id,
            TriggeredByUserId = userId,
            Data = new Dictionary<string, object>
            {
                ["VehicleAssetId"] = trip.VehicleAssetId,
                ["VehicleName"] = vehicle.Name,
                ["VehicleAssetNumber"] = vehicle.AssetNumber,
                ["VehicleLicensePlate"] = vehicle.LicensePlate ?? string.Empty,
                ["RequestedByUserId"] = trip.RequestedByUserId
            }
        });

        return (await GetTripByIdAsync(trip.Id))!;
    }

    public async Task<FleetTripDto> CompleteAsync(Guid tripId, CompleteFleetTripDto dto)
    {
        if (tripId == Guid.Empty) throw new ArgumentException("TripId is required.");
        dto ??= new CompleteFleetTripDto();

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var repo = _unitOfWork.Repository<FleetTrip>();
        var trip = await repo.FirstOrDefaultAsync(t => t.Id == tripId && t.TenantId == tenantId)
            ?? throw new ArgumentException($"Trip {tripId} not found");

        if (trip.Status != FleetTripStatuses.Dispatched)
            throw new InvalidOperationException("Trip must be Dispatched before completion.");

        var vehicle = await RequireVehicleAsync(trip.VehicleAssetId);

        var completedAt = (dto.CompletedAt ?? DateTime.UtcNow).ToUniversalTime();
        trip.CompletedAt = completedAt;
        trip.CompletedByUserId = userId;
        trip.ActualEndAt = completedAt;
        trip.Status = FleetTripStatuses.Completed;

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            trip.Notes = dto.Notes.Trim();
        }

        if (dto.EndMileage.HasValue)
            trip.EndMileage = dto.EndMileage.Value;

        if (dto.EndOperatingHours.HasValue)
            trip.EndOperatingHours = dto.EndOperatingHours.Value;

        trip.UpdatedAt = DateTime.UtcNow;
        trip.LastModifiedById = userId;

        await repo.UpdateAsync(trip);
        await _unitOfWork.SaveChangesAsync();

        // Record usage reading (best-effort).
        try
        {
            await _assetUsageTrackingService.CreateUsageRecordAsync(new CreateAssetUsageTrackingDto
            {
                AssetId = trip.VehicleAssetId,
                RecordedAt = completedAt,
                Mileage = trip.EndMileage.HasValue ? (decimal?)Convert.ToDecimal(trip.EndMileage.Value) : null,
                MileageUnit = "km",
                OperatingHours = trip.EndOperatingHours.HasValue ? (decimal?)Convert.ToDecimal(trip.EndOperatingHours.Value) : null,
                DataSource = "FleetTrip",
                Notes = $"Trip completed ({trip.Id})"
            });
        }
        catch
        {
            // Best-effort: don't fail completion if usage logging fails.
        }

        // Update vehicle current metrics (best-effort).
        if (trip.EndMileage.HasValue)
        {
            try { await _maintenanceAssetService.UpdateAssetMileageAsync(trip.VehicleAssetId, trip.EndMileage.Value); } catch { }
        }
        if (trip.EndOperatingHours.HasValue)
        {
            try { await _maintenanceAssetService.UpdateAssetOperatingHoursAsync(trip.VehicleAssetId, trip.EndOperatingHours.Value); } catch { }
        }

        await _appEventBus.PublishAsync(new EntityActivityEvent
        {
            TenantId = tenantId,
            EntityType = EntityType,
            Activity = "TripCompleted",
            Audience = "Internal",
            EntityId = trip.Id,
            TriggeredByUserId = userId,
            Data = new Dictionary<string, object>
            {
                ["VehicleAssetId"] = trip.VehicleAssetId,
                ["VehicleName"] = vehicle.Name,
                ["VehicleAssetNumber"] = vehicle.AssetNumber,
                ["VehicleLicensePlate"] = vehicle.LicensePlate ?? string.Empty,
                ["RequestedByUserId"] = trip.RequestedByUserId
            }
        });

        return (await GetTripByIdAsync(trip.Id))!;
    }

    public async Task<bool> CancelAsync(Guid tripId, string reason)
    {
        if (tripId == Guid.Empty) throw new ArgumentException("TripId is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var repo = _unitOfWork.Repository<FleetTrip>();
        var trip = await repo.FirstOrDefaultAsync(t => t.Id == tripId && t.TenantId == tenantId)
            ?? throw new ArgumentException($"Trip {tripId} not found");

        if (trip.Status == FleetTripStatuses.Completed || trip.Status == FleetTripStatuses.Cancelled)
            return true;

        if (trip.Status == FleetTripStatuses.Dispatched)
            throw new InvalidOperationException("Cannot cancel a trip that is already dispatched.");

        trip.Status = FleetTripStatuses.Cancelled;
        trip.Notes = string.IsNullOrWhiteSpace(reason) ? trip.Notes : reason.Trim();
        trip.UpdatedAt = DateTime.UtcNow;
        trip.LastModifiedById = userId;

        await repo.UpdateAsync(trip);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    private FleetTripDto Map(FleetTrip t)
    {
        var driverName = t.DriverEmployee == null
            ? null
            : string.Join(' ', new[] { t.DriverEmployee.FirstName, t.DriverEmployee.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return new FleetTripDto
        {
            Id = t.Id,
            VehicleAssetId = t.VehicleAssetId,
            VehicleName = t.VehicleAsset?.Name ?? string.Empty,
            VehicleAssetNumber = t.VehicleAsset?.AssetNumber,
            VehicleLicensePlate = t.VehicleAsset?.LicensePlate,
            RequestedByUserId = t.RequestedByUserId,
            DriverEmployeeId = t.DriverEmployeeId,
            DriverEmployeeName = driverName,
            Status = t.Status,
            Purpose = t.Purpose,
            Origin = t.Origin,
            Destination = t.Destination,
            Notes = t.Notes,
            PlannedStartAt = t.PlannedStartAt,
            PlannedEndAt = t.PlannedEndAt,
            ActualStartAt = t.ActualStartAt,
            ActualEndAt = t.ActualEndAt,
            CreatedAt = t.CreatedAt,
            ApprovedAt = t.ApprovedAt,
            ApprovedByUserId = t.ApprovedByUserId,
            RejectedAt = t.RejectedAt,
            RejectedByUserId = t.RejectedByUserId,
            RejectionReason = t.RejectionReason,
            DispatchedAt = t.DispatchedAt,
            DispatchedByUserId = t.DispatchedByUserId,
            CompletedAt = t.CompletedAt,
            CompletedByUserId = t.CompletedByUserId,
            StartMileage = t.StartMileage,
            EndMileage = t.EndMileage,
            StartOperatingHours = t.StartOperatingHours,
            EndOperatingHours = t.EndOperatingHours
        };
    }

    private async Task<MaintenanceAsset> RequireVehicleAsync(Guid vehicleAssetId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var vehicle = await _unitOfWork.Repository<MaintenanceAsset>()
            .FirstOrDefaultAsync(a => a.Id == vehicleAssetId && a.TenantId == tenantId, a => a.AssetCategory);

        if (vehicle == null) throw new ArgumentException("Vehicle not found.");
        if (!string.Equals(vehicle.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Selected asset is not a vehicle.");

        return vehicle;
    }

    private async Task<Employee> RequireEmployeeAsync(Guid employeeId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var employee = await _unitOfWork.Repository<Employee>()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId);

        if (employee == null) throw new ArgumentException("Driver employee not found.");
        return employee;
    }
}
