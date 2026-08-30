using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

/// <summary>
/// Pure lifecycle and configuration rules for the Civil handoff overlay. Financial and
/// procurement truth is deliberately read from its owning modules by the service.
/// </summary>
public static class CivilEngineeringMaintenanceCostingHandoffPolicy
{
    public static IReadOnlyList<string> ValidateCreate(CreateCivilEngineeringMaintenanceCostingHandoffRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required for safe retry.");
        if (request.AssessmentId == Guid.Empty) errors.Add("Select the approved Civil assessment.");
        if (request.ProjectId == Guid.Empty) errors.Add("Select the controlled delivery project.");
        if (request.QuantitySurveyEstimateVersionId == Guid.Empty) errors.Add("Select the approved QS estimate or cost plan.");
        return errors;
    }

    public static string CostingWorkflowEntityType(CivilEngineeringWorkClassification classification) =>
        classification == CivilEngineeringWorkClassification.AssetComplaintResolution
            ? CivilEngineeringWorkflowBindingRegistry.ComplaintCosting
            : CivilEngineeringWorkflowBindingRegistry.MaintenanceCosting;

    public static IReadOnlyList<string> ValidatePolicy(CivilEngineeringCostingApprovalValue value, WorkflowDefinition costingWorkflow, WorkflowDefinition contractorWorkflow)
    {
        var errors = new List<string>();
        if (value.MaintenanceWorkflowDefinitionId == Guid.Empty || value.ComplaintWorkflowDefinitionId == Guid.Empty || value.ContractorWorkflowDefinitionId == Guid.Empty)
            errors.Add("CIV-CFG-008 must select maintenance, complaint and contractor workflows.");
        if (value.CostReviewerRoleIds.Count == 0 || value.ApproverRoleIds.Count == 0)
            errors.Add("CIV-CFG-008 must select controlled cost-reviewer and approver roles.");
        if (!HasApprovalStep(costingWorkflow)) errors.Add("The configured Civil costing workflow must contain an approval step.");
        if (!HasApprovalStep(contractorWorkflow)) errors.Add("The configured Civil contractor-engagement workflow must contain an approval step.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateCurrentOwnerState(
        decimal approvedEstimateAmount,
        CivilEngineeringCostingApprovalValue controls,
        bool hasApprovedEstimate,
        bool hasApprovedProjectBudget,
        bool hasApprovedRequisitionBudget,
        bool hasApprovedRequisition)
    {
        var errors = new List<string>();
        if (!hasApprovedEstimate) errors.Add("The selected QS estimate is no longer approved for the selected project.");
        if (approvedEstimateAmount >= controls.QsReviewThreshold && !hasApprovedEstimate)
            errors.Add("The configured QS review threshold requires a current approved QS estimate.");
        if (controls.RequireBudgetValidation && !hasApprovedProjectBudget && !hasApprovedRequisitionBudget)
            errors.Add("CIV-CFG-008 requires a current approved project budget or a procurement requisition with validated budget control.");
        if (approvedEstimateAmount >= controls.ProcurementReviewThreshold && !hasApprovedRequisition)
            errors.Add("The configured Procurement review threshold requires an approved controlled purchase requisition.");
        return errors.Distinct(StringComparer.Ordinal).ToList();
    }

    public static bool IsTerminal(string stage) =>
        stage is CivilEngineeringMaintenanceCostingHandoffStages.Awarded or CivilEngineeringMaintenanceCostingHandoffStages.Rejected;

    public static bool HasConfiguredCostingReviewerOrApproverRole(
        IEnumerable<string> actorRoles,
        IEnumerable<string> configuredRoleNames) =>
        actorRoles.Any(actorRole =>
            configuredRoleNames.Any(configuredRole =>
                string.Equals(actorRole?.Trim(), configuredRole?.Trim(), StringComparison.OrdinalIgnoreCase)));

    private static bool HasApprovalStep(WorkflowDefinition definition) =>
        definition.Steps.Any(value => !value.IsDeleted && value.IsRequired && value.StepType == WorkflowStepType.Approval);
}
