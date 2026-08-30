using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringExtensionOfTimePolicyTests
{
    private static readonly CivilEngineeringExtensionOfTimeValue Policy = new()
    {
        WorkflowDefinitionId = Guid.NewGuid(),
        EvidenceMetadataTemplateId = Guid.NewGuid(),
        ReviewerRoleIds = [Guid.NewGuid()],
        RequireQuantitySurveyVariationForCostImpact = true,
        RequireFinanceBudgetRevalidation = true,
        RequireProcurementContractRevalidation = true,
        RequireIndependentApproval = true
    };

    [Fact]
    public void Create_requires_controlled_contract_dms_evidence_and_completion_date()
    {
        var errors = CivilEngineeringExtensionOfTimePolicy.ValidateCreate(new CreateCivilEngineeringExtensionOfTimeRequest(), Policy);

        errors.Should().Contain(value => value.Contains("client request", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("Works contract", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("revised completion", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("central-DMS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Cost_impact_requires_an_applied_qs_variation_when_policy_requires_it()
    {
        var request = Valid();
        request.HasCostImpact = true;
        request.QuantitySurveyVariationOrderId = null;

        CivilEngineeringExtensionOfTimePolicy.ValidateCreate(request, Policy)
            .Should().Contain(value => value.Contains("QS variation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Valid_controlled_request_is_accepted_by_policy()
    {
        CivilEngineeringExtensionOfTimePolicy.ValidateCreate(Valid(), Policy).Should().BeEmpty();
    }

    [Fact]
    public void Review_requires_replay_concurrency_and_a_reasoned_workflow_comment()
    {
        CivilEngineeringExtensionOfTimePolicy.ValidateReview(new ReviewCivilEngineeringExtensionOfTimeRequest())
            .Should().Contain(value => value.Contains("client request", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("Refresh", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("comment", StringComparison.OrdinalIgnoreCase));
    }

    private static CreateCivilEngineeringExtensionOfTimeRequest Valid() => new()
    {
        ClientRequestId = Guid.NewGuid(),
        ContractId = Guid.NewGuid(),
        Title = "Weather-related EOT",
        Reason = "Exceptional rainfall prevented safe execution of the governed Works scope.",
        ScopeSummary = "Foundation and drainage work package.",
        DaysRequested = 14,
        ProposedRevisedCompletionDate = DateTime.UtcNow.Date.AddDays(30),
        EvidenceDocumentRecordId = Guid.NewGuid(),
        EvidenceDocumentVersionId = Guid.NewGuid()
    };
}
