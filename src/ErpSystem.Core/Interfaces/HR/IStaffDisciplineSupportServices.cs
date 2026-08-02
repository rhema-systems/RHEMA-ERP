using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF DISCIPLINE ACTION STEP SERVICE
// ============================================================================

#region Staff Discipline Action Step Service

public interface IStaffDisciplineActionStepService
{
    Task<StaffDisciplineActionStepDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineActionStepDto>> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineActionStepDto>> GetPendingStepsAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineActionStepDto>> GetOverdueStepsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineActionStepDto>> GetByActionedByAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds action steps for a case by copying the ordered procedure steps from the case's offense.
    /// Throws if steps have already been initialised for the case.
    /// </summary>
    Task<IEnumerable<StaffDisciplineActionStepDto>> InitialiseFromOffenseProceduresAsync(Guid caseId, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<StaffDisciplineActionStepDto> UpdateAsync(UpdateActionStepDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks a step as Completed and records the completion date.</summary>
    Task<bool> CompleteStepAsync(Guid stepId, string notes, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks a step as Skipped with an explanatory reason stored in its Notes field.</summary>
    Task<bool> SkipStepAsync(Guid stepId, string reason, Guid userId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE WITNESS SERVICE
// ============================================================================

#region Staff Discipline Witness Service

public interface IStaffDisciplineWitnessService
{
    Task<StaffDisciplineWitnessDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineWitnessSummaryDto>> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineWitnessSummaryDto>> GetByEmployeeWitnessAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineWitnessSummaryDto>> GetWithoutStatementAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<StaffDisciplineWitnessDto> AddAsync(CreateStaffDisciplineWitnessDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<StaffDisciplineWitnessDto> UpdateAsync(UpdateStaffDisciplineWitnessDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE DOCUMENT SERVICE
// ============================================================================

#region Staff Discipline Document Service

public interface IStaffDisciplineDocumentService
{
    Task<StaffDisciplineDocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineDocumentSummaryDto>> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineDocumentSummaryDto>> GetByScopeAsync(Guid caseId, DisciplinaryDocumentScope scope, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineDocumentSummaryDto>> GetByActionStepIdAsync(Guid actionStepId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineDocumentSummaryDto>> GetByAppealIdAsync(Guid appealId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineDocumentSummaryDto>> GetByCategoryAsync(Guid caseId, DisciplinaryDocumentCategory category, CancellationToken cancellationToken = default);

    Task<StaffDisciplineDocumentDto> AddAsync(CreateStaffDisciplineDocumentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a scope/step/appeal combination against a case, so an upload can be rejected
    /// before any bytes are stored.
    /// </summary>
    /// <remarks>
    /// This replaces the previous stream-based <c>UploadAsync</c>. That method wrote straight to
    /// storage, bypassing the shared controlled-upload gate — no malware scan, no upload record,
    /// and into the publicly served web root. Uploading is now the controller's job via
    /// <c>IHrControlledDocumentService</c>, and removing the old method keeps that the only route.
    /// </remarks>
    Task ValidateDocumentScopeAsync(
        Guid caseId,
        DisciplinaryDocumentScope scope,
        Guid? actionStepId,
        Guid? appealId,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string FileName, string ContentType)?> OpenFileAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE NOTE SERVICE
// ============================================================================

#region Staff Discipline Note Service

public interface IStaffDisciplineNoteService
{
    Task<StaffDisciplineNoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <param name="includeConfidential">
    /// Set to <c>false</c> for non-HR roles that must not see confidential notes.
    /// </param>
    Task<IEnumerable<StaffDisciplineNoteSummaryDto>> GetByCaseIdAsync(Guid caseId, bool includeConfidential = true, CancellationToken cancellationToken = default);

    Task<IEnumerable<StaffDisciplineNoteSummaryDto>> GetByAuthorAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task<StaffDisciplineNoteDto> AddAsync(CreateStaffDisciplineNoteDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<StaffDisciplineNoteDto> UpdateAsync(UpdateStaffDisciplineNoteDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE NOTIFICATION SERVICE
// ============================================================================

#region Staff Discipline Notification Service

public interface IStaffDisciplineNotificationService
{
    Task<StaffDisciplineNotificationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineNotificationSummaryDto>> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineNotificationSummaryDto>> GetUnacknowledgedAsync(Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>Returns notifications pending a follow-up (sent &gt; <paramref name="daysOld"/> days ago, unacknowledged, no follow-up sent).</summary>
    Task<IEnumerable<StaffDisciplineNotificationSummaryDto>> GetPendingFollowupAsync(int daysOld = 3, CancellationToken cancellationToken = default);

    Task<StaffDisciplineNotificationDto> SendAsync(CreateStaffDisciplineNotificationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> AcknowledgeAsync(AcknowledgeNotificationDto dto, CancellationToken cancellationToken = default);
    Task<bool> SendFollowupAsync(SendFollowupNotificationDto dto, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINE LEGAL REVIEW SERVICE
// ============================================================================

#region Staff Discipline Legal Review Service

public interface IStaffDisciplineLegalReviewService
{
    Task<StaffDisciplineLegalReviewDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineLegalReviewDto>> GetByCaseIdAsync(Guid caseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineLegalReviewDto>> GetOpenReviewsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineLegalReviewDto>> GetByRiskLevelAsync(DisciplineLegalRiskLevel minimumRisk, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplineLegalReviewDto>> GetRequiringExternalCounselAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetTotalLegalCostsForCaseAsync(Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>Refers a case to legal review. The ReferredToLegalDate is set to UtcNow if not supplied.</summary>
    Task<StaffDisciplineLegalReviewDto> ReferAsync(CreateStaffDisciplineLegalReviewDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<StaffDisciplineLegalReviewDto> UpdateAsync(UpdateStaffDisciplineLegalReviewDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks the review complete by recording the completion date as UtcNow.</summary>
    Task<bool> CompleteAsync(Guid reviewId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion
