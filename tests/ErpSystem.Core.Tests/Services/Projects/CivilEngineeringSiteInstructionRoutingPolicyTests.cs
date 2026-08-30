using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringSiteInstructionRoutingPolicyTests
{
    [Fact]
    public void Issue_requires_idempotency_assignment_current_dms_evidence_and_a_meaningful_instruction()
    {
        var errors = CivilEngineeringSiteInstructionRoutingPolicy.ValidateIssue(
            Guid.Empty, Guid.Empty, Guid.Empty, "x", "short");

        errors.Should().Contain(error => error.Contains("client request", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("Project Engineer", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("central-DMS", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("title", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("narrative", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Contractor_response_requires_controlled_dms_evidence_but_acknowledgement_does_not()
    {
        CivilEngineeringSiteInstructionRoutingPolicy.ValidateContractorAction(
                CivilEngineeringSiteInstructionRoutingPolicy.ContractorAcknowledged,
                "Acknowledged and scheduled.", false)
            .Should().BeEmpty();

        CivilEngineeringSiteInstructionRoutingPolicy.ValidateContractorAction(
                CivilEngineeringSiteInstructionRoutingPolicy.ContractorResponded,
                "A revised method statement is supplied.", false)
            .Should().ContainSingle(error => error.Contains("central-DMS", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("AwaitingContractorAcknowledgement", "Approved", true)]
    [InlineData("ContractorResponded", "Approved", true)]
    [InlineData("PendingApproval", "Pending", false)]
    [InlineData("Rejected", "Rejected", false)]
    [InlineData("AwaitingContractorAcknowledgement", "Pending", false)]
    public void Contractor_route_opens_only_after_the_instruction_is_approved(string status, string approval, bool expected)
    {
        CivilEngineeringSiteInstructionRoutingPolicy.CanReceiveContractorResponse(status, approval).Should().Be(expected);
    }

    [Fact]
    public void Engineering_review_and_follow_up_require_reason_current_dms_evidence_and_the_correct_lifecycle_state()
    {
        CivilEngineeringSiteInstructionRoutingPolicy.ValidateEngineeringReview(true, "", false)
            .Should().HaveCount(2);
        CivilEngineeringSiteInstructionRoutingPolicy.ValidateFollowUp("Unexpected", "", false)
            .Should().HaveCount(3);
        CivilEngineeringSiteInstructionRoutingPolicy.CanEngineeringReview("AwaitingEngineeringReview", "Approved").Should().BeTrue();
        CivilEngineeringSiteInstructionRoutingPolicy.CanEngineeringReview("AwaitingContractorAcknowledgement", "Approved").Should().BeFalse();
        CivilEngineeringSiteInstructionRoutingPolicy.CanFollowUp("AwaitingEngineeringFollowUp", "Approved").Should().BeTrue();
        CivilEngineeringSiteInstructionRoutingPolicy.CanFollowUp("Closed", "Approved").Should().BeFalse();
    }
}
