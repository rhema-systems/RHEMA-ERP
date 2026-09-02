using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringComplaintResolutionProjectionPolicyTests
{
    [Fact]
    public void Closed_civil_control_makes_the_complaint_ready_for_independent_Helpdesk_resolution()
    {
        var result = CivilEngineeringComplaintResolutionProjectionPolicy.Derive(null, null, null,
            CivilEngineeringMaintenanceCompletionStages.Closed,
            CivilEngineeringMaintenanceInspectionStatuses.Passed,
            CivilEngineeringMaintenancePaymentDirectionStatuses.Directed);

        result.Stage.Should().Be("CivilClosed");
        result.ReadyForHelpdeskResolution.Should().BeTrue();
    }

    [Theory]
    [InlineData(CivilEngineeringMaintenanceCompletionStages.RemediationRequired, "RemediationRequired")]
    [InlineData(CivilEngineeringMaintenanceCompletionStages.InspectionInProgress, "InspectionInProgress")]
    [InlineData(CivilEngineeringMaintenanceCompletionStages.SceReview, "CompletionReview")]
    public void Active_completion_states_remain_within_the_Civil_owner_lifecycle(string completionStage, string expected)
    {
        var result = CivilEngineeringComplaintResolutionProjectionPolicy.Derive(null, null, null, completionStage, null, null);

        result.Stage.Should().Be(expected);
        result.ReadyForHelpdeskResolution.Should().BeFalse();
    }

    [Fact]
    public void Earlier_owner_states_are_projected_without_creating_a_second_complaint_lifecycle()
    {
        CivilEngineeringComplaintResolutionProjectionPolicy.Derive(null, null, null, null, null, null).Stage.Should().Be("IntakeLogged");
        CivilEngineeringComplaintResolutionProjectionPolicy.Derive(CivilEngineeringMaintenanceAssessmentStages.HodFinalReview, null, null, null, null, null).Stage.Should().Be("AssessmentAndRemedy");
        CivilEngineeringComplaintResolutionProjectionPolicy.Derive(null, CivilEngineeringMaintenanceCostingHandoffStages.ProcurementAndAward, null, null, null, null).Stage.Should().Be("CostingAndAward");
        CivilEngineeringComplaintResolutionProjectionPolicy.Derive(null, null, CivilEngineeringMaintenanceExecutionLinkStages.WorkInProgress, null, null, null).Stage.Should().Be("WorkExecution");
    }
}
