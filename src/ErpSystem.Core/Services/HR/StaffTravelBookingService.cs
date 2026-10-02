using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Entities.HR.StaffTravel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FleetStatus = ErpSystem.Core.Entities.Maintenance.FleetTripStatuses;

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
    private readonly IStaffTravelFleetService _fleet;
    private readonly IStaffTravelRequestService _requests;
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
        IStaffTravelFleetService fleet,
        IStaffTravelRequestService requests,
        HrCurrencyBridge currency,
        StaffTravelPolicyGuard policyGuard,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelBookingService> logger)
    {
        _policyGuard = policyGuard;
        _requests = requests;
        _flightRepository = flightRepository;
        _segmentRepository = segmentRepository;
        _hotelRepository = hotelRepository;
        _groundRepository = groundRepository;
        _carRentalRepository = carRentalRepository;
        _requestRepository = requestRepository;
        _fleet = fleet;
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
        return await WithExceptionNamesAsync(entity.ToDto(), cancellationToken);
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
    /// Applies the travel policy to a flight booking: its cabin-class cap, the notice it asks for (D-1), its
    /// preferred-vendor rule (D-1), and — for a booking that breaches either cap — the exception's state (D-8).
    /// </summary>
    /// <remarks>
    /// <c>PolicyAllowedClass</c> is written from the resolved policy, never from the payload — the
    /// caller declaring what the policy permits is the whole defect. With no policy in force the
    /// cap is recorded as the class actually booked, which is honest: nothing constrained it.
    /// The notice is counted from the day the booking was made to the trip's departure, so it does not change when
    /// the booking is edited later.
    /// </remarks>
    private async Task ApplyFlightPolicyAsync(
        StaffTravelFlightBooking entity,
        StaffTravelRequest request,
        bool exceptionRequested,
        Guid? actorEmployeeId,
        bool factsChanged,
        bool vendorNewlyNamed,
        CancellationToken cancellationToken)
    {
        // P1 (lane 4): the booked class is a bare enum too — omitted, it was stored as 0 and sat under every cap.
        if (!Enum.IsDefined(entity.BookingClass))
            throw new InvalidOperationException(
                "Choose the cabin class booked — Economy, Premium Economy, Business or First.");

        var caps = await _policyGuard.ResolveAsync(request, cancellationToken);
        await RequireVendorAsync(entity.VendorId, caps, "flight", vendorNewlyNamed, cancellationToken);

        var breaches = new List<string>();
        if (StaffTravelPolicyGuard.FlightClassBreach(entity.BookingClass, caps) is string classBreach)
            breaches.Add(classBreach);
        var bookedOn = DateOnly.FromDateTime(entity.CreatedAt == default ? DateTime.UtcNow : entity.CreatedAt);
        if (StaffTravelPolicyGuard.AdvanceBookingBreach(
                request.TravelStartDate.DayNumber - bookedOn.DayNumber, caps.AdvanceBookingDaysFlight, "flight", caps)
            is string noticeBreach)
            breaches.Add(noticeBreach);

        entity.PolicyAllowedClass = caps.MaxFlightClass ?? entity.BookingClass;

        var state = DecideExceptionState(
            breaches, exceptionRequested, entity.ClassExceptionReason, entity.ExceptionState, factsChanged, entity.Status, "flight");
        ApplyExceptionState(state, entity.ExceptionState, actorEmployeeId,
            s => entity.ExceptionState = s,
            id => entity.ExceptionRequestedById = id,
            (by, at) => { entity.ExceptionAuthorisedById = by; entity.ExceptionAuthorisedAt = at; });
        entity.ClassExceptionApproved = state == TravelBookingExceptionState.Authorised;
        if (state == TravelBookingExceptionState.None) entity.ClassExceptionReason = null;
    }

    /// <summary>Books a flight on an approved trip (lane 5, D-23), Pending — the verbs move it from there.</summary>
    public async Task<StaffTravelFlightBookingDto> CreateFlightAsync(CreateStaffTravelFlightBookingDto createDto, Guid tenantId, Guid createdByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        StaffTravelBookingRules.RequireBookable(request, "a flight booking");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.ExceptionState = TravelBookingExceptionState.None;
        await ApplyFlightPolicyAsync(
            entity, request, createDto.ClassExceptionApproved, actorEmployeeId, factsChanged: true, vendorNewlyNamed: true,
            cancellationToken);

        await _flightRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _flightRepository.GetWithDetailsAsync(tenantId, entity.Id);
        return await WithExceptionNamesAsync((reloaded ?? entity).ToDto(), cancellationToken);
    }

    /// <summary>
    /// Changes a flight booking — on an approved trip, while the booking is live, never its status (lane 5). Its policy
    /// checks run again; an authorised or refused exception stands while the class does not change, and goes back to
    /// awaiting authorisation when it does (lane 4, D-8).
    /// </summary>
    public async Task<StaffTravelFlightBookingDto> UpdateFlightAsync(UpdateStaffTravelFlightBookingDto updateDto, Guid updatedByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFlightAsync(updateDto.Id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        var label = FlightLabel(entity);
        StaffTravelBookingRules.RequireBookable(request, $"the {label}");
        StaffTravelBookingRules.RequireLive(entity.Status, label);

        var previousClass = entity.BookingClass;
        var previousVendor = entity.VendorId;

        entity.UpdateEntity(updateDto, updatedByUserId);
        await ApplyFlightPolicyAsync(
            entity, request, updateDto.ClassExceptionApproved, actorEmployeeId,
            factsChanged: entity.BookingClass != previousClass,
            vendorNewlyNamed: entity.VendorId != previousVendor,
            cancellationToken);

        await _flightRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _flightRepository.GetWithSegmentsAsync(entity.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Flight booking with ID '{entity.Id}' not found.");
        return await WithExceptionNamesAsync(refreshed.ToDto(), cancellationToken);
    }

    /// <summary>Removes a pending flight booking (lane 5) — never one that carries a policy exception (lane 4, D-20):
    /// cancel it instead.</summary>
    public async Task<bool> DeleteFlightAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFlightAsync(id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        RequireNoException(entity.ExceptionState, $"Flight booking {entity.BookingReference ?? entity.AirlineName ?? ""}".Trim());
        StaffTravelBookingRules.RequireDeletable(entity.Status, request, FlightLabel(entity));
        await _flightRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Flight segments ---------------------------------------------------

    /// <summary>
    /// The trip and the flight a segment hangs off, checked (lane 5, Q4): segments were added, changed and deleted on a
    /// cancelled booking or a closed trip, their dates never compared with the trip's.
    /// </summary>
    private async Task RequireSegmentWritableAsync(Guid flightBookingId, DateTime? departure, DateTime? arrival)
    {
        var flight = await GetOwnedFlightAsync(flightBookingId);
        var request = await RequireOwnedRequestAsync(flight.StaffTravelRequestId);
        var label = FlightLabel(flight);
        StaffTravelBookingRules.RequireBookable(request, $"the segments of the {label}");
        StaffTravelBookingRules.RequireLive(flight.Status, label);
        if (departure is not null || arrival is not null)
            StaffTravelBookingRules.RequireWithinTrip(
                request, StaffTravelBookingRules.DateOf(departure), StaffTravelBookingRules.DateOf(arrival), "flight segment");
    }

    public async Task<StaffTravelFlightSegmentDto> AddSegmentAsync(CreateStaffTravelFlightSegmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await RequireSegmentWritableAsync(createDto.StaffTravelFlightBookingId, createDto.DepartureDatetime, createDto.ArrivalDatetime);

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
        await RequireSegmentWritableAsync(entity.StaffTravelFlightBookingId, updateDto.DepartureDatetime, updateDto.ArrivalDatetime);
        entity.UpdateEntity(updateDto, updatedByUserId);
        ApplySegmentDerivations(entity);
        await _segmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteSegmentAsync(Guid segmentId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSegmentAsync(segmentId);
        await RequireSegmentWritableAsync(entity.StaffTravelFlightBookingId, null, null);
        await _segmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Hotel bookings ----------------------------------------------------

    public async Task<StaffTravelHotelBookingDto> GetHotelByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _hotelRepository.GetWithDetailsAsync(GetTenantId(), id)
            ?? throw new ArgumentException($"Hotel booking with ID '{id}' not found.");
        return await WithExceptionNamesAsync(entity.ToDto(), cancellationToken);
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
    /// Derives a hotel stay's length and cost, and applies the travel policy: its nightly-rate cap, the notice it
    /// asks for (D-1), its preferred-vendor rule (D-1), and — for a breach — the exception's state (D-8).
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
        Guid? actorEmployeeId,
        bool factsChanged,
        bool vendorNewlyNamed,
        CancellationToken cancellationToken)
    {
        if (entity.CheckOutDate < entity.CheckInDate)
            throw new InvalidOperationException("The check-out date cannot be before the check-in date.");

        entity.NumberOfNights = entity.CheckOutDate.DayNumber - entity.CheckInDate.DayNumber;
        entity.TotalCost = entity.RatePerNight * entity.NumberOfNights;

        var caps = await _policyGuard.ResolveAsync(request, cancellationToken);
        await RequireVendorAsync(entity.VendorId, caps, "hotel", vendorNewlyNamed, cancellationToken);

        // C3/T-9 (lane 4): the cap is in the policy's currency, so the rate is compared in it — converted at Finance's
        // rate for the booking's day (the same currency needs no rate).
        var policyCurrency = caps.CurrencyCode
                             ?? await _currency.GetBaseCurrencyCodeAsync(cancellationToken)
                             ?? entity.CurrencyCode;
        var rateInPolicyCurrency = caps.MaxHotelRatePerNight is > 0m
            ? decimal.Round(await _currency.ConvertBetweenAsync(
                entity.RatePerNight, entity.CurrencyCode, policyCurrency,
                DateOnly.FromDateTime(entity.BookedAt ?? DateTime.UtcNow), cancellationToken), 2, MidpointRounding.AwayFromZero)
            : entity.RatePerNight;
        var bookedRate = string.Equals(entity.CurrencyCode, policyCurrency, StringComparison.OrdinalIgnoreCase)
            ? $"{entity.CurrencyCode} {entity.RatePerNight:N2}"
            : $"{entity.CurrencyCode} {entity.RatePerNight:N2} ({policyCurrency} {rateInPolicyCurrency:N2})";

        var breaches = new List<string>();
        if (StaffTravelPolicyGuard.HotelRateBreach(rateInPolicyCurrency, policyCurrency, bookedRate, caps) is string rateBreach)
            breaches.Add(rateBreach);
        var bookedOn = DateOnly.FromDateTime(entity.CreatedAt == default ? DateTime.UtcNow : entity.CreatedAt);
        if (StaffTravelPolicyGuard.AdvanceBookingBreach(
                entity.CheckInDate.DayNumber - bookedOn.DayNumber, caps.AdvanceBookingDaysHotel, "hotel", caps)
            is string noticeBreach)
            breaches.Add(noticeBreach);

        entity.PolicyMaxRatePerNight = caps.MaxHotelRatePerNight;

        var state = DecideExceptionState(
            breaches, exceptionRequested, entity.RateExceptionReason, entity.ExceptionState, factsChanged, entity.Status, "hotel");
        ApplyExceptionState(state, entity.ExceptionState, actorEmployeeId,
            s => entity.ExceptionState = s,
            id => entity.ExceptionRequestedById = id,
            (by, at) => { entity.ExceptionAuthorisedById = by; entity.ExceptionAuthorisedAt = at; });
        entity.RateExceptionApproved = state == TravelBookingExceptionState.Authorised;
        if (state == TravelBookingExceptionState.None) entity.RateExceptionReason = null;
    }

    /// <summary>Books a hotel on an approved trip, inside its dates (lane 5, D-23), Pending.</summary>
    public async Task<StaffTravelHotelBookingDto> CreateHotelAsync(CreateStaffTravelHotelBookingDto createDto, Guid tenantId, Guid createdByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        StaffTravelBookingRules.RequireBookable(request, "a hotel booking");
        StaffTravelBookingRules.RequireWithinTrip(request, createDto.CheckInDate, createDto.CheckOutDate, "hotel stay");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.ExceptionState = TravelBookingExceptionState.None;
        await ApplyHotelDerivationsAsync(
            entity, request, createDto.RateExceptionApproved, actorEmployeeId, factsChanged: true, vendorNewlyNamed: true,
            cancellationToken);

        await _hotelRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _hotelRepository.GetWithDetailsAsync(tenantId, entity.Id);
        return await WithExceptionNamesAsync((reloaded ?? entity).ToDto(), cancellationToken);
    }

    /// <summary>
    /// Changes a hotel booking. As a flight: the exception stands while the rate, its currency and the check-in do
    /// not change, and goes back to awaiting authorisation when they do (lane 4, D-8).
    /// </summary>
    public async Task<StaffTravelHotelBookingDto> UpdateHotelAsync(UpdateStaffTravelHotelBookingDto updateDto, Guid updatedByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHotelAsync(updateDto.Id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        var label = HotelLabel(entity);
        StaffTravelBookingRules.RequireBookable(request, $"the {label}");
        StaffTravelBookingRules.RequireLive(entity.Status, label);
        // Checked when they move — a trip's dates changed through a request change (D-9) do not block unrelated edits.
        if (updateDto.CheckInDate != entity.CheckInDate || updateDto.CheckOutDate != entity.CheckOutDate)
            StaffTravelBookingRules.RequireWithinTrip(request, updateDto.CheckInDate, updateDto.CheckOutDate, "hotel stay");

        var (previousRate, previousCurrency, previousCheckIn, previousVendor) =
            (entity.RatePerNight, entity.CurrencyCode, entity.CheckInDate, entity.VendorId);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await ApplyHotelDerivationsAsync(
            entity, request, updateDto.RateExceptionApproved, actorEmployeeId,
            factsChanged: entity.RatePerNight != previousRate
                          || !string.Equals(entity.CurrencyCode, previousCurrency, StringComparison.OrdinalIgnoreCase)
                          || entity.CheckInDate != previousCheckIn,
            vendorNewlyNamed: entity.VendorId != previousVendor,
            cancellationToken);

        await _hotelRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _hotelRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return await WithExceptionNamesAsync((reloaded ?? entity).ToDto(), cancellationToken);
    }

    /// <summary>Removes a pending hotel booking (lane 5) — never one that carries a policy exception (lane 4, D-20):
    /// cancel it instead.</summary>
    public async Task<bool> DeleteHotelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHotelAsync(id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        RequireNoException(entity.ExceptionState, $"Hotel booking at {entity.HotelName}");
        StaffTravelBookingRules.RequireDeletable(entity.Status, request, HotelLabel(entity));
        await _hotelRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Policy exceptions on bookings (lane 4, D-8) ------------------------------

    /// <summary>
    /// What a booking that breaches its trip's policy is now: no exception when nothing breaches; otherwise one
    /// awaiting authorisation — or the decision already taken, while the facts it was taken on stand.
    /// </summary>
    /// <remarks>
    /// <para>A breach without a request for an exception (the booking form's switch) and its reason is refused, as
    /// before. A breach with one is saved — but only as Pending, Cancelled, Refunded, a no-show or on hold: it is
    /// not Confirmed, Ticketed or Completed until a different travel administrator authorises the exception, which
    /// before lane 4 the booker granted themselves whenever they held <c>HR.Travel.Admin</c>.</para>
    /// </remarks>
    private static TravelBookingExceptionState DecideExceptionState(
        List<string> breaches, bool exceptionRequested, string? reason, TravelBookingExceptionState current,
        bool factsChanged, TravelBookingStatus status, string what)
    {
        if (breaches.Count == 0) return TravelBookingExceptionState.None;

        var said = string.Join("; and ", breaches);
        if (!exceptionRequested)
            throw new InvalidOperationException(
                $"This {what} breaches the travel policy: {said}. Change the booking, or ask for an exception with the " +
                "reason — a travel administrator other than you then authorises it.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(
                $"Say why the exception is needed — this {what} breaches the travel policy: {said}.");

        var state = !factsChanged && current is TravelBookingExceptionState.Authorised or TravelBookingExceptionState.Refused
            ? current
            : TravelBookingExceptionState.Pending;
        // Since lane 5 the status is the verbs', so this is an edit of a booking already confirmed or ticketed: the
        // change would leave it breaching with an exception nobody has authorised.
        if (state != TravelBookingExceptionState.Authorised
            && status is TravelBookingStatus.Confirmed or TravelBookingStatus.Ticketed or TravelBookingStatus.Completed)
            throw new InvalidOperationException(
                $"This change makes the {what} breach the travel policy ({said}), and it is already " +
                $"{StaffTravelBookingRules.Describe(status)} — a confirmed booking is not changed into an unauthorised " +
                "breach. Cancel it and book again, asking for the exception, or keep the change within the policy.");
        return state;
    }

    /// <summary>Writes the state <see cref="DecideExceptionState"/> settled on: a new request records who asked;
    /// leaving the exception clears who asked and who decided.</summary>
    private static void ApplyExceptionState(
        TravelBookingExceptionState state, TravelBookingExceptionState previous, Guid? actorEmployeeId,
        Action<TravelBookingExceptionState> setState, Action<Guid?> setRequestedBy, Action<Guid?, DateTime?> setDecided)
    {
        setState(state);
        if (state == TravelBookingExceptionState.None)
        {
            setRequestedBy(null);
            setDecided(null, null);
        }
        else if (state == TravelBookingExceptionState.Pending && previous != TravelBookingExceptionState.Pending)
        {
            setRequestedBy(actorEmployeeId);
            setDecided(null, null);
        }
    }

    /// <summary>D-20: a booking carrying an exception is part of the breach register — it is cancelled, not deleted.</summary>
    private static void RequireNoException(TravelBookingExceptionState state, string what)
    {
        if (state == TravelBookingExceptionState.None) return;
        throw new InvalidOperationException(
            $"{what} breaches the travel policy and its exception is {state.ToString().ToLowerInvariant()}, so it stays on " +
            "the record of policy breaches. Cancel the booking instead of deleting it.");
    }

    /// <summary>
    /// D-1: a booking's supplier is this organisation's (any policy), and active when newly named; under a policy
    /// that books through preferred vendors only, every booking names one. <c>VendorId</c> was any id at all.
    /// </summary>
    private async Task RequireVendorAsync(
        Guid? vendorId, TravelPolicyCaps caps, string what, bool newlyNamed, CancellationToken cancellationToken)
    {
        if (vendorId is Guid id)
        {
            var tenantId = GetTenantId();
            var supplier = await _unitOfWork.Repository<ErpSystem.Core.Entities.Procurement.Supplier>()
                .GetQueryable(s => s.Id == id && s.TenantId == tenantId)
                .Select(s => new { s.Name, s.IsActive })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ArgumentException($"Supplier '{id}' not found.");
            if (newlyNamed && !supplier.IsActive)
                throw new InvalidOperationException($"{supplier.Name} is not an active supplier; choose another.");
            return;
        }
        if (caps.PreferredVendorMandatory)
            throw new InvalidOperationException(
                $"{caps.PolicyName} books through preferred vendors only — choose the supplier this {what} is booked with.");
    }

    /// <summary>
    /// Authorises or refuses a flight booking's policy exception (lane 4, D-8): a travel administrator who is not the
    /// one who asked for it, not the booker and not the traveller. A refusal keeps its reason on the trip as an
    /// internal note; the booking stays as it is, and cannot be confirmed or ticketed — it is changed or cancelled.
    /// </summary>
    public async Task<StaffTravelFlightBookingDto> DecideFlightExceptionAsync(
        Guid id, bool authorise, string? reason, Guid deciderEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFlightAsync(id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        var label = $"flight booking {entity.BookingReference ?? entity.AirlineName ?? string.Empty}".Trim();
        var decided = await DecideExceptionAsync(
            label, entity.ExceptionState, entity.ExceptionRequestedById, entity.UpdatedBy ?? entity.CreatedBy, request,
            authorise, reason, deciderEmployeeId, cancellationToken);
        entity.ExceptionState = decided;
        entity.ExceptionAuthorisedById = deciderEmployeeId;
        entity.ExceptionAuthorisedAt = DateTime.UtcNow;
        entity.ClassExceptionApproved = decided == TravelBookingExceptionState.Authorised;
        // Tracked — the booking was read alone. Saved with the trip's note, when there is one.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Flight booking {Id} exception {State} by {Decider}", id, decided, deciderEmployeeId);

        var refreshed = await _flightRepository.GetWithSegmentsAsync(entity.Id);
        return await WithExceptionNamesAsync((refreshed ?? entity).ToDto(), cancellationToken);
    }

    /// <summary>As <see cref="DecideFlightExceptionAsync"/>, for a hotel booking.</summary>
    public async Task<StaffTravelHotelBookingDto> DecideHotelExceptionAsync(
        Guid id, bool authorise, string? reason, Guid deciderEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHotelAsync(id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        var decided = await DecideExceptionAsync(
            $"hotel booking at {entity.HotelName}", entity.ExceptionState, entity.ExceptionRequestedById,
            entity.UpdatedBy ?? entity.CreatedBy, request, authorise, reason, deciderEmployeeId, cancellationToken);
        entity.ExceptionState = decided;
        entity.ExceptionAuthorisedById = deciderEmployeeId;
        entity.ExceptionAuthorisedAt = DateTime.UtcNow;
        entity.RateExceptionApproved = decided == TravelBookingExceptionState.Authorised;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Hotel booking {Id} exception {State} by {Decider}", id, decided, deciderEmployeeId);

        var reloaded = await _hotelRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return await WithExceptionNamesAsync((reloaded ?? entity).ToDto(), cancellationToken);
    }

    /// <summary>The rules every exception decision shares; returns the state to record, and queues the refusal's note.</summary>
    private async Task<TravelBookingExceptionState> DecideExceptionAsync(
        string label, TravelBookingExceptionState current, Guid? requestedById, string? bookerUserId,
        StaffTravelRequest request, bool authorise, string? reason, Guid deciderEmployeeId, CancellationToken cancellationToken)
    {
        if (current != TravelBookingExceptionState.Pending)
            throw new InvalidOperationException(current == TravelBookingExceptionState.None
                ? $"The {label} breaches nothing — there is no exception to decide."
                : $"The {label}'s exception was already {current.ToString().ToLowerInvariant()}.");
        StaffTravelRequestGuards.RequireOpen(request, "an exception decision");

        var caller = _currentUserProvider.UserId.ToString();
        if (requestedById == deciderEmployeeId
            || string.Equals(bookerUserId, caller, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException(
                $"You booked the {label} or asked for its exception, so another travel administrator decides it — the " +
                "officer who books over the policy does not also authorise the breach.");
        if (request.EmployeeId == deciderEmployeeId)
            throw new UnauthorizedAccessException(
                $"The {label} is for your own trip ({request.RequestNumber}); another travel administrator decides its exception.");

        if (authorise) return TravelBookingExceptionState.Authorised;

        var why = reason?.Trim();
        if (string.IsNullOrEmpty(why) || why.Length < 5)
            throw new InvalidOperationException("Say why the exception is refused, in at least five characters.");
        var note = $"Policy exception refused on the {label}: {why}";
        await _unitOfWork.Repository<StaffTravelRequestComment>().AddAsync(new StaffTravelRequestComment
        {
            TenantId = request.TenantId,
            StaffTravelRequestId = request.Id,
            AuthorId = deciderEmployeeId,
            CommentType = TravelRequestCommentType.InternalNote,
            Body = note.Length > 2000 ? note[..2000] : note,
            IsVisibleToTraveller = false,
            CreatedBy = caller,
        });
        return TravelBookingExceptionState.Refused;
    }

    /// <summary>
    /// The policy-breach register (lane 4, D-8): every flight and hotel booking that breaches its trip's policy,
    /// awaiting a decision first. Read as narrow projections — names only, never whole Employee rows.
    /// </summary>
    public async Task<IEnumerable<StaffTravelBookingExceptionDto>> GetBookingExceptionsAsync(
        TravelBookingExceptionState? state = null, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var flights = await _unitOfWork.Repository<StaffTravelFlightBooking>()
            .GetQueryable(f => f.TenantId == tenantId && f.ExceptionState != TravelBookingExceptionState.None
                            && (state == null || f.ExceptionState == state))
            .Select(f => new
            {
                f.Id, f.StaffTravelRequestId, f.StaffTravelRequest.RequestNumber, f.StaffTravelRequest.TravelStartDate,
                TFirst = f.StaffTravelRequest.Employee.FirstName, TLast = f.StaffTravelRequest.Employee.LastName,
                f.AirlineName, f.BookingClass, f.PolicyAllowedClass, Reason = f.ClassExceptionReason, f.Status, f.ExceptionState,
                RFirst = f.ExceptionRequestedBy != null ? f.ExceptionRequestedBy.FirstName : null,
                RLast = f.ExceptionRequestedBy != null ? f.ExceptionRequestedBy.LastName : null,
                DFirst = f.ExceptionAuthorisedBy != null ? f.ExceptionAuthorisedBy.FirstName : null,
                DLast = f.ExceptionAuthorisedBy != null ? f.ExceptionAuthorisedBy.LastName : null,
                f.ExceptionAuthorisedAt, f.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        var hotels = await _unitOfWork.Repository<StaffTravelHotelBooking>()
            .GetQueryable(h => h.TenantId == tenantId && h.ExceptionState != TravelBookingExceptionState.None
                            && (state == null || h.ExceptionState == state))
            .Select(h => new
            {
                h.Id, h.StaffTravelRequestId, h.StaffTravelRequest.RequestNumber, h.StaffTravelRequest.TravelStartDate,
                TFirst = h.StaffTravelRequest.Employee.FirstName, TLast = h.StaffTravelRequest.Employee.LastName,
                h.HotelName, h.RatePerNight, h.CurrencyCode, h.PolicyMaxRatePerNight, Reason = h.RateExceptionReason, h.Status,
                h.ExceptionState,
                RFirst = h.ExceptionRequestedBy != null ? h.ExceptionRequestedBy.FirstName : null,
                RLast = h.ExceptionRequestedBy != null ? h.ExceptionRequestedBy.LastName : null,
                DFirst = h.ExceptionAuthorisedBy != null ? h.ExceptionAuthorisedBy.FirstName : null,
                DLast = h.ExceptionAuthorisedBy != null ? h.ExceptionAuthorisedBy.LastName : null,
                h.ExceptionAuthorisedAt, h.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        static string? Name(string? first, string? last) => first is null ? null : $"{first} {last}".Trim();
        static string Spaced(string v) => System.Text.RegularExpressions.Regex.Replace(v, "(?<!^)([A-Z])", " $1");
        var rows = flights.Select(f => new StaffTravelBookingExceptionDto
            {
                BookingId = f.Id, Kind = "Flight", StaffTravelRequestId = f.StaffTravelRequestId,
                RequestNumber = f.RequestNumber, TravelStartDate = f.TravelStartDate,
                TravellerName = Name(f.TFirst, f.TLast) ?? string.Empty,
                Booking = $"{f.AirlineName ?? "Flight"} · {Spaced(f.BookingClass.ToString())}",
                PolicyCap = Spaced(f.PolicyAllowedClass.ToString()),
                Reason = f.Reason, BookingStatus = f.Status, ExceptionState = f.ExceptionState,
                RequestedByName = Name(f.RFirst, f.RLast), DecidedByName = Name(f.DFirst, f.DLast),
                DecidedAt = f.ExceptionAuthorisedAt, CreatedAt = f.CreatedAt,
            })
            .Concat(hotels.Select(h => new StaffTravelBookingExceptionDto
            {
                BookingId = h.Id, Kind = "Hotel", StaffTravelRequestId = h.StaffTravelRequestId,
                RequestNumber = h.RequestNumber, TravelStartDate = h.TravelStartDate,
                TravellerName = Name(h.TFirst, h.TLast) ?? string.Empty,
                Booking = $"{h.HotelName} · {h.CurrencyCode} {h.RatePerNight:N2} a night",
                PolicyCap = h.PolicyMaxRatePerNight is decimal cap && cap > 0m ? $"{cap:N2} a night" : null,
                Reason = h.Reason, BookingStatus = h.Status, ExceptionState = h.ExceptionState,
                RequestedByName = Name(h.RFirst, h.RLast), DecidedByName = Name(h.DFirst, h.DLast),
                DecidedAt = h.ExceptionAuthorisedAt, CreatedAt = h.CreatedAt,
            }));
        return rows
            .OrderBy(r => r.ExceptionState == TravelBookingExceptionState.Pending ? 0 : 1)
            .ThenBy(r => r.TravelStartDate)
            .ToList();
    }

    /// <summary>The exception's two names, read narrowly — the booking reads carry no Employee rows (slice 4a's lesson).</summary>
    private async Task<T> WithExceptionNamesAsync<T>(T dto, CancellationToken cancellationToken) where T : class
    {
        var (requested, authorised) = dto switch
        {
            StaffTravelFlightBookingDto f => (f.ExceptionRequestedById, f.ExceptionAuthorisedById),
            StaffTravelHotelBookingDto h => (h.ExceptionRequestedById, h.ExceptionAuthorisedById),
            _ => (null, null),
        };
        var ids = new[] { requested, authorised }.OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0) return dto;
        var names = await _unitOfWork.Repository<ErpSystem.Core.Entities.HR.Employee>()
            .GetQueryable(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.FirstName, e.MiddleName, e.LastName })
            .ToListAsync(cancellationToken);
        string? NameOf(Guid? id)
        {
            var n = names.FirstOrDefault(x => x.Id == id);
            return n is null ? null
                : string.IsNullOrEmpty(n.MiddleName) ? $"{n.FirstName} {n.LastName}" : $"{n.FirstName} {n.MiddleName} {n.LastName}";
        }
        switch (dto)
        {
            case StaffTravelFlightBookingDto f:
                f.ExceptionRequestedByName = NameOf(f.ExceptionRequestedById);
                f.ExceptionAuthorisedByName = NameOf(f.ExceptionAuthorisedById);
                break;
            case StaffTravelHotelBookingDto h:
                h.ExceptionRequestedByName = NameOf(h.ExceptionRequestedById);
                h.ExceptionAuthorisedByName = NameOf(h.ExceptionAuthorisedById);
                break;
        }
        return dto;
    }

    // ---- Ground transport --------------------------------------------------

    public async Task<StaffTravelGroundTransportDto> GetGroundTransportByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroundTransportAsync(id);
        return await GroundDtoAsync(entity, cancellationToken);
    }

    public async Task<IEnumerable<StaffTravelGroundTransportDto>> GetGroundTransportsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var legs = (await _groundRepository.GetByRequestIdAsync(requestId))
            .Where(g => g.TenantId == tenantId)
            .Select(g => g.ToDto())
            .ToList();
        await _fleet.DescribeAsync(legs, cancellationToken);   // lane 6: a company vehicle's facts are Fleet's
        return legs;
    }

    /// <summary>A leg as the desk sees it — a company vehicle's vehicle, driver and status read from Fleet (lane 6).</summary>
    private async Task<StaffTravelGroundTransportDto> GroundDtoAsync(StaffTravelGroundTransport entity, CancellationToken cancellationToken)
    {
        var dto = entity.ToDto();
        await _fleet.DescribeAsync(new[] { dto }, cancellationToken);
        return dto;
    }

    public async Task<StaffTravelGroundTransportDto> CreateGroundTransportAsync(CreateStaffTravelGroundTransportDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        var owner = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        StaffTravelBookingRules.RequireBookable(owner, "ground transport");
        StaffTravelBookingRules.RequireWithinTrip(owner,
            StaffTravelBookingRules.DateOf(createDto.PickupDatetime), StaffTravelBookingRules.DateOf(createDto.DropoffDatetime),
            "ground transport");
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        // D-1 (lane 4): the supplier rule — not for a company vehicle, which no supplier provides.
        if (createDto.TransportType != GroundTransportType.CompanyVehicle)
            await RequireVendorAsync(entity.VendorId, await _policyGuard.ResolveAsync(owner, cancellationToken),
                "ground transport", newlyNamed: true, cancellationToken);

        // A company vehicle is a real, finite resource — reserved in Fleet rather than written down here (lane 6: the
        // clashes and the vehicle's compliance checked, the trip submitted when Fleet publishes an approval route —
        // D-27). Every other mode is somebody else's vehicle and stays travel-owned.
        if (createDto.TransportType == GroundTransportType.CompanyVehicle)
        {
            if (createDto.PickupDatetime is not DateTime pickup || createDto.DropoffDatetime is not DateTime dropoff)
                throw new InvalidOperationException(
                    "A company vehicle is reserved from a pick-up time to a drop-off time — give both.");
            entity.FleetTripId = await _fleet.ReserveAsync(owner, new StaffTravelFleetReservation(
                createDto.VehicleAssetId, createDto.DriverEmployeeId, pickup, dropoff,
                createDto.PickupLocation, createDto.DropoffLocation, createDto.FleetTripDestinationId,
                $"Staff travel {owner.RequestNumber}", createDto.Notes), cancellationToken);
        }
        await _groundRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _groundRepository.GetWithDetailsAsync(tenantId, entity.Id);
        return await GroundDtoAsync(reloaded ?? entity, cancellationToken);
    }

    public async Task<StaffTravelGroundTransportDto> UpdateGroundTransportAsync(UpdateStaffTravelGroundTransportDto updateDto, Guid updatedByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroundTransportAsync(updateDto.Id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        var label = GroundLabel(entity);
        StaffTravelBookingRules.RequireBookable(request, $"the {label}");
        StaffTravelBookingRules.RequireLive(entity.Status, label);
        if (updateDto.PickupDatetime != entity.PickupDatetime || updateDto.DropoffDatetime != entity.DropoffDatetime)
            StaffTravelBookingRules.RequireWithinTrip(request,
                StaffTravelBookingRules.DateOf(updateDto.PickupDatetime), StaffTravelBookingRules.DateOf(updateDto.DropoffDatetime),
                "ground transport");
        // Lane 6 (D2): a leg does not turn into a company vehicle, or out of one — the fleet trip would be left behind,
        // or never made.
        if (updateDto.TransportType != entity.TransportType
            && (updateDto.TransportType == GroundTransportType.CompanyVehicle || entity.TransportType == GroundTransportType.CompanyVehicle))
            throw new InvalidOperationException(
                "A leg does not change to or from a company vehicle — cancel it and book again.");
        if (entity.FleetTripId is Guid fleetTripId)
        {
            if (updateDto.PickupDatetime is not DateTime pickup || updateDto.DropoffDatetime is not DateTime dropoff)
                throw new InvalidOperationException(
                    "A company vehicle is reserved from a pick-up time to a drop-off time — give both.");
            // D-35: another driver ends the old driver's own request — checked before Fleet's trip changes (G3), cancelled after.
            var driverChanges = entity.DriverTravelRequestId is not null && updateDto.DriverEmployeeId is Guid newDriver
                                && newDriver != (await GroundDtoAsync(entity, cancellationToken)).DriverEmployeeId;
            if (driverChanges)
            {
                if (actorEmployeeId is null)
                    throw new UnauthorizedAccessException(
                        "Changing the driver cancels their own travel request, which needs a login linked to an employee record.");
                await _requests.RequireDriverRequestCancellableAsync(entity.DriverTravelRequestId, request.RequestNumber, cancellationToken);
            }
            // Fleet's trip changes first, while Fleet allows (a draft or rejected trip; an approved one only its driver).
            await _fleet.UpdateReservationAsync(request, fleetTripId, new StaffTravelFleetReservation(
                updateDto.VehicleAssetId, updateDto.DriverEmployeeId, pickup, dropoff,
                updateDto.PickupLocation, updateDto.DropoffLocation, updateDto.FleetTripDestinationId,
                $"Staff travel {request.RequestNumber}", updateDto.Notes), cancellationToken);
            if (driverChanges)
            {
                await _requests.CancelDriverRequestAsync(entity.DriverTravelRequestId,
                    $"The company vehicle on {request.RequestNumber} has another driver now.", actorEmployeeId!.Value, updatedByUserId,
                    cancellationToken);
                entity.DriverTravelRequestId = null;
            }
        }
        var previousVendor = entity.VendorId;
        entity.UpdateEntity(updateDto, updatedByUserId);
        if (entity.TransportType != GroundTransportType.CompanyVehicle)
            await RequireVendorAsync(entity.VendorId, await _policyGuard.ResolveAsync(request, cancellationToken),
                "ground transport", newlyNamed: entity.VendorId != previousVendor, cancellationToken);
        await _groundRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _groundRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return await GroundDtoAsync(reloaded ?? entity, cancellationToken);
    }

    public async Task<bool> DeleteGroundTransportAsync(Guid id, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroundTransportAsync(id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        StaffTravelBookingRules.RequireDeletable(entity.Status, request, GroundLabel(entity));
        if (entity.FleetTripId is Guid fleetTripId)
        {
            // Lane 6: only a reservation the transport office has not taken up — a draft, or one it rejected — goes with
            // the leg; its fleet trip is cancelled, not left live (D2).
            var fleetStatus = (await GroundDtoAsync(entity, cancellationToken)).FleetStatus;
            if (fleetStatus is not (FleetStatus.Draft or FleetStatus.Rejected or FleetStatus.Cancelled or null))
                throw new InvalidOperationException(
                    $"The vehicle's trip is {fleetStatus.ToLowerInvariant()} in Fleet, so the leg is not deleted — cancel it instead.");
            // D-35: the driver's own request goes with the leg — checked before Fleet's cancel saves (G3).
            if (entity.DriverTravelRequestId is not null)
            {
                if (actorEmployeeId is null)
                    throw new UnauthorizedAccessException(
                        "Deleting the leg cancels its driver's own travel request, which needs a login linked to an employee record.");
                await _requests.RequireDriverRequestCancellableAsync(entity.DriverTravelRequestId, request.RequestNumber, cancellationToken);
            }
            await _fleet.CancelReservationAsync(fleetTripId, $"Travel leg deleted ({GroundLabel(entity)})", cancellationToken);
            if (entity.DriverTravelRequestId is not null)
                await _requests.CancelDriverRequestAsync(entity.DriverTravelRequestId,
                    $"Its company-vehicle leg on {request.RequestNumber} was deleted.", actorEmployeeId!.Value,
                    _currentUserProvider.UserId, cancellationToken);
        }
        await _groundRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Lane 6 (D-33, D-34): raises the driver's own travel request for a company-vehicle leg — a Draft for the driver with
    /// the trip's dates, destination and purpose, for the desk to cost and submit through the usual two-stage approval, so
    /// the driver's allowance, attendance and duty of care are covered. The leg keeps it; it goes with the leg (D-35).
    /// </summary>
    public async Task<StaffTravelGroundTransportDto> RaiseDriverRequestAsync(
        Guid legId, Guid tenantId, Guid createdByUserId, Guid? actorEmployeeId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await GetOwnedGroundTransportAsync(legId);
        if (entity.FleetTripId is null)
            throw new InvalidOperationException("Only a company vehicle's leg has a driver to raise a travel request for.");
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        StaffTravelBookingRules.RequireBookable(request, "a driver's request");
        var leg = await GroundDtoAsync(entity, cancellationToken);
        if (leg.Status == TravelBookingStatus.Cancelled)
            throw new InvalidOperationException("The leg is cancelled, so its driver travels nowhere on it.");
        if (leg.DriverEmployeeId is not Guid driverId)
            throw new InvalidOperationException("The leg names no driver yet — choose the driver first.");
        if (driverId == request.EmployeeId)
            throw new InvalidOperationException(
                "The traveller drives this vehicle, so their own trip covers them — there is no driver's request to raise.");
        if (leg.DriverTravelRequestStatus is { } raised and not (nameof(StaffTravelRequestStatus.Cancelled) or nameof(StaffTravelRequestStatus.Rejected)))
            throw new InvalidOperationException(
                $"The driver's request {leg.DriverTravelRequestNumber} is already raised ({raised}).");
        if (actorEmployeeId is not Guid initiator)
            throw new UnauthorizedAccessException("Raising a driver's request needs a login linked to an employee record.");

        var created = await _requests.CreateAsync(new CreateStaffTravelRequestDto
        {
            EmployeeId = driverId,
            InitiatedById = initiator,
            InitiatedByRole = TravelInitiatorRole.TravelDesk,
            TravelType = request.TravelType,
            TravelPurpose = request.TravelPurpose,
            PurposeDescription = $"Driver — company vehicle {leg.VehicleName}" +
                                 (string.IsNullOrWhiteSpace(leg.VehiclePlate) ? string.Empty : $" ({leg.VehiclePlate})") +
                                 $" for {request.RequestNumber}",
            Priority = request.Priority,
            DestinationCountryId = request.DestinationCountryId,
            DestinationCity = request.DestinationCity,
            OriginCountryId = request.OriginCountryId,
            OriginCity = request.OriginCity,
            TravelStartDate = request.TravelStartDate,
            TravelEndDate = request.TravelEndDate,
            // The desk costs the driver's own trip — per diem, lodging — before submitting it; a draft takes none.
            EstimatedTotalCost = 0m,
            CurrencyCode = request.CurrencyCode,
            RequiresVisa = request.RequiresVisa,
            RequiresHealthClearance = request.RequiresHealthClearance,
            RiskLevel = request.RiskLevel,
        }, tenantId, createdByUserId, cancellationToken);

        entity.DriverTravelRequestId = created.Id;
        Touch((at, by) => { entity.UpdatedAt = at; entity.UpdatedBy = by; });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Driver's request {Driver} raised for the company-vehicle leg {Leg} of {Request}",
            created.RequestNumber, entity.Id, request.RequestNumber);
        return await GroundDtoAsync(entity, cancellationToken);
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
        var owner = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        StaffTravelBookingRules.RequireBookable(owner, "a car rental");
        StaffTravelBookingRules.RequireWithinTrip(owner,
            DateOnly.FromDateTime(createDto.PickupDatetime), DateOnly.FromDateTime(createDto.DropoffDatetime), "car rental");
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        ApplyCarRentalDerivations(entity);
        await RequireVendorAsync(entity.VendorId, await _policyGuard.ResolveAsync(owner, cancellationToken),
            "car rental", newlyNamed: true, cancellationToken);
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
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        var label = CarRentalLabel(entity);
        StaffTravelBookingRules.RequireBookable(request, $"the {label}");
        StaffTravelBookingRules.RequireLive(entity.Status, label);
        if (updateDto.PickupDatetime != entity.PickupDatetime || updateDto.DropoffDatetime != entity.DropoffDatetime)
            StaffTravelBookingRules.RequireWithinTrip(request,
                DateOnly.FromDateTime(updateDto.PickupDatetime), DateOnly.FromDateTime(updateDto.DropoffDatetime), "car rental");
        var previousVendor = entity.VendorId;

        entity.UpdateEntity(updateDto, updatedByUserId);
        ApplyCarRentalDerivations(entity);
        await RequireVendorAsync(entity.VendorId, await _policyGuard.ResolveAsync(request, cancellationToken),
            "car rental", newlyNamed: entity.VendorId != previousVendor, cancellationToken);

        await _carRentalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _carRentalRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeleteCarRentalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCarRentalAsync(id);
        StaffTravelBookingRules.RequireDeletable(
            entity.Status, await RequireOwnedRequestAsync(entity.StaffTravelRequestId), CarRentalLabel(entity));
        await _carRentalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Status verbs (lane 5, D1) -----------------------------------------
    //
    // A booking's status moves only here — hold, confirm, ticket (flights), cancel, no-show, complete — through
    // StaffTravelBookingRules.Next's table. A verb never re-runs the policy's cap check: cancelling a booking that
    // breaches the policy must work whatever its exception says. Confirm and ticket need the exception settled.

    private static string FlightLabel(StaffTravelFlightBooking b)
        => $"flight booking {b.BookingReference ?? b.AirlineName ?? string.Empty}".Trim();

    private static string HotelLabel(StaffTravelHotelBooking b) => $"hotel booking at {b.HotelName}";

    private static string GroundLabel(StaffTravelGroundTransport b)
        => $"{b.TransportType} booking {b.BookingReference ?? string.Empty}".Trim();

    private static string CarRentalLabel(StaffTravelCarRentalBooking b)
        => $"car rental {b.BookingReference ?? string.Empty}".Trim();

    /// <summary>D-8: a booking breaching the policy is confirmed or ticketed only once a travel administrator other than
    /// the booker has authorised its exception.</summary>
    private static void RequireExceptionSettled(TravelBookingExceptionState state, string label)
    {
        if (state is TravelBookingExceptionState.None or TravelBookingExceptionState.Authorised) return;
        throw new InvalidOperationException(
            $"The {label} breaches the travel policy and its exception is {state.ToString().ToLowerInvariant()}, so it " +
            "is not confirmed or ticketed " +
            (state == TravelBookingExceptionState.Refused
                ? "— the refusal stands: change the booking, which asks again, or cancel it."
                : "until a travel administrator other than the booker authorises it (Staff Travel → Policy Breaches)."));
    }

    /// <summary>
    /// T-24's ticketing half: a trip that needs a visa is not ticketed until a visa application on it is approved, or
    /// the visa is recorded as not required. <c>RequiresVisa</c> gated nothing, so a flight was ticketed — the fare
    /// spent — for a traveller who might never be let in.
    /// </summary>
    private async Task RequireVisaForTicketAsync(StaffTravelRequest request, CancellationToken cancellationToken)
    {
        if (!request.RequiresVisa) return;
        var settled = await _unitOfWork.Repository<StaffTravelVisaApplication>()
            .GetQueryable(v => v.TenantId == request.TenantId && v.StaffTravelRequestId == request.Id && !v.IsDeleted
                            && (v.Status == VisaApplicationStatus.Approved || v.Status == VisaApplicationStatus.NotRequired))
            .AnyAsync(cancellationToken);
        if (!settled)
            throw new InvalidOperationException(
                $"Travel request {request.RequestNumber} needs a visa, and no visa application on it is approved — the " +
                "flight is ticketed once one is, or once the visa is recorded as not required (the Compliance tab).");
    }

    /// <summary>
    /// A cancellation's reason, kept on the trip as an internal note, and its fee: on a flight or hotel, what the
    /// supplier charged — no more than the booking cost — which the budget counts as committed; ground transport and car
    /// rentals have nowhere to keep one (Q7).
    /// </summary>
    private async Task<decimal?> RecordCancellationAsync(
        StaffTravelRequest request, string label, CancelStaffTravelBookingDto? cancel, decimal? feeCeiling,
        string currencyCode, Guid? actorEmployeeId, CancellationToken cancellationToken)
    {
        var why = cancel?.Reason?.Trim();
        if (string.IsNullOrEmpty(why) || why.Length < 5)
            throw new InvalidOperationException("Say why the booking is cancelled, in at least five characters.");
        var fee = cancel?.CancellationFee;
        if (fee is decimal f && f > 0m)
        {
            if (feeCeiling is not decimal ceiling)
                throw new InvalidOperationException(
                    $"A cancellation fee is kept on a flight or a hotel booking; the {label} has nowhere to record one. " +
                    "Cancel it without a fee, and note the charge in the reason.");
            if (f > ceiling)
                throw new InvalidOperationException(
                    $"The cancellation fee ({currencyCode} {f:N2}) is more than the booking cost ({currencyCode} {ceiling:N2}).");
        }
        if (actorEmployeeId is not Guid author)
            throw new UnauthorizedAccessException("Cancelling a booking needs a login linked to an employee record.");

        var note = $"Booking cancelled — the {label}: {why}" +
                   (fee is decimal charged && charged > 0m ? $" (cancellation fee {currencyCode} {charged:N2})" : string.Empty);
        await _unitOfWork.Repository<StaffTravelRequestComment>().AddAsync(new StaffTravelRequestComment
        {
            TenantId = request.TenantId,
            StaffTravelRequestId = request.Id,
            AuthorId = author,
            CommentType = TravelRequestCommentType.InternalNote,
            Body = note.Length > 2000 ? note[..2000] : note,
            IsVisibleToTraveller = false,
            CreatedBy = _currentUserProvider.UserId.ToString(),
        });
        return feeCeiling is null ? null : fee;
    }

    private void Touch(Action<DateTime, string> stamp) => stamp(DateTime.UtcNow, _currentUserProvider.UserId.ToString());

    public async Task<StaffTravelFlightBookingDto> MoveFlightAsync(
        Guid id, TravelBookingVerb verb, string? ticketNumber, CancelStaffTravelBookingDto? cancel, Guid? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFlightAsync(id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        var label = FlightLabel(entity);
        var next = StaffTravelBookingRules.Next(entity.Status, verb, request, label);

        if (verb is TravelBookingVerb.Confirm or TravelBookingVerb.Ticket)
            RequireExceptionSettled(entity.ExceptionState, label);
        if (verb == TravelBookingVerb.Ticket)
        {
            var number = ticketNumber?.Trim();
            if (string.IsNullOrEmpty(number))
                throw new InvalidOperationException("Give the ticket number the airline issued.");
            await RequireVisaForTicketAsync(request, cancellationToken);
            entity.TicketNumber = number;
        }
        if (verb == TravelBookingVerb.Cancel)
            entity.CancellationFee = await RecordCancellationAsync(
                request, label, cancel, entity.TotalFare + entity.TaxesAndFees, entity.CurrencyCode, actorEmployeeId, cancellationToken);

        entity.Status = next;
        var bookedAt = entity.BookedAt;
        var cancelledAt = entity.CancelledAt;
        StampBookingTimestamps(next, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;
        entity.CancelledAt = cancelledAt;
        Touch((at, by) => { entity.UpdatedAt = at; entity.UpdatedBy = by; });

        await _unitOfWork.SaveChangesAsync(cancellationToken);   // tracked — read alone
        _logger.LogInformation("Flight booking {Id} {Verb} → {Status}", id, verb, next);
        var refreshed = await _flightRepository.GetWithSegmentsAsync(entity.Id);
        return await WithExceptionNamesAsync((refreshed ?? entity).ToDto(), cancellationToken);
    }

    public async Task<StaffTravelHotelBookingDto> MoveHotelAsync(
        Guid id, TravelBookingVerb verb, CancelStaffTravelBookingDto? cancel, Guid? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHotelAsync(id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        var label = HotelLabel(entity);
        if (verb == TravelBookingVerb.Ticket)
            throw new InvalidOperationException("Only a flight is ticketed — a hotel is confirmed.");
        var next = StaffTravelBookingRules.Next(entity.Status, verb, request, label);

        if (verb == TravelBookingVerb.Confirm)
            RequireExceptionSettled(entity.ExceptionState, label);
        if (verb == TravelBookingVerb.Cancel)
            entity.CancellationFee = await RecordCancellationAsync(
                request, label, cancel, entity.TotalCost, entity.CurrencyCode, actorEmployeeId, cancellationToken);

        entity.Status = next;
        var bookedAt = entity.BookedAt;
        var cancelledAt = entity.CancelledAt;
        StampBookingTimestamps(next, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;
        entity.CancelledAt = cancelledAt;
        Touch((at, by) => { entity.UpdatedAt = at; entity.UpdatedBy = by; });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Hotel booking {Id} {Verb} → {Status}", id, verb, next);
        var reloaded = await _hotelRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return await WithExceptionNamesAsync((reloaded ?? entity).ToDto(), cancellationToken);
    }

    /// <summary>As the flight's, for ground transport. A company vehicle's Fleet trip is not touched — Fleet's side of a
    /// cancellation is lane 6's (D2).</summary>
    public async Task<StaffTravelGroundTransportDto> MoveGroundTransportAsync(
        Guid id, TravelBookingVerb verb, CancelStaffTravelBookingDto? cancel, Guid? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedGroundTransportAsync(id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        var label = GroundLabel(entity);
        if (verb == TravelBookingVerb.Ticket)
            throw new InvalidOperationException("Only a flight is ticketed — ground transport is confirmed.");
        // Lane 6 (R3): a company vehicle's leg follows its fleet trip — the transport office approves, dispatches and
        // completes it in Fleet. Cancelling is the one verb done here, and it cancels the fleet trip.
        if (entity.FleetTripId is Guid fleetTripId)
        {
            if (verb != TravelBookingVerb.Cancel)
                throw new InvalidOperationException(
                    "A company vehicle's leg follows its fleet trip — the transport office approves, dispatches and completes " +
                    "it in Fleet. Only cancelling it is done here.");
            StaffTravelBookingRules.Next(entity.Status, verb, request, label);
            var why = cancel?.Reason?.Trim();
            if (string.IsNullOrEmpty(why) || why.Length < 5)
                throw new InvalidOperationException("Say why the booking is cancelled, in at least five characters.");
            if (actorEmployeeId is null)
                throw new UnauthorizedAccessException("Cancelling a booking needs a login linked to an employee record.");
            // D-35: the driver's own request goes with the leg — checked before Fleet's cancel saves (G3).
            await _requests.RequireDriverRequestCancellableAsync(entity.DriverTravelRequestId, request.RequestNumber, cancellationToken);
            await _fleet.CancelReservationAsync(fleetTripId, why, cancellationToken);
            await _requests.CancelDriverRequestAsync(entity.DriverTravelRequestId,
                $"Its company-vehicle leg on {request.RequestNumber} was cancelled: {why}", actorEmployeeId.Value,
                _currentUserProvider.UserId, cancellationToken);
        }
        var next = StaffTravelBookingRules.Next(entity.Status, verb, request, label);
        if (verb == TravelBookingVerb.Cancel)
            await RecordCancellationAsync(request, label, cancel, null, entity.CurrencyCode, actorEmployeeId, cancellationToken);

        entity.Status = next;
        Touch((at, by) => { entity.UpdatedAt = at; entity.UpdatedBy = by; });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Ground transport {Id} {Verb} → {Status}", id, verb, next);
        var reloaded = await _groundRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return await GroundDtoAsync(reloaded ?? entity, cancellationToken);
    }

    public async Task<StaffTravelCarRentalBookingDto> MoveCarRentalAsync(
        Guid id, TravelBookingVerb verb, CancelStaffTravelBookingDto? cancel, Guid? actorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedCarRentalAsync(id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        var label = CarRentalLabel(entity);
        if (verb == TravelBookingVerb.Ticket)
            throw new InvalidOperationException("Only a flight is ticketed — a car rental is confirmed.");
        var next = StaffTravelBookingRules.Next(entity.Status, verb, request, label);
        if (verb == TravelBookingVerb.Cancel)
            await RecordCancellationAsync(request, label, cancel, null, entity.CurrencyCode, actorEmployeeId, cancellationToken);

        entity.Status = next;
        // D5: a car rental's BookedAt was stamped on update only; it is stamped when the rental is confirmed.
        var bookedAt = entity.BookedAt;
        DateTime? cancelledAt = null;
        StampBookingTimestamps(next, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;
        Touch((at, by) => { entity.UpdatedAt = at; entity.UpdatedBy = by; });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Car rental {Id} {Verb} → {Status}", id, verb, next);
        var reloaded = await _carRentalRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }
}

#endregion
