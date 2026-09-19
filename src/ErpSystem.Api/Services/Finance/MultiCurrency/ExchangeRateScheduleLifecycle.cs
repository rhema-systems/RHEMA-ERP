using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.MultiCurrency;

internal sealed record ExchangeRateScheduleApprovalResult(
    Guid RateId,
    Guid? PredecessorRateId,
    DateTime? PredecessorPreviousEndDate,
    DateTime? PredecessorApprovedEndDate,
    Guid? SuccessorRateId,
    DateTime? RequestedEndDate,
    DateTime? ApprovedEndDate);

/// <summary>
/// Applies an approved exchange rate to its effective-dated schedule. The caller must
/// execute this operation inside the same serializable transaction that completes the
/// approval workflow so competing approvals cannot create overlapping approved ranges.
/// </summary>
internal static class ExchangeRateScheduleLifecycle
{
    internal static async Task<bool> RejectAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Guid rateId,
        Guid rejectedByUserId,
        string reason,
        DateTime rejectedAt,
        CancellationToken cancellationToken)
    {
        var rate = await db.ExchangeRates.FirstOrDefaultAsync(
            candidate => candidate.TenantId == tenantId
                && candidate.Id == rateId
                && !candidate.IsDeleted,
            cancellationToken);
        if (rate == null)
        {
            return false;
        }

        if (rate.ApprovalStatus != RateApprovalStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Only a pending exchange rate can be rejected; rate {rate.Id} is {rate.ApprovalStatus}.");
        }

        rate.ApprovalStatus = RateApprovalStatus.Rejected;
        rate.Comments = string.IsNullOrWhiteSpace(rate.Comments)
            ? reason
            : $"{rate.Comments}{Environment.NewLine}{reason}";
        rate.ModifiedDate = rejectedAt;
        rate.ModifiedByUserId = rejectedByUserId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    internal static async Task<ExchangeRateScheduleApprovalResult?> ApproveAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Guid rateId,
        Guid approverUserId,
        string? comments,
        DateTime approvedAt,
        CancellationToken cancellationToken)
    {
        var rate = await db.ExchangeRates.FirstOrDefaultAsync(
            candidate => candidate.TenantId == tenantId
                && candidate.Id == rateId
                && !candidate.IsDeleted,
            cancellationToken);
        if (rate == null)
        {
            return null;
        }

        if (rate.ApprovalStatus is RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved)
        {
            return new ExchangeRateScheduleApprovalResult(
                rate.Id,
                rate.PreviousRateId,
                null,
                null,
                null,
                rate.EndDate,
                rate.EndDate);
        }

        if (rate.ApprovalStatus != RateApprovalStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Only a pending exchange rate can be approved; rate {rate.Id} is {rate.ApprovalStatus}.");
        }

        var approvedSchedule = await db.ExchangeRates
            .Where(candidate => candidate.TenantId == tenantId
                && candidate.Id != rate.Id
                && !candidate.IsDeleted
                && candidate.BaseCurrencyCode == rate.BaseCurrencyCode
                && candidate.TargetCurrencyCode == rate.TargetCurrencyCode
                && candidate.RateType == rate.RateType
                && candidate.QuoteSide == rate.QuoteSide
                && (candidate.ApprovalStatus == RateApprovalStatus.Approved
                    || candidate.ApprovalStatus == RateApprovalStatus.AutoApproved))
            .OrderBy(candidate => candidate.EffectiveDate)
            .ThenBy(candidate => candidate.CreatedDate)
            .ToListAsync(cancellationToken);

        EnsureApprovedScheduleIsValid(approvedSchedule, rate);

        var start = rate.EffectiveDate.Date;
        if (approvedSchedule.Any(candidate => candidate.EffectiveDate.Date == start))
        {
            throw new InvalidOperationException(
                $"An approved {rate.BaseCurrencyCode}/{rate.TargetCurrencyCode} {rate.RateType}/{rate.QuoteSide} rate already starts on {start:yyyy-MM-dd}.");
        }

        var predecessor = approvedSchedule.LastOrDefault(candidate => candidate.EffectiveDate.Date < start);
        var successor = approvedSchedule.FirstOrDefault(candidate => candidate.EffectiveDate.Date > start);
        var requestedEndDate = rate.EndDate?.Date;
        DateTime? approvedEndDate = requestedEndDate;

        if (successor != null)
        {
            var successorBoundary = successor.EffectiveDate.Date.AddDays(-1);
            if (!approvedEndDate.HasValue || approvedEndDate.Value > successorBoundary)
            {
                approvedEndDate = successorBoundary;
            }
        }

        if (approvedEndDate.HasValue && approvedEndDate.Value < start)
        {
            throw new InvalidOperationException(
                "The approved exchange-rate window would end before its effective date.");
        }

        var predecessorPreviousEndDate = predecessor?.EndDate?.Date;
        DateTime? predecessorApprovedEndDate = predecessorPreviousEndDate;
        if (predecessor != null
            && (!predecessor.EndDate.HasValue || predecessor.EndDate.Value.Date >= start))
        {
            predecessorApprovedEndDate = start.AddDays(-1);
            predecessor.EndDate = predecessorApprovedEndDate;
            predecessor.ModifiedDate = approvedAt;
            predecessor.ModifiedByUserId = approverUserId;
        }

        rate.EndDate = approvedEndDate;
        rate.PreviousRateId = predecessor?.Id;
        rate.ApprovalStatus = RateApprovalStatus.Approved;
        rate.ApprovalDate = approvedAt;
        rate.ApprovedByUserId = approverUserId;
        rate.Comments = comments ?? rate.Comments;
        rate.ModifiedDate = approvedAt;
        rate.ModifiedByUserId = approverUserId;

        if (successor != null)
        {
            successor.PreviousRateId = rate.Id;
            successor.ModifiedDate = approvedAt;
            successor.ModifiedByUserId = approverUserId;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ExchangeRateScheduleApprovalResult(
            rate.Id,
            predecessor?.Id,
            predecessorPreviousEndDate,
            predecessorApprovedEndDate,
            successor?.Id,
            requestedEndDate,
            approvedEndDate);
    }

    private static void EnsureApprovedScheduleIsValid(
        IReadOnlyList<ExchangeRate> approvedSchedule,
        ExchangeRate pendingRate)
    {
        for (var index = 1; index < approvedSchedule.Count; index++)
        {
            var previous = approvedSchedule[index - 1];
            var current = approvedSchedule[index];
            if (!previous.EndDate.HasValue
                || previous.EndDate.Value.Date >= current.EffectiveDate.Date)
            {
                throw new InvalidOperationException(
                    $"The approved {pendingRate.BaseCurrencyCode}/{pendingRate.TargetCurrencyCode} {pendingRate.RateType}/{pendingRate.QuoteSide} schedule already contains overlapping ranges. Repair the approved schedule before approving another rate.");
            }
        }
    }
}
