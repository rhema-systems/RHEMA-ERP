using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 3: APPROVAL WORKFLOW
// ============================================================================

#region Staff Travel Approval Workflow Template

public interface IStaffTravelApprovalWorkflowTemplateRepository : IGenericRepository<StaffTravelApprovalWorkflowTemplate>
{
    /// <summary>Returns a template with its ordered steps loaded.</summary>
    Task<StaffTravelApprovalWorkflowTemplate?> GetWithStepsAsync(Guid id);

    /// <summary>Returns all active templates.</summary>
    Task<IEnumerable<StaffTravelApprovalWorkflowTemplate>> GetActiveTemplatesAsync();

    /// <summary>Returns active templates applicable to the given travel type (or with no type restriction).</summary>
    Task<IEnumerable<StaffTravelApprovalWorkflowTemplate>> GetByTravelTypeAsync(StaffTravelType travelType);

    /// <summary>
    /// Returns the best-matching active template for the supplied request characteristics,
    /// with its steps loaded. Used when initiating an approval instance.
    /// </summary>
    Task<StaffTravelApprovalWorkflowTemplate?> GetMatchingTemplateAsync(
        StaffTravelType travelType, bool isInternational, decimal estimatedBudget, TravelRiskLevel riskLevel);
}

#endregion

#region Staff Travel Approval Workflow Step

public interface IStaffTravelApprovalWorkflowStepRepository : IGenericRepository<StaffTravelApprovalWorkflowStep>
{
    /// <summary>Returns the steps of a template ordered by step order, with approver navigations loaded.</summary>
    Task<IEnumerable<StaffTravelApprovalWorkflowStep>> GetByTemplateIdAsync(Guid templateId);
}

#endregion

#region Staff Travel Approval Instance

public interface IStaffTravelApprovalInstanceRepository : IGenericRepository<StaffTravelApprovalInstance>
{
    /// <summary>Returns all approval instances for a request, with decisions loaded.</summary>
    Task<IEnumerable<StaffTravelApprovalInstance>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns an instance with its decisions (and approvers) loaded.</summary>
    Task<StaffTravelApprovalInstance?> GetWithDecisionsAsync(Guid id);

    /// <summary>Returns instances filtered by status.</summary>
    Task<IEnumerable<StaffTravelApprovalInstance>> GetByStatusAsync(TravelApprovalInstanceStatus status);

    /// <summary>Returns the active (pending/in-progress) approval instance for a request, if any.</summary>
    Task<StaffTravelApprovalInstance?> GetActiveInstanceForRequestAsync(Guid requestId);
}

#endregion

#region Staff Travel Approval Decision

public interface IStaffTravelApprovalDecisionRepository : IGenericRepository<StaffTravelApprovalDecision>
{
    /// <summary>Returns the decisions of an approval instance, ordered by step.</summary>
    Task<IEnumerable<StaffTravelApprovalDecision>> GetByInstanceIdAsync(Guid instanceId);

    /// <summary>Returns all decisions taken (or assigned to) the given approver.</summary>
    Task<IEnumerable<StaffTravelApprovalDecision>> GetByApproverIdAsync(Guid approverId);

    /// <summary>Returns decisions still awaiting an outcome for the given approver.</summary>
    Task<IEnumerable<StaffTravelApprovalDecision>> GetPendingForApproverAsync(Guid approverId);
}

#endregion
