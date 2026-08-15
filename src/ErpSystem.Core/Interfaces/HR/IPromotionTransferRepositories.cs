using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF MOVEMENT
// ============================================================================

#region Staff Movement

public interface IStaffMovementRepository : IGenericRepository<StaffMovement>
{
    /// <summary>Returns the movement matching the unique movement number.</summary>
    Task<StaffMovement?> GetByMovementNumberAsync(Guid tenantId, string movementNumber);

    /// <summary>
    /// Returns a fully-loaded movement including all position, org-unit, location,
    /// salary, approval, history, attachment, and checklist navigations.
    /// </summary>
    Task<StaffMovement?> GetWithFullDetailsAsync(Guid tenantId, Guid id);

    /// <summary>Returns summary-navigated movements for the given parent movement IDs.</summary>
    Task<IEnumerable<StaffMovement>> GetSummariesByIdsAsync(Guid tenantId, IEnumerable<Guid> ids);

    /// <summary>Returns all movements for an employee, newest-effective-date first.</summary>
    Task<IEnumerable<StaffMovement>> GetByEmployeeAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns movements filtered by status, with summary navigations loaded.</summary>
    Task<IEnumerable<StaffMovement>> GetByStatusAsync(Guid tenantId, StaffMovementStatus status);

    /// <summary>
    /// Returns movements of the given type, optionally constrained to an effective-date window.
    /// </summary>
    Task<IEnumerable<StaffMovement>> GetByTypeAsync(Guid tenantId, StaffMovementType type, DateTime? from = null, DateTime? to = null);

    /// <summary>Returns movements of the given type that are also in the given status.</summary>
    Task<IEnumerable<StaffMovement>> GetByTypeAndStatusAsync(Guid tenantId, StaffMovementType type, StaffMovementStatus status);

    /// <summary>Returns movements whose current (source) org unit is the given unit.</summary>
    Task<IEnumerable<StaffMovement>> GetByCurrentOrganizationUnitAsync(Guid tenantId, Guid organizationUnitId);

    /// <summary>Returns movements whose new (destination) org unit is the given unit.</summary>
    Task<IEnumerable<StaffMovement>> GetByNewOrganizationUnitAsync(Guid tenantId, Guid organizationUnitId);

    /// <summary>
    /// Returns movements that are currently waiting for at least one approval-level action
    /// (i.e. at least one ApprovalLevel with Status == Pending).
    /// </summary>
    Task<IEnumerable<StaffMovement>> GetPendingApprovalAsync(Guid tenantId);

    /// <summary>
    /// Returns movements that require employee acceptance and have not yet received a response.
    /// </summary>
    Task<IEnumerable<StaffMovement>> GetPendingEmployeeAcceptanceAsync(Guid tenantId);

    /// <summary>Returns movements where handover is required but not yet completed.</summary>
    Task<IEnumerable<StaffMovement>> GetPendingHandoverAsync(Guid tenantId);

    /// <summary>
    /// Returns temporary assignments (IsTemporary = true) that have not been returned
    /// (ReturnProcessed = false) and whose TemporaryEndDate has not yet passed.
    /// </summary>
    Task<IEnumerable<StaffMovement>> GetActiveTemporaryAssignmentsAsync(Guid tenantId);

    /// <summary>
    /// Returns temporary assignments whose TemporaryEndDate falls within the
    /// specified number of days and that have not yet been returned.
    /// </summary>
    Task<IEnumerable<StaffMovement>> GetExpiringTemporaryAssignmentsAsync(Guid tenantId, int daysAhead = 30);

    /// <summary>Returns movements whose effective date falls within the given range.</summary>
    Task<IEnumerable<StaffMovement>> GetByEffectiveDateRangeAsync(Guid tenantId, DateTime from, DateTime to);

    /// <summary>Returns movements submitted by the specified requester.</summary>
    Task<IEnumerable<StaffMovement>> GetByRequestedByAsync(Guid tenantId, Guid requestedByEmployeeId);

    /// <summary>
    /// Returns the most recent movement (by effective date) for each type for an employee.
    /// Useful for building the employee career summary panel.
    /// </summary>
    Task<IEnumerable<StaffMovement>> GetLatestMovementsForEmployeeAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns all movements linked to a succession plan.</summary>
    Task<IEnumerable<StaffMovement>> GetBySuccessionPlanAsync(Guid tenantId, Guid successionPlanId);
}

#endregion

// ============================================================================
// STAFF MOVEMENT APPROVAL LEVEL
// ============================================================================

#region Staff Movement Approval Level

public interface IStaffMovementApprovalLevelRepository : IGenericRepository<StaffMovementApprovalLevel>
{
    /// <summary>Returns all approval levels for a movement, ordered by level number.</summary>
    Task<IEnumerable<StaffMovementApprovalLevel>> GetByMovementIdAsync(Guid movementId);

    /// <summary>
    /// Returns the current pending approval level for a movement — the lowest-numbered
    /// level whose status is still Pending. Returns null when all levels are actioned.
    /// </summary>
    Task<StaffMovementApprovalLevel?> GetCurrentPendingLevelAsync(Guid movementId);

    /// <summary>Returns all approval levels awaiting action by the specified approver.</summary>
    Task<IEnumerable<StaffMovementApprovalLevel>> GetPendingByApproverAsync(Guid approverId);

    /// <summary>Returns all approval levels delegated to the specified employee.</summary>
    Task<IEnumerable<StaffMovementApprovalLevel>> GetPendingByDelegateAsync(Guid delegatedToId);

    /// <summary>
    /// Returns true if all approval levels for the movement are approved,
    /// indicating the movement is ready for final authorisation.
    /// </summary>
    Task<bool> AllLevelsApprovedAsync(Guid movementId);
}

#endregion

// ============================================================================
// STAFF MOVEMENT STATUS HISTORY  (immutable — read only)
// ============================================================================

#region Staff Movement Status History

public interface IStaffMovementStatusHistoryRepository : IGenericRepository<StaffMovementStatusHistory>
{
    /// <summary>Returns the full audit trail for a movement, newest-first.</summary>
    Task<IEnumerable<StaffMovementStatusHistory>> GetByMovementIdAsync(Guid movementId);

    /// <summary>Returns the most recent status-change record for a movement.</summary>
    Task<StaffMovementStatusHistory?> GetLatestAsync(Guid movementId);

    /// <summary>
    /// Returns all status history records where the transition was to the
    /// specified status, across all movements. Useful for compliance reporting.
    /// </summary>
    Task<IEnumerable<StaffMovementStatusHistory>> GetByToStatusAsync(
        StaffMovementStatus status, DateTime? from = null, DateTime? to = null);
}

#endregion

// ============================================================================
// STAFF MOVEMENT ATTACHMENT
// ============================================================================

#region Staff Movement Attachment

public interface IStaffMovementAttachmentRepository : IGenericRepository<StaffMovementAttachment>
{
    /// <summary>Returns all attachments for a movement, newest-upload first.</summary>
    Task<IEnumerable<StaffMovementAttachment>> GetByMovementIdAsync(Guid movementId);

    /// <summary>Returns attachments for a movement filtered by document type.</summary>
    Task<IEnumerable<StaffMovementAttachment>> GetByTypeAsync(
        Guid movementId, StaffMovementAttachmentType type);
}

#endregion

// ============================================================================
// STAFF MOVEMENT CHECKLIST ITEM
// ============================================================================

#region Staff Movement Checklist Item

public interface IStaffMovementChecklistItemRepository : IGenericRepository<StaffMovementChecklistItem>
{
    /// <summary>Returns all checklist items for a movement, in display order.</summary>
    Task<IEnumerable<StaffMovementChecklistItem>> GetByMovementIdAsync(Guid movementId);

    /// <summary>Returns checklist items for a movement that are not yet completed.</summary>
    Task<IEnumerable<StaffMovementChecklistItem>> GetPendingItemsAsync(Guid movementId);

    /// <summary>
    /// Returns checklist items whose due date has passed but are not yet completed.
    /// Scoped to a single movement when movementId is provided; tenant-wide when null.
    /// </summary>
    Task<IEnumerable<StaffMovementChecklistItem>> GetOverdueItemsAsync(Guid tenantId, Guid? movementId = null);

    /// <summary>Returns all checklist items assigned to the specified responsible person.</summary>
    Task<IEnumerable<StaffMovementChecklistItem>> GetByResponsiblePersonAsync(Guid tenantId, Guid employeeId);

    /// <summary>
    /// Returns true when every required checklist item for the movement is completed.
    /// </summary>
    Task<bool> AllRequiredItemsCompletedAsync(Guid movementId);
}

#endregion

// ============================================================================
// STAFF PROMOTION
// ============================================================================

#region Staff Promotion

public interface IStaffPromotionRepository : IGenericRepository<StaffPromotion>
{
    /// <summary>Returns the promotion detail record for a given movement.</summary>
    Task<StaffPromotion?> GetByMovementIdAsync(Guid movementId);

    /// <summary>Returns promotions filtered by promotion type, with movement loaded.</summary>
    Task<IEnumerable<StaffPromotion>> GetByTypeAsync(StaffPromotionType type);

    /// <summary>Returns all acting promotions (IsActingPromotion = true) that are still within their acting period.</summary>
    Task<IEnumerable<StaffPromotion>> GetActiveActingPromotionsAsync();
}

#endregion

// ============================================================================
// STAFF TRANSFER
// ============================================================================

#region Staff Transfer

public interface IStaffTransferRepository : IGenericRepository<StaffTransfer>
{
    /// <summary>Returns the transfer detail record for a given movement.</summary>
    Task<StaffTransfer?> GetByMovementIdAsync(Guid movementId);

    /// <summary>Returns transfers filtered by transfer type, with movement loaded.</summary>
    Task<IEnumerable<StaffTransfer>> GetByTypeAsync(StaffTransferType type);

    /// <summary>Returns transfers filtered by reason category.</summary>
    Task<IEnumerable<StaffTransfer>> GetByReasonCategoryAsync(StaffTransferReasonCategory reasonCategory);

    /// <summary>Returns all inter-company transfers (IsInterCompany = true), with movement loaded.</summary>
    Task<IEnumerable<StaffTransfer>> GetInterCompanyTransfersAsync();

    /// <summary>Returns transfers involving relocation (RequiresRelocation = true).</summary>
    Task<IEnumerable<StaffTransfer>> GetRelocationTransfersAsync();

    /// <summary>
    /// Returns transfers whose transition period is currently active
    /// (TransitionStartDate &lt;= today &lt;= TransitionEndDate).
    /// </summary>
    Task<IEnumerable<StaffTransfer>> GetInTransitionAsync();
}

#endregion

// ============================================================================
// STAFF DEMOTION
// ============================================================================

#region Staff Demotion

public interface IStaffDemotionRepository : IGenericRepository<StaffDemotion>
{
    /// <summary>Returns the demotion detail record for a given movement.</summary>
    Task<StaffDemotion?> GetByMovementIdAsync(Guid movementId);

    /// <summary>Returns demotions that were raised as disciplinary actions.</summary>
    Task<IEnumerable<StaffDemotion>> GetDisciplinaryDemotionsAsync();

    /// <summary>Returns demotions linked to a performance improvement plan.</summary>
    Task<IEnumerable<StaffDemotion>> GetPerformanceRelatedDemotionsAsync();

    /// <summary>
    /// Returns demotions where the employee has a right to appeal but has not yet
    /// submitted a response, and whose appeal deadline has not yet expired.
    /// </summary>
    Task<IEnumerable<StaffDemotion>> GetWithPendingAppealsAsync();

    /// <summary>Returns demotions linked to a specific disciplinary action record.</summary>
    Task<IEnumerable<StaffDemotion>> GetByDisciplinaryActionAsync(Guid disciplinaryActionId);
}

#endregion

// ============================================================================
// STAFF SECONDMENT
// ============================================================================

#region Staff Secondment

public interface IStaffSecondmentRepository : IGenericRepository<StaffSecondment>
{
    /// <summary>Returns the secondment detail record for a given movement.</summary>
    Task<StaffSecondment?> GetByMovementIdAsync(Guid movementId);

    /// <summary>Returns secondments filtered by secondment type, with movement loaded.</summary>
    Task<IEnumerable<StaffSecondment>> GetByTypeAsync(StaffSecondmentType type);

    /// <summary>Returns external secondments (IsExternal = true), with movement and employee loaded.</summary>
    Task<IEnumerable<StaffSecondment>> GetExternalSecondmentsAsync();

    /// <summary>Returns secondments at the specified host organisation (partial-match search).</summary>
    Task<IEnumerable<StaffSecondment>> GetByHostOrganizationAsync(string hostOrganization);

    /// <summary>
    /// Returns secondments whose EndDate falls within the specified number of days
    /// and whose parent movement's ReturnProcessed is still false.
    /// </summary>
    Task<IEnumerable<StaffSecondment>> GetEndingSoonAsync(int daysAhead = 30);
}

#endregion

// ============================================================================
// STAFF ACTING APPOINTMENT
// ============================================================================

#region Staff Acting Appointment

public interface IStaffActingAppointmentRepository : IGenericRepository<StaffActingAppointment>
{
    /// <summary>Returns the appointment matching the unique appointment number.</summary>
    Task<StaffActingAppointment?> GetByAppointmentNumberAsync(string appointmentNumber);

    /// <summary>
    /// Returns a fully-loaded appointment including employee, acting position,
    /// acting-for employee, and linked movement navigations.
    /// </summary>
    Task<StaffActingAppointment?> GetWithDetailsAsync(Guid id);

    /// <summary>Returns all acting appointments for a given employee, newest-start first.</summary>
    Task<IEnumerable<StaffActingAppointment>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns appointments filtered by status, with summary navigations loaded.</summary>
    Task<IEnumerable<StaffActingAppointment>> GetByStatusAsync(StaffActingStatus status);

    /// <summary>Returns all active appointments (Status == Active), ordered by start date.</summary>
    Task<IEnumerable<StaffActingAppointment>> GetActiveAppointmentsAsync();

    /// <summary>Returns all appointments for a specific acting position.</summary>
    Task<IEnumerable<StaffActingAppointment>> GetByActingPositionAsync(Guid positionId);

    /// <summary>
    /// Returns active appointments whose EndDate falls within the specified number of days.
    /// </summary>
    Task<IEnumerable<StaffActingAppointment>> GetExpiringAppointmentsAsync(int daysAhead = 14);

    /// <summary>Returns appointments that have been converted to permanent promotions.</summary>
    Task<IEnumerable<StaffActingAppointment>> GetConvertedToPermanentAsync();

    /// <summary>Returns the acting appointment that originated from the given movement, if any.</summary>
    Task<StaffActingAppointment?> GetByOriginatingMovementAsync(Guid movementId);
}

#endregion

// ============================================================================
// EMPLOYEE CAREER PATH
// ============================================================================

#region Employee Career Path

public interface IEmployeeCareerPathRepository : IGenericRepository<EmployeeCareerPath>
{
    /// <summary>Returns the complete career history for an employee, oldest-first.</summary>
    Task<IEnumerable<EmployeeCareerPath>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns the single current career path record for an employee (IsCurrent = true).</summary>
    Task<EmployeeCareerPath?> GetCurrentPositionAsync(Guid employeeId);

    /// <summary>
    /// Returns career path records where the employee was placed in the specified
    /// org unit (either as current or past history).
    /// </summary>
    Task<IEnumerable<EmployeeCareerPath>> GetByOrganizationUnitIdAsync(Guid organizationUnitId);

    /// <summary>Returns the career path record that was created by the specified movement.</summary>
    Task<EmployeeCareerPath?> GetByMovementIdAsync(Guid movementId);

    /// <summary>Returns all career path records associated with a given salary grade.</summary>
    Task<IEnumerable<EmployeeCareerPath>> GetBySalaryGradeIdAsync(Guid salaryGradeId);

    /// <summary>Returns a fully-loaded career path record including all navigation properties.</summary>
    Task<EmployeeCareerPath?> GetWithDetailsAsync(Guid id);

    /// <summary>
    /// Returns all employees currently placed in the specified org unit
    /// (IsCurrent = true records grouped by org unit).
    /// </summary>
    Task<IEnumerable<EmployeeCareerPath>> GetCurrentOccupantsForUnitAsync(Guid organizationUnitId);
}

#endregion
