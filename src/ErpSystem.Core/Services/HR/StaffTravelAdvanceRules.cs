using System.Linq.Expressions;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// What a travel advance's money state means, in one place (travel final closure, lane 3).
/// </summary>
/// <remarks>
/// <para><b>Cash out</b> is money that left the organisation and has not come back or been accounted for: an advance
/// Disbursed, PartiallySettled or Overdue with something unsettled. Before lane 3 every reader wrote its own list, and
/// three of them — claim recovery, the separation clearance and the sweep — named only Disbursed and PartiallySettled.
/// Giving <see cref="TravelAdvanceStatus.Overdue"/> a writer would have dropped an overdue advance from all three: a
/// claim would no longer recover it, a leaver's clearance would not list it, and the reminder would stop the day it
/// became overdue (finding N1). Every reader of "what the traveller still holds" uses <see cref="CashOut"/>.</para>
///
/// <para><b>Unsettled</b> is 0 until the advance is disbursed — a requested or approved advance owes nothing, because
/// nothing has been paid (B8) — and afterwards the approved amount less what claims recovered and cash handed back
/// (<see cref="StaffTravelAdvance.SettledAmount"/> includes a refund; <see cref="StaffTravelAdvance.RefundedAmount"/>
/// says how much of it was cash).</para>
/// </remarks>
public static class StaffTravelAdvanceRules
{
    /// <summary>The days after the trip's end an advance is settled within when its policy sets no claim window.</summary>
    public const int DefaultSettlementDays = 30;

    public static bool IsCashOutStatus(TravelAdvanceStatus status)
        => status is TravelAdvanceStatus.Disbursed or TravelAdvanceStatus.PartiallySettled or TravelAdvanceStatus.Overdue;

    /// <summary>Money the traveller still holds. Translates to SQL; compose it with <c>Where</c>.</summary>
    public static readonly Expression<Func<StaffTravelAdvance, bool>> CashOut = a =>
        (a.Status == TravelAdvanceStatus.Disbursed
         || a.Status == TravelAdvanceStatus.PartiallySettled
         || a.Status == TravelAdvanceStatus.Overdue)
        && a.UnsettledAmount > 0m;

    /// <summary>Approved money that stands against the trip's budget: everything approved and not withdrawn.</summary>
    public static bool CountsAgainstBudget(TravelAdvanceStatus status)
        => status is TravelAdvanceStatus.Approved or TravelAdvanceStatus.Disbursed or TravelAdvanceStatus.PartiallySettled
            or TravelAdvanceStatus.FullySettled or TravelAdvanceStatus.Overdue or TravelAdvanceStatus.WrittenOff;

    /// <summary>
    /// The status an advance with cash out takes from its figures: settled when nothing is left, overdue once its
    /// deadline has passed with something left, partly settled once anything came back, otherwise disbursed. One rule
    /// for a claim's recovery, a refund and the sweep, so a partial settlement after the deadline does not turn an
    /// overdue advance back into a merely partly settled one until the next night.
    /// </summary>
    public static TravelAdvanceStatus SettlementStatus(StaffTravelAdvance advance, DateOnly today)
    {
        if (advance.UnsettledAmount <= 0m) return TravelAdvanceStatus.FullySettled;
        if (advance.SettlementDeadline is DateOnly deadline && deadline < today) return TravelAdvanceStatus.Overdue;
        return advance.SettledAmount > 0m ? TravelAdvanceStatus.PartiallySettled : TravelAdvanceStatus.Disbursed;
    }

    /// <summary>The trip's end plus its approved policy's claim window, or <see cref="DefaultSettlementDays"/>.</summary>
    public static DateOnly DefaultDeadline(DateOnly travelEndDate, int? policyClaimWindowDays)
        => travelEndDate.AddDays(policyClaimWindowDays is > 0 ? policyClaimWindowDays.Value : DefaultSettlementDays);

    /// <summary>No money has gone out yet, so the advance can still be withdrawn.</summary>
    public static bool IsUndisbursed(TravelAdvanceStatus status)
        => status is TravelAdvanceStatus.Requested or TravelAdvanceStatus.Approved;

    /// <summary>
    /// Withdraws an advance no money has left for — the desk's Cancel, and the trip's cancel for each of its
    /// advances (N2: a cancelled trip kept them live, and disbursement checked nothing about the trip).
    /// </summary>
    public static void ApplyCancellation(
        StaffTravelAdvance advance, string reason, Guid? byEmployeeId, Guid byUserId, DateTime now)
    {
        advance.Status = TravelAdvanceStatus.Cancelled;
        advance.CancelledAt = now;
        advance.CancelledById = byEmployeeId;
        advance.CancellationReason = reason;
        advance.UnsettledAmount = 0m;
        advance.UpdatedBy = byUserId.ToString();
        advance.UpdatedAt = now;
    }
}
