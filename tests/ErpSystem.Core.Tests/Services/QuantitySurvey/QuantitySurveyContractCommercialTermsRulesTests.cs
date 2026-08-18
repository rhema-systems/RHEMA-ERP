using ErpSystem.Core.Services.QuantitySurvey;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyContractCommercialTermsRulesTests
{
    [Fact]
    public void Valid_governed_terms_pass_every_control()
    {
        var issues = QuantitySurveyContractCommercialTermsRules.Validate(Valid());

        Assert.Empty(issues);
    }

    [Fact]
    public void Amount_retention_and_defects_limits_are_enforced()
    {
        var value = Valid() with
        {
            ProvisionalSumAmount = 800m,
            ContingencyAmount = 300m,
            RetentionPercentage = 11m,
            DefectsLiabilityDays = 366
        };

        var issues = QuantitySurveyContractCommercialTermsRules.Validate(value);

        Assert.Contains(issues, item => item.Contains("cannot exceed the contract value"));
        Assert.Contains(issues, item => item.Contains("Retention must be between"));
        Assert.Contains(issues, item => item.Contains("Defects liability must be between"));
    }

    [Fact]
    public void Controlled_selectors_clauses_and_dms_evidence_are_required()
    {
        var value = Valid() with
        {
            HasPaymentTerm = false,
            HasSubcontractPaymentTerm = false,
            RetentionClause = null,
            SectionalTakeoverClause = null,
            SubcontractTerms = null,
            ClaimClause = null,
            HasCommercialTermsDocument = false
        };

        var issues = QuantitySurveyContractCommercialTermsRules.Validate(value);

        Assert.Contains(issues, item => item.Contains("payment term"));
        Assert.Contains(issues, item => item.Contains("retention clause"));
        Assert.Contains(issues, item => item.Contains("sectional-takeover clause"));
        Assert.Contains(issues, item => item.Contains("Subcontract terms"));
        Assert.Contains(issues, item => item.Contains("claim clause"));
        Assert.Contains(issues, item => item.Contains("central-DMS"));
    }

    [Fact]
    public void Disabled_policy_features_cannot_be_smuggled_into_a_contract()
    {
        var value = Valid() with
        {
            ControlProvisionalSums = false,
            ControlContingencies = false,
            ControlDefectsLiability = false,
            ControlSectionalTakeover = false,
            ControlSubcontracts = false,
            ControlClaimClauses = false
        };

        var issues = QuantitySurveyContractCommercialTermsRules.Validate(value);

        Assert.Contains(issues, item => item.Contains("does not permit controlled provisional sums"));
        Assert.Contains(issues, item => item.Contains("does not permit controlled contingencies"));
        Assert.Contains(issues, item => item.Contains("does not permit a defects-liability term"));
        Assert.Contains(issues, item => item.Contains("Sectional takeover is not enabled"));
        Assert.Contains(issues, item => item.Contains("Subcontract commercial terms are disabled"));
        Assert.Contains(issues, item => item.Contains("Claim clauses are disabled"));
    }

    private static QuantitySurveyContractCommercialTermsRulesInput Valid() => new(
        ContractValue: 1_000m,
        ProvisionalSumAmount: 100m,
        ContingencyAmount: 50m,
        RetentionPercentage: 5m,
        DefectsLiabilityDays: 180,
        AllowSectionalTakeover: true,
        AllowSubcontracting: true,
        HasSubcontractPaymentTerm: true,
        ClaimNoticePeriodDays: 28,
        HasPaymentTerm: true,
        HasCommercialTermsDocument: true,
        RetentionClause: "Five percent is retained.",
        SectionalTakeoverClause: "Sectional takeover follows certified completion.",
        SubcontractTerms: "Approved subcontractors use the selected payment term.",
        ClaimClause: "Claims require notice and evidence.",
        MaximumRetentionPercentage: 10m,
        MaximumDefectsLiabilityDays: 365,
        SectionalTakeoverReleasePercentage: 50m,
        ControlProvisionalSums: true,
        ControlContingencies: true,
        ControlDefectsLiability: true,
        ControlSectionalTakeover: true,
        ControlSubcontracts: true,
        ControlClaimClauses: true,
        RequireCommercialTermsDocument: true);
}
