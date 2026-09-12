using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF DISCIPLINE ACTION STEP
// ============================================================================

#region Staff Discipline Action Step

public interface IStaffDisciplineActionStepRepository : IGenericRepository<StaffDisciplineActionStep>
{
    /// <summary>Returns all procedural steps for a case, ordered by the offense procedure sequence number.</summary>
    Task<IEnumerable<StaffDisciplineActionStep>> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns steps for a case that are still Pending or InProgress.</summary>
    Task<IEnumerable<StaffDisciplineActionStep>> GetPendingStepsAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns all overdue steps (due date passed, not Completed, Cancelled, or Skipped) across all cases.</summary>
    Task<IEnumerable<StaffDisciplineActionStep>> GetOverdueStepsAsync(Guid tenantId);

    /// <summary>Returns steps actioned by the specified employee across all cases.</summary>
    Task<IEnumerable<StaffDisciplineActionStep>> GetByActionedByAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns a step fully loaded with its offense procedure details and attached documents.</summary>
    Task<StaffDisciplineActionStep?> GetWithDocumentsAsync(Guid tenantId, Guid id);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE WITNESS
// ============================================================================

#region Staff Discipline Witness

public interface IStaffDisciplineWitnessRepository : IGenericRepository<StaffDisciplineWitness>
{
    /// <summary>Returns all witnesses for a case, with employee details loaded where applicable.</summary>
    Task<IEnumerable<StaffDisciplineWitness>> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns all cases where the specified employee appears as a witness.</summary>
    Task<IEnumerable<StaffDisciplineWitness>> GetByEmployeeWitnessAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns witnesses for a case who have not yet provided a statement.</summary>
    Task<IEnumerable<StaffDisciplineWitness>> GetWithoutStatementAsync(Guid tenantId, Guid caseId);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE DOCUMENT
// ============================================================================

#region Staff Discipline Document

public interface IStaffDisciplineDocumentRepository : IGenericRepository<StaffDisciplineDocument>
{
    /// <summary>Returns all documents attached to a case, ordered by upload date descending.</summary>
    Task<IEnumerable<StaffDisciplineDocument>> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns documents for a case filtered by scope (Case, ActionStep, or Appeal).</summary>
    Task<IEnumerable<StaffDisciplineDocument>> GetByScopeAsync(Guid tenantId, Guid caseId, DisciplinaryDocumentScope scope);

    /// <summary>Returns documents attached to a specific procedural step.</summary>
    Task<IEnumerable<StaffDisciplineDocument>> GetByActionStepIdAsync(Guid tenantId, Guid actionStepId);

    /// <summary>Returns documents attached to an appeal.</summary>
    Task<IEnumerable<StaffDisciplineDocument>> GetByAppealIdAsync(Guid tenantId, Guid appealId);

    /// <summary>Returns documents for a case filtered by document category.</summary>
    Task<IEnumerable<StaffDisciplineDocument>> GetByCategoryAsync(Guid tenantId, Guid caseId, DisciplinaryDocumentCategory category);

    /// <summary>Returns documents uploaded by the specified employee across all cases.</summary>
    Task<IEnumerable<StaffDisciplineDocument>> GetByUploaderAsync(Guid tenantId, Guid uploadedById);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE NOTE
// ============================================================================

#region Staff Discipline Note

public interface IStaffDisciplineNoteRepository : IGenericRepository<StaffDisciplineNote>
{
    /// <summary>
    /// Returns all notes for a case, ordered by note date descending.
    /// Set <paramref name="includeConfidential"/> to false to exclude confidential notes (for non-HR roles).
    /// </summary>
    Task<IEnumerable<StaffDisciplineNote>> GetByCaseIdAsync(Guid tenantId, Guid caseId, bool includeConfidential = true);

    /// <summary>Returns all notes authored by the specified employee across all cases.</summary>
    Task<IEnumerable<StaffDisciplineNote>> GetByAuthorAsync(Guid tenantId, Guid employeeId);

    /// <summary>Returns only confidential notes for a case.</summary>
    Task<IEnumerable<StaffDisciplineNote>> GetConfidentialAsync(Guid tenantId, Guid caseId);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE NOTIFICATION
// ============================================================================

#region Staff Discipline Notification

public interface IStaffDisciplineNotificationRepository : IGenericRepository<StaffDisciplineNotification>
{
    /// <summary>Returns all notifications sent for a case, ordered by sent date descending.</summary>
    Task<IEnumerable<StaffDisciplineNotification>> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns notifications for a case that have not yet been acknowledged by the employee.</summary>
    Task<IEnumerable<StaffDisciplineNotification>> GetUnacknowledgedAsync(Guid tenantId, Guid caseId);

    /// <summary>
    /// Returns notifications sent more than <paramref name="daysOld"/> days ago that have not been
    /// acknowledged and where a follow-up has not yet been sent.
    /// </summary>
    Task<IEnumerable<StaffDisciplineNotification>> GetPendingFollowupAsync(Guid tenantId, int daysOld = 3);

    /// <summary>Returns notifications for a case filtered by notification type.</summary>
    Task<IEnumerable<StaffDisciplineNotification>> GetByTypeAsync(Guid tenantId, Guid caseId, DisciplinaryNotificationType type);

    /// <summary>Returns notifications sent by the specified employee across all cases.</summary>
    Task<IEnumerable<StaffDisciplineNotification>> GetBySenderAsync(Guid tenantId, Guid sentById);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE LEGAL REVIEW
// ============================================================================

#region Staff Discipline Legal Review

public interface IStaffDisciplineLegalReviewRepository : IGenericRepository<StaffDisciplineLegalReview>
{
    /// <summary>Returns all legal review records for a case, ordered by referral date descending.</summary>
    Task<IEnumerable<StaffDisciplineLegalReview>> GetByCaseIdAsync(Guid tenantId, Guid caseId);

    /// <summary>Returns legal reviews with no completion date (review still open).</summary>
    Task<IEnumerable<StaffDisciplineLegalReview>> GetOpenReviewsAsync(Guid tenantId);

    /// <summary>Returns legal reviews at or above the specified risk level.</summary>
    Task<IEnumerable<StaffDisciplineLegalReview>> GetByRiskLevelAsync(Guid tenantId, DisciplineLegalRiskLevel minimumRisk);

    /// <summary>Returns open legal reviews that require engagement of external counsel.</summary>
    Task<IEnumerable<StaffDisciplineLegalReview>> GetRequiringExternalCounselAsync(Guid tenantId);

    /// <summary>Returns legal reviews referred by the specified employee.</summary>
    Task<IEnumerable<StaffDisciplineLegalReview>> GetByReferrerAsync(Guid tenantId, Guid referredById);

    /// <summary>Returns the total legal costs incurred across all reviews for a case.</summary>
    Task<decimal> GetTotalLegalCostsForCaseAsync(Guid tenantId, Guid caseId);
}

#endregion
