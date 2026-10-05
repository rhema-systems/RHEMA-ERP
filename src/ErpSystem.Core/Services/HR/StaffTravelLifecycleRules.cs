using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The trip's lifecycle tests the desk's verbs and the nightly sweep share (travel final closure, lane 8, slice 8c — D-6,
/// D-47, D-48, D-51). Tenant-explicit: each takes the trip, whose tenant it reads, so the sweep can ask with nobody signed
/// in (U3).
/// </summary>
public static class StaffTravelLifecycleRules
{
    /// <summary>
    /// What still keeps a completed trip open, or null when it is settled — lane 1's Close rule: every claim paid or
    /// rejected, every advance settled, written off, rejected or cancelled. The desk's Close and the sweep's both ask this.
    /// </summary>
    public static async Task<string?> OpenItemAsync(IUnitOfWork unitOfWork, StaffTravelRequest trip, CancellationToken cancellationToken)
    {
        var openClaim = await unitOfWork.Repository<StaffTravelExpenseClaim>().GetQueryable()
            .Where(c => c.TenantId == trip.TenantId
                     && c.StaffTravelRequestId == trip.Id
                     && c.Status != TravelClaimStatus.Paid
                     && c.Status != TravelClaimStatus.Rejected)
            .OrderBy(c => c.ClaimNumber)
            .Select(c => new { c.ClaimNumber, c.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (openClaim is not null)
            return $"Expense claim {openClaim.ClaimNumber} is {openClaim.Status}. A trip closes once every claim on it is paid or rejected.";

        var openAdvance = await unitOfWork.Repository<StaffTravelAdvance>().GetQueryable()
            .Where(a => a.TenantId == trip.TenantId
                     && a.StaffTravelRequestId == trip.Id
                     && a.Status != TravelAdvanceStatus.FullySettled
                     && a.Status != TravelAdvanceStatus.WrittenOff
                     && a.Status != TravelAdvanceStatus.Rejected
                     && a.Status != TravelAdvanceStatus.Cancelled)
            .OrderBy(a => a.AdvanceNumber)
            .Select(a => new { a.AdvanceNumber, a.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (openAdvance is not null)
            return $"Advance {openAdvance.AdvanceNumber} is {openAdvance.Status}. A trip closes once every advance on it is " +
                   "settled, written off, rejected or cancelled.";

        return null;
    }

    /// <summary>
    /// The last day a claim may first be submitted for the trip: its end plus the approved policy's claim window, or lane
    /// 3's default (<see cref="StaffTravelAdvanceRules.DefaultDeadline"/>, 30 days) when the trip has no policy with one.
    /// </summary>
    public static async Task<DateOnly> ClaimWindowLastDayAsync(IUnitOfWork unitOfWork, StaffTravelRequest trip, CancellationToken cancellationToken)
    {
        int? window = null;
        if (trip.PolicyId is Guid policyId)
            window = await unitOfWork.Repository<StaffTravelPolicy>()
                .GetQueryable(p => p.Id == policyId && p.TenantId == trip.TenantId && p.ApprovedById != null)
                .Select(p => (int?)p.ExpenseSubmissionDays)
                .FirstOrDefaultAsync(cancellationToken);
        return StaffTravelAdvanceRules.DefaultDeadline(trip.TravelEndDate, window);
    }

    /// <summary>
    /// D-48: what was spent on an under-way trip, or null when nothing was — a claim filed, advance cash out, a booking a
    /// supplier committed to or one already used, a company vehicle dispatched or used. Only a trip with nothing spent is
    /// cancelled as "did not travel"; the rest is completed, and settled as usual.
    /// </summary>
    public static async Task<string?> SpentOnAsync(IUnitOfWork unitOfWork, StaffTravelRequest trip, CancellationToken cancellationToken)
    {
        var claim = await unitOfWork.Repository<StaffTravelExpenseClaim>()
            .GetQueryable(c => c.TenantId == trip.TenantId && c.StaffTravelRequestId == trip.Id && !c.IsDeleted)
            .OrderBy(c => c.ClaimNumber)
            .Select(c => c.ClaimNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (claim is not null) return $"expense claim {claim} is filed on it";

        var advance = await unitOfWork.Repository<StaffTravelAdvance>().GetQueryable()
            .Where(a => a.TenantId == trip.TenantId && a.StaffTravelRequestId == trip.Id)
            .Where(StaffTravelAdvanceRules.CashOut)
            .OrderBy(a => a.AdvanceNumber)
            .Select(a => a.AdvanceNumber)
            .FirstOrDefaultAsync(cancellationToken);
        if (advance is not null) return $"advance {advance} has been paid out";

        var committed = await StaffTravelBookingRules.CommittedBookingsAsync(unitOfWork, trip.TenantId, trip.Id, cancellationToken);
        if (committed.Count > 0) return $"{committed[0]} is committed";

        static bool Used(TravelBookingStatus s) => s is TravelBookingStatus.Completed or TravelBookingStatus.NoShow;
        var used =
            (await unitOfWork.Repository<StaffTravelFlightBooking>()
                .GetQueryable(b => b.TenantId == trip.TenantId && b.StaffTravelRequestId == trip.Id && !b.IsDeleted)
                .Select(b => b.Status).ToListAsync(cancellationToken)).Any(Used)
            || (await unitOfWork.Repository<StaffTravelHotelBooking>()
                .GetQueryable(b => b.TenantId == trip.TenantId && b.StaffTravelRequestId == trip.Id && !b.IsDeleted)
                .Select(b => b.Status).ToListAsync(cancellationToken)).Any(Used)
            || (await unitOfWork.Repository<StaffTravelGroundTransport>()
                .GetQueryable(b => b.TenantId == trip.TenantId && b.StaffTravelRequestId == trip.Id && !b.IsDeleted && b.FleetTripId == null)
                .Select(b => b.Status).ToListAsync(cancellationToken)).Any(Used)
            || (await unitOfWork.Repository<StaffTravelCarRentalBooking>()
                .GetQueryable(b => b.TenantId == trip.TenantId && b.StaffTravelRequestId == trip.Id && !b.IsDeleted)
                .Select(b => b.Status).ToListAsync(cancellationToken)).Any(Used);
        if (used) return "a booking on it has been used or charged as a no-show";

        var fleetTripIds = await unitOfWork.Repository<StaffTravelGroundTransport>()
            .GetQueryable(g => g.TenantId == trip.TenantId && g.StaffTravelRequestId == trip.Id && !g.IsDeleted && g.FleetTripId != null)
            .Select(g => g.FleetTripId!.Value)
            .ToListAsync(cancellationToken);
        if (fleetTripIds.Count > 0
            && await unitOfWork.Repository<FleetTrip>()
                .GetQueryable(t => t.TenantId == trip.TenantId && fleetTripIds.Contains(t.Id) && t.Status == FleetTripStatuses.Completed)
                .AnyAsync(cancellationToken))
            return "its company vehicle has made the trip";

        return null;
    }
}
