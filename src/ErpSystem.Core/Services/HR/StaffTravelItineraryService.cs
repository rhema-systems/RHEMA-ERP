using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 2: ITINERARY SERVICE
// ============================================================================

#region Staff Travel Itinerary Service

/// <summary>
/// A trip's plan — versioned, with legs and activities. The rules (lane 5, slice 5b) are
/// <see cref="StaffTravelItineraryRules"/>'s: planned while the trip is open; status the server's (D-25); a finalised,
/// superseded or cancelled version not changed; days from the trip's dates; the current version not deleted (O-15); a
/// leg's booking the trip's own (Q3), its date compared with the booking's (T-19).
/// </summary>
public class StaffTravelItineraryService : IStaffTravelItineraryService
{
    private readonly IStaffTravelItineraryRepository _itineraryRepository;
    private readonly IStaffTravelItineraryLegRepository _legRepository;
    private readonly IStaffTravelItineraryActivityRepository _activityRepository;
    private readonly IStaffTravelRequestRepository _requestRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelItineraryService> _logger;

    public StaffTravelItineraryService(
        IStaffTravelItineraryRepository itineraryRepository,
        IStaffTravelItineraryLegRepository legRepository,
        IStaffTravelItineraryActivityRepository activityRepository,
        IStaffTravelRequestRepository requestRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelItineraryService> logger)
    {
        _itineraryRepository = itineraryRepository;
        _legRepository = legRepository;
        _activityRepository = activityRepository;
        _requestRepository = requestRepository;
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
    /// Confirms the travel request exists in the caller's tenant. The itinerary create took
    /// StaffTravelRequestId straight from the payload and never checked it.
    /// </summary>
    private async Task<StaffTravelRequest> RequireOwnedRequestAsync(Guid requestId)
    {
        var request = await _requestRepository.GetByIdAsync(requestId);
        if (request == null || request.TenantId != GetTenantId())
            throw new ArgumentException($"Staff travel request with ID '{requestId}' not found.");
        return request;
    }

    private async Task<StaffTravelItinerary> GetOwnedItineraryAsync(Guid id)
    {
        var entity = await _itineraryRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Itinerary with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelItineraryLeg> GetOwnedLegAsync(Guid id)
    {
        var entity = await _legRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Itinerary leg with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelItineraryActivity> GetOwnedActivityAsync(Guid id)
    {
        var entity = await _activityRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Itinerary activity with ID '{id}' not found.");
        return entity;
    }

    /// <summary>A version whose plan may change: its trip open, the version still a draft.</summary>
    private async Task<(StaffTravelItinerary Itinerary, StaffTravelRequest Request)> RequireWritableItineraryAsync(
        Guid itineraryId, string what)
    {
        var itinerary = await GetOwnedItineraryAsync(itineraryId);
        var request = await RequireOwnedRequestAsync(itinerary.StaffTravelRequestId);
        StaffTravelItineraryRules.RequirePlannable(request, what);
        StaffTravelItineraryRules.RequireEditable(itinerary);
        return (itinerary, request);
    }

    // ---- Itinerary ---------------------------------------------------------

    public async Task<StaffTravelItineraryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _itineraryRepository.GetWithLegsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Itinerary with ID '{id}' not found.");
        var dto = entity.ToDto();
        await WithLinkedBookingsAsync(dto.Legs, entity.StaffTravelRequestId, cancellationToken);
        return dto;
    }

    public async Task<IEnumerable<StaffTravelItinerarySummaryDto>> GetByRequestIdAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _itineraryRepository.GetByRequestIdAsync(requestId))
            .Where(i => i.TenantId == tenantId)
            .Select(i => i.ToSummaryDto())
            .ToList();
    }

    public async Task<StaffTravelItineraryDto?> GetCurrentVersionAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _itineraryRepository.GetCurrentVersionAsync(requestId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        var dto = entity.ToDto();
        await WithLinkedBookingsAsync(dto.Legs, requestId, cancellationToken);
        return dto;
    }

    /// <summary>
    /// A new version: on an open trip, numbered by the server, a Draft, its days the trip's. The trip's first version is
    /// current; a later one becomes current only when asked, and the one it replaces is then Superseded.
    /// </summary>
    public async Task<StaffTravelItineraryDto> CreateAsync(CreateStaffTravelItineraryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        StaffTravelItineraryRules.RequirePlannable(request, "its itinerary");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        var existing = (await _itineraryRepository.GetByRequestIdAsync(createDto.StaffTravelRequestId))
            .Where(i => i.TenantId == tenantId)
            .ToList();
        entity.VersionNumber = (existing.Select(i => (int?)i.VersionNumber).Max() ?? 0) + 1;
        StaffTravelItineraryRules.ApplyDays(entity, request);
        entity.IsCurrentVersion = createDto.IsCurrentVersion || !existing.Any(i => i.IsCurrentVersion);
        if (entity.IsCurrentVersion)
            await SupersedeCurrentAsync(createDto.StaffTravelRequestId, createdByUserId, exceptId: null, cancellationToken);

        await _itineraryRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Itinerary v{Version} created for request {RequestId}", entity.VersionNumber, entity.StaffTravelRequestId);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    /// <summary>A draft version's title and summary; its days follow the trip. Nothing else moves here (D-25).</summary>
    public async Task<StaffTravelItineraryDto> UpdateAsync(UpdateStaffTravelItineraryDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var (entity, request) = await RequireWritableItineraryAsync(updateDto.Id, "its itinerary");
        entity.UpdateEntity(updateDto, updatedByUserId);
        StaffTravelItineraryRules.ApplyDays(entity, request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);   // tracked — read alone
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    /// <summary>
    /// Puts a version in force; the one it replaces is Superseded (D-25). A superseded or cancelled version is not put
    /// back — make a new version from it.
    /// </summary>
    public async Task<bool> SetCurrentVersionAsync(Guid itineraryId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItineraryAsync(itineraryId);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        StaffTravelItineraryRules.RequirePlannable(request, "its itinerary");
        if (entity.IsCurrentVersion) return true;
        if (entity.Status is TravelItineraryStatus.Superseded or TravelItineraryStatus.Cancelled)
            throw new InvalidOperationException(
                $"Version {entity.VersionNumber} is {StaffTravelItineraryRules.Describe(entity.Status)}, so it is not put back " +
                "in force — make a new version from it.");

        await SupersedeCurrentAsync(entity.StaffTravelRequestId, updatedByUserId, exceptId: entity.Id, cancellationToken);
        entity.IsCurrentVersion = true;
        entity.UpdatedBy = updatedByUserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Finalise (D-25): the current version, with at least one leg, becomes Approved and is stamped — the agreed plan.
    /// <c>FinalizedAt</c> and the Approved status had no writer but the payload.
    /// </summary>
    public async Task<StaffTravelItineraryDto> FinaliseAsync(Guid itineraryId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var (entity, request) = await RequireWritableItineraryAsync(itineraryId, "its itinerary");
        if (!entity.IsCurrentVersion)
            throw new InvalidOperationException(
                $"Version {entity.VersionNumber} is not the one in force — make it current before finalising it.");
        var legCount = await _unitOfWork.Repository<StaffTravelItineraryLeg>()
            .GetQueryable(l => l.StaffTravelItineraryId == entity.Id && l.TenantId == entity.TenantId && !l.IsDeleted)
            .CountAsync(cancellationToken);
        if (legCount == 0)
            throw new InvalidOperationException("An itinerary with no legs is not finalised — add the trip's movements first.");

        StaffTravelItineraryRules.ApplyDays(entity, request);
        entity.Status = TravelItineraryStatus.Approved;
        entity.FinalizedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Itinerary v{Version} of request {RequestId} finalised", entity.VersionNumber, entity.StaffTravelRequestId);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    /// <summary>Removes a version that is not in force (O-15) — the current plan is replaced, never deleted.</summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedItineraryAsync(id);
        var request = await RequireOwnedRequestAsync(entity.StaffTravelRequestId);
        StaffTravelItineraryRules.RequirePlannable(request, "its itinerary");
        if (entity.IsCurrentVersion)
            throw new InvalidOperationException(
                $"Version {entity.VersionNumber} is the itinerary in force, so it is not deleted — put another version in " +
                "force first, or make a new one.");

        await _itineraryRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Legs --------------------------------------------------------------

    /// <summary>
    /// A leg's date falls inside the trip (a day either side) and its bookings are the trip's own (Q3) — a flight, hotel or
    /// ground booking of another trip, or none at all, was linked as readily as its own.
    /// </summary>
    private async Task RequireLegFitsAsync(
        StaffTravelRequest request, DateOnly legDate, Guid? flightId, Guid? hotelId, Guid? groundId, CancellationToken cancellationToken)
    {
        StaffTravelBookingRules.RequireWithinTrip(request, legDate, legDate, "leg");
        if (flightId is Guid f && !await _unitOfWork.Repository<StaffTravelFlightBooking>()
                .GetQueryable(b => b.Id == f && b.TenantId == request.TenantId && b.StaffTravelRequestId == request.Id && !b.IsDeleted)
                .AnyAsync(cancellationToken))
            throw new ArgumentException($"Flight booking '{f}' is not one of {request.RequestNumber}'s bookings.");
        if (hotelId is Guid h && !await _unitOfWork.Repository<StaffTravelHotelBooking>()
                .GetQueryable(b => b.Id == h && b.TenantId == request.TenantId && b.StaffTravelRequestId == request.Id && !b.IsDeleted)
                .AnyAsync(cancellationToken))
            throw new ArgumentException($"Hotel booking '{h}' is not one of {request.RequestNumber}'s bookings.");
        if (groundId is Guid g && !await _unitOfWork.Repository<StaffTravelGroundTransport>()
                .GetQueryable(b => b.Id == g && b.TenantId == request.TenantId && b.StaffTravelRequestId == request.Id && !b.IsDeleted)
                .AnyAsync(cancellationToken))
            throw new ArgumentException($"Ground transport '{g}' is not one of {request.RequestNumber}'s bookings.");
    }

    public async Task<StaffTravelItineraryLegDto> AddLegAsync(CreateStaffTravelItineraryLegDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var (_, request) = await RequireWritableItineraryAsync(createDto.StaffTravelItineraryId, "its itinerary");
        await RequireLegFitsAsync(request, createDto.LegDate, createDto.FlightBookingId, createDto.HotelBookingId,
            createDto.GroundTransportId, cancellationToken);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _legRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetLegByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<IEnumerable<StaffTravelItineraryLegDto>> GetLegsAsync(Guid itineraryId, CancellationToken cancellationToken = default)
    {
        var itinerary = await GetOwnedItineraryAsync(itineraryId);
        var tenantId = GetTenantId();
        var legs = (await _legRepository.GetByItineraryIdAsync(itineraryId))
            .Where(l => l.TenantId == tenantId)
            .Select(l => l.ToDto())
            .ToList();
        await WithLinkedBookingsAsync(legs, itinerary.StaffTravelRequestId, cancellationToken);
        return legs;
    }

    public async Task<StaffTravelItineraryLegDto> GetLegByIdAsync(Guid legId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _legRepository.GetWithActivitiesAsync(legId);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Itinerary leg with ID '{legId}' not found.");
        var dto = entity.ToDto();
        var itinerary = await GetOwnedItineraryAsync(entity.StaffTravelItineraryId);
        await WithLinkedBookingsAsync(new[] { dto }, itinerary.StaffTravelRequestId, cancellationToken);
        return dto;
    }

    public async Task<StaffTravelItineraryLegDto> UpdateLegAsync(UpdateStaffTravelItineraryLegDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedLegAsync(updateDto.Id);
        var (_, request) = await RequireWritableItineraryAsync(entity.StaffTravelItineraryId, "its itinerary");
        await RequireLegFitsAsync(request, updateDto.LegDate, updateDto.FlightBookingId, updateDto.HotelBookingId,
            updateDto.GroundTransportId, cancellationToken);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);   // tracked — read alone
        return await GetLegByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteLegAsync(Guid legId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedLegAsync(legId);
        await RequireWritableItineraryAsync(entity.StaffTravelItineraryId, "its itinerary");
        await _legRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Activities --------------------------------------------------------

    private async Task RequireActivityWritableAsync(Guid legId, DateTime? start, DateTime? end)
    {
        var leg = await GetOwnedLegAsync(legId);
        var (_, request) = await RequireWritableItineraryAsync(leg.StaffTravelItineraryId, "its itinerary");
        if (start is not null || end is not null)
            StaffTravelBookingRules.RequireWithinTrip(
                request, StaffTravelBookingRules.DateOf(start), StaffTravelBookingRules.DateOf(end), "activity");
    }

    public async Task<StaffTravelItineraryActivityDto> AddActivityAsync(CreateStaffTravelItineraryActivityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await RequireActivityWritableAsync(createDto.StaffTravelItineraryLegId, createDto.StartDatetime, createDto.EndDatetime);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _activityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelItineraryActivityDto>> GetActivitiesAsync(Guid legId, CancellationToken cancellationToken = default)
    {
        await GetOwnedLegAsync(legId);
        var tenantId = GetTenantId();
        return (await _activityRepository.GetByLegIdAsync(legId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto())
            .ToList();
    }

    public async Task<StaffTravelItineraryActivityDto> UpdateActivityAsync(UpdateStaffTravelItineraryActivityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActivityAsync(updateDto.Id);
        await RequireActivityWritableAsync(entity.StaffTravelItineraryLegId, updateDto.StartDatetime, updateDto.EndDatetime);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);   // tracked — read alone
        return entity.ToDto();
    }

    public async Task<bool> DeleteActivityAsync(Guid activityId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedActivityAsync(activityId);
        await RequireActivityWritableAsync(entity.StaffTravelItineraryLegId, null, null);
        await _activityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Helpers -----------------------------------------------------------

    /// <summary>
    /// The request's versions in force, other than <paramref name="exceptId"/>, stand down: no longer current, and
    /// Superseded (D-25) — <c>Superseded</c> had no writer, so a replaced version read as the draft it once was.
    /// </summary>
    private async Task SupersedeCurrentAsync(Guid requestId, Guid userId, Guid? exceptId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var current = await _unitOfWork.Repository<StaffTravelItinerary>()
            .GetQueryable(i => i.TenantId == tenantId && i.StaffTravelRequestId == requestId && !i.IsDeleted && i.IsCurrentVersion)
            .ToListAsync(cancellationToken);
        foreach (var version in current.Where(i => i.Id != exceptId))
        {
            version.IsCurrentVersion = false;
            if (version.Status != TravelItineraryStatus.Cancelled) version.Status = TravelItineraryStatus.Superseded;
            version.UpdatedBy = userId.ToString();
            version.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// T-19: each leg's booking named, with its dates, and flagged when the leg's date is not one of them — a flight's
    /// segment days, inside a hotel stay, a pick-up's day. Read as narrow projections of the request's bookings.
    /// </summary>
    private async Task WithLinkedBookingsAsync(
        IEnumerable<StaffTravelItineraryLegDto> legs, Guid requestId, CancellationToken cancellationToken)
    {
        var linked = legs.Where(l => l.FlightBookingId is not null || l.HotelBookingId is not null || l.GroundTransportId is not null)
            .ToList();
        if (linked.Count == 0) return;
        var tenantId = GetTenantId();

        var flights = await _unitOfWork.Repository<StaffTravelFlightBooking>()
            .GetQueryable(b => b.TenantId == tenantId && b.StaffTravelRequestId == requestId && !b.IsDeleted)
            .Select(b => new { b.Id, b.AirlineName, b.BookingReference, b.Status })
            .ToListAsync(cancellationToken);
        var flightIds = flights.Select(f => f.Id).ToList();
        var segments = await _unitOfWork.Repository<StaffTravelFlightSegment>()
            .GetQueryable(s => s.TenantId == tenantId && !s.IsDeleted && flightIds.Contains(s.StaffTravelFlightBookingId))
            .Select(s => new { s.StaffTravelFlightBookingId, s.DepartureDatetime, s.ArrivalDatetime })
            .ToListAsync(cancellationToken);
        var hotels = await _unitOfWork.Repository<StaffTravelHotelBooking>()
            .GetQueryable(b => b.TenantId == tenantId && b.StaffTravelRequestId == requestId && !b.IsDeleted)
            .Select(b => new { b.Id, b.HotelName, b.CheckInDate, b.CheckOutDate, b.Status })
            .ToListAsync(cancellationToken);
        var grounds = await _unitOfWork.Repository<StaffTravelGroundTransport>()
            .GetQueryable(b => b.TenantId == tenantId && b.StaffTravelRequestId == requestId && !b.IsDeleted)
            .Select(b => new { b.Id, b.TransportType, b.BookingReference, b.PickupDatetime, b.Status })
            .ToListAsync(cancellationToken);

        foreach (var leg in linked)
        {
            if (leg.FlightBookingId is Guid fid && flights.FirstOrDefault(f => f.Id == fid) is { } flight)
            {
                leg.LinkedBooking = $"Flight {flight.BookingReference ?? flight.AirlineName ?? string.Empty}".Trim()
                                    + $" ({StaffTravelBookingRules.Describe(flight.Status)})";
                var days = segments.Where(s => s.StaffTravelFlightBookingId == fid)
                    .SelectMany(s => new[] { DateOnly.FromDateTime(s.DepartureDatetime), DateOnly.FromDateTime(s.ArrivalDatetime) })
                    .Distinct().OrderBy(d => d).ToList();
                leg.LinkedBookingDates = days.Count == 0 ? "no segments yet" : string.Join(", ", days.Select(d => d.ToString("d MMM yyyy")));
                leg.LinkedBookingDateMismatch = days.Count > 0 && !days.Contains(leg.LegDate);
            }
            else if (leg.HotelBookingId is Guid hid && hotels.FirstOrDefault(h => h.Id == hid) is { } hotel)
            {
                leg.LinkedBooking = $"{hotel.HotelName} ({StaffTravelBookingRules.Describe(hotel.Status)})";
                leg.LinkedBookingDates = $"{hotel.CheckInDate:d MMM yyyy} to {hotel.CheckOutDate:d MMM yyyy}";
                leg.LinkedBookingDateMismatch = leg.LegDate < hotel.CheckInDate || leg.LegDate > hotel.CheckOutDate;
            }
            else if (leg.GroundTransportId is Guid gid && grounds.FirstOrDefault(g => g.Id == gid) is { } ground)
            {
                leg.LinkedBooking = $"{ground.TransportType} {ground.BookingReference ?? string.Empty}".Trim()
                                    + $" ({StaffTravelBookingRules.Describe(ground.Status)})";
                var pickup = StaffTravelBookingRules.DateOf(ground.PickupDatetime);
                leg.LinkedBookingDates = pickup is DateOnly p ? $"{p:d MMM yyyy}" : "no pick-up time";
                leg.LinkedBookingDateMismatch = pickup is DateOnly pd && pd != leg.LegDate;
            }
        }
    }
}

#endregion
