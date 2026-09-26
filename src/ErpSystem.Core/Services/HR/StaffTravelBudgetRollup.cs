using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// What a trip has actually committed and spent, computed from travel's own records.
/// </summary>
public sealed record TravelBudgetSpend(decimal Committed, decimal Actual);

/// <summary>
/// Rolls a travel budget's committed and actual spend up from the bookings and claims on the same
/// request.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> <c>TotalCommitted</c>, <c>TotalActual</c> and <c>Variance</c> had
/// no writer but a client <c>PUT</c> — <c>UpdateEntity</c> assigned all three straight off the DTO.
/// So a budget-versus-actual screen showed whatever someone last typed, while the bookings and
/// claims that constitute the spend sat on the same request, unread. That is worse than showing
/// nothing: a hand-entered actual looks exactly as authoritative as a computed one.</para>
///
/// <para><b>The two figures measure different routes, deliberately, and the screen must say so.</b>
/// <list type="bullet">
///   <item><b>Committed</b> — the value of bookings that have been made and not cancelled. Money the
///   organisation is on the hook for whether or not it has left yet.</item>
///   <item><b>Actual</b> — expense claims that have been <i>paid</i>. Cash that has actually gone out
///   through the claim route.</item>
/// </list>
/// A booking paid direct to a vendor is committed but never becomes a claim, so the two do not sum
/// to total spend and neither is a superset of the other. Adding them would double-count anything
/// an employee paid for and reclaimed. Labelling them as separate quantities is honest; combining
/// them would not be.</para>
///
/// <para>⚠ <b>This is a within-module rollup, not an accounting treatment.</b> Per decision D-4, all
/// GL posting waits for the comprehensive Finance sweep after the HR module is complete, and that
/// sweep may well redefine what "actual travel spend" means once advances, direct vendor payment and
/// claims are reconciled in one place. Until then this is travel's own honest arithmetic over its
/// own records, and it is registered in <c>docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md</c>.</para>
///
/// <para><b>Currency.</b> Figures are summed <b>as recorded</b>, without conversion. A budget and its
/// bookings are normally costed in one currency; where they are not, the sum is meaningless and
/// converting silently would hide that rather than fix it. Slice 6 established that travel never
/// invents a rate — if mixed-currency budgets become real, the rollup should convert through
/// <see cref="StaffTravelCurrencyBridge"/> and refuse when Finance holds no rate, exactly as claim
/// totals do.</para>
/// </remarks>
public sealed class StaffTravelBudgetRollup
{
    private readonly IStaffTravelFlightBookingRepository _flights;
    private readonly IStaffTravelHotelBookingRepository _hotels;
    private readonly IStaffTravelGroundTransportRepository _ground;
    private readonly IStaffTravelCarRentalBookingRepository _carRentals;
    private readonly IStaffTravelExpenseClaimRepository _claims;

    public StaffTravelBudgetRollup(
        IStaffTravelFlightBookingRepository flights,
        IStaffTravelHotelBookingRepository hotels,
        IStaffTravelGroundTransportRepository ground,
        IStaffTravelCarRentalBookingRepository carRentals,
        IStaffTravelExpenseClaimRepository claims)
    {
        _flights = flights;
        _hotels = hotels;
        _ground = ground;
        _carRentals = carRentals;
        _claims = claims;
    }

    /// <summary>
    /// A cancelled or refunded booking is not a commitment. Everything else is — including one still
    /// Pending, because the desk has asked for it and the budget must show that before it is
    /// confirmed, which is exactly when an overspend is still preventable.
    /// </summary>
    private static bool IsCommitted(TravelBookingStatus status)
        => status is not (TravelBookingStatus.Cancelled or TravelBookingStatus.Refunded);

    public async Task<TravelBudgetSpend> ComputeAsync(
        Guid tenantId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var flights = (await _flights.GetByRequestIdAsync(requestId))
            .Where(f => f.TenantId == tenantId && !f.IsDeleted && IsCommitted(f.Status))
            .Sum(f => f.TotalFare + f.TaxesAndFees);

        var hotels = (await _hotels.GetByRequestIdAsync(requestId))
            .Where(h => h.TenantId == tenantId && !h.IsDeleted && IsCommitted(h.Status))
            .Sum(h => h.TotalCost);

        // Ground transport records an estimate up front and an actual afterwards; the actual is the
        // better number the moment it exists.
        var ground = (await _ground.GetByRequestIdAsync(requestId))
            .Where(g => g.TenantId == tenantId && !g.IsDeleted && IsCommitted(g.Status))
            .Sum(g => g.ActualCost ?? g.EstimatedCost ?? 0m);

        var cars = (await _carRentals.GetByRequestIdAsync(requestId))
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && IsCommitted(c.Status))
            .Sum(c => c.TotalCost);

        // Actual is what has been PAID, not what has been claimed or even approved — an approved
        // claim awaiting payment is a liability, not spend. `NetPayable` is the figure that leaves
        // the organisation: it is already net of any advance recovered against the claim, so using
        // TotalApproved here would double-count money the employee was given up front.
        var paid = (await _claims.GetByRequestIdAsync(requestId))
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.Status == TravelClaimStatus.Paid)
            .Sum(c => c.NetPayable);

        return new TravelBudgetSpend(flights + hotels + ground + cars, paid);
    }
}
