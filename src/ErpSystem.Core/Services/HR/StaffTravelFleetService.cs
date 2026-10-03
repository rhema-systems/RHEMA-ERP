using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Travel's side of the seam with Fleet (travel final closure, lane 6: slice 6a — FX-1…FX-5, D2, D4, D-27; slice 6b —
/// fuel on claims, FX-6, D-30…D-32).
/// </summary>
/// <remarks>
/// <para><b>Fleet's facts stay Fleet's.</b> A company-vehicle leg keeps only its fleet trip's id; the vehicle, plate,
/// driver, status, times and mileage are read from the fleet trip whenever the leg is read, and the leg's status is
/// Fleet's mapped: Draft and Submitted → Pending, Approved and Dispatched → Confirmed, Completed → Completed, Rejected
/// and Cancelled → Cancelled.</para>
///
/// <para><b>A reservation that holds.</b> The fleet trip was made Draft and never submitted, so the transport office had
/// nothing to approve and nothing was held (FX-1), and Fleet's only clash check looked at dispatched trips (FX-2) and its
/// compliance at dispatch (FX-5). Travel now refuses a vehicle or driver with another trip planned over the leg —
/// Draft, Submitted, Approved or Dispatched — and a vehicle whose critical compliance item runs out by the return; and
/// submits the trip to Fleet's approval when Fleet publishes an approval route (D-27) — with none the engine would
/// approve it with nobody asked, so the trip stays a draft and the leg says the vehicle is not held.</para>
///
/// <para><b>Through travel's door.</b> HR holds no Maintenance permission, so the pickers are read here (FX-4).</para>
///
/// <para><b>Fuel on claims (6b).</b> A fuel expense names its company vehicle's trip and the litres; when the claim is paid
/// the fill goes into Fleet's fuel log at the amount paid, through Fleet's own service, which writes the cost entry — and
/// a voided payment takes it out again. The budget leaves those cost entries out: the paid claim counts them (R4).</para>
/// </remarks>
public class StaffTravelFleetService : IStaffTravelFleetService
{
    private static readonly string[] Planned =
        { FleetTripStatuses.Draft, FleetTripStatuses.Submitted, FleetTripStatuses.Approved, FleetTripStatuses.Dispatched };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFleetTripService _fleetTrips;
    private readonly IFleetComplianceService _compliance;
    private readonly IFleetTripDestinationService _destinations;
    private readonly IFleetFuelService _fuel;
    private readonly ILogger<StaffTravelFleetService> _logger;

    public StaffTravelFleetService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IFleetTripService fleetTrips,
        IFleetComplianceService compliance,
        IFleetTripDestinationService destinations,
        IFleetFuelService fuel,
        ILogger<StaffTravelFleetService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _fleetTrips = fleetTrips;
        _compliance = compliance;
        _fuel = fuel;
        _destinations = destinations;
        _logger = logger;
    }

    private Guid TenantId
    {
        get
        {
            var tenantId = _currentUserProvider.TenantId;
            if (tenantId == Guid.Empty)
                throw new InvalidOperationException("No tenant is associated with the current user.");
            return tenantId;
        }
    }

    /// <summary>The leg's status, read from its fleet trip.</summary>
    public static TravelBookingStatus LegStatus(string? fleetStatus) => fleetStatus switch
    {
        FleetTripStatuses.Approved or FleetTripStatuses.Dispatched => TravelBookingStatus.Confirmed,
        FleetTripStatuses.Completed => TravelBookingStatus.Completed,
        FleetTripStatuses.Rejected or FleetTripStatuses.Cancelled => TravelBookingStatus.Cancelled,
        _ => TravelBookingStatus.Pending,
    };

    /// <summary>A fleet trip the transport office has committed the vehicle to — what refuses a trip's cancel (D-24).</summary>
    public static bool IsCommitted(string? fleetStatus)
        => fleetStatus is FleetTripStatuses.Approved or FleetTripStatuses.Dispatched;

    private static string Window(DateTime? start, DateTime? end)
        => $"{start:d MMM HH:mm} to {(end ?? start):d MMM HH:mm}";

    private static string NameOf(string? first, string? last) => $"{first} {last}".Trim();

    /// <summary>D-27: Fleet publishes an approval route for its trips — otherwise its engine approves with nobody asked.</summary>
    private async Task<bool> IsApprovalRoutePublishedAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<WorkflowDefinition>().GetQueryable()
            .AnyAsync(d => d.TenantId == tenantId
                        && !d.IsDeleted
                        && d.IsActive
                        && d.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published
                        && d.EntityType != null
                        && (d.EntityType.Code == "FLEET_TRIP" || d.EntityType.Name == "FleetTrip"),
                cancellationToken);

    private async Task<bool> DestinationRequiredAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<MaintenanceSettings>()
            .GetQueryable(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => s.RequirePredefinedFleetTripDestinationOnDispatch)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<StaffTravelFleetOptionsDto> GetOptionsAsync(
        Guid requestId, DateTime? from = null, DateTime? to = null, Guid? excludeFleetTripId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var request = await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.Id == requestId && r.TenantId == tenantId && !r.IsDeleted)
            .Select(r => new { r.EmployeeId, r.TravelStartDate, r.TravelEndDate })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"Staff travel request with ID '{requestId}' not found.");
        var start = from ?? request.TravelStartDate.ToDateTime(TimeOnly.MinValue);
        var end = to ?? request.TravelEndDate.ToDateTime(new TimeOnly(23, 59));
        if (end < start) end = start;

        var vehicles = await _unitOfWork.Repository<MaintenanceAsset>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted && a.Status == AssetStatus.Active
                            && a.AssetCategory != null && a.AssetCategory.AssetType == "Vehicle")
            .OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.Name, a.AssetNumber, a.LicensePlate })
            .ToListAsync(cancellationToken);
        var vehicleIds = vehicles.Select(v => v.Id).ToList();

        var assignments = await _unitOfWork.Repository<FleetVehicleAssignment>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted && a.IsActive && vehicleIds.Contains(a.VehicleAssetId))
            .OrderByDescending(a => a.AssignedFromUtc)
            .Select(a => new { a.VehicleAssetId, a.EmployeeId, a.AssignmentType })
            .ToListAsync(cancellationToken);

        var trips = await _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && Planned.Contains(t.Status)
                            && t.PlannedStartAt != null && t.PlannedStartAt < end
                            && (t.PlannedEndAt ?? t.PlannedStartAt) > start
                            && (excludeFleetTripId == null || t.Id != excludeFleetTripId))
            .Select(t => new { t.VehicleAssetId, t.DriverEmployeeId, t.Status, t.Purpose, t.PlannedStartAt, t.PlannedEndAt })
            .ToListAsync(cancellationToken);

        var startDay = DateOnly.FromDateTime(start);
        var endDay = DateOnly.FromDateTime(end);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var licences = (await _unitOfWork.Repository<EmployeeIdentificationCard>()
                .GetQueryable(c => c.TenantId == tenantId && !c.IsDeleted && c.IsVerified
                                && c.ExpiryDate != null && c.ExpiryDate >= endDay
                                && (c.IssueDate == null || c.IssueDate <= today)
                                && c.IdentificationType.Name.ToLower().Contains("driver"))
                .Select(c => new { c.EmployeeId, c.ExpiryDate })
                .ToListAsync(cancellationToken))
            .GroupBy(c => c.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Max(c => c.ExpiryDate));
        var people = licences.Keys.Concat(assignments.Select(a => a.EmployeeId)).Distinct().ToList();
        var employees = await _unitOfWork.Repository<Employee>()
            .GetQueryable(e => e.TenantId == tenantId && !e.IsDeleted && people.Contains(e.Id))
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.EmployeeNumber, e.IsActive })
            .ToListAsync(cancellationToken);
        var leave = await _unitOfWork.Repository<LeaveRequest>().GetQueryable()
            .Where(l => l.TenantId == tenantId && people.Contains(l.EmployeeId)
                     && (l.Status == LeaveStatus.Approved || l.Status == LeaveStatus.InProgress)
                     && l.StartDate <= endDay && l.EndDate >= startDay)
            .Select(l => new { l.EmployeeId, l.StartDate, l.EndDate })
            .ToListAsync(cancellationToken);

        var dto = new StaffTravelFleetOptionsDto { WindowStart = start, WindowEnd = end };
        foreach (var v in vehicles)
        {
            var option = new StaffTravelFleetVehicleOptionDto
            {
                VehicleAssetId = v.Id, Name = v.Name, AssetNumber = v.AssetNumber, LicensePlate = v.LicensePlate,
            };
            var assigned = assignments.FirstOrDefault(a => a.VehicleAssetId == v.Id);
            if (assigned is not null && employees.FirstOrDefault(e => e.Id == assigned.EmployeeId) is { } holder)
                option.AssignedTo = $"{NameOf(holder.FirstName, holder.LastName)} ({assigned.AssignmentType})";
            foreach (var item in await _compliance.GetDispatchBlockingItemsAsync(v.Id, end))
                option.BlockingCompliance.Add(item.ExpiryDate is DateTime expiry
                    ? $"{item.ComplianceType} expires {expiry:d MMM yyyy}"
                    : $"{item.ComplianceType} has no expiry date");
            foreach (var t in trips.Where(t => t.VehicleAssetId == v.Id))
                option.Overlaps.Add($"{t.Purpose ?? "a fleet trip"}, {Window(t.PlannedStartAt, t.PlannedEndAt)} ({t.Status})");
            dto.Vehicles.Add(option);
        }

        foreach (var (employeeId, expiry) in licences)
        {
            if (employees.FirstOrDefault(e => e.Id == employeeId) is not { IsActive: true } person) continue;
            var option = new StaffTravelFleetDriverOptionDto
            {
                EmployeeId = employeeId, Name = NameOf(person.FirstName, person.LastName),
                EmployeeNumber = person.EmployeeNumber, LicenceExpiry = expiry,
            };
            foreach (var l in leave.Where(l => l.EmployeeId == employeeId))
                option.Flags.Add($"on approved leave {l.StartDate:d MMM} to {l.EndDate:d MMM}");
            foreach (var t in trips.Where(t => t.DriverEmployeeId == employeeId))
                option.Flags.Add($"driving {t.Purpose ?? "a fleet trip"}, {Window(t.PlannedStartAt, t.PlannedEndAt)}");
            dto.Drivers.Add(option);
        }
        dto.Drivers = dto.Drivers.OrderBy(d => d.Flags.Count).ThenBy(d => d.Name).ToList();

        // D-11: the traveller's own official car first. Fleet assigns a vehicle only to a licensed driver, so the
        // traveller with an assigned car drives it — unless another licensed driver is assigned to it too.
        var own = assignments.FirstOrDefault(a => a.EmployeeId == request.EmployeeId
                                                && string.Equals(a.AssignmentType, "Primary", StringComparison.OrdinalIgnoreCase));
        if (own is not null)
        {
            dto.DefaultVehicleAssetId = own.VehicleAssetId;
            dto.DefaultDriverEmployeeId = assignments
                .Where(a => a.VehicleAssetId == own.VehicleAssetId && a.EmployeeId != request.EmployeeId && licences.ContainsKey(a.EmployeeId))
                .Select(a => (Guid?)a.EmployeeId)
                .FirstOrDefault()
                ?? (licences.ContainsKey(request.EmployeeId) ? request.EmployeeId : null);
        }

        dto.DestinationRequired = await DestinationRequiredAsync(tenantId, cancellationToken);
        if (dto.DestinationRequired)
            dto.Destinations = (await _destinations.GetAllAsync(activeOnly: true))
                .Select(d => new StaffTravelFleetDestinationOptionDto { Id = d.Id, Name = d.Name })
                .ToList();
        dto.ApprovalRoutePublished = await IsApprovalRoutePublishedAsync(tenantId, cancellationToken);
        return dto;
    }

    /// <summary>FX-2, FX-5 and Fleet's destination rule, checked before Fleet is asked anything.</summary>
    private async Task RequireReservableAsync(
        Guid vehicleAssetId, StaffTravelFleetReservation r, Guid? excludeFleetTripId, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        if (r.End <= r.Start)
            throw new InvalidOperationException("A company vehicle is reserved from the pick-up to a later drop-off.");
        if (r.DestinationId is null && await DestinationRequiredAsync(tenantId, cancellationToken))
            throw new InvalidOperationException(
                "Fleet asks a predefined destination for every trip (its Maintenance settings) — choose one of Fleet's destinations.");

        var clashes = await _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && Planned.Contains(t.Status)
                            && t.PlannedStartAt != null && t.PlannedStartAt < r.End
                            && (t.PlannedEndAt ?? t.PlannedStartAt) > r.Start
                            && (excludeFleetTripId == null || t.Id != excludeFleetTripId)
                            && (t.VehicleAssetId == vehicleAssetId
                                || (r.DriverEmployeeId != null && t.DriverEmployeeId == r.DriverEmployeeId)))
            .Select(t => new { t.VehicleAssetId, t.Status, t.Purpose, t.PlannedStartAt, t.PlannedEndAt })
            .ToListAsync(cancellationToken);
        if (clashes.FirstOrDefault(c => c.VehicleAssetId == vehicleAssetId) is { } vehicleClash)
            throw new InvalidOperationException(
                $"The vehicle is already planned for {vehicleClash.Purpose ?? "another fleet trip"}, " +
                $"{Window(vehicleClash.PlannedStartAt, vehicleClash.PlannedEndAt)} ({vehicleClash.Status} in Fleet). " +
                "Choose another vehicle or other times.");
        if (clashes.FirstOrDefault() is { } driverClash)
            throw new InvalidOperationException(
                $"The driver is already planned for {driverClash.Purpose ?? "another fleet trip"}, " +
                $"{Window(driverClash.PlannedStartAt, driverClash.PlannedEndAt)} ({driverClash.Status} in Fleet). " +
                "Choose another driver or other times.");

        var blocking = await _compliance.GetDispatchBlockingItemsAsync(vehicleAssetId, r.End);
        if (blocking.Count > 0)
        {
            var first = blocking[0];
            throw new InvalidOperationException(first.ExpiryDate is DateTime expiry
                ? $"The vehicle's {first.ComplianceType} expires on {expiry:d MMM yyyy}, by the time it would be back — " +
                  "Fleet will not dispatch it. Choose another vehicle, or have the item renewed first."
                : $"The vehicle's {first.ComplianceType} has no expiry date in Fleet, so Fleet will not dispatch it.");
        }
    }

    /// <summary>D-27: submitted only when Fleet publishes an approval route; a failure leaves the trip a draft.</summary>
    private async Task<bool> SubmitIfRoutedAsync(Guid fleetTripId, CancellationToken cancellationToken)
    {
        if (!await IsApprovalRoutePublishedAsync(TenantId, cancellationToken)) return false;
        try
        {
            return await _fleetTrips.SubmitForApprovalAsync(fleetTripId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning(ex, "Fleet trip {FleetTripId} was reserved but not submitted to Fleet's approval", fleetTripId);
            return false;
        }
    }

    public async Task<Guid> ReserveAsync(StaffTravelRequest request, StaffTravelFleetReservation reservation, CancellationToken cancellationToken = default)
    {
        if (reservation.VehicleAssetId is not Guid vehicleAssetId || vehicleAssetId == Guid.Empty)
            throw new InvalidOperationException(
                "A company-vehicle leg must name the vehicle to reserve. Choose a vehicle, or pick a different transport type.");
        // Fleet gives a trip with no driver the vehicle's latest active assignment — so the clash check asks about
        // that driver too, and Fleet is handed the same one.
        var tenantId = TenantId;
        if (reservation.DriverEmployeeId is null)
            reservation = reservation with
            {
                DriverEmployeeId = await _unitOfWork.Repository<FleetVehicleAssignment>()
                    .GetQueryable(a => a.TenantId == tenantId && a.VehicleAssetId == vehicleAssetId && a.IsActive && !a.IsDeleted)
                    .OrderByDescending(a => a.AssignedFromUtc)
                    .Select(a => (Guid?)a.EmployeeId)
                    .FirstOrDefaultAsync(cancellationToken),
            };
        await RequireReservableAsync(vehicleAssetId, reservation, null, cancellationToken);

        FleetTripDto trip;
        try
        {
            trip = await _fleetTrips.CreateTripAsync(new CreateFleetTripDto
            {
                VehicleAssetId = vehicleAssetId,
                DriverEmployeeId = reservation.DriverEmployeeId,
                Purpose = reservation.Purpose,
                Origin = reservation.Origin,
                Destination = reservation.Destination,
                FleetTripDestinationId = reservation.DestinationId,
                Notes = reservation.Notes,
                PlannedStartAt = reservation.Start,
                PlannedEndAt = reservation.End,
            });
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException)
        {
            // Fleet's validation speaks ArgumentException ("Vehicle not found") — travel's contract reads that as "the
            // travel record does not exist" (404). Translated at the seam, as before lane 6.
            throw new InvalidOperationException($"The vehicle could not be reserved: {ex.Message}");
        }

        await SubmitIfRoutedAsync(trip.Id, cancellationToken);
        return trip.Id;
    }

    public async Task UpdateReservationAsync(
        StaffTravelRequest request, Guid fleetTripId, StaffTravelFleetReservation reservation, CancellationToken cancellationToken = default)
    {
        var trip = await _fleetTrips.GetTripByIdAsync(fleetTripId)
                   ?? throw new InvalidOperationException("The leg's fleet trip is no longer in Fleet — cancel the leg and book again.");
        var change = reservation with
        {
            VehicleAssetId = reservation.VehicleAssetId ?? trip.VehicleAssetId,
            DriverEmployeeId = reservation.DriverEmployeeId ?? trip.DriverEmployeeId,
        };
        if (trip.Status is not (FleetTripStatuses.Draft or FleetTripStatuses.Rejected or FleetTripStatuses.Approved))
            throw new InvalidOperationException(
                $"The vehicle's trip is {trip.Status.ToLowerInvariant()} in Fleet" +
                (trip.Status == FleetTripStatuses.Submitted ? " — the transport office is deciding it" : string.Empty) +
                ", so it is not changed now. Cancel the leg and book again.");

        await RequireReservableAsync(change.VehicleAssetId!.Value, change, fleetTripId, cancellationToken);
        try
        {
            // Fleet itself holds an approved trip to its vehicle, dates and destination: only the driver changes.
            await _fleetTrips.UpdateTripAsync(fleetTripId, new UpdateFleetTripDto
            {
                VehicleAssetId = change.VehicleAssetId!.Value,
                DriverEmployeeId = change.DriverEmployeeId,
                Purpose = trip.Status == FleetTripStatuses.Approved ? trip.Purpose : change.Purpose,
                Origin = change.Origin,
                Destination = change.Destination,
                FleetTripDestinationId = change.DestinationId ?? trip.FleetTripDestinationId,
                Notes = change.Notes,
                PlannedStartAt = change.Start,
                PlannedEndAt = change.End,
            });
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"The vehicle's trip could not be changed: {ex.Message}");
        }
        if (trip.Status != FleetTripStatuses.Approved)
            await SubmitIfRoutedAsync(fleetTripId, cancellationToken);
    }

    public async Task CancelReservationAsync(Guid fleetTripId, string reason, CancellationToken cancellationToken = default)
    {
        var trip = await _fleetTrips.GetTripByIdAsync(fleetTripId);
        if (trip is null || trip.Status is FleetTripStatuses.Completed or FleetTripStatuses.Cancelled) return;
        if (trip.Status == FleetTripStatuses.Dispatched)
            throw new InvalidOperationException(
                "The vehicle is out on this trip — Fleet completes it when it is back; it is not cancelled now.");
        await _fleetTrips.CancelAsync(fleetTripId, reason);
    }

    public async Task<int> CancelForRequestAsync(Guid tenantId, Guid requestId, string reason, CancellationToken cancellationToken = default)
    {
        var legs = await _unitOfWork.Repository<StaffTravelGroundTransport>()
            .GetQueryable(g => g.TenantId == tenantId && g.StaffTravelRequestId == requestId && !g.IsDeleted && g.FleetTripId != null)
            .ToListAsync(cancellationToken);
        var count = 0;
        foreach (var leg in legs)
        {
            var trip = await _fleetTrips.GetTripByIdAsync(leg.FleetTripId!.Value);
            if (trip is null || trip.Status is FleetTripStatuses.Completed or FleetTripStatuses.Cancelled or FleetTripStatuses.Dispatched)
                continue;
            await _fleetTrips.CancelAsync(leg.FleetTripId.Value, reason.Length > 500 ? reason[..500] : reason);
            leg.Status = TravelBookingStatus.Cancelled;
            leg.UpdatedAt = DateTime.UtcNow;
            leg.UpdatedBy = _currentUserProvider.UserId.ToString();
            count++;
        }
        return count;
    }

    public async Task DescribeAsync(IReadOnlyCollection<StaffTravelGroundTransportDto> legs, CancellationToken cancellationToken = default)
    {
        var ids = legs.Where(l => l.FleetTripId is not null).Select(l => l.FleetTripId!.Value).Distinct().ToList();
        if (ids.Count == 0) return;
        var tenantId = TenantId;
        var trips = await _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(t => t.TenantId == tenantId && ids.Contains(t.Id))
            .Select(t => new
            {
                t.Id, t.Status, t.VehicleAssetId, VehicleName = t.VehicleAsset!.Name, Plate = t.VehicleAsset!.LicensePlate,
                t.DriverEmployeeId, DFirst = t.DriverEmployee!.FirstName, DLast = t.DriverEmployee!.LastName,
                t.RejectionReason, t.DispatchedAt, t.ActualStartAt, t.ActualEndAt, t.CompletedAt, t.StartMileage, t.EndMileage,
            })
            .ToListAsync(cancellationToken);
        var routed = await IsApprovalRoutePublishedAsync(tenantId, cancellationToken);

        foreach (var leg in legs.Where(l => l.FleetTripId is not null))
        {
            var t = trips.FirstOrDefault(x => x.Id == leg.FleetTripId);
            if (t is null)
            {
                leg.FleetNote = "The fleet trip is no longer in Fleet.";
                continue;
            }
            leg.VehicleAssetId = t.VehicleAssetId;
            leg.VehicleName = t.VehicleName;
            leg.VehiclePlate = t.Plate;
            leg.DriverEmployeeId = t.DriverEmployeeId;
            leg.DriverName = t.DriverEmployeeId is null ? null : NameOf(t.DFirst, t.DLast);
            leg.FleetStatus = t.Status;
            leg.FleetRejectionReason = t.RejectionReason;
            leg.DispatchedAt = t.DispatchedAt ?? t.ActualStartAt;
            leg.ReturnedAt = t.CompletedAt ?? t.ActualEndAt;
            leg.Distance = t.StartMileage is double s && t.EndMileage is double e && e >= s ? e - s : null;
            // A leg Fleet has not decided, cancelled by travel, stays cancelled.
            leg.Status = leg.Status == TravelBookingStatus.Cancelled ? TravelBookingStatus.Cancelled : LegStatus(t.Status);
            leg.FleetNote = t.Status switch
            {
                FleetTripStatuses.Draft when !routed =>
                    "Reserved as a draft in Fleet — Fleet publishes no approval route, so the vehicle is not held yet.",
                FleetTripStatuses.Draft => "A draft in Fleet, not yet submitted to the transport office.",
                FleetTripStatuses.Submitted => "Awaiting the transport office's approval in Fleet.",
                FleetTripStatuses.Rejected => $"Fleet rejected it{(string.IsNullOrWhiteSpace(t.RejectionReason) ? "." : $": {t.RejectionReason}")}",
                _ => null,
            };
        }

        await DescribeDriversAsync(tenantId, legs.Where(l => l.FleetTripId is not null).ToList(), cancellationToken);
    }

    /// <summary>
    /// D-33, D-34: the driver's own request the leg keeps, and whether the leg keeps a driver other than the traveller away
    /// overnight — a drop-off on a later day than the pick-up, or a trip whose destination is outside its origin city.
    /// </summary>
    private async Task DescribeDriversAsync(Guid tenantId, List<StaffTravelGroundTransportDto> legs, CancellationToken cancellationToken)
    {
        if (legs.Count == 0) return;
        var requestIds = legs.Select(l => l.StaffTravelRequestId)
            .Concat(legs.Where(l => l.DriverTravelRequestId is not null).Select(l => l.DriverTravelRequestId!.Value))
            .Distinct().ToList();
        var requests = await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(r => r.TenantId == tenantId && requestIds.Contains(r.Id))
            .Select(r => new { r.Id, r.RequestNumber, r.Status, r.EmployeeId, r.OriginCity, r.DestinationCity, r.IsDeleted })
            .ToListAsync(cancellationToken);
        foreach (var leg in legs)
        {
            if (leg.DriverTravelRequestId is Guid driverRequestId
                && requests.FirstOrDefault(r => r.Id == driverRequestId && !r.IsDeleted) is { } own)
            {
                leg.DriverTravelRequestNumber = own.RequestNumber;
                leg.DriverTravelRequestStatus = own.Status.ToString();
            }
            var trip = requests.FirstOrDefault(r => r.Id == leg.StaffTravelRequestId);
            leg.DriverAwayOvernight = trip is not null
                && leg.Status != TravelBookingStatus.Cancelled
                && leg.DriverEmployeeId is Guid driver && driver != trip.EmployeeId
                && ((leg.PickupDatetime is DateTime from && leg.DropoffDatetime is DateTime to && to.Date > from.Date)
                    || !string.Equals(trip.OriginCity?.Trim(), trip.DestinationCity?.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    // ---- Slice 6c: incidents (D-29, FX-8's read half) ------------------------------------------------------------

    public async Task<IReadOnlyList<StaffTravelFleetIncidentDto>> GetIncidentsAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var tripIds = await _unitOfWork.Repository<StaffTravelGroundTransport>()
            .GetQueryable(g => g.TenantId == tenantId && g.StaffTravelRequestId == requestId && !g.IsDeleted && g.FleetTripId != null)
            .Select(g => g.FleetTripId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (tripIds.Count == 0) return Array.Empty<StaffTravelFleetIncidentDto>();
        return await _unitOfWork.Repository<FleetIncident>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted && i.FleetTripId != null && tripIds.Contains(i.FleetTripId.Value))
            .OrderByDescending(i => i.OccurredAtUtc)
            .Select(i => new StaffTravelFleetIncidentDto
            {
                Id = i.Id, FleetTripId = i.FleetTripId, VehicleName = i.VehicleAsset.Name, VehiclePlate = i.VehicleAsset.LicensePlate,
                DriverName = i.DriverEmployee == null ? null : (i.DriverEmployee.FirstName + " " + i.DriverEmployee.LastName).Trim(),
                OccurredAtUtc = i.OccurredAtUtc, IncidentType = i.IncidentType, Title = i.Title, Description = i.Description,
                Location = i.Location, Severity = i.Severity, Status = i.Status,
            })
            .ToListAsync(cancellationToken);
    }

    // ---- Lane 8, slice 8c: the sweep's signals (D-29) — tenant-explicit ---------------------------------------------

    public async Task<StaffTravelFleetSignals> GetSweepSignalsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> requestIds, DateTime incidentsSince, CancellationToken cancellationToken = default)
    {
        if (requestIds.Count == 0)
            return new StaffTravelFleetSignals(Array.Empty<StaffTravelFleetTripSignal>(), Array.Empty<StaffTravelFleetIncidentSignal>());
        var ids = requestIds.Distinct().ToList();
        var legs = await _unitOfWork.Repository<StaffTravelGroundTransport>()
            .GetQueryable(g => g.TenantId == tenantId && ids.Contains(g.StaffTravelRequestId) && !g.IsDeleted && g.FleetTripId != null
                            && g.Status != TravelBookingStatus.Cancelled)
            .Select(g => new { g.StaffTravelRequestId, FleetTripId = g.FleetTripId!.Value })
            .ToListAsync(cancellationToken);
        if (legs.Count == 0)
            return new StaffTravelFleetSignals(Array.Empty<StaffTravelFleetTripSignal>(), Array.Empty<StaffTravelFleetIncidentSignal>());
        var fleetIds = legs.Select(l => l.FleetTripId).Distinct().ToList();
        var trips = await _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(t => t.TenantId == tenantId && fleetIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Status, t.DispatchedAt, t.ActualStartAt, t.CompletedAt, t.ActualEndAt })
            .ToListAsync(cancellationToken);
        var incidents = await _unitOfWork.Repository<FleetIncident>()
            .GetQueryable(i => i.TenantId == tenantId && !i.IsDeleted && i.FleetTripId != null && fleetIds.Contains(i.FleetTripId.Value)
                            && i.OccurredAtUtc >= incidentsSince)
            .Select(i => new
            {
                i.Id, FleetTripId = i.FleetTripId!.Value, i.OccurredAtUtc, i.IncidentType, i.Title, i.Severity,
                Vehicle = i.VehicleAsset.Name, Plate = i.VehicleAsset.LicensePlate,
            })
            .ToListAsync(cancellationToken);

        var tripSignals = legs
            .Join(trips, l => l.FleetTripId, t => t.Id, (l, t) => new StaffTravelFleetTripSignal(
                l.StaffTravelRequestId, t.Id, t.Status, t.DispatchedAt ?? t.ActualStartAt, t.CompletedAt ?? t.ActualEndAt))
            .ToList();
        var incidentSignals = legs
            .Join(incidents, l => l.FleetTripId, i => i.FleetTripId, (l, i) => new StaffTravelFleetIncidentSignal(
                l.StaffTravelRequestId, i.Id, i.OccurredAtUtc, i.IncidentType, i.Title, i.Severity,
                string.IsNullOrWhiteSpace(i.Plate) ? i.Vehicle : $"{i.Vehicle} ({i.Plate})"))
            .GroupBy(s => s.IncidentId).Select(g => g.First())
            .ToList();
        return new StaffTravelFleetSignals(tripSignals, incidentSignals);
    }

    // ---- Slice 6b: fuel on claims (FX-6, D-30, D-31, D-32) ----------------------------------------------------------

    private sealed record FuelTrip(
        Guid Id, Guid VehicleAssetId, string VehicleName, string? Plate, string Status, DateTime? PlannedStartAt, DateTime? PlannedEndAt)
    {
        /// <summary>Fuel is claimed only for a trip the vehicle can have made.</summary>
        public bool Live => Status is not (FleetTripStatuses.Cancelled or FleetTripStatuses.Rejected);
        public string Label => string.IsNullOrWhiteSpace(Plate) ? VehicleName : $"{VehicleName} ({Plate})";
    }

    /// <summary>The fleet trips the request's company-vehicle legs reserved, whatever their state.</summary>
    private async Task<List<FuelTrip>> RequestFleetTripsAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken)
    {
        var ids = await _unitOfWork.Repository<StaffTravelGroundTransport>()
            .GetQueryable(g => g.TenantId == tenantId && g.StaffTravelRequestId == requestId && !g.IsDeleted && g.FleetTripId != null)
            .Select(g => g.FleetTripId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (ids.Count == 0) return new List<FuelTrip>();
        return await _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && ids.Contains(t.Id))
            .OrderBy(t => t.PlannedStartAt)
            .Select(t => new FuelTrip(t.Id, t.VehicleAssetId, t.VehicleAsset!.Name, t.VehicleAsset!.LicensePlate, t.Status,
                t.PlannedStartAt, t.PlannedEndAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>A hired car the traveller had — fuel may have been for it rather than the company vehicle (D-30).</summary>
    private Task<bool> HasCarRentalAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken)
        => _unitOfWork.Repository<StaffTravelCarRentalBooking>()
            .GetQueryable(c => c.TenantId == tenantId && c.StaffTravelRequestId == requestId && !c.IsDeleted
                            && c.Status != TravelBookingStatus.Cancelled && c.Status != TravelBookingStatus.Refunded
                            && c.Status != TravelBookingStatus.NoShow)
            .AnyAsync(cancellationToken);

    public async Task<StaffTravelFleetFuelOptionsDto> GetFuelOptionsAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var trips = await RequestFleetTripsAsync(tenantId, requestId, cancellationToken);
        var dto = new StaffTravelFleetFuelOptionsDto { HasCarRental = await HasCarRentalAsync(tenantId, requestId, cancellationToken) };
        dto.FuelNamesTrip = trips.Any(t => t.Live) && !dto.HasCarRental;
        if (trips.Count == 0) return dto;

        var ids = trips.Select(t => t.Id).ToList();
        var fuel = await _unitOfWork.Repository<FleetFuelTransaction>()
            .GetQueryable(f => f.TenantId == tenantId && !f.IsDeleted && f.FleetTripId != null && ids.Contains(f.FleetTripId.Value))
            .OrderBy(f => f.FuelledAt)
            .Select(f => new { f.Id, TripId = f.FleetTripId!.Value, f.FuelledAt, f.Quantity, f.Unit, f.TotalCost, f.VendorName })
            .ToListAsync(cancellationToken);
        var fuelIds = fuel.Select(f => f.Id).ToList();
        var fromClaims = fuelIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _unitOfWork.Repository<StaffTravelExpenseClaimLine>()
                    .GetQueryable(l => l.TenantId == tenantId && !l.IsDeleted && l.FleetFuelTransactionId != null
                                    && fuelIds.Contains(l.FleetFuelTransactionId.Value))
                    .Select(l => new { Id = l.FleetFuelTransactionId!.Value, l.ExpenseClaim.ClaimNumber })
                    .ToListAsync(cancellationToken))
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().ClaimNumber);

        foreach (var t in trips)
            dto.Trips.Add(new StaffTravelFleetFuelTripDto
            {
                FleetTripId = t.Id, VehicleName = t.VehicleName, VehiclePlate = t.Plate, PlannedStartAt = t.PlannedStartAt,
                PlannedEndAt = t.PlannedEndAt, Status = t.Status, Live = t.Live,
                Fuel = fuel.Where(f => f.TripId == t.Id)
                    .Select(f => new StaffTravelFleetFuelEntryDto
                    {
                        Id = f.Id, FuelledAt = f.FuelledAt, Quantity = f.Quantity, Unit = f.Unit, TotalCost = f.TotalCost,
                        VendorName = f.VendorName, ClaimNumber = fromClaims.GetValueOrDefault(f.Id),
                    })
                    .ToList(),
            });
        return dto;
    }

    public async Task<string?> CheckFuelLineAsync(
        StaffTravelRequest request, TravelExpenseCategory category, DateOnly expenseDate, Guid? fleetTripId, decimal? litres,
        string? duplicateReason, bool askAboutDuplicates, CancellationToken cancellationToken = default)
    {
        if (category != TravelExpenseCategory.Fuel)
        {
            if (fleetTripId is not null || litres is not null)
                throw new InvalidOperationException("Only a fuel expense names a company vehicle's trip and the litres bought.");
            return null;
        }

        var tenantId = TenantId;
        var trips = await RequestFleetTripsAsync(tenantId, request.Id, cancellationToken);
        if (fleetTripId is not Guid tripId)
        {
            if (litres is not null)
                throw new InvalidOperationException(
                    "Litres are kept for fuel bought for a company vehicle — name the vehicle's trip, or leave the litres out.");
            // D-30: with a company vehicle on the trip and no car hired, the fuel was the company vehicle's.
            var live = trips.Where(t => t.Live).ToList();
            if (live.Count > 0 && !await HasCarRentalAsync(tenantId, request.Id, cancellationToken))
                throw new InvalidOperationException(
                    $"Travel request {request.RequestNumber} travels by company vehicle ({string.Join(", ", live.Select(t => t.Label))}) " +
                    "and hires no car, so its fuel was the company vehicle's — name the vehicle's trip and the litres; Fleet's fuel " +
                    "log takes them when the claim is paid.");
            return null;
        }

        var trip = trips.FirstOrDefault(t => t.Id == tripId)
                   ?? throw new ArgumentException(
                       $"Fleet trip '{tripId}' is not one of travel request {request.RequestNumber}'s company vehicles.");
        if (!trip.Live)
            throw new InvalidOperationException(
                $"The {trip.Label} trip is {trip.Status.ToLowerInvariant()} in Fleet — fuel is claimed for a trip the vehicle made.");
        if (litres is not decimal quantity || quantity < 0.01m)
            throw new InvalidOperationException("Give the litres bought — Fleet's fuel log keeps them with the cost.");

        // S4: the fill falls within the vehicle's trip, a day either side — as a booking falls within the travel.
        if (trip.PlannedStartAt is DateTime from && trip.PlannedEndAt is DateTime to
            && (expenseDate < DateOnly.FromDateTime(from).AddDays(-1) || expenseDate > DateOnly.FromDateTime(to).AddDays(1)))
            throw new InvalidOperationException(
                $"Fuel on {expenseDate:d MMM yyyy} falls outside the {trip.Label} trip ({Window(from, to)}), allowing the day " +
                "before and the day after.");

        if (!askAboutDuplicates) return null;
        // D-32: the driver may have logged this fill in Fleet already — paid, the claim would log it a second time.
        var dayStart = DateTime.SpecifyKind(expenseDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        var sameDay = await _unitOfWork.Repository<FleetFuelTransaction>()
            .GetQueryable(f => f.TenantId == tenantId && !f.IsDeleted && f.FleetTripId == tripId
                            && f.FuelledAt >= dayStart && f.FuelledAt < dayEnd)
            .Select(f => new { f.Quantity, f.Unit, f.TotalCost })
            .ToListAsync(cancellationToken);
        if (sameDay.Count == 0) return null;
        var logged = string.Join("; ", sameDay.Select(f =>
            $"{f.Quantity:0.##} {f.Unit}" + (f.TotalCost is decimal cost ? $" for {cost:N2}" : string.Empty)));
        var why = duplicateReason?.Trim();
        if (string.IsNullOrEmpty(why) || why.Length < 5)
            throw new InvalidOperationException(
                $"Fleet already logs fuel for the {trip.Label} on {expenseDate:d MMM yyyy} ({logged}). If this is another fill, " +
                "say why it is claimed too — paid, it is logged in Fleet a second time.");
        return $"Fuel for the {trip.Label} on {expenseDate:d MMM yyyy} claimed although Fleet already logs fuel for that trip " +
               $"that day ({logged}): {why}";
    }

    public async Task<Guid> RecordClaimFuelAsync(
        Guid fleetTripId, DateOnly fuelledOn, decimal litres, decimal amountPaid, string? merchant, string reference, string notes,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var vehicleAssetId = await _unitOfWork.Repository<FleetTrip>()
            .GetQueryable(t => t.TenantId == tenantId && t.Id == fleetTripId && !t.IsDeleted)
            .Select(t => (Guid?)t.VehicleAssetId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "A fuel expense's fleet trip is no longer in Fleet, so the fill cannot be logged there — return the claim and " +
                "correct the expense.");
        try
        {
            var fuel = await _fuel.CreateAsync(new CreateFleetFuelTransactionDto
            {
                VehicleAssetId = vehicleAssetId,
                FleetTripId = fleetTripId,
                // Noon, in UTC: Fleet converts the time to UTC, and a local midnight could land on the day before.
                FuelledAt = DateTime.SpecifyKind(fuelledOn.ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Utc),
                Quantity = litres,
                Unit = "L",
                // D-31: Fleet works its total out as litres × unit cost, so the paid amount goes in as a unit cost.
                UnitCost = decimal.Round(amountPaid / litres, 4, MidpointRounding.AwayFromZero),
                VendorName = Clip(merchant, 200),
                ReceiptReference = Clip(reference, 500),
                Notes = Clip(notes, 2000),
            });
            return fuel.Id;
        }
        catch (ArgumentException ex)
        {
            // Fleet's validation speaks ArgumentException, which travel's contract reads as "not found" (404).
            throw new InvalidOperationException($"Fleet could not log the fuel: {ex.Message}");
        }
    }

    public async Task RemoveClaimFuelAsync(Guid fleetFuelTransactionId, CancellationToken cancellationToken = default)
        => await _fuel.DeleteAsync(fleetFuelTransactionId);   // with its cost entry; already gone is nothing to do

    private static string? Clip(string? text, int max)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim().Length > max ? text.Trim()[..max] : text.Trim();
}
