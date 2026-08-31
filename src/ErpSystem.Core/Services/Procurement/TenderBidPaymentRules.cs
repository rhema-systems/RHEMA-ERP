using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Services.Procurement;

internal enum TenderBidPaymentAdmissionStatus
{
    NotRequired,
    Verified,
    PendingVerification,
    PendingProviderConfirmation,
    Rejected,
    EvidenceMissing
}

internal sealed record TenderFeePaymentDecision(
    TenderFee Fee,
    TenderPayment? Payment,
    TenderBidPaymentAdmissionStatus Status);

internal sealed record TenderBidPaymentAdmissionDecision(
    IReadOnlyList<TenderFeePaymentDecision> Fees)
{
    public bool PaymentRequired => Fees.Count > 0;
    public bool HasPayment => Fees.Count == 0 || Fees.All(item => item.Payment is not null);
    public bool PaymentSatisfied => Fees.All(item =>
        item.Status is TenderBidPaymentAdmissionStatus.NotRequired or TenderBidPaymentAdmissionStatus.Verified);
    public bool CanSubmitSealed => Fees.All(item =>
        item.Status is TenderBidPaymentAdmissionStatus.NotRequired or
            TenderBidPaymentAdmissionStatus.Verified or
            TenderBidPaymentAdmissionStatus.PendingVerification);
    public bool PendingVerification => CanSubmitSealed && !PaymentSatisfied;
    public bool CanOpenOrEvaluate => PaymentSatisfied;

    public TenderBidPaymentAdmissionStatus BlockingStatus =>
        Fees.Any(item => item.Status == TenderBidPaymentAdmissionStatus.Rejected)
            ? TenderBidPaymentAdmissionStatus.Rejected
            : Fees.Any(item => item.Status == TenderBidPaymentAdmissionStatus.EvidenceMissing)
                ? TenderBidPaymentAdmissionStatus.EvidenceMissing
                : Fees.Any(item => item.Status == TenderBidPaymentAdmissionStatus.PendingProviderConfirmation)
                    ? TenderBidPaymentAdmissionStatus.PendingProviderConfirmation
                    : Fees.Any(item => item.Status == TenderBidPaymentAdmissionStatus.PendingVerification)
                        ? TenderBidPaymentAdmissionStatus.PendingVerification
                        : PaymentRequired
                            ? TenderBidPaymentAdmissionStatus.Verified
                            : TenderBidPaymentAdmissionStatus.NotRequired;

    public string Code => BlockingStatus switch
    {
        TenderBidPaymentAdmissionStatus.Rejected => "TENDER_BID_PAYMENT_REJECTED",
        TenderBidPaymentAdmissionStatus.EvidenceMissing => "TENDER_BID_PAYMENT_EVIDENCE_REQUIRED",
        TenderBidPaymentAdmissionStatus.PendingProviderConfirmation => "TENDER_BID_PAYMENT_PROVIDER_PENDING",
        TenderBidPaymentAdmissionStatus.PendingVerification => "TENDER_BID_PAYMENT_VERIFICATION_PENDING",
        _ => "TENDER_BID_PAYMENT_REQUIRED"
    };

    public string Message
    {
        get
        {
            var feeNames = string.Join(", ", Fees
                .Where(item => item.Status is not TenderBidPaymentAdmissionStatus.Verified and
                    not TenderBidPaymentAdmissionStatus.NotRequired)
                .Select(item => item.Fee.FeeType)
                .Distinct(StringComparer.OrdinalIgnoreCase));
            return BlockingStatus switch
            {
                TenderBidPaymentAdmissionStatus.Rejected =>
                    $"The required tender fee payment was rejected for: {feeNames}. This bid is excluded from opening and evaluation.",
                TenderBidPaymentAdmissionStatus.EvidenceMissing =>
                    $"A payment receipt or transaction evidence reference is required for: {feeNames}.",
                TenderBidPaymentAdmissionStatus.PendingProviderConfirmation =>
                    $"Online payment provider confirmation is still pending for: {feeNames}.",
                TenderBidPaymentAdmissionStatus.PendingVerification =>
                    $"Manual payment verification is still pending for: {feeNames}. The sealed bid cannot be opened or evaluated until verification is complete.",
                _ => "Verified payment is required before this bid can be opened or evaluated."
            };
        }
    }
}

internal static class TenderBidPaymentRules
{
    public static TenderBidPaymentAdmissionDecision Assess(
        TenderBid bid,
        IEnumerable<TenderFee> fees,
        IEnumerable<TenderPayment> payments)
    {
        var requiredFees = fees
            .Where(fee => !fee.IsDeleted &&
                          fee.TenantId == bid.TenantId &&
                          fee.TenderId == bid.TenderId &&
                          fee.IsMandatory &&
                          fee.Amount > 0m)
            .ToList();
        var scopedPayments = payments
            .Where(payment => !payment.IsDeleted &&
                              payment.TenantId == bid.TenantId &&
                              payment.BusinessPartnerId == bid.BusinessPartnerId)
            .ToList();

        var decisions = requiredFees.Select(fee =>
        {
            var feePayments = scopedPayments
                .Where(payment => payment.TenderFeeId == fee.Id)
                .OrderByDescending(payment => payment.VerifiedDate ?? payment.PaymentDate)
                .ThenByDescending(payment => payment.CreatedAt)
                .ToList();
            var satisfied = feePayments.FirstOrDefault(IsSatisfied);
            if (satisfied is not null)
                return new TenderFeePaymentDecision(fee, satisfied, TenderBidPaymentAdmissionStatus.Verified);

            var latest = feePayments.FirstOrDefault();
            if (latest is null)
                return new TenderFeePaymentDecision(fee, null, TenderBidPaymentAdmissionStatus.EvidenceMissing);
            if (string.Equals(latest.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
                return new TenderFeePaymentDecision(fee, latest, TenderBidPaymentAdmissionStatus.Rejected);
            if (!string.Equals(latest.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                return new TenderFeePaymentDecision(fee, latest, TenderBidPaymentAdmissionStatus.EvidenceMissing);
            if (!IsManual(fee.PaymentMethod))
                return new TenderFeePaymentDecision(
                    fee, latest, TenderBidPaymentAdmissionStatus.PendingProviderConfirmation);
            return HasEvidence(latest)
                ? new TenderFeePaymentDecision(fee, latest, TenderBidPaymentAdmissionStatus.PendingVerification)
                : new TenderFeePaymentDecision(fee, latest, TenderBidPaymentAdmissionStatus.EvidenceMissing);
        }).ToList();

        return new TenderBidPaymentAdmissionDecision(decisions);
    }

    public static bool IsSatisfied(TenderPayment payment) =>
        string.Equals(payment.Status, "Verified", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(payment.Status, "Completed", StringComparison.OrdinalIgnoreCase);

    public static bool HasEvidence(TenderPayment payment) =>
        !string.IsNullOrWhiteSpace(payment.PaymentProof) ||
        !string.IsNullOrWhiteSpace(payment.TransactionId);

    public static bool IsManual(string? paymentMethod)
    {
        var normalized = new string((paymentMethod ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        return normalized is "bank" or "banktransfer" or "cash" or "cheque" or "check" or
            "manual" or "bankdeposit" or "deposit";
    }
}
