using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Projects;

public enum CivilEngineeringMaintenanceAssessmentActor
{
    HeadOfCivilEngineering,
    SupervisingCivilEngineer,
    CivilEngineer
}

public sealed record CivilEngineeringMaintenanceAssessmentTransition(
    string FromStage,
    CivilEngineeringMaintenanceAssessmentAction Action,
    string ToStage,
    CivilEngineeringMaintenanceAssessmentActor Actor,
    string? AssigneeRole = null,
    bool StartsSharedWorkflow = false,
    bool AdvancesSharedWorkflow = false,
    bool CompletesSharedWorkflow = false,
    bool RequiresReason = false);

/// <summary>
/// The local routing overlay deliberately stops at role-assignment and status projection.
/// SCE and HOD approval sequencing is validated and executed by the central Workflow engine.
/// </summary>
public static class CivilEngineeringMaintenanceAssessmentPolicy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<CivilEngineeringMaintenanceAssessmentTransition> Transitions { get; } =
    [
        new(CivilEngineeringMaintenanceAssessmentStages.SceAssignment, CivilEngineeringMaintenanceAssessmentAction.AssignCivilEngineer,
            CivilEngineeringMaintenanceAssessmentStages.CivilEngineerAssessment, CivilEngineeringMaintenanceAssessmentActor.SupervisingCivilEngineer,
            CivilEngineeringAccessControlRegistry.CivilEngineerRole, RequiresReason: true),
        new(CivilEngineeringMaintenanceAssessmentStages.CivilEngineerAssessment, CivilEngineeringMaintenanceAssessmentAction.SubmitAssessment,
            CivilEngineeringMaintenanceAssessmentStages.SceAssessmentReview, CivilEngineeringMaintenanceAssessmentActor.CivilEngineer,
            StartsSharedWorkflow: true),
        new(CivilEngineeringMaintenanceAssessmentStages.SceAssessmentReview, CivilEngineeringMaintenanceAssessmentAction.ReturnAssessment,
            CivilEngineeringMaintenanceAssessmentStages.CivilEngineerAssessment, CivilEngineeringMaintenanceAssessmentActor.SupervisingCivilEngineer,
            RequiresReason: true),
        new(CivilEngineeringMaintenanceAssessmentStages.SceAssessmentReview, CivilEngineeringMaintenanceAssessmentAction.SubmitToHod,
            CivilEngineeringMaintenanceAssessmentStages.HodFinalReview, CivilEngineeringMaintenanceAssessmentActor.SupervisingCivilEngineer,
            AdvancesSharedWorkflow: true),
        new(CivilEngineeringMaintenanceAssessmentStages.HodFinalReview, CivilEngineeringMaintenanceAssessmentAction.Approve,
            CivilEngineeringMaintenanceAssessmentStages.Approved, CivilEngineeringMaintenanceAssessmentActor.HeadOfCivilEngineering,
            CompletesSharedWorkflow: true),
        new(CivilEngineeringMaintenanceAssessmentStages.HodFinalReview, CivilEngineeringMaintenanceAssessmentAction.Reject,
            CivilEngineeringMaintenanceAssessmentStages.Rejected, CivilEngineeringMaintenanceAssessmentActor.HeadOfCivilEngineering,
            CompletesSharedWorkflow: true, RequiresReason: true)
    ];

    public static CivilEngineeringMaintenanceAssessmentTransition GetRequired(string stage, CivilEngineeringMaintenanceAssessmentAction action) =>
        Transitions.SingleOrDefault(item => item.FromStage == stage && item.Action == action)
        ?? throw new InvalidOperationException($"Action {action} is not allowed while the assessment is at {stage}.");

    public static IReadOnlyList<string> ValidateStart(StartCivilEngineeringMaintenanceAssessmentRequest request)
    {
        var errors = new List<string>();
        if (request.ClientRequestId == Guid.Empty) errors.Add("A client request identifier is required for safe retry.");
        if (request.SupervisingCivilEngineerUserId == Guid.Empty) errors.Add("Select the controlled Supervising Civil Engineer.");
        if (string.IsNullOrWhiteSpace(request.Direction) || request.Direction.Trim().Length < 5) errors.Add("Provide a concise HOD assessment direction.");
        if (!request.DueAt.HasValue || request.DueAt.Value.ToUniversalTime() <= DateTime.UtcNow) errors.Add("Select a future due date for the SCE assessment assignment.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateAssessmentSubmission(
        CivilEngineeringMaintenanceAssessmentTransitionRequest request,
        CivilEngineeringMaintenanceAssessmentValue controls)
    {
        var errors = new List<string>();
        if (!request.DefectCategoryId.HasValue || request.DefectCategoryId == Guid.Empty) errors.Add("Select the controlled defect category.");
        if (controls.RequireSiteAssessment && (string.IsNullOrWhiteSpace(request.SiteAssessment) || request.SiteAssessment.Trim().Length < 5)) errors.Add("Record the required site assessment.");
        if (string.IsNullOrWhiteSpace(request.ScopeRecommendation) || request.ScopeRecommendation.Trim().Length < 5) errors.Add("Record the remediation scope recommendation.");
        if (string.IsNullOrWhiteSpace(request.RemedyRecommendation) || request.RemedyRecommendation.Trim().Length < 5) errors.Add("Record the remedy recommendation.");
        if (controls.RequireCostEstimate && (!request.EstimatedCost.HasValue || request.EstimatedCost < 0)) errors.Add("Record the required non-negative cost estimate.");
        if (!request.CentralDocumentRecordId.HasValue || !request.CentralDocumentVersionId.HasValue) errors.Add("Select the current Published central-DMS assessment evidence.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateWorkflowDefinition(WorkflowDefinition definition)
    {
        var steps = definition.Steps
            .Where(item => !item.IsDeleted && item.StepType == WorkflowStepType.Approval)
            .OrderBy(item => item.Order)
            .ToList();
        if (steps.Count < 2)
            return ["The assessment workflow must have an SCE review followed by HOD approval."];
        var errors = new List<string>();
        if (!HasApproverRole(steps[0], CivilEngineeringAccessControlRegistry.SupervisingEngineerRole))
            errors.Add($"The first assessment approval step must be assigned to {CivilEngineeringAccessControlRegistry.SupervisingEngineerRole}.");
        if (!steps.Skip(1).Any(item => HasApproverRole(item, CivilEngineeringAccessControlRegistry.HeadRole)))
            errors.Add($"An assessment approval step after SCE review must be assigned to {CivilEngineeringAccessControlRegistry.HeadRole}.");
        return errors;
    }

    public static void EnsureDistinctAssignments(Guid hodUserId, Guid supervisingCivilEngineerUserId, Guid? civilEngineerUserId)
    {
        var ids = new[] { hodUserId, supervisingCivilEngineerUserId, civilEngineerUserId ?? Guid.Empty }
            .Where(item => item != Guid.Empty).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new InvalidOperationException("HOD, Supervising Civil Engineer and Civil Engineer assignments must be held by different users.");
    }

    private static bool HasApproverRole(WorkflowStep step, string role)
    {
        if (string.Equals(step.RequiredRole?.Trim(), role, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.IsNullOrWhiteSpace(step.Configuration)) return false;
        try
        {
            return JsonSerializer.Deserialize<WorkflowStepConfigurationDto>(step.Configuration, JsonOptions)?.ApprovalConfig?.ApproverRules?.Any(item =>
                item.AssignmentType == WorkflowAssignmentType.Role && string.Equals(item.Role?.Trim(), role, StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch (JsonException) { return false; }
    }
}
