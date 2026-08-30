using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringInspectionPolicyTests
{
    [Fact]
    public void Plan_requires_controlled_site_inspector_schedule_purpose_and_dms_evidence()
    {
        var errors = CivilEngineeringInspectionPolicy.ValidateCreate(new CreateCivilEngineeringInspectionControlRequest());

        errors.Should().Contain(error => error.Contains("client request", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("Planning/GIS", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("qualified", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("scheduled", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("purpose", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("central-DMS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Failure_and_reinspection_actions_require_evidence_findings_and_an_independent_reinspection_selection()
    {
        var failed = CivilEngineeringInspectionPolicy.ValidateProcess(new ProcessCivilEngineeringInspectionControlRequest
        {
            ClientRequestId = Guid.NewGuid(), RowVersion = "version", Action = CivilEngineeringInspectionAction.RecordFailed
        });
        var corrective = CivilEngineeringInspectionPolicy.ValidateProcess(new ProcessCivilEngineeringInspectionControlRequest
        {
            ClientRequestId = Guid.NewGuid(), RowVersion = "version", Action = CivilEngineeringInspectionAction.RecordCorrectiveAction
        });

        failed.Should().Contain(error => error.Contains("findings", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("central-DMS", StringComparison.OrdinalIgnoreCase));
        corrective.Should().Contain(error => error.Contains("Corrective action", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("independent", StringComparison.OrdinalIgnoreCase))
            .And.Contain(error => error.Contains("central-DMS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Plan_review_requires_a_reason_only_when_rejected_and_does_not_require_new_dms_evidence()
    {
        var approve = CivilEngineeringInspectionPolicy.ValidateProcess(new ProcessCivilEngineeringInspectionControlRequest
        {
            ClientRequestId = Guid.NewGuid(), RowVersion = "version", Action = CivilEngineeringInspectionAction.ApprovePlan
        });
        var reject = CivilEngineeringInspectionPolicy.ValidateProcess(new ProcessCivilEngineeringInspectionControlRequest
        {
            ClientRequestId = Guid.NewGuid(), RowVersion = "version", Action = CivilEngineeringInspectionAction.RejectPlan
        });

        approve.Should().BeEmpty();
        reject.Should().ContainSingle(error => error.Contains("rejection", StringComparison.OrdinalIgnoreCase));
    }
}
