using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringMaintenanceCostingHandoffPolicyTests
{
    [Fact]
    public void Create_requires_idempotency_and_controlled_cross_owner_selectors()
    {
        var errors = CivilEngineeringMaintenanceCostingHandoffPolicy.ValidateCreate(new CreateCivilEngineeringMaintenanceCostingHandoffRequest());
        errors.Should().Contain(value => value.Contains("client request", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("assessment", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("project", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("QS estimate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Costing_entity_type_separates_complaint_from_maintenance_workflows()
    {
        CivilEngineeringMaintenanceCostingHandoffPolicy.CostingWorkflowEntityType(CivilEngineeringWorkClassification.ScheduledMaintenance)
            .Should().Be(CivilEngineeringWorkflowBindingRegistry.MaintenanceCosting);
        CivilEngineeringMaintenanceCostingHandoffPolicy.CostingWorkflowEntityType(CivilEngineeringWorkClassification.AssetComplaintResolution)
            .Should().Be(CivilEngineeringWorkflowBindingRegistry.ComplaintCosting);
    }

    [Fact]
    public void Current_owner_state_requires_budget_and_procurement_controls_at_thresholds()
    {
        var controls = new CivilEngineeringCostingApprovalValue { QsReviewThreshold = 0m, ProcurementReviewThreshold = 100m, ManagementApprovalThreshold = 1000m, RequireBudgetValidation = true };
        var errors = CivilEngineeringMaintenanceCostingHandoffPolicy.ValidateCurrentOwnerState(200m, controls, true, false, false, false);
        errors.Should().Contain(value => value.Contains("budget", StringComparison.OrdinalIgnoreCase))
            .And.Contain(value => value.Contains("Procurement", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Configured_reviewer_or_approver_role_requires_an_exact_role_match()
    {
        CivilEngineeringMaintenanceCostingHandoffPolicy
            .HasConfiguredCostingReviewerOrApproverRole(["Civil Cost Reviewer"], ["CIVIL COST REVIEWER", "Civil Approver"])
            .Should().BeTrue();

        CivilEngineeringMaintenanceCostingHandoffPolicy
            .HasConfiguredCostingReviewerOrApproverRole(["Procurement Officer"], ["Civil Cost Reviewer", "Civil Approver"])
            .Should().BeFalse();
    }
}
