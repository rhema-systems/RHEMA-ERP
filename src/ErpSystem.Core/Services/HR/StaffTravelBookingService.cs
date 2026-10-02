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

    public async Task<StaffTravelFlightBookingDto> CreateFlightAsync(CreateStaffTravelFlightBookingDto createDto, Guid tenantId, Guid createdByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        StaffTravelRequestGuards.RequireOpen(request, "a booking");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.ExceptionState = TravelBookingExceptionState.None;
        await ApplyFlightPolicyAsync(
            entity, request, createDto.ClassExceptionApproved, actorEmployeeId, factsChanged: true, vendorNewlyNamed: true,
            cancellationToken);

        var bookedAt = entity.BookedAt;
        var cancelledAt = entity.CancelledAt;
        StampBookingTimestamps(entity.Status, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;
        entity.CancelledAt = cancelledAt;

        await _flightRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _flightRepository.GetWithDetailsAsync(tenantId, entity.Id);
        return await WithExceptionNamesAsync((reloaded ?? entity).ToDto(), cancellationToken);
    }

    /// <summary>
    /// Changes a flight booking. Its policy checks run again; an authorised or refused exception stands while the
    /// class does not change, and goes back to awaiting authorisation when it does (lane 4, D-8).
    /// </summary>
    public async Task<StaffTravelFlightBookingDto> UpdateFlightAsync(UpdateStaffTravelFlightBookingDto updateDto, Guid updatedByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFlightAsync(updateDto.Id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);

        // ⚠ Captured BEFORE the mapper runs. `UpdateEntity` still copies BookedAt/CancelledAt off
        // the DTO, so re-reading them afterwards would read the client's value back — the stored
        // ones are the only truthful starting point.
        var bookedAt = entity.BookedAt;
        var cancelledAt = entity.CancelledAt;
        var previousClass = entity.BookingClass;
        var previousVendor = entity.VendorId;

        entity.UpdateEntity(updateDto, updatedByUserId);
        await ApplyFlightPolicyAsync(
            entity, request, updateDto.ClassExceptionApproved, actorEmployeeId,
            factsChanged: entity.BookingClass != previousClass,
            vendorNewlyNamed: entity.VendorId != previousVendor,
            cancellationToken);

        StampBookingTimestamps(entity.Status, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;
        entity.CancelledAt = cancelledAt;

        await _flightRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var refreshed = await _flightRepository.GetWithSegmentsAsync(entity.Id);
        if (refreshed == null || refreshed.TenantId != GetTenantId())
            throw new ArgumentException($"Flight booking with ID '{entity.Id}' not found.");
        return await WithExceptionNamesAsync(refreshed.ToDto(), cancellationToken);
    }

    /// <summary>Removes a flight booking — never one that carries a policy exception (lane 4, D-20): cancel it instead.</summary>
    public async Task<bool> DeleteFlightAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFlightAsync(id);
        RequireNoException(entity.ExceptionState, $"Flight booking {entity.BookingReference ?? entity.AirlineName ?? ""}".Trim());
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

    public async Task<StaffTravelHotelBookingDto> CreateHotelAsync(CreateStaffTravelHotelBookingDto createDto, Guid tenantId, Guid createdByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        StaffTravelRequestGuards.RequireOpen(request, "a booking");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.ExceptionState = TravelBookingExceptionState.None;
        await ApplyHotelDerivationsAsync(
            entity, request, createDto.RateExceptionApproved, actorEmployeeId, factsChanged: true, vendorNewlyNamed: true,
            cancellationToken);

        var bookedAt = entity.BookedAt;
        var cancelledAt = entity.CancelledAt;
        StampBookingTimestamps(entity.Status, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;
        entity.CancelledAt = cancelledAt;

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

        // Captured before the mapper — see UpdateFlightAsync.
        var bookedAt = entity.BookedAt;
        var cancelledAt = entity.CancelledAt;
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

        StampBookingTimestamps(entity.Status, ref bookedAt, ref cancelledAt);
        entity.BookedAt = bookedAt;
        entity.CancelledAt = cancelledAt;

        await _hotelRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _hotelRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return await WithExceptionNamesAsync((reloaded ?? entity).ToDto(), cancellationToken);
    }

    /// <summary>Removes a hotel booking — never one that carries a policy exception (lane 4, D-20): cancel it instead.</summary>
    public async Task<bool> DeleteHotelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHotelAsync(id);
        RequireNoException(entity.ExceptionState, $"Hotel booking at {entity.HotelName}");
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
        if (state != TravelBookingExceptionState.Authorised
            && status is TravelBookingStatus.Confirmed or TravelBookingStatus.Ticketed or TravelBookingStatus.Completed)
            throw new InvalidOperationException(
                $"This {what} breaches the travel policy ({said}) and its exception is " +
                (state == TravelBookingExceptionState.Refused ? "refused" : "not authorised yet") +
                $", so it cannot be {status.ToString().ToLowerInvariant()}. Save it as Pending until a travel administrator " +
                "other than the booker authorises the exception.");
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
        var owner = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        StaffTravelRequestGuards.RequireOpen(owner, "a booking");
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        // D-1 (lane 4): the supplier rule — not for a company vehicle, which no supplier provides.
        if (createDto.TransportType != GroundTransportType.CompanyVehicle)
            await RequireVendorAsync(entity.VendorId, await _policyGuard.ResolveAsync(owner, cancellationToken),
                "ground transport", newlyNamed: true, cancellationToken);

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
        var previousVendor = entity.VendorId;
        entity.UpdateEntity(updateDto, updatedByUserId);
        if (entity.TransportType != GroundTransportType.CompanyVehicle)
            await RequireVendorAsync(entity.VendorId,
                await _policyGuard.ResolveAsync(await RequireOwnedRequestAsync(entity.StaffTravelRequestId), cancellationToken),
                "ground transport", newlyNamed: entity.VendorId != previousVendor, cancellationToken);
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
        var owner = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        StaffTravelRequestGuards.RequireOpen(owner, "a booking");
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

        // Captured before the mapper — see UpdateFlightAsync. A car rental records no CancelledAt.
        var bookedAt = entity.BookedAt;
        DateTime? cancelledAt = null;
        var previousVendor = entity.VendorId;

        entity.UpdateEntity(updateDto, updatedByUserId);
        ApplyCarRentalDerivations(entity);
        await RequireVendorAsync(entity.VendorId,
            await _policyGuard.ResolveAsync(await RequireOwnedRequestAsync(entity.StaffTravelRequestId), cancellationToken),
            "car rental", newlyNamed: entity.VendorId != previousVendor, cancellationToken);

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
