using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringPermittingHodDecisionPolicyTests
{
    [Fact]
    public void HOD_decision_requires_retry_key_and_reason_for_reject_or_return()
    {
        var errors = CivilEngineeringPermittingHodDecisionPolicy.Validate(new DecideCivilEngineeringPermittingReviewRequest { Outcome = CivilEngineeringPermittingHodDecisionOutcome.ReturnForCorrection });

        errors.Should().Contain(error => error.Contains("client request", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(error => error.Contains("reason", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HOD_decision_maps_to_the_shared_workflow_action_and_final_review_stage()
    {
        CivilEngineeringPermittingHodDecisionPolicy.WorkflowAction(CivilEngineeringPermittingHodDecisionOutcome.Approve).Should().Be("Approve");
        CivilEngineeringPermittingHodDecisionPolicy.WorkflowAction(CivilEngineeringPermittingHodDecisionOutcome.Reject).Should().Be("Reject");
        CivilEngineeringPermittingHodDecisionPolicy.Stage(CivilEngineeringPermittingHodDecisionOutcome.Approve).Should().Be(CivilEngineeringPermittingEngineeringReviewStage.HodApproved);
        CivilEngineeringPermittingHodDecisionPolicy.Stage(CivilEngineeringPermittingHodDecisionOutcome.Reject).Should().Be(CivilEngineeringPermittingEngineeringReviewStage.HodRejected);
        CivilEngineeringPermittingHodDecisionPolicy.Stage(CivilEngineeringPermittingHodDecisionOutcome.ReturnForCorrection).Should().Be(CivilEngineeringPermittingEngineeringReviewStage.HodReturned);
    }
}
