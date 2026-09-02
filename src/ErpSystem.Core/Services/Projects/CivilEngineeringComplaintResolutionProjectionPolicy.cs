using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public sealed record CivilEngineeringComplaintResolutionState(
    string Stage,
    string Label,
    bool ReadyForHelpdeskResolution);

/// <summary>
/// Deterministically derives a display-only resolution state from authoritative owner records.
/// It deliberately does not transition Helpdesk, Maintenance, Finance or Civil records.
/// </summary>
public static class CivilEngineeringComplaintResolutionProjectionPolicy
{
    public static CivilEngineeringComplaintResolutionState Derive(
        string? assessmentStage,
        string? costingStage,
        string? executionStage,
        string? completionStage,
        string? inspectionStatus,
        string? paymentDirectionStatus)
    {
        if (string.Equals(completionStage, CivilEngineeringMaintenanceCompletionStages.Closed, StringComparison.Ordinal))
            return new("CivilClosed", "Civil work record closed; Helpdesk can independently record requester resolution and close the complaint.", true);
        if (string.Equals(completionStage, CivilEngineeringMaintenanceCompletionStages.RemediationRequired, StringComparison.Ordinal))
            return new("RemediationRequired", "Inspection requires remediation before the Civil work record can proceed.", false);
        if (string.Equals(completionStage, CivilEngineeringMaintenanceCompletionStages.AwaitingClosure, StringComparison.Ordinal) || string.Equals(paymentDirectionStatus, CivilEngineeringMaintenancePaymentDirectionStatuses.Directed, StringComparison.Ordinal))
            return new("AwaitingCivilClosure", "Inspection passed and Finance direction is recorded; HOD Civil closure is pending.", false);
        if (string.Equals(completionStage, CivilEngineeringMaintenanceCompletionStages.InspectionInProgress, StringComparison.Ordinal) || string.Equals(inspectionStatus, CivilEngineeringMaintenanceInspectionStatuses.Directed, StringComparison.Ordinal))
            return new("InspectionInProgress", "HOD-directed inspection evidence and outcome are pending.", false);
        if (completionStage is not null)
            return new("CompletionReview", "Civil completion report is in the SCE/HOD review and direction sequence.", false);
        if (executionStage is not null)
            return new("WorkExecution", "Maintenance-owned job-card/work-order execution is being supervised by Civil.", false);
        if (costingStage is not null)
            return new("CostingAndAward", "The approved Civil remedy is in governed costing, procurement or award follow-through.", false);
        if (assessmentStage is not null)
            return new("AssessmentAndRemedy", "Civil assessment and remedy recommendation are under review.", false);

        return new("IntakeLogged", "The Helpdesk complaint is linked to a governed Civil intake awaiting assessment assignment.", false);
    }
}
