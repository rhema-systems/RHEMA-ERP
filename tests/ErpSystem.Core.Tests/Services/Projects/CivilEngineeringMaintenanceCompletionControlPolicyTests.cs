using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceCompletionControlPolicyTests
{
    [Fact]
    public void Create_requires_completed_execution_narrative_and_central_evidence()
    {
        CivilEngineeringMaintenanceCompletionControlPolicy.ValidateCreate(new CreateCivilEngineeringMaintenanceCompletionControlRequest())
            .Should().Contain(value => value.Contains("client request", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("completed Civil Maintenance execution", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("completion summary", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("central-DMS", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(CivilEngineeringMaintenanceCompletionAction.DirectInspection)]
    [InlineData(CivilEngineeringMaintenanceCompletionAction.RecordInspectionPassed)]
    [InlineData(CivilEngineeringMaintenanceCompletionAction.RecordInspectionFailed)]
    [InlineData(CivilEngineeringMaintenanceCompletionAction.DirectPayment)]
    public void Directions_and_outcomes_require_controlled_evidence(CivilEngineeringMaintenanceCompletionAction action)
    {
        CivilEngineeringMaintenanceCompletionControlPolicy.ValidateProcess(new ProcessCivilEngineeringMaintenanceCompletionControlRequest { ClientRequestId = Guid.NewGuid(), RowVersion = "row", Action = action, Note = "Controlled direction" })
            .Should().Contain(value => value.Contains("central-DMS evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Closure_and_review_require_notes_but_only_evidence_actions_are_flagged_by_the_pure_policy()
    {
        CivilEngineeringMaintenanceCompletionControlPolicy.ValidateProcess(new ProcessCivilEngineeringMaintenanceCompletionControlRequest { ClientRequestId = Guid.NewGuid(), RowVersion = "row", Action = CivilEngineeringMaintenanceCompletionAction.Close })
            .Should().Contain(value => value.Contains("note", StringComparison.OrdinalIgnoreCase));
        CivilEngineeringMaintenanceCompletionControlPolicy.RequiresEvidence(CivilEngineeringMaintenanceCompletionAction.Close).Should().BeFalse();
        CivilEngineeringMaintenanceCompletionControlPolicy.RequiresEvidence(CivilEngineeringMaintenanceCompletionAction.DirectPayment).Should().BeTrue();
    }
}
