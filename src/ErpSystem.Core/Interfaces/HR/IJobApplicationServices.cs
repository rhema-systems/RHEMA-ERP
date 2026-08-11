using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

public interface IJobApplicationService
{
    // Queries
    Task<JobApplicationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JobApplicationDto?> GetByApplicationNumberAsync(string applicationNumber, CancellationToken cancellationToken = default);
    Task<JobApplicationDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobApplicationSummaryDto>> GetAllAsync(Guid? vacancyId = null, CancellationToken cancellationToken = default);
    Task<PagedResult<JobApplicationSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? vacancyId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobApplicationSummaryDto>> GetByVacancyIdAsync(Guid vacancyId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobApplicationSummaryDto>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobApplicationSummaryDto>> GetByStatusAsync(ApplicationStatus status, Guid? vacancyId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobApplicationSummaryDto>> GetShortlistedAsync(Guid? vacancyId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobApplicationSummaryDto>> GetByCurrentStageAsync(Guid pipelineStageId, CancellationToken cancellationToken = default);

    // CRUD
    Task<JobApplicationDto> CreateAsync(CreateJobApplicationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    Task<bool> ShortlistAsync(ShortlistApplicationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> UnshortlistAsync(UnshortlistApplicationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> WaitlistAsync(WaitlistApplicationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RejectAsync(RejectApplicationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> WithdrawAsync(WithdrawApplicationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> MoveToStageAsync(MoveApplicationToStageDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Bulk shortlisting operations.
    // Both take the vacancy explicitly and refuse ids that belong to a different one: the routes are
    // vacancy-scoped (`vacancy/{vacancyId}/bulk-shortlist`) but the ids arrive in the body, so without the
    // check a request nominally about one vacancy could shortlist or reject another vacancy's applicants.
    Task<RecruitmentBulkOperationResultDto> BulkShortlistAsync(Guid vacancyId, BulkShortlistDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<RecruitmentBulkOperationResultDto> BulkRejectAsync(Guid vacancyId, BulkRejectDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<RecruitmentBulkOperationResultDto> AutoShortlistByScoreAsync(AutoShortlistByScoreDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<RecruitmentBulkOperationResultDto> SendShortlistNotificationsAsync(Guid vacancyId, CancellationToken cancellationToken = default);
    Task<RecruitmentBulkOperationResultDto> SendRejectionNotificationsAsync(Guid vacancyId, CancellationToken cancellationToken = default);

    // Shortlist dashboard
    Task<ShortlistSummaryDto> GetShortlistSummaryAsync(Guid vacancyId, CancellationToken cancellationToken = default);
    Task<CandidateComparisonDto> GetCandidateComparisonAsync(Guid vacancyId, IEnumerable<Guid> applicationIds, CancellationToken cancellationToken = default);

    // Shortlist approval workflow
    Task<bool> SubmitShortlistForApprovalAsync(SubmitShortlistForApprovalDto dto, Guid submittedByUserId, CancellationToken cancellationToken = default);
    Task<bool> ReviewShortlistApprovalAsync(ReviewShortlistApprovalDto dto, Guid reviewedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RecallShortlistApprovalAsync(Guid vacancyId, CancellationToken cancellationToken = default);

    // Stage history (read-only)
    Task<IEnumerable<JobApplicationStageHistoryDto>> GetStageHistoryAsync(Guid applicationId, CancellationToken cancellationToken = default);

    // Test results
    Task<JobApplicantTestResultDto> AddTestResultAsync(CreateJobApplicantTestResultDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobApplicantTestResultDto>> GetTestResultsAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobApplicantTestResultDto>> GetTestResultsByVacancyAsync(Guid vacancyId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobApplicantTestResultDto>> GetTestResultsByTypeAsync(Guid applicationId, JobApplicantTestType testType, CancellationToken cancellationToken = default);
    Task<JobApplicantTestResultDto> UpdateTestResultAsync(UpdateJobApplicantTestResultDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteTestResultAsync(Guid testResultId, CancellationToken cancellationToken = default);

    // Communications
    Task<JobApplicantCommunicationDto> AddCommunicationAsync(CreateJobApplicantCommunicationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobApplicantCommunicationDto>> GetCommunicationsAsync(Guid applicationId, CancellationToken cancellationToken = default);

    // Automatic scoring
    /// <summary>
    /// Evaluates the application against the vacancy's ShortlistingCriteria,
    /// computes a weighted AutoScore, and persists both AutoScore and
    /// AutoScoreBreakdown (as a JSON array of per-criterion results).
    /// Safe to call repeatedly — always overwrites the previous score.
    /// </summary>
    Task<ApplicationAutoScoreDto> EvaluateApplicationScoreAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scores an already-loaded application entity without an additional database fetch.
    /// Used by bulk operations (<see cref="EvaluateAllScoresForVacancyAsync"/> and
    /// <see cref="IAutoScoringService"/>) to avoid N+1 round-trips.
    /// The entity must have <c>JobVacancy.ShortlistingCriteria</c>,
    /// <c>JobCandidate.Skills</c>, <c>JobCandidate.Qualifications</c>,
    /// <c>JobCandidate.Languages</c>, and <c>TestResults</c> already loaded.
    /// </summary>
    Task<ApplicationAutoScoreDto> EvaluateLoadedApplicationScoreAsync(JobApplication application, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-evaluates all active applications for a vacancy.
    /// Useful for re-scoring after criteria changes.
    /// </summary>
    Task<IEnumerable<ApplicationAutoScoreDto>> EvaluateAllScoresForVacancyAsync(Guid vacancyId, CancellationToken cancellationToken = default);

    // ── Enterprise shortlisting features ─────────────────────────────────────

    /// <summary>Returns the immutable decision audit trail for an application.</summary>
    Task<IEnumerable<ShortlistDecisionLogDto>> GetShortlistDecisionLogAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>Adds a panel reviewer score for an application.</summary>
    Task<ShortlistReviewDto> AddShortlistReviewAsync(CreateShortlistReviewDto dto, Guid tenantId, Guid reviewerId, CancellationToken cancellationToken = default);

    /// <summary>Returns the aggregated (averaged) panel review score for an application.</summary>
    Task<AggregatedReviewScoreDto> GetAggregatedReviewScoreAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>Marks a panel review as finalised and recomputes the aggregated score on the application.</summary>
    Task<ShortlistReviewDto> FinalizeShortlistReviewAsync(Guid reviewId, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Returns an EEO/diversity compliance report for a vacancy's applicant pipeline.</summary>
    Task<EeoComplianceReportDto> GetEeoReportAsync(Guid vacancyId, CancellationToken cancellationToken = default);

    /// <summary>Returns the SLA/time-to-shortlist status for a vacancy.</summary>
    Task<ShortlistSlaStatusDto> GetShortlistSlaStatusAsync(Guid vacancyId, CancellationToken cancellationToken = default);

    /// <summary>Returns PII-redacted summaries for blind screening (vacancy must have IsBlindScreeningEnabled=true).</summary>
    Task<IEnumerable<BlindApplicationSummaryDto>> GetBlindApplicationsAsync(Guid vacancyId, CancellationToken cancellationToken = default);

    /// <summary>Marks an application as an internal (existing employee) candidate and records the employee ID.</summary>
    Task<bool> MarkAsInternalCandidateAsync(MarkInternalCandidateDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Removes the internal-candidate flag from an application.</summary>
    Task<bool> UnmarkAsInternalCandidateAsync(Guid applicationId, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Exports shortlisted and waitlisted applications for a vacancy as a UTF-8 CSV byte array.</summary>
    Task<byte[]> GetShortlistCsvExportAsync(Guid vacancyId, CancellationToken cancellationToken = default);

    // ── Internal self-service application ────────────────────────────────────

    /// <summary>
    /// Allows an existing employee to apply for an internal vacancy via the Internal Job Board.
    /// Resolves or creates a shadow JobCandidate from the employee's profile, then creates a
    /// JobApplication with IsInternalCandidate=true, InternalEmployeeId set, and
    /// Source=ApplicationSource.InternalPortal.
    /// </summary>
    Task<JobApplicationDto> InternalApplyAsync(
        InternalApplyForVacancyDto dto,
        Guid employeeId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates or updates an internal draft application (status = Draft).</summary>
    Task<JobApplicationDto> InternalSaveDraftAsync(
        InternalSaveDraftDto dto,
        Guid employeeId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Transitions an internal draft application to Submitted.</summary>
    Task<JobApplicationDto> InternalSubmitDraftAsync(
        Guid applicationId,
        InternalSubmitDraftDto dto,
        Guid employeeId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all applications submitted by the given employee (via InternalEmployeeId).
    /// Used on the Internal Job Board to show "Already Applied" status.
    /// </summary>
    Task<IEnumerable<JobApplicationSummaryDto>> GetByInternalEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    // ── External self-service portal ─────────────────────────────────────────

    /// <summary>
    /// Issues a single-use ticket for a CV that has already passed the controlled-upload gate,
    /// so a later application can claim it.
    /// </summary>
    /// <remarks>
    /// The returned token is the only copy — the ticket row stores just its hash. The upload is
    /// bound to <paramref name="tenantId"/> and <paramref name="vacancyId"/>, and an unclaimed
    /// ticket is swept once it expires.
    /// </remarks>
    Task<PublicCvUploadTicketDto> MintCvUploadTicketAsync(
        Guid tenantId,
        Guid vacancyId,
        Guid fileUploadRecordId,
        string originalFileName,
        string? contentType,
        long fileSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an external candidate application from the public career portal.
    /// Resolves or creates a JobCandidate record keyed by email, creates a JobApplication
    /// with Source set per the DTO, and generates a unique ExternalTrackingToken
    /// returned in the confirmation payload.
    /// </summary>
    Task<ExternalApplicationConfirmationDto> ExternalApplyAsync(
        ExternalApplicationDto dto,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a public-safe status DTO for an external application identified by its tracking token.
    /// Throws KeyNotFoundException when the token does not match any application.
    /// </summary>
    Task<PublicApplicationStatusDto> GetApplicationStatusByTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Allows an external candidate to withdraw their own application using only their tracking token.
    /// Validates the token and sets Status = Withdrawn.
    /// </summary>
    Task<bool> WithdrawByTokenAsync(
        string token,
        string reason,
        CancellationToken cancellationToken = default);
}

// ============================================================================
// APPLICATION PIPELINE SERVICE
// ============================================================================

/// <summary>
/// Manages movement of applications between recruitment pipeline stages (Kanban) and
/// provides the aggregated pipeline view used by the board UI.
/// </summary>
public interface IApplicationPipelineService
{
    /// <summary>
    /// Moves an application to the specified pipeline stage, enforcing transition rules
    /// (stage order, CanSkip, CanRepeat, MaxAttempts), updating the application status,
    /// closing the previous stage history record, and adjusting vacancy counters.
    /// The entire operation runs inside a database transaction.
    /// </summary>
    Task MoveApplicationToStageAsync(
        Guid applicationId,
        Guid targetStageId,
        Guid movedByEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Kanban board for a vacancy: ordered pipeline stages, each populated
    /// with the applications currently sitting in that stage.
    /// </summary>
    Task<List<PipelineStageWithApplicationsDto>> GetPipelineByVacancyAsync(
        Guid vacancyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a set of applications to the same target pipeline stage.
    /// Each application is moved individually so all transition rules are enforced.
    /// Failures for individual applications are captured in the result; the remaining
    /// applications are still processed.
    /// </summary>
    Task<RecruitmentBulkOperationResultDto> BulkMoveToStageAsync(
        RecruitmentBulkMoveToStageDto dto,
        Guid movedByEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a set of applications from the pipeline in one operation.
    /// Sets <c>Status = Rejected</c> on each application and closes the current
    /// stage history record. Failures for individual applications are captured in
    /// the result without aborting the remaining items.
    /// </summary>
    Task<RecruitmentBulkOperationResultDto> BulkPipelineRejectAsync(
        RecruitmentBulkPipelineRejectDto dto,
        Guid rejectedByEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called immediately after a new application is saved. If the vacancy has a
    /// recruitment pipeline assigned, places the application into the first ordered
    /// stage of that pipeline and updates <c>ApplicationStatus</c> accordingly.
    /// If the vacancy has no pipeline, the call is a no-op so existing behaviour
    /// is preserved. Failures are non-fatal — a warning is logged and the
    /// application remains visible without a stage history row.
    /// </summary>
    Task PlaceInFirstPipelineStageAsync(
        Guid applicationId,
        Guid vacancyId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Automatically advances an application to the pipeline stage whose
    /// <see cref="RecruitmentPipelineStageType"/> matches <paramref name="stageType"/>.
    /// If the vacancy's pipeline does not contain an active stage of that type, the
    /// call is a no-op — safe to call from any domain service without a pipeline check.
    /// Failures are logged as warnings and never surface to the caller.
    /// </summary>
    Task AutoAdvanceToStageTypeAsync(
        Guid applicationId,
        RecruitmentPipelineStageType stageType,
        Guid movedByEmployeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the current open stage history record for an application without
    /// opening a new one. Use for terminal transitions (Rejected, Withdrawn) where
    /// the application leaves the pipeline entirely.
    /// If there is no open record, the call is a no-op.
    /// Failures are logged as warnings and never surface to the caller.
    /// </summary>
    Task CloseCurrentStageForExitAsync(
        Guid applicationId,
        JobApplicationStageExitReason exitReason,
        Guid closedByEmployeeId,
        CancellationToken cancellationToken = default);
}

// ============================================================================
// PIPELINE QUERY SERVICE
// ============================================================================

/// <summary>
/// Read-only query service for the production pipeline list+bulk view.
/// Separated from <see cref="IApplicationPipelineService"/> so that high-volume
/// read queries don't share a transaction scope with stage-move writes.
/// </summary>
public interface IPipelineQueryService
{
    /// <summary>
    /// Returns the pipeline overview for a vacancy: each stage with only its
    /// application count. Includes a synthetic inbox bucket (StageId = Guid.Empty)
    /// for applications not yet placed in any stage.
    /// </summary>
    Task<PipelineOverviewDto> GetPipelineOverviewAsync(
        Guid vacancyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated, filterable, sortable list of applications in a specific
    /// pipeline stage.
    /// Pass <c>stageId = Guid.Empty</c> to query the inbox (applications with no
    /// current stage history entry).
    /// </summary>
    Task<PagedResult<PipelineApplicationListItemDto>> GetStageApplicationsAsync(
        Guid vacancyId,
        Guid stageId,
        StageApplicationsQuery query,
        CancellationToken cancellationToken = default);
}

// ============================================================================
// AUTO-SCORING SERVICE
// ============================================================================

/// <summary>
/// Dedicated scoring service — evaluates <see cref="JobApplication"/> instances
/// against the vacancy's <see cref="JobShortlistingCriteria"/>, persists
/// <c>AutoScore</c> + <c>AutoScoreBreakdown</c>, and exposes a batch runner for
/// the pipeline "Run Scoring" action.
/// <para>
/// Scoring reads and writes are kept here rather than on
/// <see cref="IJobApplicationService"/> so that callers (pipeline controller,
/// submission hooks, background jobs) have a single, purpose-specific injection
/// point that is easy to mock in tests.
/// </para>
/// </summary>
public interface IAutoScoringService
{
    /// <summary>
    /// Scores a single application against its vacancy's criteria.
    /// Safe to call repeatedly — always overwrites the previous score.
    /// </summary>
    Task<ApplicationAutoScoreDto> ScoreApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scores all active (non-withdrawn, non-rejected) applications for a vacancy
    /// in one operation.  Per-application failures are captured in
    /// <see cref="RecruitmentScoringRunResultDto.Errors"/> without aborting the
    /// remaining items.
    /// </summary>
    Task<RecruitmentScoringRunResultDto> RunScoringForVacancyAsync(
        Guid vacancyId,
        CancellationToken cancellationToken = default);
}
