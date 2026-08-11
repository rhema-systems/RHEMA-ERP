namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveyFinalAccountAmounts(
    decimal GrossFinalAccountValue,
    decimal TotalDeductionAmount,
    decimal NetFinalAccountValue,
    decimal RetentionOutstandingAmount,
    decimal FinalPaymentAmount);

public static class QuantitySurveyFinalAccountRules
{
    public static QuantitySurveyFinalAccountAmounts Calculate(
        decimal originalContractValue,
        decimal approvedVariationAmount,
        decimal approvedClaimAmount,
        decimal approvedEscalationAmount,
        decimal advanceRecoveryAmount,
        decimal materialDeductionAmount,
        decimal otherDeductionAmount,
        decimal retentionHeldAmount,
        decimal retentionReleasedAmount,
        decimal paidToDateAmount)
    {
        var values = new[]
        {
            originalContractValue, approvedVariationAmount, approvedClaimAmount, approvedEscalationAmount,
            advanceRecoveryAmount, materialDeductionAmount, otherDeductionAmount,
            retentionHeldAmount, retentionReleasedAmount, paidToDateAmount
        };
        if (values.Any(value => value < 0m))
            throw new InvalidOperationException("Final-account reconciliation values cannot be negative.");
        if (retentionReleasedAmount > retentionHeldAmount)
            throw new InvalidOperationException("Retention released cannot exceed retention held.");

        var gross = Round(originalContractValue + approvedVariationAmount + approvedClaimAmount + approvedEscalationAmount);
        var deductions = Round(advanceRecoveryAmount + materialDeductionAmount + otherDeductionAmount);
        if (deductions > gross)
            throw new InvalidOperationException("Final-account deductions cannot exceed the gross final-account value.");
        var net = Round(gross - deductions);
        var retentionOutstanding = Round(retentionHeldAmount - retentionReleasedAmount);
        var finalPayment = Round(Math.Max(0m, net - paidToDateAmount));
        return new(gross, deductions, net, retentionOutstanding, finalPayment);
    }

    public static IReadOnlyList<string> ClosureBlockers(
        bool hasApprovedBoq,
        bool hasPendingCommercialRecords,
        decimal retentionOutstandingAmount,
        decimal finalPaymentAmount)
    {
        var blockers = new List<string>();
        if (!hasApprovedBoq) blockers.Add("An approved Published BoQ is required.");
        if (hasPendingCommercialRecords) blockers.Add("Pending variations, valuations, certificates, claims or escalation records must be resolved.");
        if (retentionOutstandingAmount > 0.01m) blockers.Add("Outstanding retention must be released or formally resolved.");
        if (finalPaymentAmount > 0.01m) blockers.Add("Finance-owned payments do not yet settle the final account.");
        return blockers;
    }

    public static void RequireIndependentApprover(Guid preparedById, Guid? submittedById, Guid actorUserId)
    {
        if (preparedById == Guid.Empty || actorUserId == Guid.Empty)
            throw new InvalidOperationException("Valid final-account actor identities are required.");
        if (preparedById == actorUserId || submittedById == actorUserId)
            throw new InvalidOperationException("The final-account preparer or submitter cannot approve it.");
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
