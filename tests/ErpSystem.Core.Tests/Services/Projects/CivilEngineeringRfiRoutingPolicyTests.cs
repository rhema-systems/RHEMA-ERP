using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringRfiRoutingPolicyTests
{
    [Fact]
    public void Create_requires_request_identity_valid_priority_and_evidence_when_policy_requires_it()
    {
        var errors = CivilEngineeringRfiRoutingPolicy.ValidateCreate(new CreateCivilEngineeringRfiRequest
        {
            ClientRequestId = Guid.Empty, ReferenceNumber = "", Subject = "x", Question = "short", Priority = "Invalid"
        }, true);
        errors.Should().Contain(error => error.Contains("identifier", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("priority", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("DMS", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("AwaitingProjectEngineerResponse", true)]
    [InlineData("ReturnedToProjectEngineer", true)]
    [InlineData("AwaitingProjectManagerApproval", false)]
    [InlineData("Answered", false)]
    public void Only_expected_states_accept_a_project_engineer_response(string status, bool expected)
        => CivilEngineeringRfiRoutingPolicy.CanProjectEngineerRespond(status).Should().Be(expected);

    [Theory]
    [InlineData("AwaitingProjectManagerApproval", "Pending", true)]
    [InlineData("AwaitingProjectManagerApproval", "Approved", false)]
    [InlineData("ReturnedToProjectEngineer", "Pending", false)]
    public void Project_manager_decision_requires_pending_manager_state(string status, string approvalStatus, bool expected)
        => CivilEngineeringRfiRoutingPolicy.CanProjectManagerDecide(status, approvalStatus).Should().Be(expected);

    [Fact]
    public void Project_engineer_response_requires_current_dms_record_and_version()
    {
        var errors = CivilEngineeringRfiRoutingPolicy.ValidateProjectEngineerResponse(new SubmitCivilEngineeringRfiResponseRequest
        {
            ClientRequestId = Guid.NewGuid(), RowVersion = "row", ResponseText = "A controlled Project Engineer response.",
        });
        errors.Should().ContainSingle(error => error.Contains("DMS", StringComparison.OrdinalIgnoreCase));
    }
}
