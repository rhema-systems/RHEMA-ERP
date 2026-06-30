using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Shared;
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
    private readonly IMaintenanceSettingsRepository _maintenanceSettingsRepository;

    public FleetTripService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IFleetComplianceService fleetComplianceService,
        IMaintenanceAssetService maintenanceAssetService,
        IAssetUsageTrackingService assetUsageTrackingService,
        IAppEventBus appEventBus,
        IMaintenanceSettingsRepository maintenanceSettingsRepository)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _fleetComplianceService = fleetComplianceService;
        _maintenanceAssetService = maintenanceAssetService;
        _assetUsageTrackingService = assetUsageTrackingService;
        _appEventBus = appEventBus;
        _maintenanceSettingsRepository = maintenanceSettingsRepository;
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
            .Include(t => t.DriverEmployee)
            .Include(t => t.FleetTripDestination);

        var scope = await GetMaintenanceLocationScopeAsync();
        if (scope.IsScoped)
        {
            q = scope.LocationId.HasValue
                ? q.Where(t => t.VehicleAsset.CurrentSiteLocationId == scope.LocationId.Value)
                : q.Where(t => false);
        }

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
            .FirstOrDefaultAsync(t => t.Id == tripId && t.TenantId == tenantId, t => t.VehicleAsset, t => t.DriverEmployee, t => t.FleetTripDestination);

        var scope = await GetMaintenanceLocationScopeAsync();
        if (trip != null && scope.IsScoped && (!scope.LocationId.HasValue || trip.VehicleAsset?.CurrentSiteLocationId != scope.LocationId.Value))
        {
            return null;
        }

        return trip == null ? null : Map(trip);
    }

    public async Task<FleetTripDto> CreateTripAsync(CreateFleetTripDto dto)
    {
        dto ??= new CreateFleetTripDto();
        if (dto.VehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");

        var tenantId = _currentUserProvider.TenantId;
        var userId = _currentUserProvider.UserId;

        var vehicle = await RequireVehicleAsync(dto.VehicleAssetId);
        if (vehicle.Status != AssetStatus.Active)
            throw new InvalidOperationException($"Vehicle '{vehicle.Name}' ({vehicle.AssetNumber}) is currently {vehicle.Status} and cannot be assigned to a trip.");

        await EnsureNoDispatchedConflictAsync(vehicle.Id, null, null);

        var driverEmployeeId = dto.DriverEmployeeId.HasValue && dto.DriverEmployeeId.Value != Guid.Empty
            ? dto.DriverEmployeeId
            : await _unitOfWork.Repository<FleetVehicleAssignment>()
                .GetQueryable(a => a.TenantId == tenantId && a.VehicleAssetId == vehicle.Id && a.IsActive && !a.IsDeleted)
                .OrderByDescending(a => a.AssignedFromUtc)
                .Select(a => (Guid?)a.EmployeeId)
                .FirstOrDefaultAsync();

        var driver = driverEmployeeId.HasValue && driverEmployeeId.Value != Guid.Empty
            ? await RequireEmployeeAsync(driverEmployeeId.Value)
            : null;

        if (driver != null)
        {
            await EnsureDriverLicenseValidAsync(driver);
            await EnsureNoDispatchedConflictAsync(vehicle.Id, driver.Id, null);
        }

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
            FleetTripDestinationId = dto.FleetTripDestinationId.HasValue && dto.FleetTripDestinationId.Value != Guid.Empty ? dto.FleetTripDestinationId : null,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            PlannedStartAt = dto.PlannedStartAt,
            PlannedEndAt = dto.PlannedEndAt,
            Status = FleetTripStatuses.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedById = userId
        };

        if (trip.FleetTripDestinationId.HasValue)
        {
            await ApplyTripDestinationTemplateAsync(trip, trip.FleetTripDestinationId.Value, requireActive: false);
        }

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
                ["TripId"] = trip.Id,
                ["VehicleAssetId"] = trip.VehicleAssetId,
                ["VehicleName"] = vehicle.Name,
                ["VehicleAssetNumber"] = vehicle.AssetNumber,
                ["VehicleLicensePlate"] = vehicle.LicensePlate ?? string.Empty,
                ["RequestedByUserId"] = trip.RequestedByUserId,
                ["PlannedStartAtUtc"] = trip.PlannedStartAt?.ToString("o") ?? string.Empty,
                ["PlannedEndAtUtc"] = trip.PlannedEndAt?.ToString("o") ?? string.Empty
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

        // After approval, keep the approved plan immutable but allow assigning/changing driver (dispatch preparation).
        if (trip.Status == FleetTripStatuses.Approved)
        {
            if (dto.VehicleAssetId == Guid.Empty || dto.VehicleAssetId != trip.VehicleAssetId)
                throw new InvalidOperationException("Approved trips cannot change vehicle. Only driver assignment is allowed.");

            // Treat omitted fields as "no change" to allow a minimal payload (vehicleAssetId + driverEmployeeId).
            if (dto.FleetTripDestinationId.HasValue &&
                (dto.FleetTripDestinationId.Value != (trip.FleetTripDestinationId ?? Guid.Empty)))
            {
                throw new InvalidOperationException("Approved trips cannot change trip destination. Only driver assignment is allowed.");
            }

            if (dto.Purpose != null &&
                !string.Equals(dto.Purpose.Trim(), (trip.Purpose ?? string.Empty).Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Approved trips cannot change purpose. Only driver assignment is allowed.");
            }

            if (dto.Origin != null &&
                !string.Equals(dto.Origin.Trim(), (trip.Origin ?? string.Empty).Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Approved trips cannot change origin. Only driver assignment is allowed.");
            }

            if (dto.Destination != null &&
                !string.Equals(dto.Destination.Trim(), (trip.Destination ?? string.Empty).Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Approved trips cannot change destination. Only driver assignment is allowed.");
            }

            if (dto.PlannedStartAt.HasValue && dto.PlannedStartAt.Value != trip.PlannedStartAt)
            {
                throw new InvalidOperationException("Approved trips cannot change planned dates. Only driver assignment is allowed.");
            }

            if (dto.PlannedEndAt.HasValue && dto.PlannedEndAt.Value != trip.PlannedEndAt)
            {
                throw new InvalidOperationException("Approved trips cannot change planned dates. Only driver assignment is allowed.");
            }

            var approvedDriver = dto.DriverEmployeeId.HasValue && dto.DriverEmployeeId.Value != Guid.Empty
                ? await RequireEmployeeAsync(dto.DriverEmployeeId.Value)
                : null;

            if (approvedDriver != null)
            {
                await EnsureDriverLicenseValidAsync(approvedDriver);
                await EnsureNoDispatchedConflictAsync(trip.VehicleAssetId, approvedDriver.Id, trip.Id);
            }

            trip.DriverEmployeeId = approvedDriver?.Id;

            if (!string.IsNullOrWhiteSpace(dto.Notes))
            {
                trip.Notes = dto.Notes.Trim();
            }

            trip.UpdatedAt = DateTime.UtcNow;
            trip.LastModifiedById = userId;

            await repo.UpdateAsync(trip);
            await _unitOfWork.SaveChangesAsync();

            return (await GetTripByIdAsync(trip.Id))!;
        }

        if (trip.Status != FleetTripStatuses.Draft && trip.Status != FleetTripStatuses.Rejected)
            throw new InvalidOperationException("Only Draft or Rejected trips can be edited.");

        var vehicle = await RequireVehicleAsync(dto.VehicleAssetId);
        if (dto.VehicleAssetId != trip.VehicleAssetId && vehicle.Status != AssetStatus.Active)
            throw new InvalidOperationException($"Vehicle '{vehicle.Name}' ({vehicle.AssetNumber}) is currently {vehicle.Status} and cannot be assigned to a trip.");

        var driver = dto.DriverEmployeeId.HasValue && dto.DriverEmployeeId.Value != Guid.Empty
            ? await RequireEmployeeAsync(dto.DriverEmployeeId.Value)
            : null;

        await EnsureNoDispatchedConflictAsync(vehicle.Id, driver?.Id, trip.Id);
        if (driver != null)
        {
            await EnsureDriverLicenseValidAsync(driver);
        }

        trip.VehicleAssetId = vehicle.Id;
        trip.DriverEmployeeId = driver?.Id;
        trip.Purpose = string.IsNullOrWhiteSpace(dto.Purpose) ? null : dto.Purpose.Trim();
        trip.Origin = string.IsNullOrWhiteSpace(dto.Origin) ? null : dto.Origin.Trim();
        trip.Destination = string.IsNullOrWhiteSpace(dto.Destination) ? null : dto.Destination.Trim();
        trip.FleetTripDestinationId = dto.FleetTripDestinationId.HasValue && dto.FleetTripDestinationId.Value != Guid.Empty ? dto.FleetTripDestinationId : null;
        trip.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        trip.PlannedStartAt = dto.PlannedStartAt;
        trip.PlannedEndAt = dto.PlannedEndAt;

        if (trip.FleetTripDestinationId.HasValue)
        {
            await ApplyTripDestinationTemplateAsync(trip, trip.FleetTripDestinationId.Value, requireActive: false);
        }

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

        var settings = await _maintenanceSettingsRepository.GetOrCreateDefaultAsync(tenantId, userId);
        if (settings.RequirePredefinedFleetTripDestinationOnDispatch &&
            (!trip.FleetTripDestinationId.HasValue || trip.FleetTripDestinationId.Value == Guid.Empty))
        {
            throw new InvalidOperationException("Trip destination is required before submitting for approval (per Maintenance settings).");
        }

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

        // Trip destination enforcement (predefined route/template selection).
        var settings = await _maintenanceSettingsRepository.GetOrCreateDefaultAsync(tenantId, userId);
        var effectiveTripDestinationId = dto.FleetTripDestinationId ?? trip.FleetTripDestinationId;
        if (settings.RequirePredefinedFleetTripDestinationOnDispatch &&
            (!effectiveTripDestinationId.HasValue || effectiveTripDestinationId.Value == Guid.Empty))
        {
            throw new InvalidOperationException("Dispatch blocked: a predefined trip destination must be selected before dispatch (per Maintenance settings).");
        }

        if (effectiveTripDestinationId.HasValue && effectiveTripDestinationId.Value != Guid.Empty)
        {
            await ApplyTripDestinationTemplateAsync(trip, effectiveTripDestinationId.Value, requireActive: true);
        }

        var blocking = await _fleetComplianceService.GetDispatchBlockingItemsAsync(trip.VehicleAssetId, DateTime.UtcNow);
        if (blocking.Count > 0)
        {
            var first = blocking.First();
            if (!first.ExpiryDate.HasValue)
                throw new InvalidOperationException($"Dispatch blocked: '{first.ComplianceType}' has no expiry date set.");

            throw new InvalidOperationException(
                $"Dispatch blocked: '{first.ComplianceType}' is {(first.ExpiryDate.Value.Date < DateTime.UtcNow.Date ? "overdue" : "due soon")} (expires {first.ExpiryDate.Value:yyyy-MM-dd}).");
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

        // Driver licence and concurrent-trip blocking.
        if (trip.DriverEmployeeId.HasValue && trip.DriverEmployeeId.Value != Guid.Empty)
        {
            var driverId = trip.DriverEmployeeId.Value;
            var driver = await _unitOfWork.Repository<Employee>()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == driverId && !e.IsDeleted);
            if (driver == null) throw new InvalidOperationException("Dispatch blocked: the selected driver employee record no longer exists.");
            await EnsureDriverLicenseValidAsync(driver, "Dispatch blocked");
        }

        await EnsureNoDispatchedConflictAsync(trip.VehicleAssetId, trip.DriverEmployeeId, trip.Id);

        var vehicle = await RequireVehicleAsync(trip.VehicleAssetId);
        if (vehicle.Status != AssetStatus.Active)
            throw new InvalidOperationException($"Dispatch blocked: vehicle '{vehicle.Name}' ({vehicle.AssetNumber}) is currently {vehicle.Status}.");

        vehicle.Status = AssetStatus.InUse;
        vehicle.UpdatedAt = DateTime.UtcNow;
        vehicle.LastModifiedById = userId;

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
        await _unitOfWork.Repository<MaintenanceAsset>().UpdateAsync(vehicle);
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
                ["TripId"] = trip.Id,
                ["VehicleAssetId"] = trip.VehicleAssetId,
                ["VehicleName"] = vehicle.Name,
                ["VehicleAssetNumber"] = vehicle.AssetNumber,
                ["VehicleLicensePlate"] = vehicle.LicensePlate ?? string.Empty,
                ["RequestedByUserId"] = trip.RequestedByUserId,
                ["DispatchedAtUtc"] = dispatchedAt.ToString("o")
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
        var shouldReleaseVehicle = vehicle.Status == AssetStatus.InUse && !await IsVehicleUsedByActiveWorkOrderAsync(vehicle.Id);

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

        if (shouldReleaseVehicle)
        {
            vehicle.Status = AssetStatus.Active;
            vehicle.UpdatedAt = DateTime.UtcNow;
            vehicle.LastModifiedById = userId;
            await _unitOfWork.Repository<MaintenanceAsset>().UpdateAsync(vehicle);
        }

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
                ["TripId"] = trip.Id,
                ["VehicleAssetId"] = trip.VehicleAssetId,
                ["VehicleName"] = vehicle.Name,
                ["VehicleAssetNumber"] = vehicle.AssetNumber,
                ["VehicleLicensePlate"] = vehicle.LicensePlate ?? string.Empty,
                ["RequestedByUserId"] = trip.RequestedByUserId,
                ["CompletedAtUtc"] = completedAt.ToString("o")
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

    private async Task ApplyTripDestinationTemplateAsync(FleetTrip trip, Guid destinationId, bool requireActive)
    {
        if (trip == null) throw new ArgumentNullException(nameof(trip));
        if (destinationId == Guid.Empty) throw new ArgumentException("FleetTripDestinationId is required.");

        var tenantId = _currentUserProvider.TenantId;

        var repo = _unitOfWork.Repository<FleetTripDestination>();
        var q = repo.GetQueryable(d => d.TenantId == tenantId && d.Id == destinationId && !d.IsDeleted);
        if (requireActive)
        {
            q = q.Where(d => d.IsActive);
        }

        var destination = await q.FirstOrDefaultAsync();
        if (destination == null)
        {
            throw new ArgumentException(requireActive
                ? "Selected trip destination not found or inactive."
                : "Selected trip destination not found.");
        }

        trip.FleetTripDestinationId = destination.Id;

        if (!string.IsNullOrWhiteSpace(destination.Origin))
        {
            trip.Origin = destination.Origin.Trim();
        }

        if (!string.IsNullOrWhiteSpace(destination.Destination))
        {
            trip.Destination = destination.Destination.Trim();
        }

        trip.ExpectedHours = destination.ExpectedHours;
        trip.ExpectedMileage = destination.ExpectedMileage;
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
            VehicleFuelType = t.VehicleAsset?.FuelType,
            RequestedByUserId = t.RequestedByUserId,
            DriverEmployeeId = t.DriverEmployeeId,
            DriverEmployeeName = driverName,
            Status = t.Status,
            Purpose = t.Purpose,
            Origin = t.Origin,
            Destination = t.Destination,
            FleetTripDestinationId = t.FleetTripDestinationId,
            FleetTripDestinationName = t.FleetTripDestination?.Name,
            ExpectedHours = t.ExpectedHours ?? t.FleetTripDestination?.ExpectedHours,
            ExpectedMileage = t.ExpectedMileage ?? t.FleetTripDestination?.ExpectedMileage,
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

        var scope = await GetMaintenanceLocationScopeAsync();
        if (scope.IsScoped && (!scope.LocationId.HasValue || vehicle.CurrentSiteLocationId != scope.LocationId.Value))
        {
            throw new UnauthorizedAccessException("You can only use fleet vehicles assigned to your HR location/site.");
        }

        return vehicle;
    }

    private async Task<(bool IsScoped, Guid? LocationId)> GetMaintenanceLocationScopeAsync()
    {
        if (!_currentUserProvider.IsAuthenticated)
        {
            return (false, null);
        }

        var unrestrictedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Constants.Roles.SuperAdmin,
            Constants.Roles.TenantAdmin,
            Constants.Roles.Manager,
            "MaintenanceManager",
            "MaintenanceSupervisor",
            "MaintenanceDirector",
            "FleetManager",
            "FleetSupervisor"
        };

        if ((_currentUserProvider.Roles ?? Enumerable.Empty<string>()).Any(unrestrictedRoles.Contains))
        {
            return (false, null);
        }

        if (!_currentUserProvider.Claims.TryGetValue("employee_id", out var employeeClaim) ||
            !Guid.TryParse(employeeClaim, out var employeeId))
        {
            return (false, null);
        }

        var employee = await _unitOfWork.Repository<Employee>()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == _currentUserProvider.TenantId && e.IsActive, e => e.Department);

        if (employee?.Department == null ||
            !employee.Department.Name.Contains("Maintenance", StringComparison.OrdinalIgnoreCase))
        {
            return (false, null);
        }

        return (true, employee.LocationId);
    }

    private async Task<Employee> RequireEmployeeAsync(Guid employeeId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var employee = await _unitOfWork.Repository<Employee>()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId);

        if (employee == null) throw new ArgumentException("Driver employee not found.");
        return employee;
    }

    private async Task EnsureDriverLicenseValidAsync(Employee driver, string prefix = "Driver selection blocked")
    {
        var tenantId = _currentUserProvider.TenantId;
        var license = await _unitOfWork.Repository<EmployeeIdentificationCard>()
            .GetQueryable(c =>
                c.TenantId == tenantId &&
                c.EmployeeId == driver.Id &&
                !c.IsDeleted &&
                c.DocumentType.ToLower().Contains("driver"))
            .OrderByDescending(c => c.ExpiryDate)
            .FirstOrDefaultAsync();

        var name = $"{driver.FirstName} {driver.LastName}".Trim();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        if (license == null)
            throw new InvalidOperationException($"{prefix}: '{name}' does not have a driver's license on file.");
        if (!license.IsVerified)
            throw new InvalidOperationException($"{prefix}: driver's license for '{name}' has not been verified by HR.");
        if (!license.ExpiryDate.HasValue)
            throw new InvalidOperationException($"{prefix}: driver's license for '{name}' has no expiry date.");
        if (license.IssueDate.HasValue && license.IssueDate.Value > today)
            throw new InvalidOperationException($"{prefix}: driver's license for '{name}' is not valid until {license.IssueDate.Value:yyyy-MM-dd}.");
        if (license.ExpiryDate.Value < today)
            throw new InvalidOperationException($"{prefix}: driver's license for '{name}' expired on {license.ExpiryDate.Value:yyyy-MM-dd}.");
    }

    private async Task EnsureNoDispatchedConflictAsync(Guid vehicleAssetId, Guid? driverEmployeeId, Guid? excludeTripId)
    {
        var tenantId = _currentUserProvider.TenantId;
        var activeTrips = _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(t =>
                t.TenantId == tenantId &&
                !t.IsDeleted &&
                t.Status == FleetTripStatuses.Dispatched &&
                (!excludeTripId.HasValue || t.Id != excludeTripId.Value));

        if (await activeTrips.AnyAsync(t => t.VehicleAssetId == vehicleAssetId))
            throw new InvalidOperationException("The selected vehicle is already engaged on a dispatched trip.");

        if (driverEmployeeId.HasValue && driverEmployeeId.Value != Guid.Empty &&
            await activeTrips.AnyAsync(t => t.DriverEmployeeId == driverEmployeeId.Value))
            throw new InvalidOperationException("The selected driver is already engaged on a dispatched trip.");
    }

    private async Task<bool> IsVehicleUsedByActiveWorkOrderAsync(Guid vehicleAssetId)
    {
        var tenantId = _currentUserProvider.TenantId;

        var activeWorkOrdersQ = _unitOfWork.Repository<WorkOrder>()
            .GetQueryable(w => w.TenantId == tenantId && !w.IsDeleted && w.Status != "Completed" && w.Status != "Cancelled" && w.Status != "Closed");

        var scheduleVehicleUseQ = _unitOfWork.Repository<MaintenanceStaffSchedule>()
            .GetQueryable(s => s.TenantId == tenantId && !s.IsDeleted && s.AssignedVehicleId == vehicleAssetId && s.WorkOrderId.HasValue);

        var scheduleUse = await scheduleVehicleUseQ
            .Join(activeWorkOrdersQ, s => s.WorkOrderId!.Value, w => w.Id, (_, w) => w.Id)
            .AnyAsync();

        if (scheduleUse) return true;

        var expenseVehicleUseQ = _unitOfWork.Repository<MaintenanceExpense>()
            .GetQueryable(e => e.TenantId == tenantId && !e.IsDeleted && e.VehicleId == vehicleAssetId);

        var expenseUse = await expenseVehicleUseQ
            .Join(activeWorkOrdersQ, e => e.WorkOrderId, w => w.Id, (_, w) => w.Id)
            .AnyAsync();

        return expenseUse;
    }
}
