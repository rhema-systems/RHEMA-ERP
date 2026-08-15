using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF MOVEMENT SERVICE
// ============================================================================

#region Staff Movement Service

public interface IStaffMovementService
{
    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Returns a fully-loaded movement with all sub-entity navigations.</summary>
    Task<StaffMovementDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns the movement matching the unique movement number, or null.</summary>
    Task<StaffMovementDto?> GetByMovementNumberAsync(string movementNumber, CancellationToken cancellationToken = default);

    /// <summary>Returns all movements as a summary list.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns movement summaries for many parent movement IDs in one query.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetSummariesByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a paged summary list, optionally filtered by status and/or type.</summary>
    Task<PagedResult<StaffMovementSummaryDto>> GetPagedAsync(
        int pageNumber, int pageSize,
        StaffMovementStatus? status = null,
        StaffMovementType? type = null,
        bool isPendingApproval = false,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all movements for a given employee, newest-first.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Returns movements filtered by status.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetByStatusAsync(StaffMovementStatus status, CancellationToken cancellationToken = default);

    /// <summary>Returns movements of the given type, optionally within an effective-date window.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetByTypeAsync(
        StaffMovementType type, DateTime? from = null, DateTime? to = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns movements matching both the given type and status.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetByTypeAndStatusAsync(
        StaffMovementType type, StaffMovementStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>Returns movements whose source (current) org unit is the given unit.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetByCurrentOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    /// <summary>Returns movements whose destination (new) org unit is the given unit.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetByNewOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    /// <summary>Returns movements that have at least one pending approval level.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetPendingApprovalAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns movements requiring employee acceptance that have not yet been responded to.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetPendingEmployeeAcceptanceAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns movements where handover is required but not yet completed.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetPendingHandoverAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns active temporary assignments that have not yet been returned.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetActiveTemporaryAssignmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns temporary assignments expiring within the specified number of days.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetExpiringTemporaryAssignmentsAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    /// <summary>Returns movements whose effective date falls within the given range.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetByEffectiveDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Returns movements submitted by the specified requester.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetByRequestedByAsync(Guid requestedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent movement of each type for an employee.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetLatestMovementsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Returns all movements linked to a succession plan.</summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetBySuccessionPlanAsync(Guid successionPlanId, CancellationToken cancellationToken = default);

    // ── CRUD ──────────────────────────────────────────────────────────────────

    /// <summary>Creates a new staff movement in Draft status.</summary>
    Task<StaffMovementDto> CreateAsync(CreateStaffMovementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>Updates a movement that has not yet been authorised.</summary>
    Task<StaffMovementDto> UpdateAsync(UpdateStaffMovementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a movement. Not permitted once it is authorised or completed.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // ── Workflow ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Submits a Draft movement into the approval workflow. Inoperable until a StaffMovement
    /// workflow definition is published for the tenant.
    /// </summary>
    Task<bool> SubmitAsync(SubmitStaffMovementDto dto, Guid submittedByEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the movements the caller can currently approve. Token-derived: an approver is normally
    /// a line manager, not HR, and the register is HR-only, so without this they have no queue.
    /// </summary>
    Task<IEnumerable<StaffMovementSummaryDto>> GetAwaitingMyApprovalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the caller's approval of the current workflow step. Refused unless the engine has
    /// them assigned to it — authority comes from the published definition, not from a role.
    /// </summary>
    Task<bool> ApproveAsync(Guid movementId, Guid approvingEmployeeId, string? comments = null, CancellationToken cancellationToken = default);

    /// <summary>Withdraws a submitted movement from approval and returns it to the requester as a Draft.</summary>
    Task<bool> RecallAsync(Guid movementId, Guid recallingEmployeeId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the employee's acceptance or rejection of the proposed movement.
    /// <paramref name="respondingEmployeeId"/> is the token employee and must be the movement's subject —
    /// acceptance is the employee's own testimony, so nobody, HR included, may record it on their behalf.
    /// </summary>
    Task<bool> RecordEmployeeResponseAsync(RespondToStaffMovementDto dto, Guid respondingEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Marks the handover stage as complete.</summary>
    Task<bool> CompleteHandoverAsync(CompleteHandoverDto dto, Guid completedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Rejects a movement and records the reason.</summary>
    Task<bool> RejectAsync(RejectStaffMovementDto dto, Guid rejectedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Cancels a movement and records the cancellation reason.</summary>
    Task<bool> CancelAsync(CancelStaffMovementDto dto, Guid cancelledByUserId, CancellationToken cancellationToken = default);

    /// <summary>Processes the return of an employee from a temporary assignment.</summary>
    Task<bool> ProcessReturnFromTemporaryAsync(ProcessReturnFromTemporaryDto dto, Guid processedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Marks an approved movement as Implemented (physically actioned in the system).</summary>
    Task<bool> ImplementAsync(Guid movementId, Guid implementedByUserId, CancellationToken cancellationToken = default);

    // ── Approval Level Operations (legacy, read-only) ─────────────────────────
    //
    // The bespoke approval chain has been retired in favour of the generic workflow engine, which
    // does everything it did — ordered steps, named approvers, delegation — and adds conditional
    // routing and a queue the approver can actually find their work in. Two parallel paths to
    // Approved is the dangerous shape, so the WRITES are gone; the reads stay so any chain a
    // previous build recorded is still visible on the movement.

    /// <summary>Returns all approval levels for a movement, ordered by level number.</summary>
    Task<IEnumerable<StaffMovementApprovalLevelDto>> GetApprovalLevelsAsync(Guid movementId, CancellationToken cancellationToken = default);

    /// <summary>Returns the current pending approval level for a movement, or null if all are actioned.</summary>
    Task<StaffMovementApprovalLevelDto?> GetCurrentPendingApprovalLevelAsync(Guid movementId, CancellationToken cancellationToken = default);

    /// <summary>Returns true when all approval levels for the movement are approved.</summary>
    /// <remarks>Legacy rows only — see the note on the read above.</remarks>
    Task<bool> AllLevelsApprovedAsync(Guid movementId, CancellationToken cancellationToken = default);

    // ── Status History Operations ─────────────────────────────────────────────

    /// <summary>Returns the full status audit trail for a movement, newest-first.</summary>
    Task<IEnumerable<StaffMovementStatusHistoryDto>> GetStatusHistoryAsync(Guid movementId, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent status-change record for a movement.</summary>
    Task<StaffMovementStatusHistoryDto?> GetLatestStatusAsync(Guid movementId, CancellationToken cancellationToken = default);

    // ── Attachment Operations ─────────────────────────────────────────────────

    /// <summary>Attaches a supporting document to a movement.</summary>
    Task<StaffMovementAttachmentDto> AddAttachmentAsync(CreateStaffMovementAttachmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>Returns all attachments for a movement.</summary>
    Task<IEnumerable<StaffMovementAttachmentDto>> GetAttachmentsAsync(Guid movementId, CancellationToken cancellationToken = default);

    /// <summary>Returns attachments for a movement filtered by document type.</summary>
    Task<IEnumerable<StaffMovementAttachmentDto>> GetAttachmentsByTypeAsync(Guid movementId, StaffMovementAttachmentType type, CancellationToken cancellationToken = default);

    /// <summary>Removes an attachment from a movement.</summary>
    Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    // ── Checklist Operations ──────────────────────────────────────────────────

    /// <summary>Returns all checklist items for a movement.</summary>
    Task<IEnumerable<StaffMovementChecklistItemDto>> GetChecklistItemsAsync(Guid movementId, CancellationToken cancellationToken = default);

    /// <summary>Returns checklist items for a movement that are not yet completed.</summary>
    Task<IEnumerable<StaffMovementChecklistItemDto>> GetPendingChecklistItemsAsync(Guid movementId, CancellationToken cancellationToken = default);

    /// <summary>Returns overdue checklist items. Scoped to a single movement when movementId is provided; tenant-wide when null.</summary>
    Task<IEnumerable<StaffMovementChecklistItemDto>> GetOverdueChecklistItemsAsync(Guid? movementId = null, CancellationToken cancellationToken = default);

    /// <summary>Returns all incomplete checklist items assigned to a specific person.</summary>
    Task<IEnumerable<StaffMovementChecklistItemDto>> GetChecklistItemsByResponsiblePersonAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Adds a checklist item to a movement.</summary>
    Task<StaffMovementChecklistItemDto> AddChecklistItemAsync(CreateStaffMovementChecklistItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a checklist item as complete. An item with a named responsible person may only be
    /// completed by that person, unless <paramref name="actorIsHr"/> — HR closes out items whose
    /// owner has left, moved on, or never had an account.
    /// </summary>
    Task<bool> CompleteChecklistItemAsync(CompleteChecklistItemDto dto, Guid completedByUserId, bool actorIsHr, CancellationToken cancellationToken = default);

    /// <summary>Returns true when all required checklist items for the movement are complete.</summary>
    Task<bool> AllRequiredItemsCompletedAsync(Guid movementId, CancellationToken cancellationToken = default);

    /// <summary>Removes a checklist item from a movement.</summary>
    Task<bool> DeleteChecklistItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    // ── Dashboard ─────────────────────────────────────────────────────────────

    /// <summary>Returns aggregated movement pipeline metrics for the dashboard.</summary>
    Task<StaffMovementDashboardDto> GetDashboardAsync(
        int?      filterYear = null,
        DateTime? fromDate   = null,
        DateTime? toDate     = null,
        CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF PROMOTION SERVICE
// ============================================================================

#region Staff Promotion Service

public interface IStaffPromotionService
{
    Task<StaffPromotionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffPromotionDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffPromotionDto>> GetByTypeAsync(StaffPromotionType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffPromotionDto>> GetActiveActingPromotionsAsync(CancellationToken cancellationToken = default);

    Task<StaffPromotionDto> CreateAsync(CreateStaffPromotionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffPromotionDto> UpdateAsync(UpdateStaffPromotionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF TRANSFER SERVICE
// ============================================================================

#region Staff Transfer Service

public interface IStaffTransferService
{
    Task<StaffTransferDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffTransferDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTransferDto>> GetByTypeAsync(StaffTransferType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTransferDto>> GetByReasonCategoryAsync(StaffTransferReasonCategory reasonCategory, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTransferDto>> GetInterCompanyTransfersAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTransferDto>> GetRelocationTransfersAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTransferDto>> GetInTransitionAsync(CancellationToken cancellationToken = default);

    Task<StaffTransferDto> CreateAsync(CreateStaffTransferDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTransferDto> UpdateAsync(UpdateStaffTransferDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DEMOTION SERVICE
// ============================================================================

#region Staff Demotion Service

public interface IStaffDemotionService
{
    Task<StaffDemotionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffDemotionDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDemotionDto>> GetDisciplinaryDemotionsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDemotionDto>> GetPerformanceRelatedDemotionsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDemotionDto>> GetWithPendingAppealsAsync(CancellationToken cancellationToken = default);

    Task<StaffDemotionDto> CreateAsync(CreateStaffDemotionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffDemotionDto> UpdateAsync(UpdateStaffDemotionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the employee's response to a demotion notice (appeal / acceptance).
    /// <paramref name="respondingEmployeeId"/> is the token employee and must be the demoted employee:
    /// an appeal is testimony, so it is never recorded on someone's behalf. The response date is
    /// stamped server-side for the same reason.
    /// </summary>
    Task<bool> RecordEmployeeResponseAsync(Guid demotionId, string response, Guid respondingEmployeeId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF SECONDMENT SERVICE
// ============================================================================

#region Staff Secondment Service

public interface IStaffSecondmentService
{
    Task<StaffSecondmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffSecondmentDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffSecondmentDto>> GetByTypeAsync(StaffSecondmentType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffSecondmentDto>> GetExternalSecondmentsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffSecondmentDto>> GetByHostOrganizationAsync(string hostOrganization, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffSecondmentDto>> GetEndingSoonAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    Task<StaffSecondmentDto> CreateAsync(CreateStaffSecondmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffSecondmentDto> UpdateAsync(UpdateStaffSecondmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Extends the secondment end date by the specified number of months.</summary>
    Task<StaffSecondmentDto> ExtendAsync(ExtendStaffSecondmentDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF ACTING APPOINTMENT SERVICE
// ============================================================================

#region Staff Acting Appointment Service

public interface IStaffActingAppointmentService
{
    // ── Queries ───────────────────────────────────────────────────────────────
    Task<StaffActingAppointmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffActingAppointmentDto?> GetByAppointmentNumberAsync(string appointmentNumber, CancellationToken cancellationToken = default);
    Task<StaffActingAppointmentDto> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<StaffActingAppointmentSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetByStatusAsync(StaffActingStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetActiveAppointmentsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetByActingPositionAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetExpiringAppointmentsAsync(int daysAhead = 14, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffActingAppointmentSummaryDto>> GetConvertedToPermanentAsync(CancellationToken cancellationToken = default);

    // ── CRUD & Workflow ───────────────────────────────────────────────────────
    Task<StaffActingAppointmentDto> CreateAsync(CreateStaffActingAppointmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffActingAppointmentDto> UpdateAsync(UpdateStaffActingAppointmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Marks the acting appointment as complete.</summary>
    Task<bool> CompleteAsync(CompleteStaffActingAppointmentDto dto, Guid completedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Extends the acting appointment end date and marks status as Extended.</summary>
    Task<StaffActingAppointmentDto> ExtendAsync(ExtendStaffActingAppointmentDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Converts a completed acting appointment into a permanent promotion.</summary>
    Task<bool> ConvertToPermanentAsync(ConvertActingToPermanentDto dto, Guid convertedByUserId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// EMPLOYEE CAREER PATH SERVICE
// ============================================================================

#region Employee Career Path Service

public interface IEmployeeCareerPathService
{
    Task<EmployeeCareerPathDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeCareerPathDto> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns the complete career history for an employee, oldest-first.</summary>
    Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Returns the single current career path record for an employee.</summary>
    Task<EmployeeCareerPathDto?> GetCurrentPositionAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>Returns all career path records where the employee was placed in the given org unit.</summary>
    Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetByOrganizationUnitIdAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    /// <summary>Returns the career path record caused by a specific movement.</summary>
    Task<EmployeeCareerPathDto?> GetByMovementIdAsync(Guid movementId, CancellationToken cancellationToken = default);

    /// <summary>Returns all career path records associated with a salary grade.</summary>
    Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetBySalaryGradeIdAsync(Guid salaryGradeId, CancellationToken cancellationToken = default);

    /// <summary>Returns all employees currently occupying a role in the given org unit.</summary>
    Task<IEnumerable<EmployeeCareerPathSummaryDto>> GetCurrentOccupantsForUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    /// <summary>Manually creates a career path record (typically auto-created by the movement workflow).</summary>
    Task<EmployeeCareerPathDto> CreateAsync(CreateEmployeeCareerPathDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>Updates mutable fields on a career path record (EndDate, Achievements, KeyProjects).</summary>
    Task<EmployeeCareerPathDto> UpdateAsync(UpdateEmployeeCareerPathDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion
