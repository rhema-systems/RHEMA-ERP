using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;

/// <summary>
/// Service interface for leave request management
/// </summary>
public interface ILeaveService
{
    Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestDto dto);
    Task<LeaveRequestDto> UpdateDraftAsync(Guid id, CreateLeaveRequestDto dto);
    Task<bool> SubmitForApprovalAsync(Guid id);
    Task<LeaveRequestDto> ApproveLeaveAsync(Guid id, ApproveLeaveDto dto);
    Task<LeaveRequestDto> RejectLeaveAsync(Guid id, RejectLeaveDto dto);
    Task<LeaveRequestDto> GetLeaveRequestByIdAsync(Guid id);
    Task<LeaveRequestDto?> GetLeaveRequestByNumberAsync(string applicationNumber);
    Task<PagedResult<LeaveRequestDto>> GetEmployeeLeaveHistoryAsync(Guid employeeId, int year, int pageNumber, int pageSize);
    Task<PagedResult<LeaveRequestDto>> GetPendingApprovalsAsync(Guid managerId, int pageNumber, int pageSize);
    Task<IEnumerable<LeaveBalanceDto>> GetEmployeeLeaveBalancesAsync(Guid employeeId, int year);
    Task<IEnumerable<LeaveBalanceDto>> GetAllLeaveBalancesAsync(int year, Guid? employeeId, Guid? leaveTypeId);
    Task<LeaveBalanceDetailDto?> GetLeaveBalanceDetailAsync(Guid balanceId);
    Task<IEnumerable<MandatoryLeaveComplianceDto>> GetMandatoryLeaveComplianceAsync(int year);
    Task<bool> CancelLeaveRequestAsync(Guid id, string cancellationReason);
    Task<LeaveRequestDto> CloseLeaveRequestAsync(Guid id, CloseLeaveDto dto);

    // ─── Leave Adjustments ───────────────────────────────────────────────────
    Task<LeaveAdjustmentDto> AddAdjustmentAsync(CreateLeaveAdjustmentDto dto);
    Task<IEnumerable<LeaveAdjustmentDto>> GetAdjustmentsAsync(Guid balanceId);
    Task DeleteAdjustmentAsync(Guid adjustmentId);

    // ─── Standalone Adjustment Admin ─────────────────────────────────────────
    Task<IEnumerable<LeaveAdjustmentDto>> GetAllAdjustmentsAsync(int year, Guid? employeeId, Guid? leaveTypeId, string? search);
    Task<LeaveAdjustmentDto?> GetAdjustmentByIdAsync(Guid id);
    Task<LeaveAdjustmentDto> CreateStandaloneAdjustmentAsync(CreateLeaveAdjustmentStandaloneDto dto);
    Task<LeaveAdjustmentDto> UpdateAdjustmentAsync(Guid id, UpdateLeaveAdjustmentDto dto);

    // ─── Attachments ─────────────────────────────────────────────────────────
    /// <summary>
    /// Records an attachment against a leave request. The upload/DMS identifiers come from
    /// <c>IHrControlledDocumentService</c>; <paramref name="filePath"/> stays empty for new
    /// rows and is only populated on pre-migration data.
    /// </summary>
    Task<LeaveRequestAttachmentDto> UploadAttachmentAsync(Guid leaveRequestId, Guid uploadedBy, string fileName, string filePath, string? contentType, long? fileSizeBytes, Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null);
    Task<IEnumerable<LeaveRequestAttachmentDto>> GetAttachmentsAsync(Guid leaveRequestId);
    Task<LeaveRequestAttachmentDto?> GetAttachmentByIdAsync(Guid attachmentId);
    Task<bool> DeleteAttachmentAsync(Guid attachmentId);
}

/// <summary>
/// Service interface for leave type configuration
/// </summary>
public interface ILeaveTypeService
{
    Task<IEnumerable<LeaveTypeDto>> GetAllLeaveTypesAsync(bool activeOnly = true);
    Task<LeaveTypeDto> GetLeaveTypeByIdAsync(Guid id);
    Task<LeaveTypeDetailDto> GetLeaveTypeDetailAsync(Guid id);
    Task<LeaveTypeDto> CreateLeaveTypeAsync(CreateLeaveTypeDto dto);
    Task<LeaveTypeDto> UpdateLeaveTypeAsync(Guid id, UpdateLeaveTypeDto dto);
    Task DeactivateLeaveTypeAsync(Guid id);

    // Sub types
    Task<IEnumerable<LeaveSubTypeDto>> GetSubTypesAsync(Guid leaveTypeId);
    Task<LeaveSubTypeDto> CreateSubTypeAsync(CreateLeaveSubTypeDto dto);
    Task<LeaveSubTypeDto> UpdateSubTypeAsync(Guid id, CreateLeaveSubTypeDto dto);
    Task DeleteSubTypeAsync(Guid id);

    // Category allocations
    Task<IEnumerable<LeaveCategoryAllocationDto>> GetAllocationsAsync(Guid leaveTypeId);
    Task<LeaveCategoryAllocationDto> CreateAllocationAsync(CreateLeaveCategoryAllocationDto dto);
    Task<LeaveCategoryAllocationDto> UpdateAllocationAsync(Guid id, CreateLeaveCategoryAllocationDto dto);
    Task DeleteAllocationAsync(Guid id);

    // Eligibility rules
    Task<IEnumerable<LeaveTypeEligibilityDto>> GetEligibilityRulesAsync(Guid leaveTypeId);
    Task<LeaveTypeEligibilityDto> CreateEligibilityRuleAsync(CreateLeaveTypeEligibilityDto dto);
    Task DeleteEligibilityRuleAsync(Guid id);

    /// <summary>
    /// Returns true when the employee matches at least one eligibility rule for this leave type.
    /// If no rules are configured, all employees are considered eligible.
    /// For org-type rules (Level/Unit/Position) an optional Gender qualifier on the rule must
    /// also match; the Gender-only type requires an exact gender match and ignores org fields.
    /// </summary>
    Task<bool> IsEmployeeEligibleAsync(Guid leaveTypeId, Guid employeeId);

    // Accrual policies
    Task<IEnumerable<LeaveAccrualPolicyDto>> GetAccrualPoliciesAsync(Guid leaveTypeId);
    Task<LeaveAccrualPolicyDto> CreateAccrualPolicyAsync(CreateLeaveAccrualPolicyDto dto);
    Task<LeaveAccrualPolicyDto> UpdateAccrualPolicyAsync(Guid id, CreateLeaveAccrualPolicyDto dto);
    Task DeleteAccrualPolicyAsync(Guid id);
}

/// <summary>
/// Service interface for leave plan management
/// </summary>
public interface ILeavePlanService
{
    Task<IEnumerable<LeavePlanDto>> GetByEmployeeAndYearAsync(Guid employeeId, int year);
    Task<IEnumerable<LeavePlanDto>> GetByYearAsync(int year);
    Task<LeavePlanDto> GetByIdAsync(Guid id);
    Task<LeavePlanDto> CreateLeavePlanAsync(CreateLeavePlanDto dto);
    Task<LeavePlanDto> UpdateLeavePlanAsync(Guid id, CreateLeavePlanDto dto);
    Task<LeavePlanDto> SubmitLeavePlanAsync(Guid id);
    Task<LeavePlanDto> ApproveLeavePlanAsync(Guid id);
    Task<LeavePlanDto> RejectLeavePlanAsync(Guid id, string reason);
    Task<LeavePlanDto> SuggestChangesAsync(Guid id, SuggestLeavePlanChangesDto dto);
    Task<LeavePlanDto> RespondToSuggestionAsync(Guid id, RespondToLeaveSuggestionDto dto);
    Task CancelLeavePlanAsync(Guid id);
}

/// <summary>
/// Service interface for leave encashment processing
/// </summary>
public interface ILeaveEncashmentService
{
    Task<LeaveEncashmentDto>              RequestEncashmentAsync(CreateLeaveEncashmentDto dto);
    Task<LeaveEncashmentDto>              ApproveEncashmentAsync(Guid id);
    Task<LeaveEncashmentDto>              RejectEncashmentAsync(Guid id, string reason);
    Task<LeaveEncashmentDto>              MarkAsProcessedAsync(Guid id, ProcessLeaveEncashmentDto dto);
    Task<IEnumerable<LeaveEncashmentDto>> GetEmployeeEncashmentsAsync(Guid employeeId, int year);
    Task<IEnumerable<LeaveEncashmentDto>> GetAllEncashmentsAsync(
        int year,
        Guid?     employeeId   = null,
        Guid?     leaveTypeId  = null,
        DateTime? from         = null,
        DateTime? to           = null,
        string?   search       = null);
    Task<LeaveEncashmentDto>              GetByIdAsync(Guid id);
}
