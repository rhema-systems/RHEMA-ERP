namespace ErpSystem.Core.Services.QuantitySurvey;

public sealed record QuantitySurveyContractCommercialTermsRulesInput(
    decimal ContractValue,
    decimal ProvisionalSumAmount,
    decimal ContingencyAmount,
    decimal RetentionPercentage,
    int? DefectsLiabilityDays,
    bool AllowSectionalTakeover,
    bool AllowSubcontracting,
    bool HasSubcontractPaymentTerm,
    int? ClaimNoticePeriodDays,
    bool HasPaymentTerm,
    bool HasCommercialTermsDocument,
    string? RetentionClause,
    string? SectionalTakeoverClause,
    string? SubcontractTerms,
    string? ClaimClause,
    decimal MaximumRetentionPercentage,
    int MaximumDefectsLiabilityDays,
    decimal SectionalTakeoverReleasePercentage,
    bool ControlProvisionalSums,
    bool ControlContingencies,
    bool ControlDefectsLiability,
    bool ControlSectionalTakeover,
    bool ControlSubcontracts,
    bool ControlClaimClauses,
    bool RequireCommercialTermsDocument);

public static class QuantitySurveyContractCommercialTermsRules
{
    public static IReadOnlyList<string> Validate(QuantitySurveyContractCommercialTermsRulesInput value)
    {
        var issues = new List<string>();
        if (!value.HasPaymentTerm)
            issues.Add("Select an active supplier or contractor payment term.");
        if (value.ProvisionalSumAmount < 0 || value.ContingencyAmount < 0)
            issues.Add("Provisional sums and contingencies cannot be negative.");
        if (value.ProvisionalSumAmount + value.ContingencyAmount > value.ContractValue)
            issues.Add("Provisional sums and contingencies cannot exceed the contract value.");
        if (!value.ControlProvisionalSums && value.ProvisionalSumAmount != 0)
            issues.Add("The effective QS policy does not permit controlled provisional sums.");
        if (!value.ControlContingencies && value.ContingencyAmount != 0)
            issues.Add("The effective QS policy does not permit controlled contingencies.");
        if (value.RetentionPercentage < 0 || value.RetentionPercentage > value.MaximumRetentionPercentage)
            issues.Add($"Retention must be between 0 and {value.MaximumRetentionPercentage:0.##} percent.");
        if (value.RetentionPercentage > 0 && string.IsNullOrWhiteSpace(value.RetentionClause))
            issues.Add("A retention clause is required when retention applies.");
        if (!value.ControlDefectsLiability && value.DefectsLiabilityDays.GetValueOrDefault() != 0)
            issues.Add("The effective QS policy does not permit a defects-liability term.");
        if (value.ControlDefectsLiability &&
            (value.DefectsLiabilityDays.GetValueOrDefault() <= 0 ||
             value.DefectsLiabilityDays > value.MaximumDefectsLiabilityDays))
            issues.Add($"Defects liability must be between 1 and {value.MaximumDefectsLiabilityDays} days.");
        if (value.AllowSectionalTakeover &&
            (!value.ControlSectionalTakeover || value.SectionalTakeoverReleasePercentage <= 0))
            issues.Add("Sectional takeover is not enabled by the effective QS retention policy.");
        if (value.AllowSectionalTakeover && string.IsNullOrWhiteSpace(value.SectionalTakeoverClause))
            issues.Add("A sectional-takeover clause is required when sectional takeover is allowed.");
        if (value.AllowSubcontracting && !value.ControlSubcontracts)
            issues.Add("Subcontract commercial terms are disabled by the effective QS policy.");
        if (value.AllowSubcontracting && !value.HasSubcontractPaymentTerm)
            issues.Add("Select an active supplier or contractor payment term for subcontracting.");
        if (value.AllowSubcontracting && string.IsNullOrWhiteSpace(value.SubcontractTerms))
            issues.Add("Subcontract terms are required when subcontracting is allowed.");
        if (!value.ControlClaimClauses &&
            (value.ClaimNoticePeriodDays.GetValueOrDefault() != 0 || !string.IsNullOrWhiteSpace(value.ClaimClause)))
            issues.Add("Claim clauses are disabled by the effective QS policy.");
        if (value.ControlClaimClauses && value.ClaimNoticePeriodDays.GetValueOrDefault() <= 0)
            issues.Add("A positive claim-notice period is required.");
        if (value.ControlClaimClauses && string.IsNullOrWhiteSpace(value.ClaimClause))
            issues.Add("A claim clause is required.");
        if (value.RequireCommercialTermsDocument && !value.HasCommercialTermsDocument)
            issues.Add("Select a current, malware-clean central-DMS contract document for the commercial terms.");
        return issues;
    }
}
