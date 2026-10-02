using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The rules every booking kind shares — flights, hotels, ground transport and car rentals (travel final closure, lane 5).
/// </summary>
/// <remarks>
/// <para><b>When a trip carries bookings (D-23).</b> A booking was accepted on any trip that was not Closed — a Draft, a
/// Submitted one, even a Rejected or Cancelled one — so the desk could commit the organisation's money before anyone
/// approved the trip. Bookings are made, changed, held, confirmed and ticketed while the trip is Approved or under way;
/// a booking can be cancelled on any trip that is not Closed, and recorded as completed or a no-show once the trip has
/// started.</para>
///
/// <para><b>Status by verb (D1).</b> The status was the payload's on every edit, with no transitions, so a cancelled
/// booking could be ticketed and a pending one completed. It moves only by <see cref="TravelBookingVerb"/>:
/// Pending → OnHold (hold); Pending or OnHold → Confirmed (confirm); Confirmed → Ticketed (ticket, flights only); any
/// live booking → Cancelled (cancel); Confirmed or Ticketed → NoShow or Completed. <c>Refunded</c> has no writer — a
/// cancelled booking's fee is what the budget counts either way.</para>
///
/// <para><b>Dates.</b> A booking falls inside its trip, allowing the day before departure and the day after return —
/// an overnight flight out, a late check-out. They were never compared, so a hotel could be booked for a month after
/// the traveller came home.</para>
/// </remarks>
public static class StaffTravelBookingRules
{
    /// <summary>A booking still standing with its supplier — the states an edit, a cancel or a trip's cancel acts on.</summary>
    public static bool IsLive(TravelBookingStatus status)
        => status is TravelBookingStatus.Pending or TravelBookingStatus.OnHold
            or TravelBookingStatus.Confirmed or TravelBookingStatus.Ticketed;

    /// <summary>A booking the supplier has committed to — what refuses a trip's cancel (D-24).</summary>
    public static bool IsCommittedBySupplier(TravelBookingStatus status)
        => status is TravelBookingStatus.Confirmed or TravelBookingStatus.Ticketed;

    public static string Describe(TravelBookingStatus status) => status switch
    {
        TravelBookingStatus.OnHold => "on hold",
        TravelBookingStatus.NoShow => "a no-show",
        _ => status.ToString().ToLowerInvariant(),
    };

    public static string Describe(StaffTravelRequestStatus status) => status switch
    {
        StaffTravelRequestStatus.ReturnedForRevision => "returned for revision",
        StaffTravelRequestStatus.InProgress => "under way",
        _ => status.ToString().ToLowerInvariant(),
    };

    /// <summary>D-23: bookings are made and changed while the trip is Approved or under way.</summary>
    public static void RequireBookable(StaffTravelRequest request, string what)
    {
        if (request.Status is StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.InProgress) return;
        throw new InvalidOperationException(
            $"Travel request {request.RequestNumber} is {Describe(request.Status)}, so {what} cannot be made or changed — " +
            "bookings are made once the trip is approved, and while it is under way.");
    }

    /// <summary>A closed trip is the record of what happened; nothing on it moves.</summary>
    public static void RequireNotClosed(StaffTravelRequest request, string consequence)
    {
        if (request.Status == StaffTravelRequestStatus.Closed)
            throw new InvalidOperationException($"Travel request {request.RequestNumber} is closed, so {consequence}.");
    }

    /// <summary>
    /// Only a pending booking is deleted — anything the supplier held, confirmed or ticketed is cancelled instead, so the
    /// record of what was booked stays (lane 5; a booking with a policy exception is never deleted, lane 4's D-20).
    /// </summary>
    public static void RequireDeletable(TravelBookingStatus status, StaffTravelRequest request, string label)
    {
        RequireNotClosed(request, $"the {label} is not removed");
        if (status == TravelBookingStatus.Pending) return;
        throw new InvalidOperationException(
            $"The {label} is {Describe(status)}; only a pending booking is deleted. Cancel it instead, so the record of " +
            "what was booked stays.");
    }

    /// <summary>A booking that is no longer live — cancelled, a no-show, completed — is not edited; book again.</summary>
    public static void RequireLive(TravelBookingStatus status, string label)
    {
        if (IsLive(status)) return;
        throw new InvalidOperationException(
            $"The {label} is {Describe(status)}, so it is not changed — make a new booking instead.");
    }

    /// <summary>A booking's dates fall inside its trip, allowing the day before departure and the day after return.</summary>
    public static void RequireWithinTrip(StaffTravelRequest request, DateOnly? from, DateOnly? to, string what)
    {
        var earliest = request.TravelStartDate.AddDays(-1);
        var latest = request.TravelEndDate.AddDays(1);
        bool Outside(DateOnly? d) => d is DateOnly day && (day < earliest || day > latest);
        if (!Outside(from) && !Outside(to)) return;

        var dates = from is DateOnly f && to is DateOnly t && f != t
            ? $"{f:d MMM yyyy} to {t:d MMM yyyy}"
            : $"{(from ?? to):d MMM yyyy}";
        throw new InvalidOperationException(
            $"The {what} ({dates}) falls outside travel request {request.RequestNumber}, " +
            $"{request.TravelStartDate:d MMM yyyy} to {request.TravelEndDate:d MMM yyyy} — a booking may start the day " +
            "before departure and end the day after return, no further.");
    }

    public static DateOnly? DateOf(DateTime? value) => value is DateTime v ? DateOnly.FromDateTime(v) : null;

    /// <summary>
    /// Where a verb takes a booking, refusing a move the table does not allow, and the trip states each verb needs.
    /// </summary>
    public static TravelBookingStatus Next(
        TravelBookingStatus current, TravelBookingVerb verb, StaffTravelRequest request, string label)
    {
        (TravelBookingStatus[] allowed, TravelBookingStatus to, string done) = verb switch
        {
            TravelBookingVerb.Hold => (new[] { TravelBookingStatus.Pending }, TravelBookingStatus.OnHold, "put on hold"),
            TravelBookingVerb.Confirm => (new[] { TravelBookingStatus.Pending, TravelBookingStatus.OnHold },
                TravelBookingStatus.Confirmed, "confirmed"),
            TravelBookingVerb.Ticket => (new[] { TravelBookingStatus.Confirmed }, TravelBookingStatus.Ticketed, "ticketed"),
            TravelBookingVerb.Cancel => (new[]
                {
                    TravelBookingStatus.Pending, TravelBookingStatus.OnHold,
                    TravelBookingStatus.Confirmed, TravelBookingStatus.Ticketed,
                }, TravelBookingStatus.Cancelled, "cancelled"),
            TravelBookingVerb.NoShow => (new[] { TravelBookingStatus.Confirmed, TravelBookingStatus.Ticketed },
                TravelBookingStatus.NoShow, "recorded as a no-show"),
            TravelBookingVerb.Complete => (new[] { TravelBookingStatus.Confirmed, TravelBookingStatus.Ticketed },
                TravelBookingStatus.Completed, "completed"),
            _ => throw new ArgumentOutOfRangeException(nameof(verb)),
        };

        if (!allowed.Contains(current))
            throw new InvalidOperationException(
                $"The {label} is {Describe(current)}, so it cannot be {done}" +
                (verb is TravelBookingVerb.Ticket ? " — a booking is confirmed before it is ticketed." : "."));

        switch (verb)
        {
            case TravelBookingVerb.Hold or TravelBookingVerb.Confirm or TravelBookingVerb.Ticket:
                RequireBookable(request, $"the {label}");
                break;
            case TravelBookingVerb.Cancel:
                RequireNotClosed(request, $"the {label} cannot be cancelled");
                break;
            case TravelBookingVerb.NoShow or TravelBookingVerb.Complete:
                if (request.Status is not (StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.InProgress
                    or StaffTravelRequestStatus.Completed))
                    throw new InvalidOperationException(
                        $"Travel request {request.RequestNumber} is {Describe(request.Status)}, so the {label} cannot be {done}.");
                if (DateOnly.FromDateTime(DateTime.UtcNow) < request.TravelStartDate)
                    throw new InvalidOperationException(
                        $"Travel request {request.RequestNumber} departs on {request.TravelStartDate:d MMM yyyy}, so the " +
                        $"{label} cannot be {done} before then.");
                break;
        }
        return to;
    }

    /// <summary>
    /// D-24: the trip's bookings its suppliers have committed to — confirmed or ticketed — named, so a trip's cancel can
    /// refuse while any stands. Each is cancelled on its own, with the fee the supplier charges.
    /// </summary>
    public static async Task<List<string>> CommittedBookingsAsync(
        IUnitOfWork unitOfWork, Guid tenantId, Guid requestId, CancellationToken cancellationToken)
    {
        bool Committed(TravelBookingStatus s) => IsCommittedBySupplier(s);
        var names = new List<string>();
        names.AddRange((await unitOfWork.Repository<StaffTravelFlightBooking>()
                .GetQueryable(b => b.TenantId == tenantId && b.StaffTravelRequestId == requestId && !b.IsDeleted)
                .Select(b => new { b.Status, b.AirlineName, b.BookingReference })
                .ToListAsync(cancellationToken))
            .Where(b => Committed(b.Status))
            .Select(b => $"the {b.AirlineName ?? "flight"} {b.BookingReference}".TrimEnd() + $" ({Describe(b.Status)})"));
        names.AddRange((await unitOfWork.Repository<StaffTravelHotelBooking>()
                .GetQueryable(b => b.TenantId == tenantId && b.StaffTravelRequestId == requestId && !b.IsDeleted)
                .Select(b => new { b.Status, b.HotelName })
                .ToListAsync(cancellationToken))
            .Where(b => Committed(b.Status))
            .Select(b => $"the {b.HotelName} ({Describe(b.Status)})"));
        var ground = await unitOfWork.Repository<StaffTravelGroundTransport>()
            .GetQueryable(b => b.TenantId == tenantId && b.StaffTravelRequestId == requestId && !b.IsDeleted)
            .Select(b => new { b.Status, b.TransportType, b.BookingReference, b.FleetTripId })
            .ToListAsync(cancellationToken);
        names.AddRange(ground
            .Where(b => b.FleetTripId is null && Committed(b.Status))
            .Select(b => $"the {b.TransportType} transport {b.BookingReference}".TrimEnd() + $" ({Describe(b.Status)})"));
        // Lane 6: a company vehicle's leg is committed when the transport office has approved or dispatched its fleet
        // trip — Fleet's status, not the leg's own.
        var fleetTripIds = ground.Where(b => b.FleetTripId is not null).Select(b => b.FleetTripId!.Value).ToList();
        if (fleetTripIds.Count > 0)
            names.AddRange((await unitOfWork.Repository<ErpSystem.Core.Entities.Maintenance.FleetTrip>()
                    .GetQueryable(t => t.TenantId == tenantId && fleetTripIds.Contains(t.Id))
                    .Select(t => new { t.Status, VehicleName = t.VehicleAsset!.Name })
                    .ToListAsync(cancellationToken))
                .Where(t => StaffTravelFleetService.IsCommitted(t.Status))
                .Select(t => $"the company vehicle {t.VehicleName} ({t.Status.ToLowerInvariant()} in Fleet)"));
        names.AddRange((await unitOfWork.Repository<StaffTravelCarRentalBooking>()
                .GetQueryable(b => b.TenantId == tenantId && b.StaffTravelRequestId == requestId && !b.IsDeleted)
                .Select(b => new { b.Status, b.BookingReference })
                .ToListAsync(cancellationToken))
            .Where(b => Committed(b.Status))
            .Select(b => $"the car rental {b.BookingReference}".TrimEnd() + $" ({Describe(b.Status)})"));
        return names;
    }

    /// <summary>
    /// D-24: the trip's holds — Pending or OnHold bookings, which no supplier has committed to — are cancelled with it.
    /// Tracked; the caller saves. Returns how many.
    /// </summary>
    public static async Task<int> CancelHoldsAsync(
        IUnitOfWork unitOfWork, Guid tenantId, Guid requestId, string userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        static bool Held(TravelBookingStatus s) => s is TravelBookingStatus.Pending or TravelBookingStatus.OnHold;
        var count = 0;

        foreach (var b in (await unitOfWork.Repository<StaffTravelFlightBooking>()
                     .GetQueryable(x => x.TenantId == tenantId && x.StaffTravelRequestId == requestId && !x.IsDeleted)
                     .ToListAsync(cancellationToken)).Where(x => Held(x.Status)))
        {
            b.Status = TravelBookingStatus.Cancelled; b.CancelledAt ??= now; b.UpdatedAt = now; b.UpdatedBy = userId; count++;
        }
        foreach (var b in (await unitOfWork.Repository<StaffTravelHotelBooking>()
                     .GetQueryable(x => x.TenantId == tenantId && x.StaffTravelRequestId == requestId && !x.IsDeleted)
                     .ToListAsync(cancellationToken)).Where(x => Held(x.Status)))
        {
            b.Status = TravelBookingStatus.Cancelled; b.CancelledAt ??= now; b.UpdatedAt = now; b.UpdatedBy = userId; count++;
        }
        // A company vehicle's leg is not here: its fleet trip is cancelled through Fleet (IStaffTravelFleetService.
        // CancelForRequestAsync, lane 6), which marks the leg.
        foreach (var b in (await unitOfWork.Repository<StaffTravelGroundTransport>()
                     .GetQueryable(x => x.TenantId == tenantId && x.StaffTravelRequestId == requestId && !x.IsDeleted
                                     && x.FleetTripId == null)
                     .ToListAsync(cancellationToken)).Where(x => Held(x.Status)))
        {
            b.Status = TravelBookingStatus.Cancelled; b.UpdatedAt = now; b.UpdatedBy = userId; count++;
        }
        foreach (var b in (await unitOfWork.Repository<StaffTravelCarRentalBooking>()
                     .GetQueryable(x => x.TenantId == tenantId && x.StaffTravelRequestId == requestId && !x.IsDeleted)
                     .ToListAsync(cancellationToken)).Where(x => Held(x.Status)))
        {
            b.Status = TravelBookingStatus.Cancelled; b.UpdatedAt = now; b.UpdatedBy = userId; count++;
        }
        return count;
    }
}
