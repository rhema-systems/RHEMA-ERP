using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// What a trip has actually committed and spent, computed from travel's own records, in the budget's currency.
/// <see cref="Actual"/> is <see cref="ClaimsPaid"/> plus <see cref="AdvancesPaidOut"/> (lane 3).
/// </summary>
public sealed record TravelBudgetSpend(decimal Committed, decimal Actual, decimal ClaimsPaid, decimal AdvancesPaidOut);

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
///   <item><b>Actual</b> — cash that has actually gone out: expense claims <i>paid</i> and, since lane 3,
///   advances paid out.</item>
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
/// <para><b>Currency (lane 3).</b> Every figure is converted into the budget's currency — the trip's —
/// through <see cref="HrCurrencyBridge"/>, at the rate on the day it applies to: a booking when it was made,
/// a claim when it was paid, an advance when it went out; the same currency needs no rate. It summed
/// everything as recorded: a claim is kept in the base currency and an international trip is often costed
/// in dollars, so the actual figure added cedis to dollars. Finance holding no rate is refused, as a claim
/// line is — travel never invents one.</para>
///
/// <para><b>What lane 3 changed (B10).</b> <i>Actual</i> counts advance cash paid out — less what was handed
/// back — as well as claims paid: a disbursed advance is money that has left, and was counted nowhere. A claim
/// paid against an advance pays only the balance, so the two together are the trip's cash and nothing is
/// counted twice. <i>Committed</i> leaves out a no-show and keeps a cancelled or refunded booking's
/// cancellation fee, which is still owed.</para>
/// </remarks>
public sealed class StaffTravelBudgetRollup
{
    private readonly IStaffTravelFlightBookingRepository _flights;
    private readonly IStaffTravelHotelBookingRepository _hotels;
    private readonly IStaffTravelGroundTransportRepository _ground;
    private readonly IStaffTravelCarRentalBookingRepository _carRentals;
    private readonly IStaffTravelExpenseClaimRepository _claims;
    private readonly IStaffTravelAdvanceRepository _advances;
    private readonly HrCurrencyBridge _currency;

    public StaffTravelBudgetRollup(
        IStaffTravelFlightBookingRepository flights,
        IStaffTravelHotelBookingRepository hotels,
        IStaffTravelGroundTransportRepository ground,
        IStaffTravelCarRentalBookingRepository carRentals,
        IStaffTravelExpenseClaimRepository claims,
        IStaffTravelAdvanceRepository advances,
        HrCurrencyBridge currency)
    {
        _flights = flights;
        _hotels = hotels;
        _ground = ground;
        _carRentals = carRentals;
        _claims = claims;
        _advances = advances;
        _currency = currency;
    }

    /// <summary>
    /// A cancelled, refunded or no-show booking is not a commitment. Everything else is — including one
    /// still Pending, because the desk has asked for it and the budget must show that before it is
    /// confirmed, which is exactly when an overspend is still preventable.
    /// </summary>
    private static bool IsCommitted(TravelBookingStatus status)
        => status is not (TravelBookingStatus.Cancelled or TravelBookingStatus.Refunded or TravelBookingStatus.NoShow);

    /// <summary>A cancelled or refunded booking still costs its cancellation fee (lane 3, B10).</summary>
    private static bool IsCancelled(TravelBookingStatus status)
        => status is TravelBookingStatus.Cancelled or TravelBookingStatus.Refunded;

    public async Task<TravelBudgetSpend> ComputeAsync(
        Guid tenantId, Guid requestId, string budgetCurrency, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        async Task<decimal> In(decimal amount, string? currency, DateTime? at)
            => amount == 0m ? 0m : await _currency.ConvertBetweenAsync(
                amount, string.IsNullOrWhiteSpace(currency) ? budgetCurrency : currency, budgetCurrency,
                at is DateTime d ? DateOnly.FromDateTime(d) : today, cancellationToken);

        var committed = 0m;
        foreach (var f in (await _flights.GetByRequestIdAsync(requestId)).Where(f => f.TenantId == tenantId && !f.IsDeleted))
        {
            if (IsCommitted(f.Status)) committed += await In(f.TotalFare + f.TaxesAndFees, f.CurrencyCode, f.BookedAt ?? f.CreatedAt);
            else if (IsCancelled(f.Status)) committed += await In(f.CancellationFee ?? 0m, f.CurrencyCode, f.BookedAt ?? f.CreatedAt);
        }

        foreach (var h in (await _hotels.GetByRequestIdAsync(requestId)).Where(h => h.TenantId == tenantId && !h.IsDeleted))
        {
            if (IsCommitted(h.Status)) committed += await In(h.TotalCost, h.CurrencyCode, h.BookedAt ?? h.CreatedAt);
            else if (IsCancelled(h.Status)) committed += await In(h.CancellationFee ?? 0m, h.CurrencyCode, h.BookedAt ?? h.CreatedAt);
        }

        // Ground transport records an estimate up front and an actual afterwards; the actual is the
        // better number the moment it exists.
        foreach (var g in (await _ground.GetByRequestIdAsync(requestId))
                     .Where(g => g.TenantId == tenantId && !g.IsDeleted && IsCommitted(g.Status)))
            committed += await In(g.ActualCost ?? g.EstimatedCost ?? 0m, g.CurrencyCode, g.CreatedAt);

        foreach (var c in (await _carRentals.GetByRequestIdAsync(requestId))
                     .Where(c => c.TenantId == tenantId && !c.IsDeleted && IsCommitted(c.Status)))
            committed += await In(c.TotalCost, c.CurrencyCode, c.BookedAt ?? c.CreatedAt);

        // Claims paid: what is PAID, not claimed or even approved — an approved claim awaiting payment is a
        // liability, not spend. `NetPayable` is what left the organisation: already net of any advance
        // recovered, which is counted below as the advance itself.
        var claimsPaid = 0m;
        foreach (var c in (await _claims.GetByRequestIdAsync(requestId))
                     .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.Status == TravelClaimStatus.Paid))
            claimsPaid += await In(c.NetPayable, c.CurrencyCode, c.PaidAt);

        // Advances paid out, less cash handed back: money that left, whether a claim later recovered it or it
        // was written off (B10 — it was counted nowhere).
        var advancesPaidOut = 0m;
        foreach (var a in (await _advances.GetByRequestIdAsync(requestId))
                     .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.DisbursedAt != null
                              && a.Status is TravelAdvanceStatus.Disbursed or TravelAdvanceStatus.PartiallySettled
                                  or TravelAdvanceStatus.FullySettled or TravelAdvanceStatus.Overdue
                                  or TravelAdvanceStatus.WrittenOff))
            advancesPaidOut += await In((a.ApprovedAmount ?? 0m) - a.RefundedAmount, a.CurrencyCode, a.DisbursedAt);

        var round = (decimal v) => decimal.Round(v, 2, MidpointRounding.AwayFromZero);
        return new TravelBudgetSpend(
            round(committed), round(claimsPaid + advancesPaidOut), round(claimsPaid), round(advancesPaidOut));
    }
}
