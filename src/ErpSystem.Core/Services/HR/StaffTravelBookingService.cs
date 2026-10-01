using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Entities.HR.StaffTravel;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 4: BOOKINGS SERVICE
// ============================================================================

#region Staff Travel Booking Service

public class StaffTravelBookingService : IStaffTravelBookingService
{
    private readonly IStaffTravelFlightBookingRepository _flightRepository;
    private readonly IStaffTravelFlightSegmentRepository _segmentRepository;
    private readonly IStaffTravelHotelBookingRepository _hotelRepository;
    private readonly IStaffTravelGroundTransportRepository _groundRepository;
    private readonly IStaffTravelCarRentalBookingRepository _carRentalRepository;
    private readonly IStaffTravelRequestRepository _requestRepository;
    private readonly IFleetTripService _fleetTrips;
    private readonly HrCurrencyBridge _currency;
    private readonly StaffTravelPolicyGuard _policyGuard;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelBookingService> _logger;

    public StaffTravelBookingService(
        IStaffTravelFlightBookingRepository flightRepository,
        IStaffTravelFlightSegmentRepository segmentRepository,
        IStaffTravelHotelBookingRepository hotelRepository,
        IStaffTravelGroundTransportRepository groundRepository,
        IStaffTravelCarRentalBookingRepository carRentalRepository,
        IStaffTravelRequestRepository requestRepository,
        IFleetTripService fleetTrips,
        HrCurrencyBridge currency,
        StaffTravelPolicyGuard policyGuard,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelBookingService> logger)
    {
        _policyGuard = policyGuard;
        _flightRepository = flightRepository;
        _segmentRepository = segmentRepository;
        _hotelRepository = hotelRepository;
        _groundRepository = groundRepository;
        _carRentalRepository = carRentalRepository;
        _requestRepository = requestRepository;
        _fleetTrips = fleetTrips;
        _currency = currency;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    /// <summary>
    /// Confirms the travel request exists in the caller's tenant before anything is hung off it.
    /// The four top-level booking creates took StaffTravelRequestId straight from the payload, so a
    /// flight could be booked against any request id at all — including another tenant's.
    /// </summary>
    private async Task<StaffTravelRequest> RequireOwnedRequestAsync(Guid requestId)
    {
        var request = await _requestRepository.GetByIdAsync(requestId);
        if (request == null || request.TenantId != GetTenantId())
            throw new ArgumentException($"Staff travel request with ID '{requestId}' not found.");
        return request;
    }

    /// <summary>
    /// Stamps <c>BookedAt</c> / <c>CancelledAt</c> from the status the booking has actually reached.
    /// </summary>
    /// <remarks>
    /// Both were caller-declared fields on the update DTOs — the same fiction shape as F-09's
    /// <c>NotificationSentAt</c>, where the caller asserted that something happened and nothing
    /// checked. A booking is booked when it is confirmed or ticketed, and cancelled when it is
    /// cancelled; neither is an opinion the client is entitled to. Idempotent: the first transition
    /// wins, so re-saving a confirmed booking does not move its booking date.
    /// </remarks>
    private static void StampBookingTimestamps(
        TravelBookingStatus status, ref DateTime? bookedAt, ref DateTime? cancelledAt)
    {
        var now = DateTime.UtcNow;

        if (bookedAt is null && status is TravelBookingStatus.Confirmed
                or TravelBookingStatus.Ticketed or TravelBookingStatus.Completed)
            bookedAt = now;

        if (cancelledAt is null && status is TravelBookingStatus.Cancelled
                or TravelBookingStatus.Refunded)
            cancelledAt = now;
    }

    private async Task<StaffTravelFlightBooking> GetOwnedFlightAsync(Guid id)
    {
        var entity = await _flightRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Flight booking with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelFlightSegment> GetOwnedSegmentAsync(Guid id)
    {
        var entity = await _segmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Flight segment with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelHotelBooking> GetOwnedHotelAsync(Guid id)
    {
        var entity = await _hotelRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Hotel booking with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelGroundTransport> GetOwnedGroundTransportAsync(Guid id)
    {
        var entity = await _groundRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Ground transport with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelCarRentalBooking> GetOwnedCarRentalAsync(Guid id)
    {
        var entity = await _carRentalRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Car rental booking with ID '{id}' not found.");
        return entity;
    }

    // ---- Flight bookings ---------------------------------------------------

    public async Task<StaffTravelFlightBookingDto> GetFlightByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _flightRepository.GetWithSegmentsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Flight booking with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelFlightBookingSummaryDto>> GetFlightsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _flightRepository.GetByRequestIdAsync(requestId))
            .Where(f => f.TenantId == tenantId)
            .Select(f => f.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelFlightBookingSummaryDto>> GetFlightsByStatusAsync(TravelBookingStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _flightRepository.GetByStatusAsync(status))
            .Where(f => f.TenantId == tenantId)
            .Select(f => f.ToSummaryDto())
            .ToList();
    }

    /// <summary>
    /// Applies the travel policy's cabin-class cap to a flight booking.
    /// </summary>
    /// <remarks>
    /// <c>PolicyAllowedClass</c> is written from the resolved policy, never from the payload — the
    /// caller declaring what the policy permits is the whole defect. With no policy in force the
    /// cap is recorded as the class actually booked, which is honest: nothing constrained it.
    /// </remarks>
    private async Task ApplyFlightPolicyAsync(
        StaffTravelFlightBooking entity,
        StaffTravelRequest request,
        bool exceptionRequested,
        bool callerMayApproveExceptions,
        CancellationToken cancellationToken)
    {
        var caps = await _policyGuard.ResolveAsync(request, cancellationToken);

        entity.ClassExceptionApproved = _policyGuard.RequireFlightClassWithinPolicy(
            entity.BookingClass, caps, exceptionRequested, callerMayApproveExceptions);

        entity.PolicyAllowedClass = caps.MaxFlightClass ?? entity.BookingClass;

        if (entity.ClassExceptionApproved && string.IsNullOrWhiteSpace(entity.ClassExceptionReason))
            throw new InvalidOperationException(
                "A booking above the policy cap must record why the exception was granted.");
    }

    public async Task<StaffTravelFlightBookingDto> CreateFlightAsync(CreateStaffTravelFlightBookingDto createDto, Guid tenantId, Guid createdByUserId, bool callerMayApproveExceptions = false, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await ApplyFlightPolicyAsync(
            entity, request, createDto.ClassExceptionApproved, callerMayApproveExceptions, cancellationToken);

        var bookedAt = entity.BookedAt;
        var cancelledAt = entity.CancelledAt;
        StampBookingTimestamps(entity.Status, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;
        entity.CancelledAt = cancelledAt;

        await _flightRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _flightRepository.GetWithDetailsAsync(tenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffTravelFlightBookingDto> UpdateFlightAsync(UpdateStaffTravelFlightBookingDto updateDto, Guid updatedByUserId, bool callerMayApproveExceptions = false, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFlightAsync(updateDto.Id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);

        // ⚠ Captured BEFORE the mapper runs. `UpdateEntity` still copies BookedAt/CancelledAt off
        // the DTO, so re-reading them afterwards would read the client's value back — the stored
        // ones are the only truthful starting point.
        var bookedAt = entity.BookedAt;
        var cancelledAt = entity.CancelledAt;

        entity.UpdateEntity(updateDto, updatedByUserId);
        await ApplyFlightPolicyAsync(
            entity, request, updateDto.ClassExceptionApproved, callerMayApproveExceptions, cancellationToken);

        StampBookingTimestamps(entity.Status, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;
        entity.CancelledAt = cancelledAt;

        await _flightRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _flightRepository.GetWithSegmentsAsync(entity.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Flight booking with ID '{entity.Id}' not found.");
        return refreshed.ToDto();
    }

    public async Task<bool> DeleteFlightAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFlightAsync(id);
        await _flightRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Flight segments ---------------------------------------------------

    public async Task<StaffTravelFlightSegmentDto> AddSegmentAsync(CreateStaffTravelFlightSegmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedFlightAsync(createDto.StaffTravelFlightBookingId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        ApplySegmentDerivations(entity);
        await _segmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    /// <summary>
    /// Derives a flight segment's duration from its two datetimes.
    /// </summary>
    /// <remarks>
    /// <c>DurationMinutes</c> was caller-declared beside the departure and arrival that define it,
    /// so a segment could claim any length at all — and the itinerary reads it. Both datetimes are
    /// stored as given: they are local to their own airports, so an arrival earlier than the
    /// departure is a legitimate westward crossing, not an error, and only a duration computed from
    /// UTC would be meaningful. That is why this clamps at zero rather than refusing — an honest
    /// zero is better than a fabricated number, and the fix is to carry the offsets.
    /// </remarks>
    private static void ApplySegmentDerivations(StaffTravelFlightSegment entity)
    {
        var minutes = (entity.ArrivalDatetime - entity.DepartureDatetime).TotalMinutes;
        entity.DurationMinutes = minutes > 0 ? (int)Math.Round(minutes) : 0;
    }

    public async Task<IEnumerable<StaffTravelFlightSegmentDto>> GetSegmentsAsync(Guid flightBookingId, CancellationToken cancellationToken = default)
    {
        await GetOwnedFlightAsync(flightBookingId);
        var tenantId = GetTenantId();
        return (await _segmentRepository.GetByBookingIdAsync(flightBookingId))
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.ToDto())
            .ToList();
    }

    public async Task<StaffTravelFlightSegmentDto> UpdateSegmentAsync(UpdateStaffTravelFlightSegmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSegmentAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        ApplySegmentDerivations(entity);
        await _segmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteSegmentAsync(Guid segmentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSegmentAsync(segmentId);
        await _segmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Hotel bookings ----------------------------------------------------

    public async Task<StaffTravelHotelBookingDto> GetHotelByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _hotelRepository.GetWithDetailsAsync(GetTenantId(), id)
            ?? throw new ArgumentException($"Hotel booking with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelHotelBookingSummaryDto>> GetHotelsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _hotelRepository.GetByRequestIdAsync(requestId))
            .Where(h => h.TenantId == tenantId)
            .Select(h => h.ToSummaryDto())
            .ToList();
    }

    /// <summary>
    /// Derives a hotel stay's length and cost, and applies the policy's nightly-rate cap.
    /// </summary>
    /// <remarks>
    /// <c>NumberOfNights</c> and <c>TotalCost</c> were caller-declared alongside the two dates and
    /// the nightly rate that determine them — so a three-night stay could be recorded as one night,
    /// or a 500-a-night room as costing 100 in total, and every report downstream would believe it.
    /// Nothing on this record needs the client's arithmetic.
    /// </remarks>
    private async Task ApplyHotelDerivationsAsync(
        StaffTravelHotelBooking entity,
        StaffTravelRequest request,
        bool exceptionRequested,
        bool callerMayApproveExceptions,
        CancellationToken cancellationToken)
    {
        if (entity.CheckOutDate < entity.CheckInDate)
            throw new InvalidOperationException("The check-out date cannot be before the check-in date.");

        entity.NumberOfNights = entity.CheckOutDate.DayNumber - entity.CheckInDate.DayNumber;
        entity.TotalCost = entity.RatePerNight * entity.NumberOfNights;

        var caps = await _policyGuard.ResolveAsync(request, cancellationToken);

        entity.RateExceptionApproved = _policyGuard.RequireHotelRateWithinPolicy(
            entity.RatePerNight, caps, exceptionRequested, callerMayApproveExceptions);

        entity.PolicyMaxRatePerNight = caps.MaxHotelRatePerNight;

        if (entity.RateExceptionApproved && string.IsNullOrWhiteSpace(entity.RateExceptionReason))
            throw new InvalidOperationException(
                "A booking above the policy rate cap must record why the exception was granted.");
    }

    public async Task<StaffTravelHotelBookingDto> CreateHotelAsync(CreateStaffTravelHotelBookingDto createDto, Guid tenantId, Guid createdByUserId, bool callerMayApproveExceptions = false, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await ApplyHotelDerivationsAsync(
            entity, request, createDto.RateExceptionApproved, callerMayApproveExceptions, cancellationToken);

        var bookedAt = entity.BookedAt;
        var cancelledAt = entity.CancelledAt;
        StampBookingTimestamps(entity.Status, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;
        entity.CancelledAt = cancelledAt;

        await _hotelRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _hotelRepository.GetWithDetailsAsync(tenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffTravelHotelBookingDto> UpdateHotelAsync(UpdateStaffTravelHotelBookingDto updateDto, Guid updatedByUserId, bool callerMayApproveExceptions = false, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHotelAsync(updateDto.Id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);

        // Captured before the mapper — see UpdateFlightAsync.
        var bookedAt = entity.BookedAt;
        var cancelledAt = entity.CancelledAt;

        entity.UpdateEntity(updateDto, updatedByUserId);
        await ApplyHotelDerivationsAsync(
            entity, request, updateDto.RateExceptionApproved, callerMayApproveExceptions, cancellationToken);

        StampBookingTimestamps(entity.Status, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;
        entity.CancelledAt = cancelledAt;

        await _hotelRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _hotelRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeleteHotelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHotelAsync(id);
        await _hotelRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Ground transport --------------------------------------------------

    public async Task<StaffTravelGroundTransportDto> GetGroundTransportByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroundTransportAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelGroundTransportDto>> GetGroundTransportsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _groundRepository.GetByRequestIdAsync(requestId))
            .Where(g => g.TenantId == tenantId)
            .Select(g => g.ToDto())
            .ToList();
    }

    public async Task<StaffTravelGroundTransportDto> CreateGroundTransportAsync(CreateStaffTravelGroundTransportDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        // A company vehicle is a real, finite resource — reserve it in Fleet rather than writing a
        // note here. Every other mode is somebody else's vehicle and stays travel-owned.
        if (createDto.TransportType == GroundTransportType.CompanyVehicle)
        {
            if (createDto.VehicleAssetId is not Guid vehicleAssetId || vehicleAssetId == Guid.Empty)
                throw new InvalidOperationException(
                    "A company-vehicle leg must name the vehicle to reserve. Choose a vehicle, or pick a different transport type.");

            var request = await _requestRepository.GetByIdAsync(createDto.StaffTravelRequestId);

            FleetTripDto trip;
            try
            {
                trip = await _fleetTrips.CreateTripAsync(new CreateFleetTripDto
                {
                    VehicleAssetId = vehicleAssetId,
                    DriverEmployeeId = createDto.DriverEmployeeId,
                    Purpose = $"Staff travel {request?.RequestNumber}".Trim(),
                    Origin = createDto.PickupLocation,
                    Destination = createDto.DropoffLocation,
                    PlannedStartAt = createDto.PickupDatetime,
                    PlannedEndAt = createDto.DropoffDatetime,
                    Notes = createDto.Notes,
                });
            }
            catch (ArgumentException ex)
            {
                // Fleet uses ArgumentException for VALIDATION failures — "Vehicle not found",
                // "Selected asset is not a vehicle". Travel's error contract reads ArgumentException
                // as "the travel record does not exist" and answers 404, so letting Fleet's
                // exception through told the caller their travel request was missing when in fact
                // their vehicle choice was wrong. Translate at the seam: another module's exception
                // vocabulary must not leak into this one's contract.
                throw new InvalidOperationException($"The vehicle could not be reserved: {ex.Message}");
            }

            entity.FleetTripId = trip.Id;
        }
        await _groundRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _groundRepository.GetWithDetailsAsync(tenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffTravelGroundTransportDto> UpdateGroundTransportAsync(UpdateStaffTravelGroundTransportDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroundTransportAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _groundRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _groundRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeleteGroundTransportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroundTransportAsync(id);
        await _groundRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Car rental bookings -----------------------------------------------

    public async Task<StaffTravelCarRentalBookingDto> GetCarRentalByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCarRentalAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelCarRentalBookingDto>> GetCarRentalsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _carRentalRepository.GetByRequestIdAsync(requestId))
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.ToDto())
            .ToList();
    }

    public async Task<StaffTravelCarRentalBookingDto> CreateCarRentalAsync(CreateStaffTravelCarRentalBookingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        ApplyCarRentalDerivations(entity);
        await _carRentalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _carRentalRepository.GetWithDetailsAsync(tenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    /// <summary>
    /// Derives a car rental's total from its daily rate and the hire period.
    /// </summary>
    /// <remarks>
    /// <c>TotalCost</c> was caller-declared beside the rate and the two datetimes that determine it.
    /// A part-day counts as a day, which is how hire is charged; a same-day return is one day, not
    /// zero. The rental has no policy cap on the policy entity, so unlike hotels there is nothing
    /// to enforce here — only arithmetic to stop trusting the client for.
    /// </remarks>
    private static void ApplyCarRentalDerivations(StaffTravelCarRentalBooking entity)
    {
        if (entity.DropoffDatetime < entity.PickupDatetime)
            throw new InvalidOperationException("The drop-off cannot be before the pick-up.");

        var span = entity.DropoffDatetime - entity.PickupDatetime;
        var days = Math.Max(1, (int)Math.Ceiling(span.TotalDays));
        entity.TotalCost = entity.DailyRate * days;
    }

    public async Task<StaffTravelCarRentalBookingDto> UpdateCarRentalAsync(UpdateStaffTravelCarRentalBookingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCarRentalAsync(updateDto.Id);

        // Captured before the mapper — see UpdateFlightAsync. A car rental records no CancelledAt.
        var bookedAt = entity.BookedAt;
        DateTime? cancelledAt = null;

        entity.UpdateEntity(updateDto, updatedByUserId);
        ApplyCarRentalDerivations(entity);

        StampBookingTimestamps(entity.Status, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;

        await _carRentalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _carRentalRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeleteCarRentalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCarRentalAsync(id);
        await _carRentalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
