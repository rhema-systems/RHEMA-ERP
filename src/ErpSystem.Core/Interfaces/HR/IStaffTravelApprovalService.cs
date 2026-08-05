using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 3: APPROVAL WORKFLOW SERVICE
// ============================================================================

#region Staff Travel Approval Service

public interface IStaffTravelApprovalService
{
    // Workflow templates
    Task<StaffTravelApprovalWorkflowTemplateDto> GetTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelApprovalWorkflowTemplateSummaryDto>> GetAllTemplatesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelApprovalWorkflowTemplateSummaryDto>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default);
    Task<StaffTravelApprovalWorkflowTemplateDto> CreateTemplateAsync(CreateStaffTravelApprovalWorkflowTemplateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelApprovalWorkflowTemplateDto> UpdateTemplateAsync(UpdateStaffTravelApprovalWorkflowTemplateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow steps
    Task<StaffTravelApprovalWorkflowStepDto> AddStepAsync(CreateStaffTravelApprovalWorkflowStepDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelApprovalWorkflowStepDto>> GetStepsAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<StaffTravelApprovalWorkflowStepDto> UpdateStepAsync(UpdateStaffTravelApprovalWorkflowStepDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteStepAsync(Guid stepId, CancellationToken cancellationToken = default);

    // Approval instances
    Task<StaffTravelApprovalInstanceDto> InitiateAsync(CreateStaffTravelApprovalInstanceDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelApprovalInstanceDto> GetInstanceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelApprovalInstanceSummaryDto>> GetInstancesByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelApprovalInstanceSummaryDto>> GetInstancesByStatusAsync(TravelApprovalInstanceStatus status, CancellationToken cancellationToken = default);
    Task<StaffTravelApprovalInstanceDto?> GetActiveInstanceForRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    // Decisions
    Task<StaffTravelApprovalDecisionDto> RecordDecisionAsync(RecordStaffTravelApprovalDecisionDto decisionDto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelApprovalDecisionDto>> GetDecisionsForInstanceAsync(Guid instanceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelApprovalDecisionDto>> GetPendingDecisionsForApproverAsync(Guid approverId, CancellationToken cancellationToken = default);
}

#endregion
