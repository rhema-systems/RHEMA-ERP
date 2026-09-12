using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF REQUISITION SERVICE
// ============================================================================

#region Staff Requisition Service

public interface IStaffRequisitionService
{
    // ── Queries ──────────────────────────────────────────────────────────────

    /// <summary>Returns a requisition with its lightweight navigation properties loaded.</summary>
    Task<StaffRequisitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns a fully-loaded requisition including costs, attachments, comments, and history. Returns null if not found.</summary>
    Task<StaffRequisitionDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns counts of requisitions grouped by status for the summary card display.</summary>
    Task<StaffRequisitionStatusSummaryDto> GetStatusSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the requisition matching the given requisition number, or null if not found.</summary>
    Task<StaffRequisitionDto?> GetByRequisitionNumberAsync(string requisitionNumber, CancellationToken cancellationToken = default);

    /// <summary>Returns a summary list of all non-deleted requisitions for the tenant.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a paged summary list of requisitions ordered by request date descending.</summary>
    Task<PagedResult<StaffRequisitionSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Returns all requisitions for the given organisation unit.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    /// <summary>Returns all requisitions for the given location.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);

    /// <summary>Returns all requisitions for the given position.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetByPositionAsync(Guid positionId, CancellationToken cancellationToken = default);

    /// <summary>Returns all requisitions filtered by status.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetByStatusAsync(StaffRequisitionStatus status, CancellationToken cancellationToken = default);

    /// <summary>Returns all requisitions filtered by type.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetByTypeAsync(StaffRequisitionType type, CancellationToken cancellationToken = default);

    /// <summary>Returns all requisitions raised by the specified employee.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetByRequestedByAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Returns all requisitions in Submitted or UnderReview state awaiting action.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetPendingReviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all open (non-cancelled, non-fulfilled) requisitions.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetOpenRequisitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all unfulfilled requisitions whose target fill date has passed.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetOverdueAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all requisitions linked to the given job vacancy.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetByJobVacancyAsync(Guid jobVacancyId, CancellationToken cancellationToken = default);

    /// <summary>Returns requisitions whose desired start date falls within the next <paramref name="daysAhead"/> days.</summary>
    Task<IEnumerable<StaffRequisitionSummaryDto>> GetUpcomingStartDateAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    // ── CRUD ─────────────────────────────────────────────────────────────────

    /// <summary>Creates a new staff requisition in Draft status and auto-assigns a requisition number.</summary>
    Task<StaffRequisitionDto> CreateAsync(CreateStaffRequisitionDto createDto, Guid tenantId, Guid requestedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Updates an editable requisition. Only Draft or Rejected requisitions may be edited.</summary>
    Task<StaffRequisitionDto> UpdateAsync(UpdateStaffRequisitionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a requisition. Only Draft requisitions may be deleted.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // ── Workflow ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Submits a Draft or Rejected requisition for approval, after budget enforcement. Starts the
    /// <c>StaffRequisition</c> workflow — the resulting status comes from the engine's outcome, not
    /// from this method — and records a history entry.
    /// </summary>
    Task<bool> SubmitAsync(SubmitStaffRequisitionDto submitDto, Guid submittedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an approval step on a Submitted or UnderReview requisition. Refuses unless the
    /// caller is an approver for the current workflow step, and unless they are someone other than
    /// the requester. Records a history entry.
    /// </summary>
    Task<bool> ApproveAsync(ApproveStaffRequisitionDto approveDto, Guid approvedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a Submitted or UnderReview requisition through the workflow engine, returning it to
    /// the requester as Rejected (editable and re-submittable). Records a history entry.
    /// </summary>
    Task<bool> RejectAsync(RejectStaffRequisitionDto rejectDto, Guid rejectedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws a requisition awaiting approval back to Draft, at the requester's own request.
    /// Records a history entry.
    /// </summary>
    Task<bool> RecallAsync(Guid requisitionId, string? reason, Guid recalledByUserId, CancellationToken cancellationToken = default);

    /// <summary>Puts an active requisition on hold. Records a history entry.</summary>
    Task<bool> PutOnHoldAsync(HoldStaffRequisitionDto holdDto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Cancels a requisition that has not yet been fulfilled. Records a history entry.</summary>
    Task<bool> CancelAsync(CancelStaffRequisitionDto cancelDto, Guid cancelledByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records positions filled against an Approved or PartiallyFulfilled requisition.
    /// Sets status to PartiallyFulfilled or Fulfilled based on the count. Records a history entry.
    /// </summary>
    Task<bool> FulfillAsync(FulfillStaffRequisitionDto fulfillDto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Links a requisition to a job vacancy by storing the vacancy ID.</summary>
    Task<bool> LinkToVacancyAsync(LinkStaffRequisitionToVacancyDto linkDto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a requisition against the position's approved manpower budget line for the fiscal
    /// year and returns the advisory result (used to surface a warning banner in the UI before
    /// submit/approve). Enforcement mode comes from <c>CompanyHrPolicySettings.BudgetEnforcementMode</c>;
    /// when no budget line exists for the position the check is always non-blocking.
    /// </summary>
    Task<RequisitionBudgetCheckDto> CheckBudgetAsync(Guid requisitionId, CancellationToken cancellationToken = default);

    // ── Cost operations ───────────────────────────────────────────────────────

    /// <summary>Records a new cost entry against a requisition.</summary>
    Task<StaffRequisitionCostDto> AddCostAsync(CreateStaffRequisitionCostDto createDto, Guid tenantId, Guid recordedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Returns all cost entries for the given requisition.</summary>
    Task<IEnumerable<StaffRequisitionCostDto>> GetCostsAsync(Guid requisitionId, CancellationToken cancellationToken = default);

    /// <summary>Returns the total base-currency cost recorded against a requisition.</summary>
    Task<decimal> GetTotalCostAsync(Guid requisitionId, CancellationToken cancellationToken = default);

    /// <summary>Returns cost entries for the given requisition filtered by category.</summary>
    Task<IEnumerable<StaffRequisitionCostDto>> GetCostsByCategoryAsync(Guid requisitionId, StaffRequisitionCostCategory category, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing cost entry.</summary>
    Task<StaffRequisitionCostDto> UpdateCostAsync(UpdateStaffRequisitionCostDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a cost entry.</summary>
    Task<bool> DeleteCostAsync(Guid costId, CancellationToken cancellationToken = default);

    // ── Attachment operations ─────────────────────────────────────────────────

    /// <summary>
    /// Records an attachment against a requisition, from a file the controlled-upload gate has
    /// already scanned and registered.
    ///
    /// <para>This used to take a <c>CreateStaffRequisitionAttachmentDto</c> carrying a
    /// caller-supplied <c>filePath</c>, so the endpoint stored no file and recorded whatever path
    /// was posted to it. The DTO is gone rather than ignored, so it cannot drift back — the same
    /// treatment the five appraisal-domain attachment paths got.</para>
    /// </summary>
    Task<StaffRequisitionAttachmentDto> AddAttachmentAsync(
        Guid requisitionId,
        Guid uploadedById,
        string fileName,
        long fileSize,
        string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null,
        Guid? documentRecordId = null,
        Guid? documentVersionId = null);

    /// <summary>Returns all attachments for the given requisition.</summary>
    Task<IEnumerable<StaffRequisitionAttachmentDto>> GetAttachmentsAsync(Guid requisitionId, CancellationToken cancellationToken = default);

    /// <summary>Returns all attachments uploaded by a specific employee across all requisitions.</summary>
    Task<IEnumerable<StaffRequisitionAttachmentDto>> GetAttachmentsByUploaderAsync(Guid uploadedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Deletes an attachment record.</summary>
    Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    // ── Comment operations ────────────────────────────────────────────────────

    /// <summary>Posts a new top-level comment or a reply on a requisition.</summary>
    Task<StaffRequisitionCommentDto> AddCommentAsync(CreateStaffRequisitionCommentDto createDto, Guid tenantId, Guid authorId, CancellationToken cancellationToken = default);

    /// <summary>Returns all top-level comments with their replies for the given requisition.</summary>
    Task<IEnumerable<StaffRequisitionCommentDto>> GetCommentsAsync(Guid requisitionId, CancellationToken cancellationToken = default);

    /// <summary>Returns a flat list of all comments (including replies) for the given requisition.</summary>
    Task<IEnumerable<StaffRequisitionCommentDto>> GetAllCommentsAsync(Guid requisitionId, CancellationToken cancellationToken = default);

    /// <summary>Returns all comments posted by the specified author across all requisitions.</summary>
    Task<IEnumerable<StaffRequisitionCommentDto>> GetCommentsByAuthorAsync(Guid authorId, CancellationToken cancellationToken = default);

    /// <summary>Returns all direct replies to the specified parent comment.</summary>
    Task<IEnumerable<StaffRequisitionCommentDto>> GetCommentRepliesAsync(Guid parentCommentId, CancellationToken cancellationToken = default);

    /// <summary>Edits the body of an existing comment.</summary>
    Task<StaffRequisitionCommentDto> UpdateCommentAsync(UpdateStaffRequisitionCommentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a comment (and its replies via cascade).</summary>
    Task<bool> DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken = default);

    // ── History (read-only) ───────────────────────────────────────────────────

    /// <summary>Returns the full audit trail for a requisition, newest first.</summary>
    Task<IEnumerable<StaffRequisitionHistoryDto>> GetHistoryAsync(Guid requisitionId, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent history entry for a requisition.</summary>
    Task<StaffRequisitionHistoryDto?> GetLatestHistoryAsync(Guid requisitionId, CancellationToken cancellationToken = default);
}

#endregion

