using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceExecutionLinkPolicyTests
{
    [Fact]
    public void New_job_card_requires_controlled_maintenance_type_and_priority()
    {
        var errors = CivilEngineeringMaintenanceExecutionLinkPolicy.ValidateCreate(new CreateCivilEngineeringMaintenanceExecutionLinkRequest
        {
            ClientRequestId = Guid.NewGuid(), HandoffId = Guid.NewGuid(), LinkMode = CivilEngineeringMaintenanceExecutionLinkModes.CreateJobCard
        });

        errors.Should().Contain(value => value.Contains("Maintenance type", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("priority", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Existing_link_rejects_free_form_new_job_card_controls()
    {
        var errors = CivilEngineeringMaintenanceExecutionLinkPolicy.ValidateCreate(new CreateCivilEngineeringMaintenanceExecutionLinkRequest
        {
            ClientRequestId = Guid.NewGuid(), HandoffId = Guid.NewGuid(), LinkMode = CivilEngineeringMaintenanceExecutionLinkModes.LinkExisting,
            JobCardId = Guid.NewGuid(), MaintenanceTypeId = Guid.NewGuid()
        });

        errors.Should().Contain(value => value.Contains("selected by the existing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Owner_state_only_projects_authoritative_maintenance_lifecycle()
    {
        CivilEngineeringMaintenanceExecutionLinkPolicy.DeriveOwnerState("Approved", "Approved", null, null)
            .Should().Be((CivilEngineeringMaintenanceExecutionLinkStages.AwaitingWorkOrder, CivilEngineeringMaintenanceExecutionLinkStatuses.Active, "The linked Maintenance job card is approved and awaiting authoritative work-order creation."));
        CivilEngineeringMaintenanceExecutionLinkPolicy.DeriveOwnerState("Approved", "Approved", Guid.NewGuid(), "Completed")
            .Stage.Should().Be(CivilEngineeringMaintenanceExecutionLinkStages.Completed);
        CivilEngineeringMaintenanceExecutionLinkPolicy.DeriveOwnerState("Rejected", "Rejected", null, null)
            .Status.Should().Be(CivilEngineeringMaintenanceExecutionLinkStatuses.Blocked);
    }
}
