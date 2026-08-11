namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveySubcontractValuationAmounts(
    decimal PreviouslyCertified, decimal CurrentCertified, decimal RetentionHeld,
    decimal RetentionReleased, decimal BackCharge, decimal ContraCharge, decimal Tax, decimal Net);

public static class QuantitySurveySubcontractRules
{
    public static IReadOnlyList<string> ValidateContract(
        decimal parentContractValue, decimal subcontractValue, decimal retentionPercent,
        DateTime startDate, DateTime? endDate, bool parentAllowsSubcontracting,
        bool policyControlsSubcontracts, bool activePartner, bool paymentTermValid)
    {
        var issues = new List<string>();
        if (!parentAllowsSubcontracting) issues.Add("The parent Works contract does not allow subcontracting.");
        if (!policyControlsSubcontracts) issues.Add("Subcontract controls are disabled by the effective QS policy.");
        if (!activePartner) issues.Add("Select an active approved supplier or contractor.");
        if (!paymentTermValid) issues.Add("Select an active Finance payment term.");
        if (subcontractValue <= 0 || subcontractValue > parentContractValue) issues.Add("Subcontract value must be positive and cannot exceed the parent Works-contract value.");
        if (retentionPercent is < 0 or > 100) issues.Add("Retention percentage must be between zero and 100.");
        if (startDate == default) issues.Add("Start date is required.");
        if (endDate.HasValue && endDate.Value.Date < startDate.Date) issues.Add("End date cannot precede the start date.");
        return issues;
    }

    public static QuantitySurveySubcontractValuationAmounts Calculate(
        decimal subcontractValue, decimal claimedToDate, decimal assessedToDate,
        decimal previouslyCertified, decimal retentionPercent, decimal retentionReleased,
        decimal approvedBackCharge, decimal approvedContraCharge, decimal tax,
        bool taxInclusive = false)
    {
        decimal R(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        subcontractValue = R(subcontractValue); claimedToDate = R(claimedToDate); assessedToDate = R(assessedToDate);
        previouslyCertified = R(previouslyCertified); retentionReleased = R(retentionReleased);
        approvedBackCharge = R(approvedBackCharge); approvedContraCharge = R(approvedContraCharge); tax = R(tax);
        if (claimedToDate <= 0 || claimedToDate > subcontractValue) throw new ArgumentOutOfRangeException(nameof(claimedToDate));
        if (assessedToDate < previouslyCertified || assessedToDate > claimedToDate || assessedToDate > subcontractValue)
            throw new ArgumentOutOfRangeException(nameof(assessedToDate));
        var current = R(assessedToDate - previouslyCertified);
        var held = R(current * retentionPercent / 100m);
        if (retentionReleased < 0 || retentionReleased > R(previouslyCertified * retentionPercent / 100m + held))
            throw new ArgumentOutOfRangeException(nameof(retentionReleased));
        if (approvedBackCharge < 0 || approvedContraCharge < 0 || tax < 0) throw new ArgumentOutOfRangeException(nameof(approvedBackCharge));
        var net = R(current - held + retentionReleased - approvedBackCharge - approvedContraCharge
            + (taxInclusive ? 0m : tax));
        if (net < 0) throw new ArgumentOutOfRangeException(nameof(net));
        return new(previouslyCertified, current, held, retentionReleased, approvedBackCharge, approvedContraCharge, tax, net);
    }
}
