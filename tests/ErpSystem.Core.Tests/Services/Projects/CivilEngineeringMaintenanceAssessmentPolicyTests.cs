using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceAssessmentPolicyTests
{
    [Fact]
    public void HOD_direction_requires_a_distinct_SCE_and_future_due_date()
    {
        var request = new StartCivilEngineeringMaintenanceAssessmentRequest
        {
            ClientRequestId = Guid.NewGuid(), Direction = "Assess the reported defect and prepare a controlled remedy scope.",
            DueAt = DateTime.UtcNow.AddDays(-1)
        };

        CivilEngineeringMaintenanceAssessmentPolicy.ValidateStart(request)
            .Should().Contain(value => value.Contains("Supervising Civil Engineer", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("future due date", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Assessment_transition_allows_only_hod_sce_ce_sequence()
    {
        CivilEngineeringMaintenanceAssessmentPolicy.GetRequired(
                CivilEngineeringMaintenanceAssessmentStages.SceAssignment,
                CivilEngineeringMaintenanceAssessmentAction.AssignCivilEngineer)
            .ToStage.Should().Be(CivilEngineeringMaintenanceAssessmentStages.CivilEngineerAssessment);

        CivilEngineeringMaintenanceAssessmentPolicy.GetRequired(
                CivilEngineeringMaintenanceAssessmentStages.CivilEngineerAssessment,
                CivilEngineeringMaintenanceAssessmentAction.SubmitAssessment)
            .StartsSharedWorkflow.Should().BeTrue();

        CivilEngineeringMaintenanceAssessmentPolicy.GetRequired(
                CivilEngineeringMaintenanceAssessmentStages.SceAssessmentReview,
                CivilEngineeringMaintenanceAssessmentAction.SubmitToHod)
            .AdvancesSharedWorkflow.Should().BeTrue();

        Action invalid = () => CivilEngineeringMaintenanceAssessmentPolicy.GetRequired(
            CivilEngineeringMaintenanceAssessmentStages.SceAssignment,
            CivilEngineeringMaintenanceAssessmentAction.Approve);
        invalid.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Assignments_require_independent_hod_sce_and_civil_engineer()
    {
        var hod = Guid.NewGuid();
        var sce = Guid.NewGuid();
        Action duplicate = () => CivilEngineeringMaintenanceAssessmentPolicy.EnsureDistinctAssignments(hod, sce, hod);

        duplicate.Should().Throw<InvalidOperationException>();
    }
}
