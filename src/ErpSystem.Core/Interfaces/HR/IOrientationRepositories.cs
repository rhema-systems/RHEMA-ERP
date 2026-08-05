using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// ORIENTATION MODULE REPOSITORIES
// Only methods beyond IGenericRepository<T> (custom queries with eager-loading,
// ordering, soft-delete-aware lookups, and roll-up counts) are declared here.
// ============================================================================

// ============================================================================
// SECTION 1 — CATALOG
// ============================================================================

#region Orientation Category

public interface IOrientationCategoryRepository : IGenericRepository<OrientationCategory>
{
    /// <summary>Returns root (top-level) categories ordered by display order, with sub-categories loaded.</summary>
    Task<IEnumerable<OrientationCategory>> GetRootCategoriesAsync();

    /// <summary>Returns the direct child categories of the given parent (null = root level).</summary>
    Task<IEnumerable<OrientationCategory>> GetByParentAsync(Guid? parentCategoryId);

    /// <summary>Returns all active categories ordered by display order.</summary>
    Task<IEnumerable<OrientationCategory>> GetActiveAsync();

    /// <summary>Returns a category with its parent and sub-categories loaded.</summary>
    Task<OrientationCategory?> GetWithSubCategoriesAsync(Guid id);
}

#endregion

#region Orientation Program

public interface IOrientationProgramRepository : IGenericRepository<OrientationProgram>
{
    /// <summary>Returns the program matching the unique program code, with category loaded.</summary>
    Task<OrientationProgram?> GetByProgramCodeAsync(string programCode);

    /// <summary>Returns true if a program with the given code already exists (optionally excluding one id).</summary>
    Task<bool> ProgramCodeExistsAsync(string programCode, Guid? excludeId = null);

    /// <summary>
    /// Returns a fully-loaded program: category, owner unit, modules + content items,
    /// audience rules, prerequisites + prerequisite programs, and assessment questions + options.
    /// </summary>
    Task<OrientationProgram?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns programs filtered by status, with category loaded, for list views.</summary>
    Task<IEnumerable<OrientationProgram>> GetByStatusAsync(OrientationProgramStatus status);

    /// <summary>Returns programs in a category.</summary>
    Task<IEnumerable<OrientationProgram>> GetByCategoryAsync(Guid categoryId);

    /// <summary>Returns programs of a given type.</summary>
    Task<IEnumerable<OrientationProgram>> GetByTypeAsync(OrientationProgramType programType);

    /// <summary>Returns programs whose status is Active (available for enrollment).</summary>
    Task<IEnumerable<OrientationProgram>> GetActiveProgramsAsync();

    /// <summary>Returns programs owned by the specified organization unit.</summary>
    Task<IEnumerable<OrientationProgram>> GetByOwnerOrganizationUnitAsync(Guid organizationUnitId);

    /// <summary>Returns the highest numeric suffix among existing program codes with the given prefix (for code generation).</summary>
    Task<int> GetMaxProgramCodeSequenceAsync(string prefix);
}

#endregion

#region Orientation Module

public interface IOrientationModuleRepository : IGenericRepository<OrientationModule>
{
    /// <summary>Returns all modules for a program ordered by sequence, with content items loaded.</summary>
    Task<IEnumerable<OrientationModule>> GetByProgramIdAsync(Guid programId);

    /// <summary>Returns a module with its content items loaded.</summary>
    Task<OrientationModule?> GetWithContentAsync(Guid id);

    /// <summary>Returns the highest sequence order used by modules in a program (0 if none).</summary>
    Task<int> GetMaxSequenceOrderAsync(Guid programId);
}

#endregion

#region Orientation Content Item

public interface IOrientationContentItemRepository : IGenericRepository<OrientationContentItem>
{
    /// <summary>Returns all content items for a module ordered by sequence.</summary>
    Task<IEnumerable<OrientationContentItem>> GetByModuleIdAsync(Guid moduleId);

    /// <summary>Returns all content items across a whole program (via its modules), ordered.</summary>
    Task<IEnumerable<OrientationContentItem>> GetByProgramIdAsync(Guid programId);

    /// <summary>Returns the highest sequence order used by content items in a module (0 if none).</summary>
    Task<int> GetMaxSequenceOrderAsync(Guid moduleId);
}

#endregion

#region Orientation Prerequisite

public interface IOrientationPrerequisiteRepository : IGenericRepository<OrientationPrerequisite>
{
    /// <summary>Returns the prerequisites of a program, with the prerequisite programs loaded.</summary>
    Task<IEnumerable<OrientationPrerequisite>> GetByProgramIdAsync(Guid programId);

    /// <summary>Returns the programs that depend on the given program as a prerequisite.</summary>
    Task<IEnumerable<OrientationPrerequisite>> GetDependentsAsync(Guid prerequisiteProgramId);

    /// <summary>Returns true if the prerequisite link already exists.</summary>
    Task<bool> ExistsAsync(Guid programId, Guid prerequisiteProgramId);
}

#endregion

#region Orientation Audience Rule

public interface IOrientationAudienceRuleRepository : IGenericRepository<OrientationAudienceRule>
{
    /// <summary>Returns all audience rules for a program.</summary>
    Task<IEnumerable<OrientationAudienceRule>> GetByProgramIdAsync(Guid programId);

    /// <summary>Returns active rules for the given trigger across all programs (for the auto-enrollment engine).</summary>
    Task<IEnumerable<OrientationAudienceRule>> GetActiveByTriggerAsync(OrientationEnrollmentTrigger trigger);
}

#endregion

// ============================================================================
// SECTION 2 — DELIVERY
// ============================================================================

#region Orientation Session

public interface IOrientationSessionRepository : IGenericRepository<OrientationSession>
{
    /// <summary>Returns the session matching the unique session code, with program loaded.</summary>
    Task<OrientationSession?> GetBySessionCodeAsync(string sessionCode);

    /// <summary>Returns true if a session with the given code already exists (optionally excluding one id).</summary>
    Task<bool> SessionCodeExistsAsync(string sessionCode, Guid? excludeId = null);

    /// <summary>Returns all sessions for a program, ordered by scheduled start.</summary>
    Task<IEnumerable<OrientationSession>> GetByProgramIdAsync(Guid programId);

    /// <summary>Returns a session with program, facilitators and enrollments loaded.</summary>
    Task<OrientationSession?> GetWithDetailsAsync(Guid id);

    /// <summary>Returns sessions filtered by status.</summary>
    Task<IEnumerable<OrientationSession>> GetByStatusAsync(OrientationSessionStatus status);

    /// <summary>Returns sessions scheduled to start within the specified number of days.</summary>
    Task<IEnumerable<OrientationSession>> GetUpcomingAsync(int daysAhead = 30);

    /// <summary>Returns sessions currently open for enrollment.</summary>
    Task<IEnumerable<OrientationSession>> GetOpenForEnrollmentAsync();

    /// <summary>Returns the number of (non-withdrawn/cancelled) enrollments for a session.</summary>
    Task<int> GetEnrolledCountAsync(Guid sessionId);
}

#endregion

#region Orientation Session Facilitator

public interface IOrientationSessionFacilitatorRepository : IGenericRepository<OrientationSessionFacilitator>
{
    /// <summary>Returns all facilitators assigned to a session.</summary>
    Task<IEnumerable<OrientationSessionFacilitator>> GetBySessionIdAsync(Guid sessionId);

    /// <summary>Returns all facilitator assignments for an (internal) employee.</summary>
    Task<IEnumerable<OrientationSessionFacilitator>> GetByEmployeeIdAsync(Guid employeeId);
}

#endregion

#region Orientation Attendance Record

public interface IOrientationAttendanceRecordRepository : IGenericRepository<OrientationAttendanceRecord>
{
    /// <summary>Returns all attendance records for an enrollment, ordered by session day.</summary>
    Task<IEnumerable<OrientationAttendanceRecord>> GetByEnrollmentIdAsync(Guid enrollmentId);

    /// <summary>Returns all attendance records for a session (across all enrolled participants).</summary>
    Task<IEnumerable<OrientationAttendanceRecord>> GetBySessionIdAsync(Guid sessionId);

    /// <summary>Returns the attendance record for a specific enrollment and session day, or null.</summary>
    Task<OrientationAttendanceRecord?> GetByEnrollmentAndDayAsync(Guid enrollmentId, int sessionDay);
}

#endregion

// ============================================================================
// SECTION 3 — ENROLLMENT & PROGRESS
// ============================================================================

#region Employee Orientation (Enrollment)

public interface IEmployeeOrientationRepository : IGenericRepository<EmployeeOrientation>
{
    /// <summary>
    /// Returns a fully-loaded enrollment: program, session, content progress, assessment
    /// responses, acknowledgements, feedback, attendance records and certificates.
    /// </summary>
    Task<EmployeeOrientation?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns all enrollments for an employee (their orientation list), with program/session loaded.</summary>
    Task<IEnumerable<EmployeeOrientation>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns all enrollments for a program.</summary>
    Task<IEnumerable<EmployeeOrientation>> GetByProgramIdAsync(Guid programId);

    /// <summary>Returns all enrollments for a session.</summary>
    Task<IEnumerable<EmployeeOrientation>> GetBySessionIdAsync(Guid sessionId);

    /// <summary>Returns an employee's enrollment in a specific program, or null if not enrolled.</summary>
    Task<EmployeeOrientation?> GetByEmployeeAndProgramAsync(Guid employeeId, Guid programId);

    /// <summary>Returns true if the employee already has an enrollment in the program.</summary>
    Task<bool> ExistsForEmployeeAndProgramAsync(Guid employeeId, Guid programId);

    /// <summary>Returns enrollments filtered by completion status.</summary>
    Task<IEnumerable<EmployeeOrientation>> GetByCompletionStatusAsync(OrientationCompletionStatus status);

    /// <summary>Returns enrollments filtered by enrollment status.</summary>
    Task<IEnumerable<EmployeeOrientation>> GetByEnrollmentStatusAsync(OrientationEnrollmentStatus status);

    /// <summary>Returns overdue enrollments (past due date and not completed/exempted).</summary>
    Task<IEnumerable<EmployeeOrientation>> GetOverdueAsync();

    /// <summary>Returns enrollments whose due date falls within the specified number of days and are not yet complete.</summary>
    Task<IEnumerable<EmployeeOrientation>> GetDueSoonAsync(int daysAhead = 7);

    /// <summary>Returns waitlisted enrollments for a session, ordered by waitlist position.</summary>
    Task<IEnumerable<EmployeeOrientation>> GetWaitlistedForSessionAsync(Guid sessionId);

    /// <summary>Returns the count of enrollments for a program in a given completion status (for dashboards).</summary>
    Task<int> CountByProgramAndCompletionStatusAsync(Guid programId, OrientationCompletionStatus status);
}

#endregion

#region Orientation Content Progress

public interface IOrientationContentProgressRepository : IGenericRepository<OrientationContentProgress>
{
    /// <summary>Returns all content-progress rows for an enrollment, with content items loaded.</summary>
    Task<IEnumerable<OrientationContentProgress>> GetByEnrollmentIdAsync(Guid employeeOrientationId);

    /// <summary>Returns the progress row for a specific enrollment + content item (upsert lookup), or null.</summary>
    Task<OrientationContentProgress?> GetByEnrollmentAndContentAsync(Guid employeeOrientationId, Guid contentItemId);

    /// <summary>Returns the number of completed content items for an enrollment.</summary>
    Task<int> CountCompletedForEnrollmentAsync(Guid employeeOrientationId);
}

#endregion

// ============================================================================
// SECTION 4 — ASSESSMENT
// ============================================================================

#region Orientation Assessment Question

public interface IOrientationAssessmentQuestionRepository : IGenericRepository<OrientationAssessmentQuestion>
{
    /// <summary>Returns all questions for a program ordered by sequence, with options loaded.</summary>
    Task<IEnumerable<OrientationAssessmentQuestion>> GetByProgramIdAsync(Guid programId);

    /// <summary>Returns only active questions for a program (for delivering the assessment), with options loaded.</summary>
    Task<IEnumerable<OrientationAssessmentQuestion>> GetActiveByProgramIdAsync(Guid programId);

    /// <summary>Returns a question with its options loaded.</summary>
    Task<OrientationAssessmentQuestion?> GetWithOptionsAsync(Guid id);

    /// <summary>Returns the highest sequence order used by questions in a program (0 if none).</summary>
    Task<int> GetMaxSequenceOrderAsync(Guid programId);
}

#endregion

#region Orientation Assessment Option

public interface IOrientationAssessmentOptionRepository : IGenericRepository<OrientationAssessmentOption>
{
    /// <summary>Returns all options for a question, ordered by display order.</summary>
    Task<IEnumerable<OrientationAssessmentOption>> GetByQuestionIdAsync(Guid questionId);

    /// <summary>Returns all options for the given set of questions (for batch grading).</summary>
    Task<IEnumerable<OrientationAssessmentOption>> GetByQuestionIdsAsync(IEnumerable<Guid> questionIds);
}

#endregion

#region Orientation Assessment Response

public interface IOrientationAssessmentResponseRepository : IGenericRepository<OrientationAssessmentResponse>
{
    /// <summary>Returns all responses for an enrollment, with question and selected option loaded.</summary>
    Task<IEnumerable<OrientationAssessmentResponse>> GetByEnrollmentIdAsync(Guid employeeOrientationId);

    /// <summary>Returns the responses an employee gave to a specific question within an enrollment.</summary>
    Task<IEnumerable<OrientationAssessmentResponse>> GetByEnrollmentAndQuestionAsync(Guid employeeOrientationId, Guid questionId);
}

#endregion

// ============================================================================
// SECTION 5 — COMPLETION ARTIFACTS
// ============================================================================

#region Orientation Acknowledgement

public interface IOrientationAcknowledgementRepository : IGenericRepository<OrientationAcknowledgement>
{
    /// <summary>Returns all acknowledgements for an enrollment.</summary>
    Task<IEnumerable<OrientationAcknowledgement>> GetByEnrollmentIdAsync(Guid employeeOrientationId);

    /// <summary>Returns acknowledgements for an enrollment that are still pending or presented (unsigned).</summary>
    Task<IEnumerable<OrientationAcknowledgement>> GetUnsignedByEnrollmentAsync(Guid employeeOrientationId);
}

#endregion

#region Orientation Feedback

public interface IOrientationFeedbackRepository : IGenericRepository<OrientationFeedback>
{
    /// <summary>Returns all feedback entries for an enrollment.</summary>
    Task<IEnumerable<OrientationFeedback>> GetByEnrollmentIdAsync(Guid employeeOrientationId);

    /// <summary>Returns all feedback entries for a program (across its enrollments), for analytics.</summary>
    Task<IEnumerable<OrientationFeedback>> GetByProgramIdAsync(Guid programId);

    /// <summary>Returns the average overall rating for a program (0 if no rated feedback).</summary>
    Task<double> GetAverageOverallRatingForProgramAsync(Guid programId);
}

#endregion

#region Orientation Certificate

public interface IOrientationCertificateRepository : IGenericRepository<OrientationCertificate>
{
    /// <summary>Returns the certificate matching the unique certificate number.</summary>
    Task<OrientationCertificate?> GetByCertificateNumberAsync(string certificateNumber);

    /// <summary>Returns true if a certificate with the given number already exists.</summary>
    Task<bool> CertificateNumberExistsAsync(string certificateNumber);

    /// <summary>Returns all certificates for an enrollment.</summary>
    Task<IEnumerable<OrientationCertificate>> GetByEnrollmentIdAsync(Guid employeeOrientationId);

    /// <summary>Returns all certificates held by an employee (across enrollments), newest first.</summary>
    Task<IEnumerable<OrientationCertificate>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns active certificates expiring within the specified number of days.</summary>
    Task<IEnumerable<OrientationCertificate>> GetExpiringAsync(int daysAhead = 30);
}

#endregion

#region Orientation Notification

public interface IOrientationNotificationRepository : IGenericRepository<OrientationNotification>
{
    /// <summary>Returns notifications for a recipient, newest first; optionally only unread.</summary>
    Task<IEnumerable<OrientationNotification>> GetByRecipientAsync(Guid recipientEmployeeId, bool unreadOnly = false);

    /// <summary>Returns the count of unread notifications for a recipient.</summary>
    Task<int> GetUnreadCountAsync(Guid recipientEmployeeId);

    /// <summary>Returns all notifications related to an enrollment.</summary>
    Task<IEnumerable<OrientationNotification>> GetByEnrollmentIdAsync(Guid employeeOrientationId);
}

#endregion
