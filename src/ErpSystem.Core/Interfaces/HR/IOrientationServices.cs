using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// ORIENTATION MODULE SERVICES
// Services are grouped by aggregate / use-case rather than one per entity:
//   - IOrientationCategoryService   : category catalog
//   - IOrientationProgramService    : program authoring (program + modules + content
//                                     + prerequisites + audience rules + assessment bank)
//   - IOrientationSessionService    : sessions + facilitators + attendance
//   - IEmployeeOrientationService   : enrollment runtime (progress, assessment,
//                                     acknowledgements, feedback, certificates)
//   - IOrientationNotificationService : lifecycle notifications
// ============================================================================

#region Orientation Category Service

public interface IOrientationCategoryService
{
    Task<OrientationCategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationCategoryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationCategoryDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationCategoryDto>> GetRootCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationCategoryDto>> GetByParentAsync(Guid? parentCategoryId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationCategoryLookupDto>> GetLookupAsync(CancellationToken cancellationToken = default);

    Task<OrientationCategoryDto> CreateAsync(CreateOrientationCategoryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<OrientationCategoryDto> UpdateAsync(UpdateOrientationCategoryDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

#region Orientation Program Service

public interface IOrientationProgramService
{
    // Program queries
    Task<OrientationProgramDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrientationProgramDto?> GetByProgramCodeAsync(string programCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationProgramSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<OrientationProgramSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationProgramSummaryDto>> GetByStatusAsync(OrientationProgramStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationProgramSummaryDto>> GetByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationProgramSummaryDto>> GetByTypeAsync(OrientationProgramType programType, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationProgramSummaryDto>> GetActiveProgramsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationProgramSummaryDto>> GetByOwnerOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    // Program CRUD + lifecycle
    Task<OrientationProgramDto> CreateAsync(CreateOrientationProgramDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>Round 4, lane J2: copy a programme and everything it is made of, as a Draft.</summary>
    Task<OrientationProgramDto> CloneAsync(Guid sourceId, CloneOrientationProgramDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<OrientationProgramDto> UpdateAsync(UpdateOrientationProgramDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> ChangeStatusAsync(ChangeOrientationProgramStatusDto changeDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Modules
    Task<OrientationModuleDto> AddModuleAsync(CreateOrientationModuleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationModuleDto>> GetModulesAsync(Guid programId, CancellationToken cancellationToken = default);
    Task<OrientationModuleDto> UpdateModuleAsync(UpdateOrientationModuleDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteModuleAsync(Guid moduleId, CancellationToken cancellationToken = default);

    // Content items
    Task<OrientationContentItemDto> AddContentItemAsync(CreateOrientationContentItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationContentItemDto>> GetContentItemsAsync(Guid moduleId, CancellationToken cancellationToken = default);
    Task<OrientationContentItemDto> UpdateContentItemAsync(UpdateOrientationContentItemDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteContentItemAsync(Guid contentItemId, CancellationToken cancellationToken = default);

    // Prerequisites
    Task<OrientationPrerequisiteDto> AddPrerequisiteAsync(CreateOrientationPrerequisiteDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationPrerequisiteDto>> GetPrerequisitesAsync(Guid programId, CancellationToken cancellationToken = default);
    Task<bool> DeletePrerequisiteAsync(Guid prerequisiteId, CancellationToken cancellationToken = default);

    // Audience rules
    Task<OrientationAudienceRuleDto> AddAudienceRuleAsync(CreateOrientationAudienceRuleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationAudienceRuleDto>> GetAudienceRulesAsync(Guid programId, CancellationToken cancellationToken = default);
    Task<OrientationAudienceRuleDto> UpdateAudienceRuleAsync(UpdateOrientationAudienceRuleDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAudienceRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);

    // Assessment questions (options created/replaced as part of the question)
    Task<OrientationAssessmentQuestionDto> AddQuestionAsync(CreateOrientationAssessmentQuestionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationAssessmentQuestionDto>> GetQuestionsAsync(Guid programId, CancellationToken cancellationToken = default);
    Task<OrientationAssessmentQuestionDto> UpdateQuestionAsync(UpdateOrientationAssessmentQuestionDto updateDto, Guid tenantId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteQuestionAsync(Guid questionId, CancellationToken cancellationToken = default);
}

#endregion

#region Orientation Session Service

public interface IOrientationSessionService
{
    // Queries
    Task<OrientationSessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrientationSessionDto?> GetBySessionCodeAsync(string sessionCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationSessionSummaryDto>> GetByProgramIdAsync(Guid programId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationSessionSummaryDto>> GetByStatusAsync(OrientationSessionStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationSessionSummaryDto>> GetUpcomingAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationSessionSummaryDto>> GetOpenForEnrollmentAsync(CancellationToken cancellationToken = default);

    // CRUD + lifecycle
    Task<OrientationSessionDto> CreateAsync(CreateOrientationSessionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>Round 4, lane J3: run a session again on a new date.</summary>
    Task<OrientationSessionDto> CloneAsync(Guid sourceId, CloneOrientationSessionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<OrientationSessionDto> UpdateAsync(UpdateOrientationSessionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> ChangeStatusAsync(ChangeOrientationSessionStatusDto changeDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Facilitators
    Task<OrientationSessionFacilitatorDto> AddFacilitatorAsync(CreateOrientationSessionFacilitatorDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationSessionFacilitatorDto>> GetFacilitatorsAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<OrientationSessionFacilitatorDto> UpdateFacilitatorAsync(UpdateOrientationSessionFacilitatorDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveFacilitatorAsync(Guid facilitatorId, CancellationToken cancellationToken = default);

    // Attendance
    Task<IEnumerable<OrientationAttendanceRecordDto>> MarkAttendanceAsync(MarkOrientationAttendanceDto markDto, Guid markedByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationAttendanceRecordDto>> GetAttendanceForSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationAttendanceRecordDto>> GetAttendanceForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
}

#endregion

#region Employee Orientation Service

public interface IEmployeeOrientationService
{
    // Queries
    Task<EmployeeOrientationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeOrientationSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeOrientationSummaryDto>> GetByProgramIdAsync(Guid programId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeOrientationSummaryDto>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeOrientationSummaryDto>> GetByCompletionStatusAsync(OrientationCompletionStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeOrientationSummaryDto>> GetOverdueAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeOrientationSummaryDto>> GetDueSoonAsync(int daysAhead = 7, CancellationToken cancellationToken = default);
    Task<PagedResult<EmployeeOrientationSummaryDto>> GetPagedByProgramAsync(Guid programId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    // Enrollment
    Task<EmployeeOrientationDto> EnrollAsync(CreateEmployeeOrientationDto createDto, Guid tenantId, Guid enrolledByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeOrientationDto>> BulkEnrollAsync(BulkEnrollOrientationDto bulkDto, Guid tenantId, Guid enrolledByUserId, CancellationToken cancellationToken = default);
    Task<EmployeeOrientationDto> UpdateAsync(UpdateEmployeeOrientationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> WithdrawAsync(WithdrawOrientationDto withdrawDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Content progress
    Task<OrientationContentProgressDto> TrackContentProgressAsync(TrackOrientationContentProgressDto trackDto, Guid tenantId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationContentProgressDto>> GetContentProgressAsync(Guid enrollmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The live module and content-item structure of the program behind an enrollment, for the person
    /// working through it.
    ///
    /// The catalogue reads on <c>IOrientationProgramService</c> are HR-only, and content progress rows
    /// only exist once an item has been tracked — so without this a participant had no way to discover
    /// what they were meant to work through, nor the content item ids that
    /// <see cref="TrackContentProgressAsync"/> requires.
    /// </summary>
    Task<IEnumerable<OrientationModuleDto>> GetProgramContentAsync(Guid enrollmentId, CancellationToken cancellationToken = default);

    // Assessment
    Task<IEnumerable<OrientationAssessmentQuestionDto>> GetAssessmentForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
    Task<OrientationAssessmentResultDto> SubmitAssessmentAsync(SubmitOrientationAssessmentDto submitDto, Guid tenantId, Guid submittedByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationAssessmentResponseDto>> GetAssessmentResponsesAsync(Guid enrollmentId, CancellationToken cancellationToken = default);

    // Acknowledgements
    Task<OrientationAcknowledgementDto> AddAcknowledgementAsync(CreateOrientationAcknowledgementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationAcknowledgementDto>> GetAcknowledgementsAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
    Task<OrientationAcknowledgementDto> SignAcknowledgementAsync(SignOrientationAcknowledgementDto signDto, string? ipAddress, Guid signedByUserId, CancellationToken cancellationToken = default);

    // Feedback
    Task<OrientationFeedbackDto> SubmitFeedbackAsync(CreateOrientationFeedbackDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationFeedbackDto>> GetFeedbackAsync(Guid enrollmentId, CancellationToken cancellationToken = default);

    /// <summary>The caller's own orientation feedback, across every enrollment they have had.</summary>
    /// <remarks>Lane 6: the read the portal form needed before it could exist. Without it a
    /// submission was write-only, and a second press of the button filed a second row.</remarks>
    Task<IEnumerable<OrientationFeedbackDto>> GetMyFeedbackAsync(CancellationToken cancellationToken = default);

    // Certificates
    Task<OrientationCertificateDto> IssueCertificateAsync(IssueOrientationCertificateDto issueDto, Guid tenantId, Guid issuedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RevokeCertificateAsync(RevokeOrientationCertificateDto revokeDto, Guid revokedByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationCertificateDto>> GetCertificatesForEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationCertificateDto>> GetCertificatesForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<OrientationCertificateDto?> GetCertificateByNumberAsync(string certificateNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationCertificateDto>> GetExpiringCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
}

#endregion

#region Orientation Dashboard Service

public interface IOrientationDashboardService
{
    Task<OrientationDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}

#endregion

#region Orientation Notification Service

public interface IOrientationNotificationService
{
    Task<IEnumerable<OrientationNotificationDto>> GetByRecipientAsync(Guid recipientEmployeeId, bool unreadOnly = false, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid recipientEmployeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrientationNotificationDto>> GetByEnrollmentIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
    Task<OrientationNotificationDto> CreateAsync(CreateOrientationNotificationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Marks a notification read on behalf of its recipient. Refuses an id belonging to anyone else —
    /// read state is per-recipient, so this is not an operation one user performs on another's inbox.
    /// </summary>
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid recipientEmployeeId, CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(Guid recipientEmployeeId, CancellationToken cancellationToken = default);
}

#endregion
