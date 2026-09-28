using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SECTION 1 — JOB VACANCY
// ============================================================================

#region Job Vacancy

public class JobVacancyDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string VacancyNumber { get; set; } = string.Empty;
    public string? CustomAdvertTitle { get; set; }
    public string JobTitle { get; set; } = string.Empty;

    public Guid StaffRequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string? OrgUnitName { get; set; }

    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public int NumberOfPositions { get; set; }

    public Guid? HiringManagerId { get; set; }
    public string? HiringManagerName { get; set; }
    public Guid? RecruiterId { get; set; }
    public string? RecruiterName { get; set; }

    public JobVacancyStatus VacancyStatus { get; set; }
    public string VacancyStatusName => VacancyStatus.ToString();
    public DateTime? PublishDate { get; set; }
    public DateTime? ActualPublishDate { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public DateTime? ShortlistingDeadline { get; set; }
    public int? NumberOfInterviewRounds { get; set; }
    public DateTime? ClosedDate { get; set; }
    public DateTime? FilledDate { get; set; }

    public JobVacancyClosureReason? ClosureReason { get; set; }
    public string? ClosureReasonName => ClosureReason?.ToString();
    public string? ClosureNotes { get; set; }

    public bool IsSalaryVisible { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public string EmploymentTypeName => EmploymentType.ToString();
    public WorkMode WorkMode { get; set; }
    public string WorkModeName => WorkMode.ToString();
    public decimal? SalaryRangeMin { get; set; }
    public decimal? SalaryRangeMax { get; set; }
    public string? SalaryCurrencyCode { get; set; }
    public int? RequiredMinExperienceYears { get; set; }
    public string? KeyBenefitsSummary { get; set; }
    public DateOnly? TargetStartDate { get; set; }

    public bool RequiresWrittenTest { get; set; }
    public bool RequiresPracticalTest { get; set; }

    public Guid? RecruitmentPipelineId { get; set; }
    public string? PipelineName { get; set; }

    public bool AllowInternalCandidates { get; set; }
    public bool AllowExternalCandidates { get; set; }

    public int ApplicationCount { get; set; }
    public int ShortlistedCount { get; set; }
    public int InterviewCount { get; set; }
    public int OfferCount { get; set; }
    public int HireCount { get; set; }

    // SLA / test / blind screening
    public DateTime? ShortlistCompletedAt { get; set; }
    public int? TimeToShortlistDays { get; set; }
    public bool ShortlistingSlaBreached { get; set; }
    public int TestScoreWeight { get; set; }
    public int InternalCandidateBoostPoints { get; set; }
    public bool IsBlindScreeningEnabled { get; set; }

    // Auto-shortlisting configuration
    public decimal? AutoShortlistMinScore { get; set; }
    public bool AutoShortlistRequireAllMandatory { get; set; }

    /// <summary>
    /// Derived (round 3, lane K; decision D-7): the vacancy carries a Gender or Age criterion. Such a
    /// criterion may inform a score, never disqualify — the screen says so wherever this is true.
    /// </summary>
    public bool UsesProtectedCharacteristicCriterion { get; set; }

    /// <summary>
    /// The statuses this vacancy may legally move to from where it is now.
    /// </summary>
    /// <remarks>
    /// <para>Derived from the service's own <c>AllowedTransitions</c> map, so the screen and the
    /// server cannot disagree (G-5.7, 2026-09-15). The "Advance to…" picker previously held a
    /// hardcoded list of the five hiring stages and offered all of them minus the current one from
    /// any non-terminal status — so a Draft vacancy was offered <i>Filled</i> and a Published one
    /// <i>Interviewing</i>, both refused on arrival.</para>
    ///
    /// <para>Sending the map rather than mirroring it in TypeScript is the point: a mirrored
    /// constant is a second source of truth that drifts silently the first time the real map
    /// changes, and this one just did change — <c>OnHold</c> became reachable (G-5.3).</para>
    /// </remarks>
    public List<JobVacancyStatus> AllowedNextStatuses { get; set; } = new();

    public Guid? WorkflowInstanceId { get; set; }

    // Shortlist approval
    public ShortlistApprovalStatus ShortlistApprovalStatus  { get; set; }
    public string ShortlistApprovalStatusName => ShortlistApprovalStatus.ToString();
    public DateTime? ShortlistSubmittedAt     { get; set; }
    public string?   ShortlistSubmittedByName { get; set; }
    public DateTime? ShortlistApprovedAt      { get; set; }
    public string?   ShortlistApprovedByName  { get; set; }
    public string?   ShortlistApprovalNotes   { get; set; }
}

public class JobVacancySummaryDto
{
    public Guid Id { get; set; }
    public string VacancyNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public Guid StaffRequisitionId { get; set; }
    public string? RequisitionNumber { get; set; }
    public string? OrgUnitName { get; set; }
    public JobVacancyStatus VacancyStatus { get; set; }
    public string VacancyStatusName => VacancyStatus.ToString();
    public EmploymentType EmploymentType { get; set; }
    public string EmploymentTypeName => EmploymentType.ToString();
    public DateTime? PublishDate { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public DateTime? ShortlistingDeadline { get; set; }
    public int NumberOfPositions { get; set; }
    public string? HiringManagerName { get; set; }
    public string? RecruiterName { get; set; }
    public bool AllowInternalCandidates { get; set; }
    public bool AllowExternalCandidates { get; set; }
    public int ApplicationCount { get; set; }
    public int ShortlistedCount { get; set; }

    /// <summary>
    /// How many of this vacancy's candidates have reached the interview stage.
    /// </summary>
    /// <remarks>
    /// Added 2026-09-15 for G-15.1. The entity has always maintained it — the same stage movement
    /// that maintains the three counts around it — but the summary DTO omitted it, so the
    /// dashboard's Pipeline card had no people-count for the middle of its funnel and used a count
    /// of interview <i>sessions</i> instead, on a shared scale beside three counts of people.
    /// </remarks>
    public int InterviewCount { get; set; }

    public int OfferCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class JobVacancyDetailDto : JobVacancyDto
{
    public List<JobVacancyAttachmentDto> Attachments { get; set; } = new();
    public List<JobPostingSummaryDto> JobPostings { get; set; } = new();
    public List<JobShortlistingCriteriaDto> ShortlistingCriteria { get; set; } = new();
    public List<JobApplicationSummaryDto> Applications { get; set; } = new();
    public List<JobInterviewSummaryDto> Interviews { get; set; } = new();
    public List<JobVacancyStatusHistoryDto> StatusHistory { get; set; } = new();
}

public class CreateJobVacancyDto : CreateDtoBase
{
    [Required]
    public Guid StaffRequisitionId { get; set; }

    [MaxLength(200)]
    public string? CustomAdvertTitle { get; set; }

    public int NumberOfPositions { get; set; } = 1;
    public Guid? HiringManagerId { get; set; }
    public Guid? RecruiterId { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public DateTime? ShortlistingDeadline { get; set; }
    public int? NumberOfInterviewRounds { get; set; }
    public DateOnly? TargetStartDate { get; set; }
    public bool IsSalaryVisible { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;
    public WorkMode WorkMode { get; set; } = WorkMode.OnSite;
    public decimal? SalaryRangeMin { get; set; }
    public decimal? SalaryRangeMax { get; set; }

    [MaxLength(10)]
    public string? SalaryCurrencyCode { get; set; }

    public int? RequiredMinExperienceYears { get; set; }

    [MaxLength(2000)]
    public string? KeyBenefitsSummary { get; set; }

    public bool RequiresWrittenTest { get; set; }
    public bool RequiresPracticalTest { get; set; }

    /// <summary>
    /// Whether reviewers screen this vacancy's applications with the candidate's name, gender, age,
    /// location and contact details withheld.
    ///
    /// <para>Present on <see cref="UpdateJobVacancyDto"/> and <see cref="TransitionJobVacancyDto"/>
    /// but missing here, which made the whole blind-screening feature unreachable: it could not be
    /// set when the vacancy was opened, and no edit form sent it either, so
    /// <c>GET api/job-applications/vacancy/{id}/blind-applications</c> answered 422 for every
    /// vacancy that had ever existed.</para>
    /// </summary>
    public bool IsBlindScreeningEnabled { get; set; }

    /// <summary>
    /// Weight (0–100) given to test scores in the composite shortlist score. 0 ignores them.
    /// </summary>
    /// <remarks>
    /// <para>⚠ G-5.2 (2026-09-15). This and <see cref="InternalCandidateBoostPoints"/> appeared on
    /// <c>JobVacancyDto</c> — the <b>read</b> DTO — and nowhere else. Neither the create DTO nor
    /// the update DTO carried them, and no frontend file mentioned either name, so both defaulted
    /// to 0 and could not be changed by any means. Since the scoring algorithm's step 5 requires
    /// <c>TestScoreWeight &gt; 0</c> and step 6 requires <c>InternalCandidateBoostPoints &gt; 0</c>,
    /// two documented, implemented branches of it could never execute.</para>
    ///
    /// <para>The visible consequence: the form's *Requires a written test* and *Requires a
    /// practical test* checkboxes recorded a requirement whose results could never affect a score,
    /// and <c>JobApplicantTestResult</c> rows were captured and never weighed.</para>
    /// </remarks>
    [Range(0, 100)]
    public int TestScoreWeight { get; set; }

    /// <summary>
    /// Flat point bonus (0–20) added to an internal candidate's composite score. 0 disables it.
    /// </summary>
    /// <remarks>See <see cref="TestScoreWeight"/> — same gap, same fix.</remarks>
    [Range(0, 20)]
    public int InternalCandidateBoostPoints { get; set; }

    public Guid? RecruitmentPipelineId { get; set; }
    public decimal? AutoShortlistMinScore { get; set; }
    public bool AutoShortlistRequireAllMandatory { get; set; } = true;
}

public class UpdateJobVacancyDto : UpdateDtoBase
{
    [MaxLength(200)]
    public string? CustomAdvertTitle { get; set; }

    public int NumberOfPositions { get; set; } = 1;
    public Guid? HiringManagerId { get; set; }
    public Guid? RecruiterId { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public DateTime? ShortlistingDeadline { get; set; }
    public int? NumberOfInterviewRounds { get; set; }
    public DateOnly? TargetStartDate { get; set; }
    public bool IsSalaryVisible { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public WorkMode WorkMode { get; set; }
    public decimal? SalaryRangeMin { get; set; }
    public decimal? SalaryRangeMax { get; set; }

    [MaxLength(10)]
    public string? SalaryCurrencyCode { get; set; }

    public int? RequiredMinExperienceYears { get; set; }

    [MaxLength(2000)]
    public string? KeyBenefitsSummary { get; set; }

    public bool RequiresWrittenTest { get; set; }
    public bool RequiresPracticalTest { get; set; }
    public bool IsBlindScreeningEnabled { get; set; }

    /// <inheritdoc cref="CreateJobVacancyDto.TestScoreWeight"/>
    [Range(0, 100)]
    public int TestScoreWeight { get; set; }

    /// <inheritdoc cref="CreateJobVacancyDto.InternalCandidateBoostPoints"/>
    [Range(0, 20)]
    public int InternalCandidateBoostPoints { get; set; }

    public DateTime? PublishDate { get; set; }
    public Guid? RecruitmentPipelineId { get; set; }
    public decimal? AutoShortlistMinScore { get; set; }
    public bool AutoShortlistRequireAllMandatory { get; set; }
}

/// <summary>
/// Atomically applies field updates AND a status transition in a single DB transaction.
/// Used by the edit form's transition buttons (Submit for Approval, Approve, Publish, etc.)
/// so that form state and the status change are committed together — no partial-failure risk.
/// </summary>
public class TransitionJobVacancyDto
{
    [Required]
    public Guid Id { get; set; }

    // ── Field updates (mirrors UpdateJobVacancyDto) ────────────────────────
    [MaxLength(200)]
    public string? CustomAdvertTitle { get; set; }
    public int NumberOfPositions { get; set; } = 1;
    public Guid? HiringManagerId { get; set; }
    public Guid? RecruiterId { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public DateTime? ShortlistingDeadline { get; set; }
    public int? NumberOfInterviewRounds { get; set; }
    public DateOnly? TargetStartDate { get; set; }
    public bool IsSalaryVisible { get; set; }
    /// <summary>The employment type, or null to leave it as it is.</summary>
    /// <remarks>
    /// ⚠ <b>Nullable, and that is the fix.</b> These two were non-nullable with no initialiser,
    /// unlike <see cref="CreateJobVacancyDto"/> which defaults them. A transition payload that did
    /// not mention them therefore bound to <c>default</c> = <b>0</b> — a value outside BOTH enums,
    /// since <c>EmploymentType.Permanent</c> is 1 and <c>WorkMode.OnSite</c> is 1 — and the mapper
    /// wrote it. Publishing a vacancy through the API without restating every field silently erased
    /// its employment type and work mode, and <c>JobOfferService.CreateAsync</c> then copied the
    /// garbage onto every offer raised from it. The UI never hit this because it posts a full
    /// update payload; a harness sending only what it meant to change did.
    /// </remarks>
    public EmploymentType? EmploymentType { get; set; }

    /// <inheritdoc cref="EmploymentType"/>
    public WorkMode? WorkMode { get; set; }
    public decimal? SalaryRangeMin { get; set; }
    public decimal? SalaryRangeMax { get; set; }
    [MaxLength(10)]
    public string? SalaryCurrencyCode { get; set; }
    public int? RequiredMinExperienceYears { get; set; }
    [MaxLength(2000)]
    public string? KeyBenefitsSummary { get; set; }
    public bool RequiresWrittenTest { get; set; }
    public bool RequiresPracticalTest { get; set; }
    public bool IsBlindScreeningEnabled { get; set; }

    /// <inheritdoc cref="CreateJobVacancyDto.TestScoreWeight"/>
    [Range(0, 100)]
    public int TestScoreWeight { get; set; }

    /// <inheritdoc cref="CreateJobVacancyDto.InternalCandidateBoostPoints"/>
    [Range(0, 20)]
    public int InternalCandidateBoostPoints { get; set; }

    public DateTime? PublishDate { get; set; }
    public Guid? RecruitmentPipelineId { get; set; }
    public decimal? AutoShortlistMinScore { get; set; }
    public bool AutoShortlistRequireAllMandatory { get; set; }

    // ── Status transition ──────────────────────────────────────────────────
    [Required]
    public JobVacancyStatus NewStatus { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

public class CloseJobVacancyDto
{
    [Required]
    public Guid VacancyId { get; set; }

    [Required]
    public JobVacancyClosureReason ClosureReason { get; set; }

    [MaxLength(2000)]
    public string? ClosureNotes { get; set; }
}

public class CloseForApplicationsDto
{
    [Required]
    public Guid VacancyId { get; set; }

    [Required]
    public JobVacancyClosureReason Reason { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class ChangeJobVacancyStatusDto
{
    [Required]
    public Guid VacancyId { get; set; }

    [Required]
    public JobVacancyStatus NewStatus { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

#endregion

// ============================================================================
// SECTION 1b — JOB VACANCY ATTACHMENT
// ============================================================================

#region Job Vacancy Attachment

public class JobVacancyAttachmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobVacancyId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

// CreateJobVacancyAttachmentDto was deleted deliberately, not left unused. It carried a
// caller-supplied FilePath, so the endpoint recorded a path to a file it had never received or
// scanned. Attachments now arrive as multipart through the controlled-upload gate and the row is
// written from the stored document's own metadata — see IJobVacancyService.AddAttachmentAsync.

#endregion

// ============================================================================
// SECTION 1c — JOB VACANCY STATUS HISTORY
// ============================================================================

#region Job Vacancy Status History

public class JobVacancyStatusHistoryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobVacancyId { get; set; }
    public JobVacancyStatus FromStatus { get; set; }
    public string FromStatusName => FromStatus.ToString();
    public JobVacancyStatus ToStatus { get; set; }
    public string ToStatusName => ToStatus.ToString();
    public DateTime ChangedDate { get; set; }
    public Guid ChangedById { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Comments { get; set; }
}

#endregion

// ============================================================================
// SECTION 1b — VACANCY PIPELINE STAGE ASSIGNMENTS
// ============================================================================

#region Vacancy Pipeline Stage Assignments

public class VacancyPipelineStageAssignmentDto : BaseDto
{
    public Guid JobVacancyId { get; set; }
    public Guid PipelineStageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int StageOrder { get; set; }
    public RecruitmentPipelineStageType StageType { get; set; }
    public string StageTypeName => StageType.ToString();

    public Guid AssignedToId { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public Guid AssignedById { get; set; }
    public string AssignedByName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }

    public DateTime? DueDate { get; set; }

    public VacancyStageAssignmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }

    public bool EscalationEnabled { get; set; }
    public int? EscalationDaysAfterDue { get; set; }
    public Guid? EscalateToId { get; set; }
    public string? EscalateToName { get; set; }
    public DateTime? EscalatedAt { get; set; }
    [MaxLength(2000)]
    public string? EscalationNotes { get; set; }
}

public class CreateVacancyPipelineStageAssignmentDto : CreateDtoBase
{
    [Required]
    public Guid JobVacancyId { get; set; }
    [Required]
    public Guid PipelineStageId { get; set; }
    [Required]
    public Guid AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public bool EscalationEnabled { get; set; }
    public int? EscalationDaysAfterDue { get; set; }
    public Guid? EscalateToId { get; set; }
}

public class UpdateVacancyPipelineStageAssignmentDto
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public Guid AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public bool EscalationEnabled { get; set; }
    public int? EscalationDaysAfterDue { get; set; }
    public Guid? EscalateToId { get; set; }
}

public class CompleteVacancyPipelineStageAssignmentDto
{
    [Required]
    public Guid Id { get; set; }
    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }
}

public class SkipVacancyPipelineStageAssignmentDto
{
    [Required]
    public Guid Id { get; set; }
    [MaxLength(2000)]
    public string? Reason { get; set; }
}

#endregion

// ============================================================================
// SECTION 2 — JOB POSTING
// ============================================================================

#region Job Posting

public class JobPostingDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobVacancyId { get; set; }
    public string VacancyNumber { get; set; } = string.Empty;
    public JobPostingChannel Channel { get; set; }
    public string ChannelName => Channel.ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PostingUrl { get; set; }
    public string? ExternalPostingId { get; set; }
    public DateTime PublishDate { get; set; }
    public DateTime? ActualPublishDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public JobPostingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsActive { get; set; }
    public int ApplicationCount { get; set; }
    public Guid? PostedById { get; set; }
    public string? PostedByName { get; set; }

    // Advert copy (print channels)
    public string? AdvertHeadline { get; set; }
    public string? AdvertBody { get; set; }
    public string? HowToApply { get; set; }
    public string? ClosingDateText { get; set; }
    public bool ShowSalaryInAdvert { get; set; }
    public string? ContactDetails { get; set; }
}

public class JobPostingSummaryDto
{
    public Guid Id { get; set; }
    /// <summary>The vacancy this advert belongs to — an advert has no page of its own; its detail is the vacancy's Adverts tab.</summary>
    public Guid JobVacancyId { get; set; }
    public string? VacancyNumber { get; set; }
    public JobPostingChannel Channel { get; set; }
    public string ChannelName => Channel.ToString();
    public string Title { get; set; } = string.Empty;
    public JobPostingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsActive { get; set; }
    public DateTime PublishDate { get; set; }
    public DateTime? ActualPublishDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int ApplicationCount { get; set; }
    public string? PostingUrl { get; set; }
}

public class CreateJobPostingDto : CreateDtoBase
{
    [Required]
    public Guid JobVacancyId { get; set; }

    [Required]
    public JobPostingChannel Channel { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? PostingUrl { get; set; }

    [MaxLength(200)]
    public string? ExternalPostingId { get; set; }

    [Required]
    public DateTime PublishDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    // Advert copy (print channels e.g. Newspaper)
    [MaxLength(200)] public string? AdvertHeadline { get; set; }
    [MaxLength(8000)] public string? AdvertBody { get; set; }
    [MaxLength(1000)] public string? HowToApply { get; set; }
    [MaxLength(200)] public string? ClosingDateText { get; set; }
    public bool ShowSalaryInAdvert { get; set; }
    [MaxLength(500)] public string? ContactDetails { get; set; }
}

public class UpdateJobPostingDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? PostingUrl { get; set; }

    [MaxLength(200)]
    public string? ExternalPostingId { get; set; }

    public DateTime? ExpiryDate { get; set; }
    public JobPostingStatus Status { get; set; }
    public bool IsActive { get; set; }

    // Advert copy (print channels)
    [MaxLength(200)] public string? AdvertHeadline { get; set; }
    [MaxLength(8000)] public string? AdvertBody { get; set; }
    [MaxLength(1000)] public string? HowToApply { get; set; }
    [MaxLength(200)] public string? ClosingDateText { get; set; }
    public bool ShowSalaryInAdvert { get; set; }
    [MaxLength(500)] public string? ContactDetails { get; set; }
}

public class JobPostingAttachmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobPostingId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

// CreateJobPostingAttachmentDto was deleted deliberately — same reason as its vacancy sibling
// above. See IJobPostingService.AddAttachmentAsync.

public class PublishJobPostingDto
{
    public DateTime? ActualPublishDate { get; set; }
}

#endregion

// ============================================================================
// SECTION 3 — RECRUITMENT PIPELINE
// ============================================================================

#region Recruitment Pipeline

public class RecruitmentPipelineDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int? DefaultTimeToCompleteDays { get; set; }
    public List<RecruitmentPipelineStageDto> Stages { get; set; } = new();
}

public class RecruitmentPipelineSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int StageCount { get; set; }
    public int? DefaultTimeToCompleteDays { get; set; }
}

public class CreateRecruitmentPipelineDto : CreateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public int? DefaultTimeToCompleteDays { get; set; }
    public List<CreateRecruitmentPipelineStageDto> Stages { get; set; } = new();
}

public class UpdateRecruitmentPipelineDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int? DefaultTimeToCompleteDays { get; set; }
}

#endregion

#region Recruitment Pipeline Stage

public class RecruitmentPipelineStageDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid RecruitmentPipelineId { get; set; }
    public string PipelineName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
    public RecruitmentPipelineStageType StageType { get; set; }
    public string StageTypeName => StageType.ToString();
    public bool IsActive { get; set; }
    public bool IsFinalStage { get; set; }
    public bool IsRequired { get; set; }
    public int? DefaultTimeToCompleteDays { get; set; }
    public bool CanSkip { get; set; }
    public bool CanRepeat { get; set; }
    public int? MaxAttempts { get; set; }
    public string? Instructions { get; set; }
}

public class CreateRecruitmentPipelineStageDto : CreateDtoBase
{
    public Guid RecruitmentPipelineId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public int Order { get; set; }

    [Required]
    public RecruitmentPipelineStageType StageType { get; set; }

    public bool IsFinalStage { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsRequired { get; set; } = true;
    public int? DefaultTimeToCompleteDays { get; set; }
    public bool CanSkip { get; set; }
    public bool CanRepeat { get; set; }
    public int? MaxAttempts { get; set; }

    [MaxLength(2000)]
    public string? Instructions { get; set; }
}

public class UpdateRecruitmentPipelineStageDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public int Order { get; set; }

    [Required]
    public RecruitmentPipelineStageType StageType { get; set; }

    public bool IsActive { get; set; }
    public bool IsFinalStage { get; set; }
    public bool IsRequired { get; set; }
    public int? DefaultTimeToCompleteDays { get; set; }
    public bool CanSkip { get; set; }
    public bool CanRepeat { get; set; }
    public int? MaxAttempts { get; set; }

    [MaxLength(2000)]
    public string? Instructions { get; set; }
}

#endregion

// ============================================================================
// SECTION 4 — JOB SHORTLISTING CRITERIA
// ============================================================================

#region Job Shortlisting Criteria

public class JobShortlistingCriteriaDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobVacancyId { get; set; }
    public string CriteriaName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JobShortlistingCriteriaType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? RequiredValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsMandatory { get; set; }
    /// <summary>Controls how multi-valued RequiredValue lists are matched (any vs all).</summary>
    public MandatoryMatchMode MatchMode { get; set; }
    /// <summary>Controls how individual required values are compared against candidate strings.</summary>
    public ValueMatchStrategy MatchStrategy { get; set; }
    /// <summary>Optional catalogue ID for Skill-type criteria — used for ID-first matching in scoring.</summary>
    public Guid? RequiredSkillId { get; set; }
    /// <summary>Optional catalogue ID for Qualification-type criteria — used for ID-first matching in scoring.</summary>
    public Guid? RequiredQualificationId { get; set; }
    public int Weight { get; set; }
    public ShortlistingComparisonOperator? ComparisonOperator { get; set; }
    public string? ComparisonOperatorName => ComparisonOperator?.ToString();
    /// <summary>The accepted values, one row each (round 3, lane K). <c>RequiredValue</c> above mirrors their labels.</summary>
    public List<JobShortlistingCriteriaValueDto> Values { get; set; } = new();
}

/// <summary>One accepted value on a criterion (round 3, lane K; register row R-8).</summary>
public class JobShortlistingCriteriaValueDto
{
    public Guid Id { get; set; }
    public ShortlistingValueKind Kind { get; set; }
    public string KindName => Kind.ToString();
    /// <summary>The catalogue row for a catalogue kind; null for Gender and Text.</summary>
    public Guid? ReferenceId { get; set; }
    /// <summary>The catalogue name (mirrored), the gender member, or the typed text.</summary>
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>
/// An accepted value on the criterion save: a catalogue id (its name is mirrored), a gender
/// member's name, or typed text. The kind follows the criterion's type; the server resolves it.
/// </summary>
public class ShortlistingCriteriaValueInputDto
{
    public Guid? ReferenceId { get; set; }

    [MaxLength(200)]
    public string? Label { get; set; }
}

/// <summary>
/// What a criterion type is made of — served by <c>GET api/job-vacancies/criteria/shapes</c>
/// (round 3, lane K; plan § 5.4) so the panel and the service agree on one table.
/// </summary>
public class ShortlistingCriteriaShapeDto
{
    public JobShortlistingCriteriaType Type { get; set; }
    public string TypeName => Type.ToString();
    public string Label { get; set; } = string.Empty;
    public ShortlistingValueKind? ValueKind { get; set; }
    public string? ValueKindName => ValueKind?.ToString();
    public bool IsList { get; set; }
    public bool IsNumeric { get; set; }
    public bool RequiresValues { get; set; }
    public bool AllowsMandatory { get; set; }
    public bool IsProtectedCharacteristic { get; set; }
    public bool IsAutoEvaluated { get; set; }
    public List<string> Operators { get; set; } = new();
    public string Hint { get; set; } = string.Empty;
    public string? MandatoryRefusal { get; set; }
}

public class CreateJobShortlistingCriteriaDto : CreateDtoBase
{
    [Required]
    public Guid JobVacancyId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CriteriaName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public JobShortlistingCriteriaType Type { get; set; }

    [MaxLength(500)]
    public string? RequiredValue { get; set; }

    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsMandatory { get; set; }

    /// <summary>Controls how multi-valued RequiredValue lists are matched (any vs all). Defaults to AnyMatched.</summary>
    public MandatoryMatchMode MatchMode { get; set; } = MandatoryMatchMode.AnyMatched;
    /// <summary>Controls how individual required values are compared against candidate strings. Defaults to Exact.</summary>
    public ValueMatchStrategy MatchStrategy { get; set; } = ValueMatchStrategy.Exact;
    public Guid? RequiredSkillId { get; set; }
    public Guid? RequiredQualificationId { get; set; }

    [Range(1, 100)]
    public int Weight { get; set; } = 1;

    public ShortlistingComparisonOperator? ComparisonOperator { get; set; }

    /// <summary>
    /// The accepted values, as the whole set (round 3, lane K). Null keeps the legacy shape: the
    /// comma-separated <c>RequiredValue</c> is split into text values. An empty list on a type that
    /// needs values is refused — a blank criterion passes every candidate.
    /// </summary>
    public List<ShortlistingCriteriaValueInputDto>? Values { get; set; }
}

public class UpdateJobShortlistingCriteriaDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string CriteriaName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public JobShortlistingCriteriaType Type { get; set; }

    [MaxLength(500)]
    public string? RequiredValue { get; set; }

    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsMandatory { get; set; }

    /// <summary>Controls how multi-valued RequiredValue lists are matched (any vs all). Defaults to AnyMatched.</summary>
    public MandatoryMatchMode MatchMode { get; set; } = MandatoryMatchMode.AnyMatched;
    /// <summary>Controls how individual required values are compared against candidate strings. Defaults to Exact.</summary>
    public ValueMatchStrategy MatchStrategy { get; set; } = ValueMatchStrategy.Exact;
    public Guid? RequiredSkillId { get; set; }
    public Guid? RequiredQualificationId { get; set; }

    [Range(1, 100)]
    public int Weight { get; set; }

    public ShortlistingComparisonOperator? ComparisonOperator { get; set; }

    /// <summary>The accepted values, as the whole set (round 3, lane K). Null keeps the legacy comma-separated shape.</summary>
    public List<ShortlistingCriteriaValueInputDto>? Values { get; set; }
}

#endregion

// ============================================================================
// SECTION 5 — JOB CANDIDATE
// ============================================================================

#region Job Candidate

public class JobCandidateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string CandidateNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    /// <remarks>
    /// ⚠ <b>A second copy of the entity's expression, and it had the same bug.</b> Fixing
    /// <c>JobCandidate.FullName</c> alone left this one rendering the double space, so the paged
    /// application read (which goes through the entity) and the candidate read (which goes through
    /// here) disagreed about the same person's name — caught by slice B, whose assertion compares
    /// the two. Keep the two in step, or delete this one in favour of the entity's.
    /// </remarks>
    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{FirstName} {LastName}".Trim()
        : $"{FirstName} {MiddleName} {LastName}".Trim();
    public DateTime DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string GenderName => Gender.ToString();
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public string? PostalAddress { get; set; }
    public string? DigitalAddress { get; set; }
    /// <summary>⚠ A display snapshot resolved from <see cref="GeoAreaId"/> when one is set.</summary>
    public string City { get; set; } = string.Empty;
    /// <summary>⚠ A display snapshot resolved from <see cref="GeoAreaId"/> when one is set.</summary>
    public string? Region { get; set; }

    /// <summary>
    /// Where the candidate lives, as one reference to the geography tree — the lowest tier known.
    /// The edit form re-opens its cascade from this by asking for the area's ancestors.
    /// </summary>
    /// <remarks>
    /// ⚠ No <c>GeoAreaName</c> or full path beside it, deliberately — the same call the employee
    /// read DTO makes. Either would be null on every read whose query did not Include the
    /// navigation, and lists print <see cref="Region"/> and <see cref="City"/>, which is what the
    /// snapshot columns are kept for.
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

    public string? Nationality { get; set; }
    /// <summary>Optional since slice 13b — an internal candidate may have no country on file.</summary>
    public Guid? CountryId { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public bool IsInTalentPool { get; set; }
    public DateTime? TalentPoolAddedDate { get; set; }
    public string? LinkedInProfile { get; set; }
    public string? PortfolioUrl { get; set; }
    public string? GitHubUrl { get; set; }
    // Professional profile
    public string? Headline { get; set; }
    public string? ProfessionalSummary { get; set; }
    public string? CurrentJobTitle { get; set; }
    public string? CurrentEmployer { get; set; }
    public int? TotalYearsExperience { get; set; }
    // Availability & preferences
    public int? NoticePeriodDays { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public ErpSystem.Core.Enums.PreferredWorkArrangement PreferredWorkArrangement { get; set; }
    public string PreferredWorkArrangementName => PreferredWorkArrangement.ToString();
    // Compensation
    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }
    public string? ExpectedSalaryCurrency { get; set; }
    // Compliance
    public ErpSystem.Core.Enums.WorkAuthorizationStatus WorkAuthorizationStatus { get; set; }
    public string WorkAuthorizationStatusName => WorkAuthorizationStatus.ToString();
    // National identity (round 3, lane C1) — the employee's trio
    public Guid? NationalIdTypeId { get; set; }
    public string? NationalIdTypeName { get; set; }
    public string? NationalIdNumber { get; set; }
    public DateTime? NationalIdExpiryDate { get; set; }
    // Documents
    public string? CvFilePath { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    /// <summary>
    /// Whether a photograph is on file (round 3, lane C2). Derived from the gated upload record, or
    /// the legacy URL on rows written before photos went private; the screen fetches
    /// <c>GET /{id}/photo</c> only when this is true.
    /// </summary>
    public bool HasPhoto { get; set; }
    public int ApplicationCount { get; set; }
}

public class JobCandidateSummaryDto
{
    public Guid Id { get; set; }
    public string CandidateNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    /// <summary>Round 4, lane A — so the register can say "Tema, Greater Accra" without a tree walk.</summary>
    public string? Region { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public bool IsInTalentPool { get; set; }
    /// <summary>Round 3, lane C2 — the list shows a face beside the name when one is on file.</summary>
    public bool HasPhoto { get; set; }
    public int ApplicationCount { get; set; }
}

public class JobCandidateDetailDto : JobCandidateDto
{
    public List<JobCandidateQualificationDto> Qualifications { get; set; } = new();
    public List<JobCandidateWorkHistoryDto> WorkHistories { get; set; } = new();
    public List<JobCandidateRefereeDto> Referees { get; set; } = new();
    public List<JobCandidateSkillDto> Skills { get; set; } = new();
    public List<JobCandidateLanguageDto> Languages { get; set; } = new();
    public List<JobCandidateInterestDto> Interests { get; set; } = new();
    public List<JobCandidateDocumentDto> Documents { get; set; } = new();
    public List<JobCandidateNoteDto> Notes { get; set; } = new();
    public List<JobApplicationSummaryDto> Applications { get; set; } = new();
}

/// <summary>
/// The one statement of what counts as an address on a candidate, shared by the create and update
/// DTOs so the two cannot drift.
/// </summary>
/// <remarks>
/// ⚠ Kept deliberately permissive about <i>which</i> form. Most of the world has no geography
/// scheme loaded, so insisting on an area would make the careers form unfillable outside Ghana;
/// insisting on a city would make it unfillable inside Ghana, where the cascade writes the city and
/// the input is disabled. One or the other, never neither.
/// </remarks>
internal static class JobCandidateAddressRule
{
    public static IEnumerable<ValidationResult> Validate(Guid? geoAreaId, string? city)
    {
        if (geoAreaId is null && string.IsNullOrWhiteSpace(city))
            yield return new ValidationResult(
                "Say where the candidate is: pick an area, or type a city.",
                new[] { nameof(CreateJobCandidateDto.City), nameof(CreateJobCandidateDto.GeoAreaId) });
    }
}

public class CreateJobCandidateDto : CreateDtoBase, IValidatableObject
{
    /// <summary>
    /// An address in one form or the other. <see cref="City"/> stopped being <c>[Required]</c> when
    /// the geography cascade arrived, and without this a candidate could be created with no stated
    /// location at all — which the Location shortlisting criterion would then silently fail.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => JobCandidateAddressRule.Validate(GeoAreaId, City);

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public DateTime DateOfBirth { get; set; }

    [Required]
    public Gender Gender { get; set; }

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? AlternatePhone { get; set; }

    [MaxLength(200)]
    public string? PostalAddress { get; set; }

    [MaxLength(30)]
    public string? DigitalAddress { get; set; }

    /// <summary>
    /// The candidate's town or city, as free text.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>No longer <c>[Required]</c> as of round 4, lane A</b>, and that is deliberate rather
    /// than a relaxation. When <see cref="GeoAreaId"/> is supplied the service <i>overwrites</i>
    /// this from the tree, so a form whose cascade is filled in has nothing to put here — and a
    /// required field the user is not allowed to type into is a form that cannot be submitted.
    /// The address is still compulsory: <c>Validate</c> refuses a payload carrying neither an area
    /// nor a city.
    /// </remarks>
    [MaxLength(100)]
    public string? City { get; set; }

    /// <summary>
    /// Where the candidate lives, as one reference to the geography tree — the lowest tier chosen.
    /// </summary>
    /// <remarks>
    /// <para>Null is "no area", not "leave unchanged": this DTO replaces the record's address
    /// wholesale, so there is no <c>ClearGeoArea</c> flag of the kind the employee's patch-style
    /// update needs.</para>
    ///
    /// <para>The area's country is reconciled with <c>CountryId</c> on save — a candidate cannot
    /// claim Nigeria while pointing at a Ghanaian district — and a stated country that is silent is
    /// filled in from the area rather than refused.</para>
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

    /// <summary>
    /// The candidate's nationality, as free text.
    /// </summary>
    /// <remarks>
    /// <para>⚠ G-7.3 (2026-09-15). This field exists on the entity and is rendered on the Personal
    /// card, but appeared on the <b>read</b> DTO only — no create DTO, no update DTO, no form field,
    /// and no candidate-portal path wrote it. The <b>only</b> assignment anywhere in the solution
    /// was <c>TdcDemoRecruitmentHistorySeeder</c>, which sets <c>"Ghanaian"</c>.</para>
    ///
    /// <para>So on a real tenant the Nationality row always read "—", and on the demo tenant it
    /// always read "Ghanaian": a field that looked populated in every walkthrough and was
    /// unreachable in production. Appendix C's fourth pattern — when a field looks fine on the demo
    /// tenant, check who writes it before concluding it works.</para>
    ///
    /// <para>Free text rather than a lookup, deliberately. Nationality is not the same question as
    /// <see cref="CountryId"/>, which is where the candidate is; a dual national or a stateless
    /// applicant is not served by a single foreign key into the country table.</para>
    /// </remarks>
    [MaxLength(100)]
    public string? Nationality { get; set; }

    /// <summary>
    /// The candidate's country. <b>Optional</b>, matching the entity: the FK was made nullable on
    /// 2026-08-27 (<c>MakeJobCandidateCountryOptional</c>) because "a country is a requirement the
    /// foreign key invented". ⚠ This was <c>[Required] Guid</c> until 2026-09-14 — and
    /// <c>[Required]</c> on a non-nullable <c>Guid</c> is a NO-OP, so an omitted country posted
    /// <c>Guid.Empty</c> and the insert died on the foreign key with a 500. A supplied country is
    /// checked against the tenant's live list; <c>Guid.Empty</c> is read as "none".
    /// </summary>
    public Guid? CountryId { get; set; }

    [MaxLength(200)]
    public string? LinkedInProfile { get; set; }

    [MaxLength(200)]
    public string? PortfolioUrl { get; set; }

    [MaxLength(200)]
    public string? GitHubUrl { get; set; }

    // National identity (round 3, lane C1). The type must be an active IdentificationType of the
    // tenant; the service refuses an unknown one.
    public Guid? NationalIdTypeId { get; set; }

    [MaxLength(50)]
    public string? NationalIdNumber { get; set; }

    public DateTime? NationalIdExpiryDate { get; set; }

    // ── Professional profile and availability ────────────────────────────────
    //
    // Round 4, lane B. These were writable ONLY through the candidate's own portal profile, while
    // the talent pool's match rubric scores on three of them and the pool list has a column for two.
    // So a candidate HR typed in by hand — which is how a career fair, a referral and an
    // unsolicited CV all reach the pool — could never rank above the "nothing on file" tier,
    // whatever HR knew about them, and there was no box to put it in.
    //
    // ⚠ This is the shape G-13.2 warned about from the other side: the rubric was corrected so that
    // silence stops scoring like a match, which is right — and then the only people who could break
    // the silence were the candidates themselves.

    [MaxLength(300)]
    public string? Headline { get; set; }

    [MaxLength(4000)]
    public string? ProfessionalSummary { get; set; }

    [MaxLength(200)]
    public string? CurrentJobTitle { get; set; }

    [MaxLength(200)]
    public string? CurrentEmployer { get; set; }

    [Range(0, 60)]
    public int? TotalYearsExperience { get; set; }

    [Range(0, 1825)]
    public int? NoticePeriodDays { get; set; }

    public DateTime? AvailableFrom { get; set; }

    public ErpSystem.Core.Enums.PreferredWorkArrangement PreferredWorkArrangement { get; set; }

    public bool IsInTalentPool { get; set; }
}

public class UpdateJobCandidateDto : UpdateDtoBase, IValidatableObject
{
    /// <inheritdoc cref="CreateJobCandidateDto.Validate"/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => JobCandidateAddressRule.Validate(GeoAreaId, City);

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public DateTime DateOfBirth { get; set; }

    [Required]
    public Gender Gender { get; set; }

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? AlternatePhone { get; set; }

    [MaxLength(200)]
    public string? PostalAddress { get; set; }

    [MaxLength(30)]
    public string? DigitalAddress { get; set; }

    /// <summary>
    /// The candidate's town or city, as free text.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>No longer <c>[Required]</c> as of round 4, lane A</b>, and that is deliberate rather
    /// than a relaxation. When <see cref="GeoAreaId"/> is supplied the service <i>overwrites</i>
    /// this from the tree, so a form whose cascade is filled in has nothing to put here — and a
    /// required field the user is not allowed to type into is a form that cannot be submitted.
    /// The address is still compulsory: <c>Validate</c> refuses a payload carrying neither an area
    /// nor a city.
    /// </remarks>
    [MaxLength(100)]
    public string? City { get; set; }

    /// <summary>
    /// Where the candidate lives, as one reference to the geography tree — the lowest tier chosen.
    /// </summary>
    /// <remarks>
    /// <para>Null is "no area", not "leave unchanged": this DTO replaces the record's address
    /// wholesale, so there is no <c>ClearGeoArea</c> flag of the kind the employee's patch-style
    /// update needs.</para>
    ///
    /// <para>The area's country is reconciled with <c>CountryId</c> on save — a candidate cannot
    /// claim Nigeria while pointing at a Ghanaian district — and a stated country that is silent is
    /// filled in from the area rather than refused.</para>
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

    /// <summary>
    /// The candidate's nationality, as free text.
    /// </summary>
    /// <remarks>
    /// <para>⚠ G-7.3 (2026-09-15). This field exists on the entity and is rendered on the Personal
    /// card, but appeared on the <b>read</b> DTO only — no create DTO, no update DTO, no form field,
    /// and no candidate-portal path wrote it. The <b>only</b> assignment anywhere in the solution
    /// was <c>TdcDemoRecruitmentHistorySeeder</c>, which sets <c>"Ghanaian"</c>.</para>
    ///
    /// <para>So on a real tenant the Nationality row always read "—", and on the demo tenant it
    /// always read "Ghanaian": a field that looked populated in every walkthrough and was
    /// unreachable in production. Appendix C's fourth pattern — when a field looks fine on the demo
    /// tenant, check who writes it before concluding it works.</para>
    ///
    /// <para>Free text rather than a lookup, deliberately. Nationality is not the same question as
    /// <see cref="CountryId"/>, which is where the candidate is; a dual national or a stateless
    /// applicant is not served by a single foreign key into the country table.</para>
    /// </remarks>
    [MaxLength(100)]
    public string? Nationality { get; set; }

    /// <summary>
    /// The candidate's country. <b>Optional</b>, matching the entity: the FK was made nullable on
    /// 2026-08-27 (<c>MakeJobCandidateCountryOptional</c>) because "a country is a requirement the
    /// foreign key invented". ⚠ This was <c>[Required] Guid</c> until 2026-09-14 — and
    /// <c>[Required]</c> on a non-nullable <c>Guid</c> is a NO-OP, so an omitted country posted
    /// <c>Guid.Empty</c> and the insert died on the foreign key with a 500. A supplied country is
    /// checked against the tenant's live list; <c>Guid.Empty</c> is read as "none".
    /// </summary>
    public Guid? CountryId { get; set; }

    [MaxLength(200)]
    public string? LinkedInProfile { get; set; }

    [MaxLength(200)]
    public string? PortfolioUrl { get; set; }

    [MaxLength(200)]
    public string? GitHubUrl { get; set; }

    // National identity (round 3, lane C1). The type must be an active IdentificationType of the
    // tenant; the service refuses an unknown one.
    public Guid? NationalIdTypeId { get; set; }

    [MaxLength(50)]
    public string? NationalIdNumber { get; set; }

    public DateTime? NationalIdExpiryDate { get; set; }

    /// <inheritdoc cref="CreateJobCandidateDto.Headline"/>
    [MaxLength(300)]
    public string? Headline { get; set; }

    [MaxLength(4000)]
    public string? ProfessionalSummary { get; set; }

    [MaxLength(200)]
    public string? CurrentJobTitle { get; set; }

    [MaxLength(200)]
    public string? CurrentEmployer { get; set; }

    [Range(0, 60)]
    public int? TotalYearsExperience { get; set; }

    [Range(0, 1825)]
    public int? NoticePeriodDays { get; set; }

    public DateTime? AvailableFrom { get; set; }

    public ErpSystem.Core.Enums.PreferredWorkArrangement PreferredWorkArrangement { get; set; }

    public bool IsInTalentPool { get; set; }
}

#endregion

#region Job Candidate Qualification

public class JobCandidateQualificationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobCandidateId { get; set; }
    public QualificationType QualificationType { get; set; }
    public string QualificationTypeName => QualificationType.ToString();
    public Guid? QualificationId { get; set; }
    public string QualificationName { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public DateOnly DateAwarded { get; set; }
    public string? Grade { get; set; }

    /// <summary>The rung the row itself states (round 4, lane Q). What a form edits.</summary>
    public Guid? QualificationLevelId { get; set; }

    /// <summary>
    /// The rung the engine scores: <see cref="QualificationLevelId"/>, or else the catalogue entry's.
    /// </summary>
    public Guid? EffectiveQualificationLevelId { get; set; }

    /// <summary>The effective rung's name, so a list is readable without a second call.</summary>
    public string? EffectiveQualificationLevelName { get; set; }
}

public class CreateJobCandidateQualificationDto : CreateDtoBase
{
    [Required]
    public Guid JobCandidateId { get; set; }

    [Required]
    public QualificationType QualificationType { get; set; }

    public Guid? QualificationId { get; set; }

    /// <summary>
    /// The rung of the qualification ladder (round 4, lane Q). Required for Education unless the
    /// catalogue entry picked already sits on one; optional otherwise.
    /// </summary>
    public Guid? QualificationLevelId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Institution { get; set; } = string.Empty;

    [Required]
    public DateOnly DateAwarded { get; set; }

    [MaxLength(100)]
    public string? Grade { get; set; }

    [MaxLength(200)]
    public string? QualificationFreeText { get; set; }
}

public class UpdateJobCandidateQualificationDto : UpdateDtoBase
{
    [Required]
    public QualificationType QualificationType { get; set; }

    public Guid? QualificationId { get; set; }

    /// <summary>
    /// The rung of the qualification ladder (round 4, lane Q). ⚠ Whole-record, like every field
    /// here: an update that omits it clears it.
    /// </summary>
    public Guid? QualificationLevelId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Institution { get; set; } = string.Empty;

    [Required]
    public DateOnly DateAwarded { get; set; }

    [MaxLength(100)]
    public string? Grade { get; set; }

    [MaxLength(200)]
    public string? QualificationFreeText { get; set; }
}

#endregion

#region Job Candidate Work History

public class JobCandidateWorkHistoryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobCandidateId { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public string PositionHeld { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent => !EndDate.HasValue;
    public string? Responsibilities { get; set; }
    public string? ReasonForLeaving { get; set; }
}

public class CreateJobCandidateWorkHistoryDto : CreateDtoBase
{
    [Required]
    public Guid JobCandidateId { get; set; }

    [Required]
    [MaxLength(200)]
    public string InstitutionName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string PositionHeld { get; set; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [MaxLength(4000)]
    public string? Responsibilities { get; set; }

    [MaxLength(200)]
    public string? ReasonForLeaving { get; set; }
}

public class UpdateJobCandidateWorkHistoryDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string InstitutionName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string PositionHeld { get; set; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [MaxLength(4000)]
    public string? Responsibilities { get; set; }

    [MaxLength(200)]
    public string? ReasonForLeaving { get; set; }
}

#endregion

#region Job Candidate Referee

public class JobCandidateRefereeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobCandidateId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;

    /// <summary>The catalogue row behind <see cref="Relationship"/>, for re-opening the dropdown.</summary>
    public Guid? RelationshipTypeId { get; set; }

    public int YearsKnown { get; set; }
}

public class CreateJobCandidateRefereeDto : CreateDtoBase
{
    [Required]
    public Guid JobCandidateId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Position { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Organization { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// The tie, as words. Overwritten when <see cref="RelationshipTypeId"/> names a catalogue row.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Relationship { get; set; } = string.Empty;

    /// <summary>The tie, from the tenant's relationship catalogue (round 2, lane D2).</summary>
    /// <remarks>
    /// ⚠ PROFESSIONAL and OTHER only. A candidate may name a pastor or a family friend (both
    /// other); they may not name their mother. Null on the update CLEARS the link — every field on
    /// these DTOs is full-replace.
    /// </remarks>
    public Guid? RelationshipTypeId { get; set; }

    [Range(0, 60)]
    public int YearsKnown { get; set; }
}

public class UpdateJobCandidateRefereeDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Position { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Organization { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// The tie, as words. Overwritten when <see cref="RelationshipTypeId"/> names a catalogue row.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Relationship { get; set; } = string.Empty;

    /// <summary>The tie, from the tenant's relationship catalogue (round 2, lane D2).</summary>
    /// <remarks>
    /// ⚠ PROFESSIONAL and OTHER only. A candidate may name a pastor or a family friend (both
    /// other); they may not name their mother. Null on the update CLEARS the link — every field on
    /// these DTOs is full-replace.
    /// </remarks>
    public Guid? RelationshipTypeId { get; set; }

    [Range(0, 60)]
    public int YearsKnown { get; set; }
}

#endregion

#region Job Candidate Skill

public class JobCandidateSkillDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobCandidateId { get; set; }
    public Guid? SkillId { get; set; }
    public string? SkillCatalogueName { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public ProficiencyLevel? Proficiency { get; set; }
    public string? ProficiencyName => Proficiency?.ToString();
    public int? YearsOfExperience { get; set; }
    public bool IsCertified { get; set; }
    public string? CertificationName { get; set; }
    public string? CertificationNumber { get; set; }
    public string? CertifyingBody { get; set; }
    public DateTime? CertificationExpiryDate { get; set; }
}

public class CreateJobCandidateSkillDto : CreateDtoBase
{
    [Required]
    public Guid JobCandidateId { get; set; }

    public Guid? SkillId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SkillName { get; set; } = string.Empty;

    public ProficiencyLevel? Proficiency { get; set; }

    [Range(0, 50)]
    public int? YearsOfExperience { get; set; }

    public bool IsCertified { get; set; }

    [MaxLength(200)]
    public string? CertificationName { get; set; }

    // Round 3, lane C1. Ignored (cleared) unless IsCertified is true.
    [MaxLength(100)]
    public string? CertificationNumber { get; set; }

    [MaxLength(200)]
    public string? CertifyingBody { get; set; }

    public DateTime? CertificationExpiryDate { get; set; }
}

public class UpdateJobCandidateSkillDto : UpdateDtoBase
{
    public Guid? SkillId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SkillName { get; set; } = string.Empty;

    public ProficiencyLevel? Proficiency { get; set; }

    [Range(0, 50)]
    public int? YearsOfExperience { get; set; }

    public bool IsCertified { get; set; }

    [MaxLength(200)]
    public string? CertificationName { get; set; }

    // Round 3, lane C1. Ignored (cleared) unless IsCertified is true.
    [MaxLength(100)]
    public string? CertificationNumber { get; set; }

    [MaxLength(200)]
    public string? CertifyingBody { get; set; }

    public DateTime? CertificationExpiryDate { get; set; }
}

#endregion

#region Job Candidate Interest

public class JobCandidateInterestDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobCandidateId { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public class CreateJobCandidateInterestDto : CreateDtoBase
{
    [Required]
    public Guid JobCandidateId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Detail { get; set; } = string.Empty;
}

public class UpdateJobCandidateInterestDto : UpdateDtoBase
{
    [Required]
    [MaxLength(500)]
    public string Detail { get; set; } = string.Empty;
}

#endregion

#region Job Candidate Document

public class JobCandidateDocumentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobCandidateId { get; set; }
    public JobCandidateDocumentType DocumentType { get; set; }
    public string DocumentTypeName => DocumentType.ToString();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
    /// <summary>What the file is, in the uploader's words (round 3, lane C1).</summary>
    public string? Description { get; set; }
}

// CreateJobCandidateDocumentDto is deliberately absent. It carried a caller-supplied FilePath, so the
// endpoint that took it stored no file, scanned nothing, and wrote a row pointing at a path the server
// had never received. Candidate documents are now posted as multipart to
// POST api/job-candidates/{candidateId}/documents and go through the controlled-upload gate, exactly
// like the requisition, vacancy and posting attachments. Deleted rather than left unused so the shape
// cannot drift back.

#endregion

#region Job Candidate Note

public class JobCandidateNoteDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobCandidateId { get; set; }
    public string NoteText { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
}

public class CreateJobCandidateNoteDto : CreateDtoBase
{
    [Required]
    public Guid JobCandidateId { get; set; }

    [Required]
    [MaxLength(4000)]
    public string NoteText { get; set; } = string.Empty;

    public bool IsPrivate { get; set; }
}

public class UpdateJobCandidateNoteDto : UpdateDtoBase
{
    [Required]
    [MaxLength(4000)]
    public string NoteText { get; set; } = string.Empty;

    public bool IsPrivate { get; set; }
}

#endregion

// ============================================================================
// SECTION 6 — JOB APPLICATION
// ============================================================================

#region Job Application

public class JobApplicationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public Guid JobVacancyId { get; set; }
    public string VacancyNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public Guid JobCandidateId { get; set; }
    public string CandidateNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public string CandidatePhone { get; set; } = string.Empty;

    /// <summary>
    /// Whether the candidate has a photograph on file, so a screen can show a face beside the name
    /// (round 4, lane B5).
    /// </summary>
    /// <remarks>
    /// &#9888; A flag, not the image. Photographs go through the gated
    /// <c>GET api/job-candidates/{id}/photo</c> like every other controlled upload; this exists so a
    /// list of thirty applications does not fire thirty requests that will each come back 404.
    /// </remarks>
    public bool CandidateHasPhoto { get; set; }

    public DateTime ApplicationDate { get; set; }
    public ApplicationStatus Status { get; set; }
    public string StatusName => FormatApplicationStatus(Status);
    public ApplicationSource Source { get; set; }
    public string SourceName => Source.ToString();
    public Guid? JobPostingId { get; set; }
    public string? JobPostingChannel { get; set; }
    /// <summary>The advert's own title (round 3, lane A), so the detail can say which posting it came through.</summary>
    public string? JobPostingTitle { get; set; }
    public int? YearsOfExperience { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public string? CoverLetter { get; set; }
    public decimal? AutoScore { get; set; }
    public string? AutoScoreBreakdown { get; set; }
    public DateTime? ScoredAt { get; set; }
    public bool ScoreIsStale { get; set; }
    public bool SnapshotAvailable { get; set; }
    public ShortlistDecisionSource? DecisionSource { get; set; }
    public string? DecisionSourceName => DecisionSource?.ToString();
    public DateTime? ShortlistedDate { get; set; }
    public Guid? ShortlistedById { get; set; }
    public string? ShortlistedByName { get; set; }
    public string? ShortlistingNotes { get; set; }
    public bool IsShortlisted { get; set; }
    public string? CurrentStageName { get; set; }
    public Guid? CurrentStageId { get; set; }
    public DateTime? WaitlistedDate { get; set; }
    public string? WaitlistReason { get; set; }
    public DateTime? WithdrawnDate { get; set; }
    public string? WithdrawalReason { get; set; }
    public DateTime? RejectedDate { get; set; }
    public Guid? RejectedById { get; set; }
    public string? RejectedByName { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsInternalCandidate { get; set; }
    public Guid? InternalEmployeeId { get; set; }
    public decimal? AggregatedReviewScore { get; set; }
    public bool HasOffer { get; set; }
    public bool HasHireRecord { get; set; }

    internal static string FormatApplicationStatus(ApplicationStatus status) => status switch
    {
        ApplicationStatus.UnderReview        => "Under Review",
        ApplicationStatus.InterviewScheduled => "Interview Scheduled",
        ApplicationStatus.InterviewCompleted => "Interview Completed",
        ApplicationStatus.AssessmentPending  => "Assessment Pending",
        ApplicationStatus.PreEmploymentCheck => "Pre-Employment Check",
        ApplicationStatus.OfferExtended      => "Offer Extended",
        ApplicationStatus.OfferAccepted      => "Offer Accepted",
        ApplicationStatus.OfferDeclined      => "Offer Declined",
        _                                    => status.ToString(),
    };
}

public class JobApplicationSummaryDto
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public Guid JobVacancyId { get; set; }
    public string VacancyNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public Guid JobCandidateId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;

    /// <inheritdoc cref="JobApplicationDto.CandidateHasPhoto"/>
    public bool CandidateHasPhoto { get; set; }

    public DateTime ApplicationDate { get; set; }
    public ApplicationStatus Status { get; set; }
    public string StatusName => JobApplicationDto.FormatApplicationStatus(Status);
    public ApplicationSource Source { get; set; }
    public string SourceName => Source.ToString();
    public int? YearsOfExperience { get; set; }
    public decimal? AutoScore { get; set; }
    public bool ScoreIsStale { get; set; }
    public bool SnapshotAvailable { get; set; }
    public DateTime? ScoredAt { get; set; }
    public DateTime? ShortlistedDate { get; set; }
    public bool IsShortlisted { get; set; }
    public bool IsInternalCandidate { get; set; }
    public decimal? AggregatedReviewScore { get; set; }
    public Guid? CurrentStageId { get; set; }
    public string? CurrentStageName { get; set; }
}

public class JobApplicationDetailDto : JobApplicationDto
{
    public List<JobApplicationStageHistoryDto> StageHistories { get; set; } = new();
    public List<JobApplicantTestResultDto> TestResults { get; set; } = new();
    public List<JobIntervieweeSummaryDto> InterviewSlots { get; set; } = new();
    public List<JobApplicantCommunicationDto> Communications { get; set; } = new();
    public JobOfferSummaryDto? Offer { get; set; }
    public JobHireRecordSummaryDto? HireRecord { get; set; }
}

/// <summary>
/// DTO used by employees applying internally via the Internal Job Board.
/// The backend resolves/creates a shadow JobCandidate from the employee's profile.
/// </summary>
public class InternalApplyForVacancyDto
{
    [Required]
    public Guid VacancyId { get; set; }

    [Range(0, 60)]
    public int? YearsOfExperience { get; set; }

    public DateTime? AvailableFrom { get; set; }

    [MaxLength(5000)]
    public string? CoverLetter { get; set; }
}

/// <summary>Payload for saving an internal application draft. Same fields as apply.</summary>
public class InternalSaveDraftDto
{
    [Required]
    public Guid VacancyId { get; set; }

    [Range(0, 60)]
    public int? YearsOfExperience { get; set; }

    public DateTime? AvailableFrom { get; set; }

    [MaxLength(5000)]
    public string? CoverLetter { get; set; }
}

/// <summary>Payload for submitting an existing internal draft. Optional final edits.</summary>
public class InternalSubmitDraftDto
{
    [Range(0, 60)]
    public int? YearsOfExperience { get; set; }

    public DateTime? AvailableFrom { get; set; }

    [MaxLength(5000)]
    public string? CoverLetter { get; set; }
}

/// <summary>
/// HR corrects how an application arrived (round 3, lane A; register row R-4): the source, and
/// the advert it came through — which must belong to the application's vacancy.
/// </summary>
public class UpdateJobApplicationSourceDto
{
    [Required]
    public ApplicationSource Source { get; set; }

    public Guid? JobPostingId { get; set; }
}

public class CreateJobApplicationDto : CreateDtoBase
{
    [Required]
    public Guid JobVacancyId { get; set; }

    [Required]
    public Guid JobCandidateId { get; set; }

    public ApplicationSource Source { get; set; } = ApplicationSource.CompanyWebsite;
    public Guid? JobPostingId { get; set; }

    [Range(0, 60)]
    public int? YearsOfExperience { get; set; }

    public DateTime? AvailableFrom { get; set; }

    [MaxLength(5000)]
    public string? CoverLetter { get; set; }
}

public class ShortlistApplicationDto
{
    [Required]
    public Guid ApplicationId { get; set; }

    [MaxLength(2000)]
    public string? ShortlistingNotes { get; set; }
}

public class RejectApplicationDto
{
    [Required]
    public Guid ApplicationId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string RejectionReason { get; set; } = string.Empty;
}

/// <summary>
/// One internal application, as the APPLICANT sees it (area 25 slice 13b).
/// </summary>
/// <remarks>
/// <para>Deliberately not <see cref="JobApplicationDetailDto"/>. That is the recruiter's view and
/// carries the auto-score and its criterion-by-criterion breakdown, the shortlisting notes, the
/// names of whoever shortlisted or rejected them, the applicant-communication log and the test
/// results. Handing a candidate the scoring they were assessed under — and the internal notes
/// written about them — is a different act from telling them where their application stands.</para>
///
/// <para>What IS here: their own submission back, the vacancy they applied to, the status and its
/// dates, and the outcome reason when there is one. The rejection reason is included on purpose,
/// following the module's own precedent from area 25 slice 6, where a training request's rejection
/// reason was finally made to reach the person who asked.</para>
/// </remarks>
public class MyJobApplicationDto
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;

    public Guid JobVacancyId { get; set; }
    public string VacancyNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string? OrgUnitName { get; set; }
    public DateTime? ApplicationDeadline { get; set; }

    public ApplicationStatus Status { get; set; }
    public string StatusName => JobApplicationDto.FormatApplicationStatus(Status);
    public DateTime ApplicationDate { get; set; }

    // ── What the applicant themselves sent ───────────────────────────────────
    public int? YearsOfExperience { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public string? CoverLetter { get; set; }

    // ── Where it got to ──────────────────────────────────────────────────────
    public bool IsShortlisted { get; set; }
    public DateTime? ShortlistedDate { get; set; }
    public DateTime? WithdrawnDate { get; set; }
    public string? WithdrawalReason { get; set; }
    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Whether the applicant may still take it back — computed here so the screen need not guess.</summary>
    public bool CanWithdraw { get; set; }
}

/// <summary>
/// The applicant's own withdrawal. The reason is optional here — unlike the desk
/// <see cref="WithdrawApplicationDto"/>, where a recruiter withdrawing on somebody's behalf must
/// say why — because a candidate who no longer wants the job owes no explanation.
/// </summary>
public class WithdrawMyApplicationDto
{
    [MaxLength(1000)]
    public string? WithdrawalReason { get; set; }
}

public class WithdrawApplicationDto
{
    [Required]
    public Guid ApplicationId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string WithdrawalReason { get; set; } = string.Empty;
}

public class MoveApplicationToStageDto
{
    [Required]
    public Guid ApplicationId { get; set; }

    [Required]
    public Guid PipelineStageId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// SECTION 7 — JOB APPLICATION STAGE HISTORY
// ============================================================================

#region Job Application Stage History

public class JobApplicationStageHistoryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public Guid PipelineStageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public RecruitmentPipelineStageType StageType { get; set; }
    public string StageTypeName => StageType.ToString();
    public DateTime EnteredAt { get; set; }
    public DateTime? ExitedAt { get; set; }
    public JobApplicationStageExitReason? ExitReason { get; set; }
    public string? ExitReasonName => ExitReason?.ToString();
    public string? Notes { get; set; }
    public Guid? MovedById { get; set; }
    public string MovedByName { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}

#endregion

// ============================================================================
// SECTION 8 — APPLICANT TEST RESULTS
// ============================================================================

#region Job Applicant Test Result

public class JobApplicantTestResultDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public JobApplicantTestType TestType { get; set; }
    public string TestTypeName => TestType.ToString();
    public string TestName { get; set; } = string.Empty;
    public DateTime TestDate { get; set; }
    public string? Venue { get; set; }
    public decimal? Score { get; set; }
    public decimal? MaxScore { get; set; }
    public decimal? ScorePercentage { get; set; }
    public bool? Passed { get; set; }
    public string? Remarks { get; set; }
    public Guid? InvigilatedById { get; set; }
    public string? InvigilatedByName { get; set; }
    public Guid? MarkedById { get; set; }
    public string? MarkedByName { get; set; }
    public DateTime? MarkedDate { get; set; }
}

public class CreateJobApplicantTestResultDto : CreateDtoBase
{
    [Required]
    public Guid JobApplicationId { get; set; }

    [Required]
    public JobApplicantTestType TestType { get; set; }

    [Required]
    [MaxLength(200)]
    public string TestName { get; set; } = string.Empty;

    [Required]
    public DateTime TestDate { get; set; }

    [MaxLength(500)]
    public string? Venue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Score { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxScore { get; set; }

    public bool? Passed { get; set; }

    [MaxLength(2000)]
    public string? Remarks { get; set; }

    public Guid? InvigilatedById { get; set; }
}

public class UpdateJobApplicantTestResultDto : UpdateDtoBase
{
    [Range(0, double.MaxValue)]
    public decimal? Score { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxScore { get; set; }

    public bool? Passed { get; set; }

    [MaxLength(2000)]
    public string? Remarks { get; set; }

    public Guid? MarkedById { get; set; }
    public DateTime? MarkedDate { get; set; }
}

#endregion

// ============================================================================
// SECTION 9 — APPLICANT COMMUNICATION
// ============================================================================

#region Job Applicant Communication

public class JobApplicantCommunicationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public JobApplicantCommunicationType Type { get; set; }
    public string TypeName => Type.ToString();
    public JobApplicantCommunicationDirection Direction { get; set; }
    public string DirectionName => Direction.ToString();
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public Guid? SentById { get; set; }
    public string? SentByName { get; set; }
    public Guid? TemplateId { get; set; }
    public string? ExternalMessageId { get; set; }
}

public class CreateJobApplicantCommunicationDto : CreateDtoBase
{
    [Required]
    public Guid JobApplicationId { get; set; }

    [Required]
    public JobApplicantCommunicationType Type { get; set; }

    [Required]
    public JobApplicantCommunicationDirection Direction { get; set; }

    [Required]
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [MaxLength(8000)]
    public string Body { get; set; } = string.Empty;

    public Guid? TemplateId { get; set; }

    [MaxLength(500)]
    public string? ExternalMessageId { get; set; }
}

#endregion

// ============================================================================
// SECTION 10 — INTERVIEW QUESTION BANK
// ============================================================================

#region Interview Question Type

public class JobInterviewQuestionTypeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int QuestionCount { get; set; }
}

public class JobInterviewQuestionTypeSummaryDto
{
    public Guid Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; }
    public int QuestionCount { get; set; }
}

public class CreateJobInterviewQuestionTypeDto : CreateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string TypeName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateJobInterviewQuestionTypeDto : UpdateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string TypeName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }
}

#endregion

#region Interview Question Detail

public class JobInterviewQuestionDetailDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int Weight { get; set; }
    public int MinScore { get; set; }
    public int MaxScore { get; set; }
    /// <summary>What a good answer sounds like. Printed on the paper scoring sheet when set.</summary>
    public string? ScoringGuide { get; set; }
    public Guid QuestionTypeId { get; set; }
    public string QuestionTypeName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateJobInterviewQuestionDetailDto : CreateDtoBase
{
    [Required]
    [MaxLength(500)]
    public string QuestionText { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Weight { get; set; } = 1;

    [Range(0, 100)]
    public int MinScore { get; set; } = 1;

    [Range(1, 100)]
    public int MaxScore { get; set; } = 10;

    /// <summary>
    /// What a good answer sounds like — printed beside the question on the paper scoring sheet.
    /// Optional; a question that needs no guidance should not carry an empty heading on every sheet.
    /// </summary>
    [MaxLength(2000)]
    public string? ScoringGuide { get; set; }

    [Required]
    public Guid QuestionTypeId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateJobInterviewQuestionDetailDto : UpdateDtoBase
{
    [Required]
    [MaxLength(500)]
    public string QuestionText { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Weight { get; set; }

    [Range(0, 100)]
    public int MinScore { get; set; }

    [Range(1, 100)]
    public int MaxScore { get; set; }

    /// <inheritdoc cref="CreateJobInterviewQuestionDetailDto.ScoringGuide"/>
    [MaxLength(2000)]
    public string? ScoringGuide { get; set; }

    [Required]
    public Guid QuestionTypeId { get; set; }

    public bool IsActive { get; set; }
}

#endregion

#region Interview Question Preset

// ============================================================================
// SECTION 10b — INTERVIEW QUESTION PRESETS
// ============================================================================

public class InterviewQuestionPresetDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<InterviewQuestionPresetItemDto> Items { get; set; } = new();
}

public class InterviewQuestionPresetSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int ItemCount { get; set; }
}

public class CreateInterviewQuestionPresetDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<CreateInterviewQuestionPresetItemDto> Items { get; set; } = new();
}

public class UpsertInterviewPresetItemDto
{
    /// <summary>Guid.Empty = new item; existing Guid = update in-place.</summary>
    public Guid Id { get; set; }

    [Required]
    public Guid QuestionTypeId { get; set; }

    [Range(1, 50)]
    public int RequiredQuestionCount { get; set; } = 1;

    [Range(1, 200)]
    public int AllowedPoolSize { get; set; } = 5;

    public int DisplayOrder { get; set; }
}

public class UpdateInterviewQuestionPresetDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// Full replacement list of items. Items with Id == Guid.Empty are inserted;
    /// items with a known Id are updated; DB items absent from this list are deleted.
    /// </summary>
    public List<UpsertInterviewPresetItemDto>? Items { get; set; }
}

public class InterviewQuestionPresetItemDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PresetId { get; set; }
    public Guid QuestionTypeId { get; set; }
    public string QuestionTypeName { get; set; } = string.Empty;
    public int RequiredQuestionCount { get; set; }
    public int AllowedPoolSize { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateInterviewQuestionPresetItemDto : CreateDtoBase
{
    [Required]
    public Guid PresetId { get; set; }

    [Required]
    public Guid QuestionTypeId { get; set; }

    [Range(1, 50)]
    public int RequiredQuestionCount { get; set; } = 1;

    [Range(1, 200)]
    public int AllowedPoolSize { get; set; } = 5;

    public int DisplayOrder { get; set; }
}

public class UpdateInterviewQuestionPresetItemDto : UpdateDtoBase
{
    [Required]
    public Guid QuestionTypeId { get; set; }

    [Range(1, 50)]
    public int RequiredQuestionCount { get; set; }

    [Range(1, 200)]
    public int AllowedPoolSize { get; set; }

    public int DisplayOrder { get; set; }
}

#endregion

// ============================================================================
// SECTION 11 — JOB INTERVIEW
// ============================================================================

#region Job Interview

public class JobInterviewDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string InterviewNumber { get; set; } = string.Empty;
    public Guid JobVacancyId { get; set; }
    public string VacancyNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public int Round { get; set; }
    public JobInterviewType Type { get; set; }
    public string TypeName => Type.ToString();
    public InterviewMode Mode { get; set; }
    public string ModeName => Mode.ToString();
    public JobInterviewStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly ScheduledDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? LocationOrLink { get; set; }
    public string? Instructions { get; set; }
    public string? RescheduleReason { get; set; }
    public DateOnly? OriginalDate { get; set; }
    public string? CancellationReason { get; set; }
    public int IntervieweeCount { get; set; }
    public int PanelistCount { get; set; }
    public Guid? QuestionPresetId { get; set; }

    // ── Round 4, lane D ────────────────────────────────────────────────────────

    /// <inheritdoc cref="CreateJobInterviewDto.RoomBookingId"/>
    public Guid? RoomBookingId { get; set; }

    /// <summary>The held room's name and booking number, so the screen need not fetch the booking.</summary>
    public string? RoomName { get; set; }
    public string? RoomBookingNumber { get; set; }

    /// <summary>
    /// Set when this interview was scheduled over a panelist's confirmed commitment. Null is the
    /// ordinary case; a value means somebody decided, and said why.
    /// </summary>
    public string? PanelClashOverrideReason { get; set; }
    public string? PanelClashOverrideDetail { get; set; }
    public DateTime? PanelClashOverriddenAt { get; set; }
}

public class JobInterviewSummaryDto
{
    public Guid Id { get; set; }
    public string InterviewNumber { get; set; } = string.Empty;
    public Guid JobVacancyId { get; set; }
    public string VacancyNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public int Round { get; set; }
    public JobInterviewType Type { get; set; }
    public string TypeName => Type.ToString();
    public InterviewMode Mode { get; set; }
    public string ModeName => Mode.ToString();
    public JobInterviewStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly ScheduledDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? LocationOrLink { get; set; }
    public int IntervieweeCount { get; set; }
    public int PanelistCount { get; set; }
    /// <summary>Names of all candidates (interviewees) linked to this interview.</summary>
    public List<string> CandidateNames { get; set; } = new();
    /// <summary>Names of all panelists (internal + external) linked to this interview.</summary>
    public List<string> PanelistNames { get; set; } = new();
    public Guid? QuestionPresetId { get; set; }
}

public class JobInterviewDetailDto : JobInterviewDto
{
    public List<JobIntervieweeDto> Interviewees { get; set; } = new();
    public List<JobInterviewPanelistDto> Panelists { get; set; } = new();
    public List<JobInterviewExternalPanelistDto> ExternalPanelists { get; set; } = new();
    public List<JobInterviewQuestionDto> Questions { get; set; } = new();
}

public class CreateJobInterviewDto : CreateDtoBase
{
    [Required]
    public Guid JobVacancyId { get; set; }

    [Range(1, 20)]
    public int Round { get; set; } = 1;

    public JobInterviewType Type { get; set; } = JobInterviewType.Panel;

    public InterviewMode Mode { get; set; } = InterviewMode.InPerson;

    [Required]
    public DateOnly ScheduledDate { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [MaxLength(500)]
    public string? LocationOrLink { get; set; }

    [MaxLength(2000)]
    public string? Instructions { get; set; }

    public List<Guid>? ApplicationIds { get; set; }

    /// <summary>Optional per-candidate slot overrides. Only respected for IDs that also appear in ApplicationIds.</summary>
    public List<ApplicationSlotEntry>? ApplicationSlots { get; set; }

    public List<Guid>? PanelistEmployeeIds { get; set; }
    public List<Guid>? ExternalPanelistAssociateIds { get; set; }
    public Guid? QuestionPresetId { get; set; }

    /// <summary>
    /// Why this interview may be scheduled despite a panelist's confirmed commitment
    /// (round 4, D3; decision D-5).
    /// </summary>
    /// <remarks>
    /// ⚠ Without it a HARD clash REFUSES the write — the check stopped being advisory in round 4.
    /// Supplying it schedules anyway and records the reason, who gave it and what was overridden,
    /// on the interview. A reason supplied where there is no clash is discarded.
    /// </remarks>
    [MaxLength(1000)]
    public string? PanelClashOverrideReason { get; set; }

    /// <summary>
    /// The room to HOLD for this interview, booked through the meeting-room register (D8).
    /// </summary>
    /// <remarks>
    /// ⚠ Distinct from <c>LocationOrLink</c>, which is free text and reserves nothing. A room named
    /// only in that box is invisible to the room's own double-booking check.
    /// </remarks>
    public Guid? RoomBookingId { get; set; }
}

public sealed class ApplicationSlotEntry
{
    public Guid      ApplicationId { get; set; }
    public TimeSpan? SlotStartTime { get; set; }
    public TimeSpan? SlotEndTime   { get; set; }
}

/// <summary>
/// Edits the schedule and setup of an interview.
///
/// <para><b>Status is deliberately absent.</b> It used to be copied straight onto the entity, so a plain
/// PUT could cancel an interview without a cancellation reason, complete one that never happened, or
/// mark one "Rescheduled" without moving the date, rotating the candidates' confirmation tokens or
/// re-sending a single invitation. Status belongs to <c>reschedule</c>, <c>cancel</c> and
/// <c>complete</c>, which carry those side effects — the same reasoning that took <c>Status</c> off
/// <c>UpdateAppraisalCycleDto</c>.</para>
/// </summary>
public class UpdateJobInterviewDto : UpdateDtoBase
{
    [Range(1, 20)]
    public int Round { get; set; }

    public JobInterviewType Type { get; set; }
    public InterviewMode Mode { get; set; }

    [Required]
    public DateOnly ScheduledDate { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [MaxLength(500)]
    public string? LocationOrLink { get; set; }

    [MaxLength(2000)]
    public string? Instructions { get; set; }
    public Guid? QuestionPresetId { get; set; }

    /// <inheritdoc cref="CreateJobInterviewDto.PanelClashOverrideReason"/>
    [MaxLength(1000)]
    public string? PanelClashOverrideReason { get; set; }

    /// <inheritdoc cref="CreateJobInterviewDto.RoomBookingId"/>
    public Guid? RoomBookingId { get; set; }
}

public class RescheduleJobInterviewDto
{
    [Required]
    public Guid InterviewId { get; set; }

    [Required]
    public DateOnly NewDate { get; set; }

    [Required]
    public TimeSpan NewStartTime { get; set; }

    [Required]
    public TimeSpan NewEndTime { get; set; }

    [MaxLength(500)]
    public string? LocationOrLink { get; set; }

    [Required]
    [MaxLength(2000)]
    public string RescheduleReason { get; set; } = string.Empty;

    /// <inheritdoc cref="CreateJobInterviewDto.PanelClashOverrideReason"/>
    [MaxLength(1000)]
    public string? PanelClashOverrideReason { get; set; }

    /// <inheritdoc cref="CreateJobInterviewDto.RoomBookingId"/>
    public Guid? RoomBookingId { get; set; }
}

public class CancelJobInterviewDto
{
    [Required]
    public Guid InterviewId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string CancellationReason { get; set; } = string.Empty;
}

// ── Panelist availability / conflict detection ───────────────────────────────

/// <summary>
/// Advisory availability result for a set of panelists over a proposed interview slot. Mirrors the
/// Training trainer-availability pattern. Non-blocking — surfaced as a warning in the UI.
/// </summary>
public class PanelistAvailabilityCheckDto
{
    /// <summary>True when anyone has anything at all — hard or soft.</summary>
    public bool HasConflicts { get; set; }

    /// <summary>
    /// True when at least one clash is HARD, which is what makes a schedule refuse without an
    /// override reason (round 4, decision D-5).
    /// </summary>
    public bool HasHardConflicts { get; set; }

    /// <summary>
    /// Which sources actually answered, so an empty result can be told from an unregistered source.
    /// </summary>
    /// <remarks>
    /// ⚠ Diagnostic, and load-bearing for that reason. A commitment source that exists but was never
    /// registered contributes nothing and the check cheerfully reports "free" — the exact failure
    /// this lane exists to remove, reappearing as a DI omission. If a source you expect is missing
    /// from this list, it is not wired up.
    /// </remarks>
    public List<string> SourcesConsulted { get; set; } = new();

    public List<PanelistAvailabilityDto> Panelists { get; set; } = new();
}

/// <summary>Per-panelist conflict breakdown for the proposed slot.</summary>
/// <remarks>
/// ⚠ Round 4, lane D1. The three typed lists — interviews, leave, travel — were replaced by one
/// <see cref="Commitments"/> list. There are seven sources now and there will be more; a DTO with a
/// named list per source needs editing every time the organisation learns to track something else,
/// and the screen needs editing with it.
/// </remarks>
public class PanelistAvailabilityDto
{
    /// <summary>Employee id for internal panelists, or associate id for external panelists.</summary>
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>
    /// True when this row is an external associate. Only the sources keyed on an interview or a
    /// company-wide closure can speak about them — there is no leave, travel or training on file.
    /// </summary>
    public bool IsExternal { get; set; }

    public bool HasConflicts { get; set; }
    public bool HasHardConflicts { get; set; }

    public List<PanelistCommitmentDto> Commitments { get; set; } = new();
}

/// <summary>One thing standing between this panelist and the proposed window.</summary>
public class PanelistCommitmentDto
{
    public CommitmentKind Kind { get; set; }
    public string KindName => Kind.ToString();

    public CommitmentHardness Hardness { get; set; }
    public string HardnessName => Hardness.ToString();

    /// <summary>What a recruiter reads — "Interview panel for Senior Accountant".</summary>
    public string Label { get; set; } = string.Empty;

    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    /// <summary>
    /// True when the source records whole days, so the times above are the day's bounds rather than
    /// a real window. Leave, travel, closures and all-day events.
    /// </summary>
    /// <remarks>
    /// ⚠ The screen must not print a day-granular commitment as "09:00–11:00" — that is a precision
    /// the record does not have, and it is why these are soft.
    /// </remarks>
    public bool IsDayGranular { get; set; }

    /// <summary>The record's own number, where it has one, so a recruiter can go and look.</summary>
    public string? Reference { get; set; }
}

/// <summary>
/// A window in which the whole panel is free — the answer to "then when?" (round 4, D4).
/// </summary>
/// <remarks>
/// ⚠ A slot is suggested when no panelist has a HARD commitment. Soft ones do not exclude it but
/// are reported on it: a slot where somebody is nominally on leave is still a slot HR may want, and
/// hiding it would be the system making that call on day-granular evidence.
/// </remarks>
public class PanelSlotSuggestionDto
{
    public DateOnly Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public bool HasSoftConflicts { get; set; }
    public string? SoftConflictSummary { get; set; }
}

public class PanelistInterviewConflictDto
{
    public Guid InterviewId { get; set; }
    public string InterviewNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public DateOnly ScheduledDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public JobInterviewStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class PanelistLeaveConflictDto
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PanelistTravelConflictDto
{
    public string RequestNumber { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

#endregion

#region Job Interview Panelist

public class JobInterviewPanelistDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobInterviewId { get; set; }
    public string InterviewNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeePositionTitle { get; set; }
    public JobInterviewPanelistRole Role { get; set; }
    public string RoleName => Role.ToString();
    public bool IsRequired { get; set; }
    public bool? Attended { get; set; }
    public string? NoShowReason { get; set; }
    public DateTime? InvitationSentDate { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime? ConfirmationDate { get; set; }
}

public class AddJobInterviewPanelistDto : CreateDtoBase
{
    [Required]
    public Guid JobInterviewId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public JobInterviewPanelistRole Role { get; set; }

    public bool IsRequired { get; set; }
}

public class UpdateJobInterviewPanelistDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public JobInterviewPanelistRole Role { get; set; }

    public bool IsRequired { get; set; }
}

public class RecordPanelistAttendanceDto
{
    [Required]
    public Guid PanelistId { get; set; }

    [Required]
    public bool? Attended { get; set; }

    [MaxLength(1000)]
    public string? NoShowReason { get; set; }
}

public class RecordExternalPanelistAttendanceDto
{
    [Required]
    public Guid ExtPanelistId { get; set; }

    [Required]
    public bool? Attended { get; set; }

    [MaxLength(1000)]
    public string? NoShowReason { get; set; }
}

public class ConfirmPanelistDto
{
    [Required]
    public Guid PanelistId { get; set; }

    public bool IsConfirmed { get; set; } = true;
}

#endregion

#region Job Interview External Panelist

public class JobInterviewExternalPanelistDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobInterviewId { get; set; }
    public string InterviewNumber { get; set; } = string.Empty;
    public Guid AssociateId { get; set; }
    public string AssociateName { get; set; } = string.Empty;
    public string? AssociateOrganization { get; set; }
    public JobInterviewPanelistRole Role { get; set; }
    public string RoleName => Role.ToString();
    public bool IsRequired { get; set; }
    public bool? Attended { get; set; }
    public string? NoShowReason { get; set; }
    public DateTime? InvitationSentDate { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime? ConfirmationDate { get; set; }
}

public class AddJobInterviewExternalPanelistDto : CreateDtoBase
{
    [Required]
    public Guid JobInterviewId { get; set; }

    [Required]
    public Guid AssociateId { get; set; }

    [Required]
    public JobInterviewPanelistRole Role { get; set; }

    public bool IsRequired { get; set; }
}

public class UpdateJobInterviewExternalPanelistDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public JobInterviewPanelistRole Role { get; set; }

    public bool IsRequired { get; set; }
}

#endregion

#region Job Interviewee

public class JobIntervieweeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobInterviewId { get; set; }
    public string InterviewNumber { get; set; } = string.Empty;
    public Guid JobApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;
    public Guid? JobCandidateId { get; set; }
    public TimeSpan? SlotStartTime { get; set; }
    public TimeSpan? SlotEndTime { get; set; }
    public DateTime? InvitationSentDate { get; set; }
    public bool? ConfirmedAttendance { get; set; }
    public DateTime? ConfirmationDate { get; set; }
    public bool? CandidateAttended { get; set; }
    public string? NoShowReason { get; set; }
    public JobInterviewOutcome? Outcome { get; set; }
    public string? OutcomeName => Outcome?.ToString();
}

public class JobIntervieweeSummaryDto
{
    public Guid Id { get; set; }
    public Guid JobInterviewId { get; set; }
    public string InterviewNumber { get; set; } = string.Empty;
    public DateOnly ScheduledDate { get; set; }
    public JobInterviewStatus InterviewStatus { get; set; }
    public string InterviewStatusName => InterviewStatus.ToString();
    public bool? CandidateAttended { get; set; }
    public JobInterviewOutcome? Outcome { get; set; }
    public string? OutcomeName => Outcome?.ToString();
}

public class AddJobIntervieweeDto : CreateDtoBase
{
    [Required]
    public Guid JobInterviewId { get; set; }

    [Required]
    public Guid JobApplicationId { get; set; }
}

/// <summary>Request body for POST /api/job-interviews/{id}/send-invites.</summary>
public class SendInterviewInvitesDto
{
    /// <summary>
    /// Application IDs to send invites to.
    /// When empty, invites are sent to ALL interviewees of the interview.
    /// </summary>
    public List<Guid> ApplicationIds { get; set; } = new();
}

/// <summary>Per-candidate result row returned by the send-invites endpoint.</summary>
public class InterviewInviteResultItemDto
{
    public Guid    ApplicationId { get; set; }
    public string  CandidateName { get; set; } = string.Empty;
    public bool    Sent          { get; set; }
    public string? Error         { get; set; }
}

/// <summary>Aggregate result returned by POST /api/job-interviews/{id}/send-invites.</summary>
public class SendInterviewInvitesResultDto
{
    public int                             TotalRequested { get; set; }
    public int                             Sent           { get; set; }
    public int                             Skipped        { get; set; }
    public List<InterviewInviteResultItemDto> Results     { get; set; } = new();
}

/// <summary>Request body for POST /api/job-interviews/{id}/panelist-notifications.</summary>
public class SendPanelistNotificationsDto
{
    /// <summary>
    /// Employee IDs of internal panelists to notify.
    /// null  = notify all internal panelists.
    /// empty = skip internal panelists entirely.
    /// </summary>
    public List<Guid>? EmployeeIds { get; set; }

    /// <summary>
    /// Associate IDs of external panelists to notify.
    /// null  = notify all external panelists.
    /// empty = skip external panelists entirely.
    /// </summary>
    public List<Guid>? ExternalAssociateIds { get; set; }
}

public class PanelistNotificationResultItemDto
{
    public Guid    PersonId   { get; set; } // EmployeeId or AssociateId
    public string  Name       { get; set; } = string.Empty;
    public bool    IsExternal { get; set; }
    public bool    Sent       { get; set; }
    public string? Error      { get; set; }
}

public class SendPanelistNotificationsResultDto
{
    public int TotalRequested { get; set; }
    public int Sent           { get; set; }
    public int Skipped        { get; set; }
    public List<PanelistNotificationResultItemDto> Results { get; set; } = new();
}

public class UpdateIntervieweeSlotDto
{
    [Required]
    public Guid IntervieweeId { get; set; }

    public TimeSpan? SlotStartTime { get; set; }
    public TimeSpan? SlotEndTime   { get; set; }
}

// ── Slot apportionment (round 4, lane C) ────────────────────────────────────────────────────────

/// <summary>One rest period inside the interview window that no candidate may be booked into.</summary>
public class InterviewBreakDto
{
    [Required] public TimeSpan Start { get; set; }
    [Required] public TimeSpan End   { get; set; }

    /// <summary>What the break is for — printed on the timetable. "Lunch", "Panel conference".</summary>
    [MaxLength(100)]
    public string? Label { get; set; }
}

/// <summary>
/// Asks what a day would look like at a given interval, without writing anything.
/// </summary>
/// <remarks>
/// A dry run on purpose: a recruiter changes the interval three times before they like the shape of
/// the day, and each attempt must not rewrite nine candidates' times — nor send anybody anything.
/// </remarks>
public class ApportionInterviewSlotsDto
{
    [Required]
    public Guid InterviewId { get; set; }

    [Range(InterviewSlotLimits.MinSlotMinutes, InterviewSlotLimits.MaxSlotMinutes)]
    public int SlotMinutes { get; set; } = 30;

    [Range(0, InterviewSlotLimits.MaxBufferMinutes)]
    public int BufferMinutes { get; set; }

    public List<InterviewBreakDto> Breaks { get; set; } = new();

    /// <summary>
    /// The candidates to place, in the order they should be seen. Omit to place everyone currently
    /// booked into the session, in the order they were added.
    /// </summary>
    public List<Guid>? ApplicationIds { get; set; }
}

/// <summary>The bounds shared by the DTO attributes and the apportioner, so the two cannot drift.</summary>
public static class InterviewSlotLimits
{
    public const int MinSlotMinutes = 5;
    public const int MaxSlotMinutes = 480;
    public const int MaxBufferMinutes = 120;
}

/// <summary>One candidate's place in the day.</summary>
public class InterviewSlotAssignmentDto
{
    public Guid IntervieweeId { get; set; }
    public Guid JobApplicationId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string ApplicationNumber { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public TimeSpan SlotStartTime { get; set; }
    public TimeSpan SlotEndTime { get; set; }
}

/// <summary>
/// The timetable, and — the reason this endpoint exists — whether everybody actually fits.
/// </summary>
public class InterviewSlotPlanDto
{
    public Guid InterviewId { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public TimeSpan WindowStart { get; set; }
    public TimeSpan WindowEnd { get; set; }
    public int SlotMinutes { get; set; }
    public int BufferMinutes { get; set; }
    public List<InterviewBreakDto> Breaks { get; set; } = new();

    public List<InterviewSlotAssignmentDto> Slots { get; set; } = new();

    /// <summary>Candidates the window could not hold, in the order they would have been seen.</summary>
    public List<InterviewSlotAssignmentDto> Unplaced { get; set; } = new();

    /// <summary>True when every candidate fits. The single fact the screen leads with.</summary>
    public bool AllFit { get; set; }

    /// <summary>
    /// The first time of day after the window that is not inside a break — where a second session
    /// could pick up. Null when everybody fits. The DATE is the recruiter's to choose.
    /// </summary>
    public TimeSpan? FirstFreeAfterWindow { get; set; }

    /// <summary>A sentence a recruiter can act on without reading the table.</summary>
    public string Summary { get; set; } = string.Empty;
}

public class RecordIntervieweeAttendanceDto
{
    [Required]
    public Guid IntervieweeId { get; set; }

    [Required]
    public bool? Attended { get; set; }

    [MaxLength(1000)]
    public string? NoShowReason { get; set; }
}

public class RecordIntervieweeOutcomeDto
{
    [Required]
    public Guid IntervieweeId { get; set; }

    [Required]
    public JobInterviewOutcome Outcome { get; set; }
}

/// <summary>Result returned by GET /api/job-interviews/confirm-attendance/{token}.</summary>
public class ConfirmInterviewAttendanceResultDto
{
    public bool     AlreadyConfirmed { get; set; }
    public string   CandidateName   { get; set; } = string.Empty;
    public string   JobTitle        { get; set; } = string.Empty;
    public DateOnly ScheduledDate   { get; set; }
    public string   InterviewNumber { get; set; } = string.Empty;
}

public class ConfirmPanelistAssignmentResultDto
{
    public bool     AlreadyConfirmed { get; set; }
    public string   PanelistName     { get; set; } = string.Empty;
    public string   JobTitle         { get; set; } = string.Empty;
    public DateOnly ScheduledDate    { get; set; }
    public string   InterviewNumber  { get; set; } = string.Empty;
    public bool     IsExternal       { get; set; }
}

#endregion

#region Job Interview Question (per-interview question plan)

public class JobInterviewQuestionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobInterviewId { get; set; }
    public string InterviewNumber { get; set; } = string.Empty;
    public Guid QuestionTypeId { get; set; }
    public string QuestionTypeName { get; set; } = string.Empty;
    public int RequiredQuestionCount { get; set; }
    public int AllowedPoolSize { get; set; }
    public int DisplayOrder { get; set; }
    public List<JobInterviewSelectedQuestionDto> SelectedQuestions { get; set; } = new();
}

public class CreateJobInterviewQuestionDto : CreateDtoBase
{
    [Required]
    public Guid JobInterviewId { get; set; }

    [Required]
    public Guid QuestionTypeId { get; set; }

    [Range(1, 20)]
    public int RequiredQuestionCount { get; set; } = 2;

    [Range(1, 50)]
    public int AllowedPoolSize { get; set; } = 8;

    public int DisplayOrder { get; set; }
}

public class UpdateJobInterviewQuestionDto : UpdateDtoBase
{
    [Range(1, 20)]
    public int RequiredQuestionCount { get; set; }

    [Range(1, 50)]
    public int AllowedPoolSize { get; set; }

    public int DisplayOrder { get; set; }
}

#endregion

#region Job Interview Selected Question

public class JobInterviewSelectedQuestionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobInterviewQuestionId { get; set; }
    public Guid QuestionDetailId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int Weight { get; set; }
    public int MinScore { get; set; }
    public int MaxScore { get; set; }
    /// <summary>
    /// What a good answer sounds like. Carried here because this is the shape the printed scoring
    /// sheet renders from — the drawn questions, not the bank.
    /// </summary>
    public string? ScoringGuide { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateJobInterviewSelectedQuestionDto : CreateDtoBase
{
    [Required]
    public Guid JobInterviewQuestionId { get; set; }

    [Required]
    public Guid QuestionDetailId { get; set; }

    public int DisplayOrder { get; set; }
}

#endregion

#region Job Interview Score Summary and Entries

public class JobInterviewScoreSummaryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobIntervieweeId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string ApplicationNumber { get; set; } = string.Empty;
    public Guid? InternalPanelistId { get; set; }
    public string? InternalPanelistName { get; set; }
    public Guid? ExternalPanelistId { get; set; }
    public string? ExternalPanelistName { get; set; }
    public string PanelistName => InternalPanelistName ?? ExternalPanelistName ?? string.Empty;
    public decimal TotalRawScore { get; set; }
    public decimal TotalWeightedScore { get; set; }
    public JobInterviewRecommendation Recommendation { get; set; }
    public string RecommendationName => Recommendation.ToString();
    public string? Comments { get; set; }
    public DateTime EvaluationDate { get; set; }
    public bool IsFinalized { get; set; }
    public DateTime? FinalizedDate { get; set; }

    /// <summary>
    /// Typed by the panelist, or transcribed by HR from a signed paper sheet. Null on scorecards
    /// recorded before this was tracked — which is not a claim that they were filed online.
    /// </summary>
    public InterviewScoreSource? ScoreSource { get; set; }

    /// <summary>
    /// The HR person who filed it on the panelist's behalf. Null when the panelist did.
    /// ⚠ An <b>employee</b> id — see the entity's note on why it is not a user id.
    /// </summary>
    public Guid? FiledByHrOnBehalfOfEmployeeId { get; set; }

    /// <summary>Their name, when the read loaded it. Null when nobody filed on anybody's behalf.</summary>
    public string? FiledByHrOnBehalfOfName { get; set; }

    /// <summary>
    /// A one-line statement of provenance for the screen, so the badge does not have to be
    /// reassembled from three nullable fields at every call site.
    /// </summary>
    /// <remarks>
    /// ⚠ It says <i>filed on their behalf</i>, not <i>transcribed from a signed sheet</i>. The paper
    /// sheet is how this normally happens and is what the screen offers, but the only thing the
    /// server actually witnessed is that somebody other than the panelist pressed the button. An
    /// audit line should claim exactly that and no more.
    /// </remarks>
    public string? FiledOnBehalfNote => ScoreSource == InterviewScoreSource.PaperSheet
        ? string.IsNullOrWhiteSpace(FiledByHrOnBehalfOfName)
            ? $"Filed by HR on behalf of {PanelistName}"
            : $"Filed by {FiledByHrOnBehalfOfName} on behalf of {PanelistName}"
        : null;
}

public class JobInterviewScoreSummaryDetailDto : JobInterviewScoreSummaryDto
{
    public List<JobInterviewScoreEntryDto> ScoreEntries { get; set; } = new();
}

public class CreateJobInterviewScoreSummaryDto : CreateDtoBase
{
    [Required]
    public Guid JobIntervieweeId { get; set; }

    public Guid? InternalPanelistId { get; set; }
    public Guid? ExternalPanelistId { get; set; }

    [Required]
    public JobInterviewRecommendation Recommendation { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }

    public DateTime EvaluationDate { get; set; } = DateTime.UtcNow;
    public List<CreateJobInterviewScoreEntryDto> ScoreEntries { get; set; } = new();

    // ⚠ There is deliberately NO ScoreSource here. Provenance is derived by the service from who is
    // calling — a panelist filing their own card is Online, anyone filing somebody else's is not —
    // because a source the client could assert freely would be worth nothing in an audit, and a
    // field the server ignores reads to the next developer as a control that exists when it does
    // not. The "from a paper sheet" mode on the scorecard screen is a workflow, not a flag.
}

public class FinalizeInterviewScoreDto
{
    [Required]
    public Guid ScoreSummaryId { get; set; }
}

public class JobInterviewScoreEntryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ScoreSummaryId { get; set; }
    public Guid QuestionDetailId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public decimal RawScore { get; set; }
    public decimal WeightedScore { get; set; }
    public string? Remarks { get; set; }
}

public class CreateJobInterviewScoreEntryDto : CreateDtoBase
{
    public Guid ScoreSummaryId { get; set; }

    [Required]
    public Guid QuestionDetailId { get; set; }

    [Required]
    [Range(0, 100)]
    public decimal RawScore { get; set; }

    [MaxLength(1000)]
    public string? Remarks { get; set; }
}

// ── Score Drafts ──────────────────────────────────────────────────────────────

/// <summary>A single question's draft score entry (partial — RawScore may be null).</summary>
public class DraftScoreEntryDto
{
    public Guid     QuestionDetailId { get; set; }
    public decimal? RawScore         { get; set; }
    public string?  Remarks          { get; set; }
}

/// <summary>Payload sent by the Blazor client to save or update a panelist's draft.</summary>
public class SaveInterviewScoreDraftDto
{
    [Required]
    public Guid JobIntervieweeId { get; set; }

    public Guid? InternalPanelistId { get; set; }
    public Guid? ExternalPanelistId { get; set; }

    public List<DraftScoreEntryDto> ScoreEntries { get; set; } = new();

    [MaxLength(2000)]
    public string? Comments        { get; set; }

    public int?    Recommendation  { get; set; }
}

/// <summary>Read DTO returned by the GET / PUT draft endpoints.</summary>
public class InterviewScoreDraftDto : BaseDto
{
    public Guid    JobInterviewId     { get; set; }
    public Guid    JobIntervieweeId   { get; set; }
    public Guid?   InternalPanelistId { get; set; }
    public Guid?   ExternalPanelistId { get; set; }
    public List<DraftScoreEntryDto> ScoreEntries { get; set; } = new();
    public string? Comments           { get; set; }
    public int?    Recommendation     { get; set; }
    public DateTime LastModified      { get; set; }
}

#region Question Preview & Commit

/// <summary>One question returned by a dry-run question randomisation (no DB write).</summary>
public class QuestionPreviewItemDto
{
    public Guid   QuestionDetailId { get; set; }
    public string QuestionText     { get; set; } = string.Empty;
    public int    Weight           { get; set; }
    public int    MinScore         { get; set; }
    public int    MaxScore         { get; set; }
    public int    DisplayOrder     { get; set; }
}

/// <summary>One question-plan's proposed questions from a dry-run randomisation.</summary>
public class QuestionPlanPreviewDto
{
    public Guid   PlanId               { get; set; }
    public Guid   QuestionTypeId       { get; set; }
    public string QuestionTypeName     { get; set; } = string.Empty;
    public int    RequiredQuestionCount { get; set; }
    public int    AllowedPoolSize       { get; set; }
    public int    DisplayOrder          { get; set; }

    /// <summary>
    /// Active questions of this type in the bank. The draw is capped at <c>AllowedPoolSize</c>, but the
    /// bank may hold fewer than the plan requires — nothing surfaced that, so the shortfall only showed
    /// up when the panel ran out of questions in the room.
    /// </summary>
    public int    AvailableQuestionCount { get; set; }

    /// <summary>False when the bank cannot supply <c>RequiredQuestionCount</c> questions of this type.</summary>
    public bool   MeetsRequiredCount     { get; set; }

    public List<QuestionPreviewItemDto> Questions { get; set; } = new();
}

/// <summary>One plan's explicit list of question-detail IDs to commit as selections.</summary>
public class QuestionPlanSelectionDto
{
    [Required]
    public Guid       PlanId            { get; set; }
    public List<Guid> QuestionDetailIds { get; set; } = new();
}

/// <summary>Payload for POST {id}/commit-questions — saves user-confirmed question selections.</summary>
public class CommitInterviewQuestionsDto
{
    [Required]
    public List<QuestionPlanSelectionDto> Plans { get; set; } = new();
}

#endregion

#endregion

// ============================================================================
// SECTION 12 — JOB OFFER
// ============================================================================

#region Job Offer

public class JobOfferDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string OfferNumber { get; set; } = string.Empty;
    public Guid JobApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public JobOfferStatus OfferStatus { get; set; }
    public string OfferStatusName => OfferStatus.ToString();
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public string ReportsToTitle { get; set; } = string.Empty;
    public string GradeTitle { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public WorkMode WorkMode { get; set; }
    public string WorkModeName => WorkMode.ToString();
    public decimal? SalaryGradeMin { get; set; }
    public decimal? SalaryGradeMax { get; set; }
    public Guid? SalaryLevelId { get; set; }
    public string? SalaryLevelName { get; set; }
    public Guid? SalaryNotchId { get; set; }
    public int? SalaryNotchNumber { get; set; }
    public decimal? SalaryNotchAmount { get; set; }
    public int? ProbationPeriodMonths { get; set; }
    public int? NoticePeriodMonths { get; set; }
    public int? AnnualLeaveDays { get; set; }
    public decimal? WeeklyHours { get; set; }
    public bool NdaRequired { get; set; }
    public Guid? LocationLevelId { get; set; }
    public string? LocationLevelName { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public string EmploymentTypeName => EmploymentType.ToString();
    public int? ContractDurationMonths { get; set; }
    public decimal? BaseSalary { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? Bonus { get; set; }
    public string? BonusTerms { get; set; }
    public decimal? Commission { get; set; }
    public string? CommissionStructure { get; set; }
    public IEnumerable<JobOfferBenefitDto> Benefits { get; set; } = new List<JobOfferBenefitDto>();
    public DateOnly? ProposedStartDate { get; set; }
    public string? AdditionalTerms { get; set; }
    public Guid? PreparedById { get; set; }
    public string? PreparedByName { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? OfferDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? OfferLetterPath { get; set; }
    public string? SignedOfferLetterPath { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public DateTime? CounteredDate { get; set; }
    public string? CandidateResponseNotes { get; set; }
    public DateTime? DeclinedDate { get; set; }
    public string? DeclineReason { get; set; }
    public DateTime? RevokedDate { get; set; }
    public string? RevocationReason { get; set; }
    public string? ApprovalRejectionReason { get; set; }
    public int Version { get; set; }
    public Guid? PreviousOfferId { get; set; }
    public bool IsConditional { get; set; }
    public Guid? PreEmploymentCheckId { get; set; }
    public PreEmploymentCheckStatus? PreEmploymentCheckStatus { get; set; }
    public string? PreEmploymentCheckStatusName => PreEmploymentCheckStatus?.ToString();
    public Guid CandidateId { get; set; }
}

public class JobOfferSummaryDto
{
    public Guid Id { get; set; }
    public string OfferNumber { get; set; } = string.Empty;
    public Guid JobApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public JobOfferStatus OfferStatus { get; set; }
    public string OfferStatusName => OfferStatus.ToString();
    public string PositionTitle { get; set; } = string.Empty;
    public EmploymentType EmploymentType { get; set; }
    public string EmploymentTypeName => EmploymentType.ToString();
    public decimal? BaseSalary { get; set; }
    public string? CurrencyCode { get; set; }
    public DateOnly? ProposedStartDate { get; set; }
    public DateTime? OfferDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Version { get; set; }

    /// <summary>
    /// False when a revision has replaced this version.
    /// </summary>
    /// <remarks>
    /// Added 2026-09-15 for G-10.2 / G-15.2. <c>Version</c> alone cannot answer "is this the live
    /// offer?" — a v1 is the live offer until a v2 exists — so every reader that wanted to exclude
    /// superseded offers had to load the whole chain or guess. The dashboard's "offers pending
    /// response" guessed, and counted them.
    /// </remarks>
    public bool IsLatestVersion { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Raising an offer against an application. Carries the <b>negotiated terms only</b>.
///
/// <para>⚠ <c>PositionId</c>, <c>PositionTitle</c>, <c>ReportsToTitle</c>, <c>GradeTitle</c> and
/// <c>EmploymentType</c> have been REMOVED. All five were <c>[Required]</c>, and
/// <c>CreateAsync</c> overwrote every one of them from the application's vacancy and position as
/// "always server-authoritative" — so a caller was forced to supply values that were guaranteed to
/// be discarded. A vacancy's <c>PositionId</c> is non-nullable and the seeding query includes it,
/// so the fallback branch that would have used these never runs.</para>
///
/// <para>That is the same failure as naming the approver in <c>ApproveJobOfferDto</c>: a field the
/// caller believes is doing something and which is silently ignored. Removed rather than merely
/// un-required, and with unmapped members disallowed, so anyone still sending them is told.</para>
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateJobOfferDto : CreateDtoBase
{
    [Required]
    public Guid JobApplicationId { get; set; }

    public Guid? LocationLevelId { get; set; }
    public Guid? LocationId { get; set; }

    public int? ContractDurationMonths { get; set; }
    public int? ProbationPeriodMonths { get; set; }
    public int? NoticePeriodMonths { get; set; }
    public int? AnnualLeaveDays { get; set; }
    public decimal? WeeklyHours { get; set; }
    public bool NdaRequired { get; set; }
    public bool IsConditional { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal? BaseSalary { get; set; }

    public Guid? SalaryLevelId { get; set; }

    public Guid? SalaryNotchId { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    public decimal? Bonus { get; set; }

    [MaxLength(2000)]
    public string? BonusTerms { get; set; }

    public decimal? Commission { get; set; }

    [MaxLength(2000)]
    public string? CommissionStructure { get; set; }

    public DateOnly? ProposedStartDate { get; set; }

    [MaxLength(5000)]
    public string? AdditionalTerms { get; set; }
}

/// <summary>
/// Editing a draft or pending-approval offer. Like <see cref="CreateJobOfferDto"/>, the
/// <b>negotiated terms only</b>.
///
/// <para>⚠ <c>PositionTitle</c>, <c>ReportsToTitle</c>, <c>GradeTitle</c>, <c>EmploymentType</c> and
/// <c>WorkMode</c> have been REMOVED — and here they were worse than on create. Create discarded
/// them and took the role snapshot from the position; <b>update wrote them straight onto the
/// entity</b>, so an editor could rewrite the position title, the reporting line, the grade and the
/// employment type of an offer to anything at all, and the record would no longer describe the role
/// it was raised against. The two paths contradicted each other about who owns these values; the
/// position owns them.</para>
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdateJobOfferDto : UpdateDtoBase
{
    public Guid? LocationLevelId { get; set; }
    public Guid? LocationId { get; set; }

    public int? ContractDurationMonths { get; set; }
    public int? ProbationPeriodMonths { get; set; }
    public int? NoticePeriodMonths { get; set; }
    public int? AnnualLeaveDays { get; set; }
    public decimal? WeeklyHours { get; set; }
    public bool NdaRequired { get; set; }
    public bool IsConditional { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal? BaseSalary { get; set; }

    public Guid? SalaryLevelId { get; set; }

    public Guid? SalaryNotchId { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    public decimal? Bonus { get; set; }

    [MaxLength(2000)]
    public string? BonusTerms { get; set; }

    public decimal? Commission { get; set; }

    [MaxLength(2000)]
    public string? CommissionStructure { get; set; }

    public DateOnly? ProposedStartDate { get; set; }

    [MaxLength(5000)]
    public string? AdditionalTerms { get; set; }
}

/// <summary>
/// Issuing an approved offer to the candidate: sets the dates, mints their single-use response
/// token and emails them the link.
///
/// <para>⚠ <c>OfferLetterPath</c> is gone. It let the caller write an arbitrary server path onto the
/// offer, which is the same shape the seven recruitment attachment paths were fixed out of — the
/// letter is uploaded through <c>upload-letter</c>, which runs the controlled-upload gate and
/// registers the file in the DMS.</para>
/// </summary>
/// <remarks>
/// ⚠ <c>[JsonUnmappedMemberHandling(Disallow)]</c>: a field that was REMOVED from this payload
/// because it let the caller decide something they should not is refused with a 400 rather than
/// quietly dropped. Silently ignoring it is worse than rejecting it — the caller believes the value
/// took effect, which for an approver, a date or an attachment is precisely the misunderstanding
/// that hides a bug.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class IssueJobOfferDto
{
    [Required]
    public Guid OfferId { get; set; }

    public DateTime OfferDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiryDate { get; set; }
}

public class RecordOfferResponseDto
{
    [Required]
    public Guid OfferId { get; set; }

    [Required]
    public JobOfferStatus Response { get; set; }

    [MaxLength(2000)]
    public string? CandidateResponseNotes { get; set; }
}

public class RevokeJobOfferDto
{
    [Required]
    public Guid OfferId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string RevocationReason { get; set; } = string.Empty;
}

/// <summary>
/// Approving an offer that is out for approval.
///
/// <para>⚠ <c>ApprovedById</c> and <c>ApprovedDate</c> are gone. The approver used to be named in
/// the body — so a caller could record someone else as having approved — and the date came with it,
/// letting an approval be dated to whenever suited. Both now come from the token and the server
/// clock. What remains is the approver's own comment, which the workflow engine records on the
/// history row.</para>
/// </summary>
/// <remarks>
/// ⚠ <c>[JsonUnmappedMemberHandling(Disallow)]</c>: a field that was REMOVED from this payload
/// because it let the caller decide something they should not is refused with a 400 rather than
/// quietly dropped. Silently ignoring it is worse than rejecting it — the caller believes the value
/// took effect, which for an approver, a date or an attachment is precisely the misunderstanding
/// that hides a bug.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ApproveJobOfferDto
{
    [Required]
    public Guid OfferId { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

public class SubmitForApprovalDto
{
    [Required]
    public Guid OfferId { get; set; }
}

public class RejectJobOfferDto
{
    [Required]
    public Guid OfferId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string RejectionReason { get; set; } = string.Empty;
}

/// <summary>Returned to a candidate when they validate their offer token.</summary>
public class CandidateOfferSummaryDto
{
    public Guid OfferId           { get; set; }
    public string OfferNumber     { get; set; } = string.Empty;
    public string PositionTitle   { get; set; } = string.Empty;
    public string? BaseSalary     { get; set; }  // formatted string, e.g. "GHS 45,000.00"
    public string? StartDate      { get; set; }  // formatted string
    public string? ExpiryDate     { get; set; }  // formatted string
    public string? AdditionalTerms{ get; set; }
    public bool   IsConditional   { get; set; }
    public string? OfferLetterUrl { get; set; }
    public bool   TokenExpired    { get; set; }
    public bool   TokenAlreadyUsed{ get; set; }
}

/// <summary>Submitted by the candidate via the public response page.</summary>
public class CandidateResponseDto
{
    [Required]
    public Guid Token { get; set; }

    /// <summary>Must map to Accepted, Negotiating, or Declined.</summary>
    [Required]
    public JobOfferStatus Response { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }

    [MaxLength(2000)]
    public string? DeclineReason { get; set; }
}

/// <summary>Offer details returned to the candidate via the authenticated portal.</summary>
public class CandidatePortalOfferDto
{
    public Guid   Id               { get; set; }
    public string OfferNumber      { get; set; } = string.Empty;
    public string PositionTitle    { get; set; } = string.Empty;
    public string? DepartmentName  { get; set; }
    public string? ReportsToTitle  { get; set; }
    public string? GradeTitle      { get; set; }
    public string? LocationName    { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public string EmploymentTypeName => EmploymentType.ToString();
    public int? ContractDurationMonths { get; set; }
    public WorkMode WorkMode       { get; set; }
    public string WorkModeName     => WorkMode.ToString();
    public decimal? BaseSalary     { get; set; }
    public string?  CurrencyCode   { get; set; }
    public decimal? Bonus          { get; set; }
    public string?  BonusTerms     { get; set; }
    public decimal? Commission     { get; set; }
    public string?  CommissionStructure { get; set; }
    public IEnumerable<JobOfferBenefitDto> Benefits { get; set; } = new List<JobOfferBenefitDto>();
    public int? ProbationPeriodMonths { get; set; }
    public int? NoticePeriodMonths    { get; set; }
    public int? AnnualLeaveDays       { get; set; }
    public decimal? WeeklyHours       { get; set; }
    public bool   NdaRequired      { get; set; }
    public DateOnly? ProposedStartDate { get; set; }
    public DateTime? ExpiryDate    { get; set; }
    public string? AdditionalTerms { get; set; }
    public string? OfferLetterPath { get; set; }
    public bool   IsConditional    { get; set; }
    public JobOfferStatus OfferStatus { get; set; }
    public string? CandidateResponseNotes { get; set; }
    public DateTime? AcceptedDate  { get; set; }
    public DateTime? DeclinedDate  { get; set; }
}

/// <summary>
/// Submitted by the candidate via the authenticated portal to record their
/// accept / negotiate / decline response.
/// </summary>
public class CandidatePortalOfferResponseDto
{
    /// <summary>Must be Accepted, Negotiating, or Declined.</summary>
    [Required]
    public JobOfferStatus Response { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }

    [MaxLength(2000)]
    public string? DeclineReason { get; set; }
}

#endregion

// ============================================================================
// SECTION 13 — JOB HIRE RECORD
// ============================================================================

#region Job Hire Record

public class JobHireRecordDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string HireNumber { get; set; } = string.Empty;
    public Guid ApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public Guid OfferId { get; set; }
    public string OfferNumber { get; set; } = string.Empty;
    public JobHireStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly ExpectedStartDate { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public Guid? ConfirmedById { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateTime? ConfirmedDate { get; set; }
    public string? Notes { get; set; }
}

public class JobHireRecordSummaryDto
{
    public Guid Id { get; set; }
    public string HireNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public JobHireStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly ExpectedStartDate { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public string? EmployeeNumber { get; set; }
}

public class CreateJobHireRecordDto : CreateDtoBase
{
    [Required]
    public Guid ApplicationId { get; set; }

    [Required]
    public Guid OfferId { get; set; }

    [Required]
    public DateOnly ExpectedStartDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateJobHireRecordStatusDto
{
    [Required]
    public Guid HireRecordId { get; set; }

    [Required]
    public JobHireStatus NewStatus { get; set; }

    public Guid? EmployeeId { get; set; }
    public DateOnly? ActualStartDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// SECTION 14 — PRE-EMPLOYMENT CHECK
// ============================================================================

#region Pre-Employment Check

public class PreEmploymentCheckDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobOfferId { get; set; }
    public string OfferNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public JobOfferStatus OfferStatus { get; set; }
    public PreEmploymentCheckStatus OverallStatus { get; set; }
    public string OverallStatusName => OverallStatus.ToString();
    public Guid? CoordinatedById { get; set; }
    public string? CoordinatedByName { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Notes { get; set; }
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public int PassedItems { get; set; }
    public int FailedItems { get; set; }
}

public class PreEmploymentCheckDetailDto : PreEmploymentCheckDto
{
    public List<PreEmploymentCheckItemDto> Items { get; set; } = new();
}

public class CreatePreEmploymentCheckDto : CreateDtoBase
{
    [Required]
    public Guid JobOfferId { get; set; }

    public Guid? CoordinatedById { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public List<CreatePreEmploymentCheckItemDto> Items { get; set; } = new();
}

#endregion

#region Pre-Employment Check Item

public class PreEmploymentCheckItemDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PreEmploymentCheckId { get; set; }
    public PreEmploymentCheckType CheckType { get; set; }
    public string CheckTypeName => CheckType.ToString();
    public string? Name { get; set; }
    public string DisplayName => !string.IsNullOrEmpty(Name) ? Name : CheckTypeName;
    public string? ServiceProviderName { get; set; }
    /// <summary>The supplier behind the provider name, when it is one (round 3, lane G; D-14).</summary>
    public Guid? ServiceProviderSupplierId { get; set; }
    public CheckItemStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? RequestedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool? Passed { get; set; }
    public string? Instructions { get; set; }
    public string? Remarks { get; set; }
    // ⚠ The raw storage path is deliberately not exposed. It is a server filesystem location, of no
    // use to a client and of some use to an attacker; the file is fetched from the download route.
    /// <summary>True once evidence has been uploaded through the controlled-upload gate.</summary>
    public bool HasDocument { get; set; }

    /// <summary>Original file name of the uploaded evidence, for display and download.</summary>
    public string? DocumentFileName { get; set; }
    public int? ExpectedDays { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsBlockingOnFail { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public bool HasReferenceResponse { get; set; }
}

public class CreatePreEmploymentCheckItemDto : CreateDtoBase
{
    public Guid PreEmploymentCheckId { get; set; }

    [Required]
    public PreEmploymentCheckType CheckType { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>Typed when the provider is not a supplier on file; mirrored from the supplier when one is named.</summary>
    [MaxLength(200)]
    public string? ServiceProviderName { get; set; }

    /// <summary>A Procurement supplier (round 3, lane G; D-14) — must be the tenant's, live; refused otherwise.</summary>
    public Guid? ServiceProviderSupplierId { get; set; }

    [MaxLength(2000)]
    public string? Instructions { get; set; }

    public bool IsMandatory { get; set; } = true;
    public bool IsBlockingOnFail { get; set; } = true;
    public int? ExpectedDays { get; set; }
}

/// <remarks>
/// ⚠ <c>[JsonUnmappedMemberHandling(Disallow)]</c>: a field that was REMOVED from this payload
/// because it let the caller decide something they should not is refused with a 400 rather than
/// quietly dropped. Silently ignoring it is worse than rejecting it — the caller believes the value
/// took effect, which for an approver, a date or an attachment is precisely the misunderstanding
/// that hides a bug.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdatePreEmploymentCheckItemDto : UpdateDtoBase
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(200)]
    public string? ServiceProviderName { get; set; }

    /// <summary>A Procurement supplier (round 3, lane G; D-14); null keeps the typed name only.</summary>
    public Guid? ServiceProviderSupplierId { get; set; }

    public CheckItemStatus Status { get; set; }
    public DateTime? RequestedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool? Passed { get; set; }

    [MaxLength(2000)]
    public string? Instructions { get; set; }

    [MaxLength(2000)]
    public string? Remarks { get; set; }

    // ⚠ DocumentPath removed: evidence is uploaded through the controlled-upload gate, not named by
    // the caller. See PreEmploymentCheckItem.DocumentPath.

    public int? ExpectedDays { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedDate { get; set; }
}

#endregion

#region Reference Check Response

public class ReferenceCheckResponseDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid CheckItemId { get; set; }
    public Guid? RefereeId { get; set; }
    public string RefereeName { get; set; } = string.Empty;
    public string RefereeOrganisation { get; set; } = string.Empty;
    public string RefereePosition { get; set; } = string.Empty;
    public string RefereeEmail { get; set; } = string.Empty;
    public string? RefereePhone { get; set; }
    public DateTime? ResponseDate { get; set; }
    public ReferenceResponseMethod ResponseMethod { get; set; }
    public string ResponseMethodName => ResponseMethod.ToString();
    public ReferenceRating? OverallRating { get; set; }
    public string? OverallRatingName => OverallRating?.ToString();
    public string? Comments { get; set; }
    public bool? WouldRehire { get; set; }
    public bool? ConfirmedDatesOfEmployment { get; set; }
    public bool? ConfirmedPositionHeld { get; set; }
    public bool? ConfirmedReasonForLeaving { get; set; }
    /// <summary>True once evidence has been uploaded through the controlled-upload gate.</summary>
    public bool HasDocument { get; set; }

    /// <summary>Original file name of the uploaded evidence, for display and download.</summary>
    public string? DocumentFileName { get; set; }
}

/// <remarks>
/// ⚠ <c>[JsonUnmappedMemberHandling(Disallow)]</c>: a field that was REMOVED from this payload
/// because it let the caller decide something they should not is refused with a 400 rather than
/// quietly dropped. Silently ignoring it is worse than rejecting it — the caller believes the value
/// took effect, which for an approver, a date or an attachment is precisely the misunderstanding
/// that hides a bug.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateReferenceCheckResponseDto : CreateDtoBase
{
    [Required]
    public Guid CheckItemId { get; set; }

    public Guid? RefereeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string RefereeName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string RefereeOrganisation { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string RefereePosition { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string RefereeEmail { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? RefereePhone { get; set; }

    [Required]
    public ReferenceResponseMethod ResponseMethod { get; set; }

    public ReferenceRating? OverallRating { get; set; }

    [MaxLength(5000)]
    public string? Comments { get; set; }

    public bool? WouldRehire { get; set; }
    public bool? ConfirmedDatesOfEmployment { get; set; }
    public bool? ConfirmedPositionHeld { get; set; }
    public bool? ConfirmedReasonForLeaving { get; set; }

    // ⚠ DocumentPath removed — a written reference is uploaded through the gate.
}

/// <remarks>
/// ⚠ <c>[JsonUnmappedMemberHandling(Disallow)]</c>: a field that was REMOVED from this payload
/// because it let the caller decide something they should not is refused with a 400 rather than
/// quietly dropped. Silently ignoring it is worse than rejecting it — the caller believes the value
/// took effect, which for an approver, a date or an attachment is precisely the misunderstanding
/// that hides a bug.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class UpdateReferenceCheckResponseDto : UpdateDtoBase
{
    public ReferenceRating? OverallRating { get; set; }

    [MaxLength(5000)]
    public string? Comments { get; set; }

    public bool? WouldRehire { get; set; }
    public bool? ConfirmedDatesOfEmployment { get; set; }
    public bool? ConfirmedPositionHeld { get; set; }
    public bool? ConfirmedReasonForLeaving { get; set; }

    // ⚠ DocumentPath removed — a written reference is uploaded through the gate.
}

#endregion

#region Pre-Employment Check Templates

/// <summary>
/// The stored-document handles behind one piece of pre-employment evidence, for the download route.
/// Not a client-facing shape — the controller turns it into a file stream.
/// </summary>
public class PreEmploymentDocumentHandleDto
{
    public Guid? FileUploadRecordId { get; set; }
    public Guid? DocumentRecordId { get; set; }
    public Guid? DocumentVersionId { get; set; }

    /// <summary>Legacy path, for documents stored before the controlled-upload gate.</summary>
    public string? LegacyPath { get; set; }

    public string FileName { get; set; } = string.Empty;
}

public class PreEmploymentCheckTemplateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int ItemCount { get; set; }
}

public class PreEmploymentCheckTemplateDetailDto : PreEmploymentCheckTemplateDto
{
    public List<PreEmploymentCheckTemplateItemDto> Items { get; set; } = new();
}

/// <summary>
/// A supplier that provides one kind of pre-employment check (round 3, lane G; register row R-7;
/// decision D-14). The check-type → provider cascade on the check and template screens reads these.
/// </summary>
public class PreEmploymentCheckProviderServiceDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public bool SupplierIsActive { get; set; }
    public PreEmploymentCheckType CheckType { get; set; }
    public string CheckTypeName => CheckType.ToString();
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>One supplier, the check types it provides — one row is written per type; an existing pairing is kept.</summary>
public class CreatePreEmploymentCheckProviderServicesDto
{
    [Required]
    public Guid SupplierId { get; set; }

    [Required, MinLength(1)]
    public List<PreEmploymentCheckType> CheckTypes { get; set; } = new();

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class PreEmploymentCheckTemplateItemDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid TemplateId { get; set; }
    public PreEmploymentCheckType CheckType { get; set; }
    public string CheckTypeName => CheckType.ToString();
    public string? DefaultServiceProvider { get; set; }
    /// <summary>The supplier behind the default provider, when it is one (round 3, lane G; D-14).</summary>
    public Guid? DefaultServiceProviderSupplierId { get; set; }
    public string? Instructions { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsBlockingOnFail { get; set; }
    public int? ExpectedDays { get; set; }
}

public class CreatePreEmploymentCheckTemplateDto : CreateDtoBase
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public List<CreatePreEmploymentCheckTemplateItemDto> Items { get; set; } = new();
}

public class UpdatePreEmploymentCheckTemplateDto : UpdateDtoBase
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }
}

public class CreatePreEmploymentCheckTemplateItemDto : CreateDtoBase
{
    [Required]
    public Guid TemplateId { get; set; }

    [Required]
    public PreEmploymentCheckType CheckType { get; set; }

    [MaxLength(200)]
    public string? DefaultServiceProvider { get; set; }

    /// <summary>A Procurement supplier (round 3, lane G; D-14) — the tenant's, live; refused otherwise.</summary>
    public Guid? DefaultServiceProviderSupplierId { get; set; }

    [MaxLength(2000)]
    public string? Instructions { get; set; }

    public bool IsMandatory { get; set; } = true;
    public bool IsBlockingOnFail { get; set; } = true;
    public int? ExpectedDays { get; set; }
}

public class UpdatePreEmploymentCheckTemplateItemDto : UpdateDtoBase
{
    /// <summary>Parent template — needed to load the right aggregate.</summary>
    [Required]
    public Guid TemplateId { get; set; }

    /// <summary>CheckType is intentionally excluded; delete + re-add to change it.</summary>

    [MaxLength(200)]
    public string? DefaultServiceProvider { get; set; }

    /// <summary>A Procurement supplier (round 3, lane G; D-14) — the tenant's, live; refused otherwise.</summary>
    public Guid? DefaultServiceProviderSupplierId { get; set; }

    [MaxLength(2000)]
    public string? Instructions { get; set; }

    public bool IsMandatory { get; set; } = true;
    public bool IsBlockingOnFail { get; set; } = true;
    public int? ExpectedDays { get; set; }
}

/// <summary>
/// Request body for applying a template to an existing PreEmploymentCheck.
/// Items from the template are copied in; existing items of the same CheckType
/// are skipped to avoid duplication.
/// </summary>
public class ApplyTemplateDto
{
    [Required]
    public Guid TemplateId { get; set; }

    /// <summary>
    /// When true, items that already exist on the check (same CheckType) are
    /// overwritten with template defaults. Default false = skip duplicates.
    /// </summary>
    public bool OverwriteExisting { get; set; } = false;
}

#endregion

// ============================================================================
// SECTION 15 — ONBOARDING
// ============================================================================

#region Onboarding Plan Template

public class OnboardingPlanTemplateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int TaskTemplateCount { get; set; }
}

public class OnboardingPlanTemplateSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int TaskTemplateCount { get; set; }
}

public class OnboardingPlanTemplateDetailDto : OnboardingPlanTemplateDto
{
    public List<OnboardingTaskTemplateDto> TaskTemplates { get; set; } = new();
}

/// <summary>
/// Copy an onboarding plan template under a new name (round 4, lane J1). Its tasks come with it; its
/// audience does not, and it is never the default — see <c>OnboardingPlanTemplateService.CloneAsync</c>.
/// </summary>
public class CloneOnboardingPlanTemplateDto
{
    [Required]
    [MaxLength(200)]
    public string NewName { get; set; } = string.Empty;
}

public class CreateOnboardingPlanTemplateDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateOnboardingPlanTemplateDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

#endregion

#region Onboarding Task Template

public class OnboardingTaskTemplateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PlanTemplateId { get; set; }
    public string PlanTemplateName { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public OnboardingTaskCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public int DueDaysFromStartDate { get; set; }
    public bool IsMandatory { get; set; }
    public int DisplayOrder { get; set; }
    public string? InstructionsUrl { get; set; }
    public Guid? OwnerPositionId { get; set; }
    public string? OwnerPositionTitle { get; set; }
}

public class CreateOnboardingTaskTemplateDto : CreateDtoBase
{
    [Required]
    public Guid PlanTemplateId { get; set; }

    [Required]
    [MaxLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public OnboardingTaskCategory Category { get; set; }

    /// <summary>
    /// Days from the start date; negative for a task due before it (the contract, the accounts). The
    /// template screen always offered −90…365 — "Negative for before the start date" — while this
    /// range was 0…365 from the port, so every pre-start task was refused (found by round 4, lane J).
    /// </summary>
    [Range(-90, 365)]
    public int DueDaysFromStartDate { get; set; }

    public bool IsMandatory { get; set; } = true;
    public int DisplayOrder { get; set; }

    [MaxLength(500)]
    public string? InstructionsUrl { get; set; }

    public Guid? OwnerPositionId { get; set; }
}

public class UpdateOnboardingTaskTemplateDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public OnboardingTaskCategory Category { get; set; }

    /// <summary>Negative for a task due before the start date — see the create DTO.</summary>
    [Range(-90, 365)]
    public int DueDaysFromStartDate { get; set; }

    public bool IsMandatory { get; set; }
    public int DisplayOrder { get; set; }

    [MaxLength(500)]
    public string? InstructionsUrl { get; set; }

    public Guid? OwnerPositionId { get; set; }
}

#endregion

#region Onboarding Plan

public class OnboardingPlanDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public Guid? TemplatePlanId { get; set; }
    public string? TemplatePlanName { get; set; }
    public OnboardingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly StartDate { get; set; }
    public DateOnly? TargetCompletionDate { get; set; }
    public DateOnly? ActualCompletionDate { get; set; }
    public Guid? AssignedBuddyId { get; set; }
    public string? AssignedBuddyName { get; set; }
    public Guid? OnboardingCoordinatorId { get; set; }
    public string? OnboardingCoordinatorName { get; set; }
    public string? Notes { get; set; }

    /// <summary>Set when the system created the plan on hire confirmation: which template and why
    /// (round 4, lane I4). Null on a plan a person created.</summary>
    public string? TemplateSelectionReason { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
}

public class OnboardingPlanSummaryDto
{
    public Guid Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public OnboardingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly StartDate { get; set; }
    public DateOnly? TargetCompletionDate { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
}

public class OnboardingPlanDetailDto : OnboardingPlanDto
{
    public List<OnboardingTaskDto> Tasks { get; set; } = new();
    public List<OnboardingAssetDto> Assets { get; set; } = new();
}

public class CreateOnboardingPlanDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? TemplatePlanId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? TargetCompletionDate { get; set; }
    public Guid? AssignedBuddyId { get; set; }
    public Guid? OnboardingCoordinatorId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateOnboardingPlanDto : UpdateDtoBase
{
    public OnboardingStatus Status { get; set; }
    public DateOnly? TargetCompletionDate { get; set; }
    public DateOnly? ActualCompletionDate { get; set; }
    public Guid? AssignedBuddyId { get; set; }
    public Guid? OnboardingCoordinatorId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Onboarding Task

public class OnboardingTaskDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid OnboardingPlanId { get; set; }
    public Guid? TaskTemplateId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public OnboardingTaskCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public OnboardingTaskStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly DueDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsOverdue => Status != OnboardingTaskStatus.Completed && DueDate < DateOnly.FromDateTime(DateTime.UtcNow);
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public Guid? AssignedOrganizationUnitId { get; set; }
    public string? AssignedOrganizationUnitName { get; set; }

    /// <summary>The role that owes this task, carried over from the template at instantiation.</summary>
    public Guid? OwnerPositionId { get; set; }
    public string? OwnerPositionTitle { get; set; }

    public Guid? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
    public string? CompletionNotes { get; set; }
    public string? EvidenceFilePath { get; set; }
    public bool RequiresVerification { get; set; }

    /// <summary>True once the task is done and, where required, signed off by a second party.</summary>
    public bool AwaitingVerification => Status == OnboardingTaskStatus.PendingVerification;

    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public int DisplayOrder { get; set; }
}

public class OnboardingTaskSummaryDto
{
    public Guid Id { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public OnboardingTaskCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public OnboardingTaskStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly DueDate { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsOverdue => Status != OnboardingTaskStatus.Completed && DueDate < DateOnly.FromDateTime(DateTime.UtcNow);
    public string? AssignedToName { get; set; }
}

public class OnboardingTaskDetailDto : OnboardingTaskDto
{
    public List<OnboardingTaskCommentDto> Comments { get; set; } = new();
}

public class CreateOnboardingTaskDto : CreateDtoBase
{
    [Required]
    public Guid OnboardingPlanId { get; set; }

    public Guid? TaskTemplateId { get; set; }

    [Required]
    [MaxLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public OnboardingTaskCategory Category { get; set; }

    [Required]
    public DateOnly DueDate { get; set; }

    public bool IsMandatory { get; set; } = true;
    public Guid? AssignedToId { get; set; }
    public Guid? AssignedOrganizationUnitId { get; set; }
    public bool RequiresVerification { get; set; }
    public int DisplayOrder { get; set; }
}

public class UpdateOnboardingTaskDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateOnly DueDate { get; set; }
    public Guid? AssignedToId { get; set; }
    public Guid? AssignedOrganizationUnitId { get; set; }
    public bool RequiresVerification { get; set; }
    public int DisplayOrder { get; set; }
}

public class CompleteOnboardingTaskDto
{
    [Required]
    public Guid TaskId { get; set; }

    // The actor is taken from the authenticated user's employee record, not the payload — a [Required]
    // attribute on a non-nullable Guid is satisfied by Guid.Empty, so an omitted id would reach the
    // database as an unmatched FK and come back as a 500 with raw SQL in it.

    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }

    [MaxLength(500)]
    public string? EvidenceFilePath { get; set; }
}

public class VerifyOnboardingTaskDto
{
    [Required]
    public Guid TaskId { get; set; }

    // Verifier is taken from the authenticated user's employee record — see the note above.
}

#endregion

#region Onboarding Task Comment

public class OnboardingTaskCommentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid TaskId { get; set; }
    public string Comment { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CommentDate { get; set; }
}

public class CreateOnboardingTaskCommentDto : CreateDtoBase
{
    [Required]
    public Guid TaskId { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Comment { get; set; } = string.Empty;
}

#endregion

#region Onboarding Asset

public class OnboardingAssetDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid OnboardingPlanId { get; set; }
    public OnboardingAssetType AssetType { get; set; }
    public string AssetTypeName => AssetType.ToString();
    public string AssetName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AssetTag { get; set; }
    public string? SerialNumber { get; set; }
    public OnboardingAssetProvisionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly? RequiredByDate { get; set; }
    public DateTime? ProvisionedDate { get; set; }
    public Guid? ProvisionedById { get; set; }
    public string? ProvisionedByName { get; set; }
    public DateTime? IssuedToEmployeeDate { get; set; }
    public bool AcknowledgedByEmployee { get; set; }
    public DateTime? AcknowledgementDate { get; set; }
    public string? AcknowledgementDocumentPath { get; set; }
    public string? Notes { get; set; }
}

public class CreateOnboardingAssetDto : CreateDtoBase
{
    [Required]
    public Guid OnboardingPlanId { get; set; }

    [Required]
    public OnboardingAssetType AssetType { get; set; }

    [Required]
    [MaxLength(200)]
    public string AssetName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? AssetTag { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    public DateOnly? RequiredByDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateOnboardingAssetDto : UpdateDtoBase
{
    public OnboardingAssetProvisionStatus Status { get; set; }

    [MaxLength(100)]
    public string? AssetTag { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    public DateOnly? RequiredByDate { get; set; }
    public DateTime? ProvisionedDate { get; set; }
    public Guid? ProvisionedById { get; set; }
    public DateTime? IssuedToEmployeeDate { get; set; }
    public bool AcknowledgedByEmployee { get; set; }
    public DateTime? AcknowledgementDate { get; set; }

    [MaxLength(500)]
    public string? AcknowledgementDocumentPath { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// SECTION 16 — PROBATION
// ============================================================================

#region Probation Period

public class ProbationPeriodDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public Guid ContractDetailId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly OriginalEndDate { get; set; }
    public DateOnly CurrentEndDate { get; set; }
    public int DurationMonths { get; set; }
    public ProbationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? OutcomeNotes { get; set; }
    public int ExtensionCount { get; set; }
    public int ReviewCount { get; set; }
}

public class ProbationPeriodSummaryDto
{
    public Guid Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly CurrentEndDate { get; set; }
    public int DurationMonths { get; set; }
    public ProbationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int ExtensionCount { get; set; }
    public int ReviewCount { get; set; }
}

public class ProbationPeriodDetailDto : ProbationPeriodDto
{
    public List<ProbationReviewSummaryDto> Reviews { get; set; } = new();
}

public class CreateProbationPeriodDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid ContractDetailId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// How long the probation runs. <b>Omit it</b> and the length is resolved from the employee's
    /// staff category, which is what FR-HR-031 asks for (senior 6 months, junior 3).
    /// </summary>
    /// <remarks>
    /// Nullable since slice 4. A supplied value that contradicts the category is refused for
    /// permanent staff — see <c>GET api/probations/policy/{employeeId}</c>, which returns the
    /// resolved length and where it came from, and is what a create form should call.
    /// </remarks>
    [Range(1, 24)]
    public int? DurationMonths { get; set; }

    [MaxLength(2000)]
    public string? OutcomeNotes { get; set; }
}

/// <summary>
/// The probation length that applies to one employee, and where it was resolved from.
/// </summary>
/// <remarks>
/// FR-HR-031: "The system shall apply probation periods by staff category (senior 6 months; junior
/// 3 months)." Measured on the reference tenant 2026-08-18, the position master already encodes
/// exactly that — JNR 3 months across 47 positions, SNR 6 across 60, MGT 6 across 16 — so the rule
/// is <b>read from maintained data</b> rather than hard-coded against tenant-specific level codes.
/// If TDC ever wants the rule stated independently of positions, its home is a
/// <c>ProbationMonths</c> column on <c>StaffLevel</c>.
/// </remarks>
public class ProbationPolicyDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;

    public Guid? StaffLevelId { get; set; }
    public string? StaffLevelName { get; set; }
    public string? StaffLevelCode { get; set; }

    public EmploymentType EmploymentType { get; set; }
    public string EmploymentTypeName => EmploymentType.ToString();

    /// <summary>The length that will be applied when the caller does not supply one.</summary>
    public int ExpectedDurationMonths { get; set; }

    /// <summary>Where <see cref="ExpectedDurationMonths"/> came from: Position, PolicyDefault.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>The position's own probation length, when it carries one.</summary>
    public int? PositionProbationMonths { get; set; }

    /// <summary>The tenant-wide fallback from CompanyHrPolicySettings.</summary>
    public int PolicyDefaultMonths { get; set; }

    /// <summary>
    /// Whether the length is enforced. Only permanent staff are bound by FR-HR-031; a contract or
    /// temporary appointment is governed by its own contract, so a supplied length is accepted.
    /// </summary>
    public bool IsEnforced { get; set; }

    /// <summary>How many days before the end date the expiry alerts start (FR-HR-140).</summary>
    public int EndLeadDays { get; set; }

    /// <summary>
    /// Who will confirm this probation (FR-HR-032), or null when no rule covers the employee.
    /// </summary>
    /// <remarks>
    /// Carried here so a create form can show, before the probation is even opened, who is going to
    /// have to act on it — and so an unconfigured tenant is visible at the point of creation rather
    /// than a month later when the reminder has nobody to go to.
    /// </remarks>
    public Guid? ConfirmingAuthorityEmployeeId { get; set; }
    public string? ConfirmingAuthorityName { get; set; }
    public string? ConfirmingAuthorityScope { get; set; }
}

public class ExtendProbationPeriodDto
{
    [Required]
    public Guid ProbationPeriodId { get; set; }

    [Required]
    [Range(1, 12)]
    public int AdditionalMonths { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ExtensionReason { get; set; } = string.Empty;
}

public class ConfirmProbationOutcomeDto
{
    [Required]
    public Guid ProbationPeriodId { get; set; }

    [Required]
    public ProbationStatus Outcome { get; set; }

    [MaxLength(2000)]
    public string? OutcomeNotes { get; set; }
}

#endregion

#region Probation Review

public class ProbationReviewDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ProbationPeriodId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public int ReviewNumber { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public DateOnly? ActualDate { get; set; }
    public ProbationReviewStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public ProbationPerformanceRating? PerformanceRating { get; set; }
    public string? PerformanceRatingName => PerformanceRating?.ToString();
    public ProbationPerformanceRating? ConductRating { get; set; }
    public string? ConductRatingName => ConductRating?.ToString();
    public ProbationPerformanceRating? AttitudeRating { get; set; }
    public string? AttitudeRatingName => AttitudeRating?.ToString();
    public string? StrengthsObserved { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? ReviewerComments { get; set; }
    public string? EmployeeResponse { get; set; }
    public ProbationReviewRecommendation? Recommendation { get; set; }
    public string? RecommendationName => Recommendation?.ToString();
    public int? ProposedExtensionMonths { get; set; }
    public Guid ReviewedById { get; set; }
    public string ReviewedByName { get; set; } = string.Empty;
    public Guid? SecondReviewerId { get; set; }
    public string? SecondReviewerName { get; set; }
    public bool EmployeeAcknowledged { get; set; }
    public DateTime? EmployeeAcknowledgementDate { get; set; }
    public bool HrApproved { get; set; }
    public Guid? HrApprovedById { get; set; }
    public string? HrApprovedByName { get; set; }
    public DateTime? HrApprovalDate { get; set; }
    public string? SignedDocumentPath { get; set; }
}

public class ProbationReviewSummaryDto
{
    public Guid Id { get; set; }
    public int ReviewNumber { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public DateOnly? ActualDate { get; set; }
    public ProbationReviewStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public ProbationReviewRecommendation? Recommendation { get; set; }
    public string? RecommendationName => Recommendation?.ToString();
    public string ReviewedByName { get; set; } = string.Empty;
    public bool HrApproved { get; set; }
}

public class CreateProbationReviewDto : CreateDtoBase
{
    [Required]
    public Guid ProbationPeriodId { get; set; }

    [Range(1, 20)]
    public int ReviewNumber { get; set; }

    [Required]
    public DateOnly ScheduledDate { get; set; }

    [Required]
    public Guid ReviewedById { get; set; }

    public Guid? SecondReviewerId { get; set; }
}

public class SubmitProbationReviewDto
{
    [Required]
    public Guid ReviewId { get; set; }

    public DateOnly ActualDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public ProbationPerformanceRating? PerformanceRating { get; set; }
    public ProbationPerformanceRating? ConductRating { get; set; }
    public ProbationPerformanceRating? AttitudeRating { get; set; }

    [MaxLength(5000)]
    public string? StrengthsObserved { get; set; }

    [MaxLength(5000)]
    public string? AreasForImprovement { get; set; }

    [MaxLength(5000)]
    public string? ReviewerComments { get; set; }

    public ProbationReviewRecommendation? Recommendation { get; set; }
    public int? ProposedExtensionMonths { get; set; }

    [MaxLength(500)]
    public string? SignedDocumentPath { get; set; }
}

public class AcknowledgeProbationReviewDto
{
    [Required]
    public Guid ReviewId { get; set; }

    [MaxLength(5000)]
    public string? EmployeeResponse { get; set; }
}

/// <summary>
/// HR sign-off on a completed probation review.
/// </summary>
/// <remarks>
/// ⚠ This DTO used to carry <c>HrApprovedById</c> and <c>HrApprovalDate</c>, both supplied by the
/// caller — so a request could name someone else as the approver and back-date the approval. The
/// approver is now taken from the authenticated token and the date is stamped server-side; there
/// is deliberately nothing left here to spoof. See the actor rule in
/// <c>plans/HR-Area-15b-Probation-Confirmation-Build-Plan.md</c> §5 D-5.
/// </remarks>
public class ApproveProbationReviewDto
{
    [MaxLength(2000)]
    public string? Comments { get; set; }
}

public class TerminateProbationPeriodDto
{
    [Required]
    public Guid ProbationId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateProbationReviewDto : UpdateDtoBase
{
    public DateOnly? ScheduledDate { get; set; }

    public Guid? SecondReviewerId { get; set; }
}

#endregion

// ============================================================================
// PROBATION EXTENSION DTOs
// ============================================================================

#region Probation Extension DTOs

/// <summary>The rendered FR-HR-032 confirmation letter, ready to display or print to PDF.</summary>
public class ProbationConfirmationLetterDto
{
    public Guid ProbationId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public DateOnly ConfirmationDate { get; set; }

    /// <summary>Rendered subject line, used when the letter is emailed.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Self-contained HTML document body, suitable for display and print-to-PDF.</summary>
    public string HtmlBody { get; set; } = string.Empty;
}

public class ProbationExtensionDto : BaseDto
{
    public Guid ProbationPeriodId { get; set; }
    public DateOnly PreviousEndDate { get; set; }
    public DateOnly NewEndDate { get; set; }
    public int ExtensionMonths { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ExtendedById { get; set; }
    public string? ExtendedByName { get; set; }
    public DateTime ExtendedDate { get; set; }
    public string? Comments { get; set; }
}

public class CreateProbationExtensionDto : CreateDtoBase
{
    [Required]
    public Guid ProbationPeriodId { get; set; }

    [Required]
    public DateOnly NewEndDate { get; set; }

    [Required]
    [Range(1, 24)]
    public int ExtensionMonths { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

#endregion

// ============================================================================
// JOB OFFER BENEFIT DTOs
// ============================================================================

#region Job Offer Benefit DTOs

public class JobOfferBenefitDto : BaseDto
{
    public Guid JobOfferId { get; set; }
    public string BenefitName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? MonetaryValue { get; set; }
    public string? CurrencyCode { get; set; }
    public bool IsMonetary { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateJobOfferBenefitDto : CreateDtoBase
{
    [Required]
    public Guid JobOfferId { get; set; }

    [Required]
    [MaxLength(200)]
    public string BenefitName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public decimal? MonetaryValue { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    public bool IsMonetary { get; set; }

    public int DisplayOrder { get; set; }
}

public class UpdateJobOfferBenefitDto : UpdateDtoBase
{
    [MaxLength(200)]
    public string? BenefitName { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public decimal? MonetaryValue { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    public bool? IsMonetary { get; set; }

    public int? DisplayOrder { get; set; }
}

public class ReviseJobOfferDto
{
    [Required]
    public Guid OriginalOfferId { get; set; }

    /// <summary>Updated terms for the revised offer. Null fields retain the original values.</summary>
    public decimal? NewBaseSalary { get; set; }
    public DateOnly? NewProposedStartDate { get; set; }

    [MaxLength(5000)]
    public string? NewAdditionalTerms { get; set; }

    [MaxLength(2000)]
    public string? RevisionReason { get; set; }
}

// ============================================================================
// JOB OFFER NOTE DTOs
// ============================================================================

public class JobOfferNoteDto : BaseDto
{
    public Guid   JobOfferId  { get; set; }
    public string Body        { get; set; } = string.Empty;
    public string? AuthorName { get; set; }
}

public class CreateJobOfferNoteDto : CreateDtoBase
{
    [Required]
    public Guid JobOfferId { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;
}

#endregion

// ============================================================================
// APPLICATION AUTO-SCORE DTOs
// ============================================================================

#region Application Auto-Score DTOs

/// <summary>
/// Result returned from EvaluateApplicationScoreAsync.
/// </summary>
public class ApplicationAutoScoreDto
{
    public Guid ApplicationId { get; set; }
    /// <summary>
    /// Null when the vacancy has no criteria (round 3, lane K): nothing was measured, so nothing is
    /// scored — and an unscored application is never auto-shortlisted. It used to be 100.
    /// </summary>
    public decimal? AutoScore { get; set; }
    /// <summary>False when the vacancy has no criteria to score against.</summary>
    public bool HasCriteria { get; set; } = true;
    public DateTime ScoredAt { get; set; }
    public bool AllMandatoryPassed { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal MaxPossibleScore { get; set; } = 100m;
    public List<CriterionScoreResult> Breakdown { get; set; } = new();
}

public class CriterionScoreResult
{
    public Guid CriteriaId { get; set; }
    public string CriteriaName { get; set; } = string.Empty;
    public JobShortlistingCriteriaType Type { get; set; }
    public bool IsMandatory { get; set; }
    public int Weight { get; set; }
    public bool Passed { get; set; }
    public decimal RawScore { get; set; }
    public decimal WeightedScore { get; set; }
    public string? Notes { get; set; }
    /// <summary>
    /// False for a criterion the engine does not score (Other): it contributes nothing and its
    /// weight is left out of the total, so it neither lifts nor lowers anybody (round 3, lane K).
    /// </summary>
    public bool AutoEvaluated { get; set; } = true;
}

/// <summary>
/// Result of a bulk scoring run triggered via <c>IAutoScoringService.RunScoringForVacancyAsync</c>.
/// Carries per-application outcomes so the pipeline UI can report exactly which applications
/// were scored, which were skipped, and why.
/// </summary>
public class RecruitmentScoringRunResultDto
{
    public Guid VacancyId { get; set; }
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public DateTime RanAt { get; set; }
    public List<ApplicationAutoScoreDto> Results { get; set; } = new();
    public List<RecruitmentScoringRunErrorDto> Errors { get; set; } = new();
}

/// <summary>Captures the error for a single application that failed during a scoring run.</summary>
public class RecruitmentScoringRunErrorDto
{
    public Guid ApplicationId { get; set; }
    public string Error { get; set; } = string.Empty;
}

#endregion

// =============================================================================
// SHORTLISTING OPERATION DTOs
// =============================================================================

#region Shortlisting Operation DTOs

public class UnshortlistApplicationDto
{
    [Required]
    public Guid ApplicationId { get; set; }

    [MaxLength(2000)]
    public string? Reason { get; set; }
}

public class WaitlistApplicationDto
{
    [Required]
    public Guid ApplicationId { get; set; }

    [MaxLength(1000)]
    public string? WaitlistReason { get; set; }
}

public class BulkShortlistDto
{
    [Required]
    [MinLength(1)]
    public List<Guid> ApplicationIds { get; set; } = new();

    [MaxLength(2000)]
    public string? ShortlistingNotes { get; set; }
}

public class BulkRejectDto
{
    [Required]
    [MinLength(1)]
    public List<Guid> ApplicationIds { get; set; } = new();

    [Required]
    [MaxLength(2000)]
    public string RejectionReason { get; set; } = string.Empty;
}

/// <summary>
/// Moves a set of applications to the same target pipeline stage in one operation.
/// Each application is moved individually so transition rules are enforced per-application.
/// Individual failures are collected in <see cref="RecruitmentBulkOperationResultDto.Results"/>
/// and do not abort the remaining items.
/// </summary>
public class RecruitmentBulkMoveToStageDto
{
    [Required]
    [MinLength(1)]
    public List<Guid> ApplicationIds { get; set; } = new();

    [Required]
    public Guid TargetStageId { get; set; }
}

/// <summary>
/// Rejects a set of applications from within the pipeline in one operation.
/// In addition to setting <c>Status = Rejected</c>, closes the current stage
/// history record for each application (ExitReason = Rejected).
/// </summary>
public class RecruitmentBulkPipelineRejectDto
{
    [Required]
    [MinLength(1)]
    public List<Guid> ApplicationIds { get; set; } = new();

    [Required]
    [MaxLength(2000)]
    public string RejectionReason { get; set; } = string.Empty;
}

public class AutoShortlistByScoreDto
{
    [Required]
    public Guid VacancyId { get; set; }

    /// <summary>Minimum score (0–100) for automatic shortlisting.</summary>
    [Required]
    [Range(1, 100)]
    public decimal MinScore { get; set; }

    /// <summary>If true, only applications where every mandatory criterion passed are eligible.</summary>
    public bool RequireAllMandatoryPassed { get; set; } = true;

    [MaxLength(2000)]
    public string? ShortlistingNotes { get; set; }
}

/// <summary>Summary dashboard stats for a vacancy's shortlist panel.</summary>
public class ShortlistSummaryDto
{
    public Guid VacancyId { get; set; }
    public int TotalApplications { get; set; }
    public int Scored { get; set; }
    public int StaleScores { get; set; }
    public int NeverScored { get; set; }
    public int Shortlisted { get; set; }
    public int Waitlisted { get; set; }
    public int Rejected { get; set; }
    public int Withdrawn { get; set; }
    public decimal? HighestScore { get; set; }
    public decimal? LowestScore { get; set; }
    public decimal? AverageScore { get; set; }
    public bool IsShortlistDeadlinePassed { get; set; }
    public bool IsShortlistApproved { get; set; }
    public ShortlistApprovalStatus ApprovalStatus { get; set; }
    public string ApprovalStatusName => ApprovalStatus.ToString();
}

/// <summary>One row in the candidate comparison matrix.</summary>
public class CandidateComparisonRowDto
{
    public Guid ApplicationId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string ApplicationNumber { get; set; } = string.Empty;
    public decimal? TotalScore { get; set; }
    public List<CriterionComparisonCellDto> CriterionScores { get; set; } = new();
}

public class CriterionComparisonCellDto
{
    public Guid CriteriaId { get; set; }
    public string CriteriaName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public int Weight { get; set; }
    public bool Passed { get; set; }
    public decimal RawScore { get; set; }
    public decimal WeightedScore { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Full candidate comparison matrix for a set of applications.</summary>
public class CandidateComparisonDto
{
    public Guid VacancyId { get; set; }
    public List<JobShortlistingCriteriaDto> Criteria { get; set; } = new();
    public List<CandidateComparisonRowDto> Candidates { get; set; } = new();
}

/// <summary>Submit the completed shortlist for hiring manager approval.</summary>
public class SubmitShortlistForApprovalDto
{
    [Required]
    public Guid VacancyId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>Approve or reject the submitted shortlist.</summary>
public class ReviewShortlistApprovalDto
{
    [Required]
    public Guid VacancyId { get; set; }

    [Required]
    public bool Approved { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>Result of a bulk shortlist/reject operation.</summary>
public class RecruitmentBulkOperationResultDto
{
    public int Succeeded { get; set; }
    public int Skipped { get; set; }
    public List<RecruitmentBulkOperationItemResult> Results { get; set; } = new();
}

public class RecruitmentBulkOperationItemResult
{
    public Guid ApplicationId { get; set; }
    /// <summary>Set by talent-pool bulk operations, whose subject is a candidate, not an application.
    /// Additive rather than a rename — the shortlist/reject callers key on ApplicationId.</summary>
    public Guid? CandidateId { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}

#endregion

// ============================================================================
// SECTION X — SHORTLIST DECISION LOG (immutable audit trail)
// ============================================================================

#region Shortlist Decision Log

public class ShortlistDecisionLogDto : BaseDto
{
    public Guid JobApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public ShortlistDecisionType DecisionType { get; set; }
    public string DecisionTypeName => DecisionType.ToString();
    public Guid? DecisionById { get; set; }
    public string? DecisionByName { get; set; }
    public DateTime DecisionAt { get; set; }
    public decimal? AutoScoreAtDecision { get; set; }
    public bool IsAutoDecision { get; set; }
    public string? Notes { get; set; }
    public Guid? OverridesDecisionLogId { get; set; }
}

#endregion

// ============================================================================
// SECTION X — SHORTLIST REVIEW (multi-reviewer scoring)
// ============================================================================

#region Shortlist Review

public class ShortlistReviewDto : BaseDto
{
    public Guid JobApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string? CandidateName { get; set; }
    public Guid ReviewerId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string? Notes { get; set; }
    public DateTime ReviewedAt { get; set; }
    public bool IsFinalized { get; set; }
    public DateTime? FinalizedAt { get; set; }
}

public class CreateShortlistReviewDto
{
    [Required]
    public Guid ApplicationId { get; set; }

    /// <summary>Score from 0–100.</summary>
    [Range(0, 100)]
    public decimal Score { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateShortlistReviewDto
{
    [Required]
    public Guid ReviewId { get; set; }

    [Range(0, 100)]
    public decimal Score { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class AggregatedReviewScoreDto
{
    public Guid ApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string? CandidateName { get; set; }
    public decimal? AggregatedScore { get; set; }
    public int ReviewerCount { get; set; }
    public List<ShortlistReviewDto> Reviews { get; set; } = new();
}

#endregion

// ============================================================================
// SECTION X — EEO / DIVERSITY COMPLIANCE REPORT
// ============================================================================

#region EEO Report

public class EeoComplianceReportDto
{
    public Guid VacancyId { get; set; }
    public string VacancyNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<EeoStatusBreakdownDto> ByStatus { get; set; } = new();
    public EeoPipelineStageDto AllApplicants { get; set; } = new();
    public EeoPipelineStageDto Shortlisted { get; set; } = new();
    public EeoPipelineStageDto Rejected { get; set; } = new();
    public EeoPipelineStageDto Hired { get; set; } = new();
}

public class EeoStatusBreakdownDto
{
    public string Status { get; set; } = string.Empty;
    public int Total { get; set; }
    public EeoPipelineStageDto Demographics { get; set; } = new();
}

public class EeoPipelineStageDto
{
    public int Total { get; set; }
    public int Male { get; set; }
    public int Female { get; set; }
    public int Other { get; set; }
    public int PreferNotToSay { get; set; }
    public decimal? MalePercent => Total > 0 ? Math.Round(Male * 100m / Total, 1) : null;
    public decimal? FemalePercent => Total > 0 ? Math.Round(Female * 100m / Total, 1) : null;
    public decimal? AverageAge { get; set; }
    public int InternalCandidateCount { get; set; }
}

#endregion

// ============================================================================
// SECTION X — SLA STATUS
// ============================================================================

#region SLA Status

public class ShortlistSlaStatusDto
{
    public Guid VacancyId { get; set; }
    public string VacancyNumber { get; set; } = string.Empty;
    public DateTime? PublishDate { get; set; }
    public DateTime? ShortlistingDeadline { get; set; }
    public DateTime? ShortlistCompletedAt { get; set; }
    public int? TimeToShortlistDays { get; set; }
    public bool ShortlistingSlaBreached { get; set; }
    public int? DaysOverdue { get; set; }
    public int? DaysRemaining { get; set; }
    public ShortlistApprovalStatus ApprovalStatus { get; set; }
    public string ApprovalStatusName => ApprovalStatus.ToString();
}

#endregion

// ============================================================================
// SECTION X — BLIND SCREENING
// ============================================================================

#region Blind Screening

/// <summary>
/// Anonymised application view — all PII removed.
/// Returned when a vacancy has IsBlindScreeningEnabled = true.
/// </summary>
public class BlindApplicationSummaryDto
{
    public Guid ApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime ApplicationDate { get; set; }
    public int? YearsOfExperience { get; set; }
    public decimal? AutoScore { get; set; }
    public bool ScoreIsStale { get; set; }
    public DateTime? ScoredAt { get; set; }
    public bool IsInternalCandidate { get; set; }
    // No name, gender, age, location, or contact details
}

#endregion

// ============================================================================
// SECTION X — INTERNAL CANDIDATE PREFERENCING
// ============================================================================

#region Internal Candidate

public class MarkInternalCandidateDto
{
    [Required]
    public Guid ApplicationId { get; set; }

    /// <summary>Optional — link to the employee record of the internal applicant.</summary>
    public Guid? InternalEmployeeId { get; set; }
}

#endregion

// ============================================================================
// SECTION — APPLICATION PIPELINE (KANBAN)
// ============================================================================

#region Application Pipeline

/// <summary>Request body for moving an application to a different pipeline stage.</summary>
public class MoveStageRequest
{
    [Required]
    public Guid ApplicationId { get; set; }

    [Required]
    public Guid TargetStageId { get; set; }
}

/// <summary>Lightweight application card shown on a Kanban column.</summary>
public class ApplicationPipelineCardDto
{
    public Guid ApplicationId { get; set; }
    public Guid CandidateId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public Guid CurrentStageId { get; set; }
    public decimal? AutoScore { get; set; }
    public ApplicationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime DateApplied { get; set; }
    public DateTime EnteredStageAt { get; set; }
}

/// <summary>A single Kanban column: one pipeline stage with its current applications.</summary>
public class PipelineStageWithApplicationsDto
{
    public Guid StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int Order { get; set; }
    public RecruitmentPipelineStageType StageType { get; set; }
    public string StageTypeName => StageType.ToString();
    public List<ApplicationPipelineCardDto> Applications { get; set; } = new();
}

// ── Pipeline Query DTOs (list+bulk view) ─────────────────────────────────────

/// <summary>
/// Header for a single pipeline stage shown in the pipeline overview bar.
/// Contains only counts — no application data.
/// </summary>
public class PipelineStageHeaderDto
{
    /// <summary>Guid.Empty for the synthetic inbox bucket.</summary>
    public Guid StageId { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int Order { get; set; }
    public RecruitmentPipelineStageType? StageType { get; set; }
    public string StageTypeName => IsInbox ? "Inbox" : StageType?.ToString() ?? string.Empty;
    public int ApplicationCount { get; set; }
    /// <summary>
    /// True for the synthetic inbox bucket (applications not yet in any pipeline stage).
    /// StageId is Guid.Empty for this bucket.
    /// </summary>
    public bool IsInbox { get; set; }
}

/// <summary>
/// Lightweight pipeline overview for a vacancy: each stage with its application count only.
/// Use this to render the pipeline header bar without loading any application data.
/// </summary>
public class PipelineOverviewDto
{
    public Guid VacancyId { get; set; }
    /// <summary>False when no pipeline is assigned to the vacancy.</summary>
    public bool HasPipeline { get; set; }
    public List<PipelineStageHeaderDto> Stages { get; set; } = new();
    public int TotalApplications { get; set; }
}

/// <summary>
/// A single row in the paginated stage application list.
/// </summary>
public class PipelineApplicationListItemDto
{
    public Guid ApplicationId { get; set; }
    public Guid CandidateId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string CandidateEmail { get; set; } = string.Empty;

    /// <inheritdoc cref="JobApplicationDto.CandidateHasPhoto"/>
    public bool CandidateHasPhoto { get; set; }

    public ApplicationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public ApplicationSource Source { get; set; }
    public string SourceName => Source.ToString();
    public int? YearsOfExperience { get; set; }
    public decimal? AutoScore { get; set; }
    public bool ScoreIsStale { get; set; }
    public DateTime DateApplied { get; set; }
    /// <summary>When the application entered this stage. Null for inbox items.</summary>
    public DateTime? EnteredStageAt { get; set; }
    public bool IsInternalCandidate { get; set; }
    /// <summary>True when this item is from the inbox bucket (no pipeline stage yet).</summary>
    public bool IsInbox { get; set; }
}

/// <summary>
/// Filter, sort and pagination parameters for the paginated stage application list.
/// </summary>
public class StageApplicationsQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? NameSearch { get; set; }
    public ApplicationStatus? Status { get; set; }
    public ApplicationSource? Source { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public decimal? MinScore { get; set; }
    public decimal? MaxScore { get; set; }
    /// <summary>Column to sort by: "name" | "date" | "score" | "status". Defaults to "date".</summary>
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

#endregion

// ============================================================================
// SECTION 17 — PUBLIC CAREER PORTAL
// ============================================================================

#region Public Career Portal

/// <summary>
/// A safe, public-facing view of a vacancy. Sensitive internal HR fields
/// (recruiter names, application counts, pipeline details, score config) are omitted.
/// </summary>
public class PublicVacancyDto
{
    public Guid   Id                         { get; set; }
    public string VacancyNumber              { get; set; } = string.Empty;
    public string JobTitle                   { get; set; } = string.Empty;
    public string PositionTitle              { get; set; } = string.Empty;
    public string DepartmentName             { get; set; } = string.Empty;
    public string LocationName               { get; set; } = string.Empty;

    public EmploymentType EmploymentType     { get; set; }
    public string EmploymentTypeName         => EmploymentType.ToString();

    public WorkMode WorkMode                 { get; set; }
    public string WorkModeName               => WorkMode.ToString();

    public int    NumberOfPositions          { get; set; }
    public int?   RequiredMinExperienceYears { get; set; }
    public string? KeyBenefitsSummary        { get; set; }
    public DateOnly? TargetStartDate         { get; set; }

    // Salary — only shown when IsSalaryVisible is true
    public bool    IsSalaryVisible           { get; set; }
    public decimal? SalaryRangeMin           { get; set; }
    public decimal? SalaryRangeMax           { get; set; }
    public string? SalaryCurrencyCode        { get; set; }

    public DateTime? PublishDate             { get; set; }
    public DateTime? ApplicationDeadline     { get; set; }

    public bool RequiresWrittenTest          { get; set; }
    public bool RequiresPracticalTest        { get; set; }

    /// <summary>Pre-computed description for the public job advert (rich text / markdown).</summary>
    public string? JobDescription            { get; set; }

    /// <summary>
    /// The vacancy's live adverts (round 3, lane A): a posting link carries one of these ids as
    /// <c>?posting=</c>, and an application made through it records the posting and takes its
    /// source from the channel.
    /// </summary>
    public List<PublicVacancyPostingDto> Postings { get; set; } = new();
}

public class PublicVacancyPostingDto
{
    public Guid Id { get; set; }
    public JobPostingChannel Channel { get; set; }
    public string ChannelName => Channel.ToString();
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// External candidate application form submitted via the public career portal.
/// Creates both a <see cref="JobCandidate"/> record (or matches an existing one by email)
/// and a <see cref="JobApplication"/> record with Source = CompanyWebsite.
/// </summary>
public class ExternalApplicationDto
{
    // ── Vacancy ──────────────────────────────────────────────────────────────

    [Required]
    public Guid VacancyId { get; set; }

    // ── Personal details ──────────────────────────────────────────────────────

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public DateTime DateOfBirth { get; set; }

    [Required]
    public Gender Gender { get; set; }

    [Required]
    [MaxLength(150)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Phone]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? City { get; set; }

    /// <summary>
    /// Required on the PUBLIC form, and only there. <c>JobCandidate.CountryId</c> itself is optional
    /// (slice 13b) so that an internal applicant with no country on file can still apply; an external
    /// candidate, by contrast, is asked directly and can answer. The public portal exposes
    /// GET /api/public/countries specifically to populate this field.
    /// </summary>
    [Required(ErrorMessage = "Country is required.")]
    public Guid? CountryId { get; set; }

    // ── Online presence (optional) ────────────────────────────────────────────

    [MaxLength(300)]
    [Url]
    public string? LinkedInProfile { get; set; }

    [MaxLength(300)]
    [Url]
    public string? PortfolioUrl { get; set; }

    // ── Application details ────────────────────────────────────────────────────

    [Range(0, 60)]
    public int? YearsOfExperience { get; set; }

    public DateTime? AvailableFrom { get; set; }

    [MaxLength(5000)]
    public string? CoverLetter { get; set; }

    public ApplicationSource Source { get; set; } = ApplicationSource.CompanyWebsite;

    /// <summary>
    /// Single-use token returned by <c>POST /api/public/cv-upload</c>, identifying the CV this
    /// application should claim. Optional — an application may be submitted without a CV.
    /// </summary>
    /// <remarks>
    /// This replaces the former <c>CvFilePath</c>, which was a free-text storage path supplied by
    /// an anonymous caller. Even with shape validation, accepting a path meant the client chose
    /// which stored file the candidate record pointed at. A token is unguessable, single-use, and
    /// bound server-side to the tenant and vacancy it was minted for.
    /// </remarks>
    [MaxLength(64)]
    public string? CvUploadToken { get; set; }

    /// <summary>
    /// Opt-in flag: candidate consents to being added to the talent pool for future vacancies.
    /// </summary>
    public bool AddToTalentPool { get; set; }

    // ── Structured profile collections ────────────────────────────────────────


    public List<ExternalWorkHistoryDto>    WorkHistories  { get; set; } = new();
    public List<ExternalQualificationDto>  Qualifications { get; set; } = new();
    public List<ExternalRefereeDto>        Referees       { get; set; } = new();
    public List<ExternalSkillDto>          Skills         { get; set; } = new();
    public List<ExternalLanguageDto>       Languages      { get; set; } = new();
}

/// <summary>One previous employment record submitted by an external candidate.</summary>
public class ExternalWorkHistoryDto
{
    /// <summary>Guid.Empty for new records; existing DB row Id for updates.</summary>
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string InstitutionName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string PositionHeld { get; set; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; set; }

    /// <summary>Null indicates the candidate is currently employed here.</summary>
    public DateOnly? EndDate { get; set; }

    [MaxLength(4000)]
    public string? Responsibilities { get; set; }

    [MaxLength(200)]
    public string? ReasonForLeaving { get; set; }
}

/// <summary>An education or certification record submitted by an external candidate.</summary>
public class ExternalQualificationDto
{
    /// <summary>Guid.Empty for new records; existing DB row Id for updates.</summary>
    public Guid Id { get; set; }

    [Required]
    public QualificationType QualificationType { get; set; } = QualificationType.Education;

    /// <summary>Free-text name of the qualification, e.g. "BSc Computer Science".</summary>
    [Required]
    [MaxLength(200)]
    public string QualificationName { get; set; } = string.Empty;

    /// <summary>Optional link to the Qualification catalogue. Null when the candidate typed free text.</summary>
    public Guid? QualificationId { get; set; }

    /// <summary>
    /// The rung of the qualification ladder (round 4, lane Q). The careers profile requires it for
    /// Education unless the catalogue entry picked already sits on one.
    /// </summary>
    public Guid? QualificationLevelId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Institution { get; set; } = string.Empty;

    [Required]
    public DateOnly DateAwarded { get; set; }

    [MaxLength(100)]
    public string? Grade { get; set; }
}

/// <summary>A referee provided by an external candidate as part of their application.</summary>
public class ExternalRefereeDto
{
    /// <summary>Guid.Empty for new records; existing DB row Id for updates.</summary>
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Position { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Organization { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Relationship { get; set; } = string.Empty;

    [Range(0, 60)]
    public int YearsKnown { get; set; }
}

/// <summary>A skill entry submitted by an external candidate.</summary>
public class ExternalSkillDto
{
    /// <summary>Guid.Empty for new records; existing DB row Id for updates.</summary>
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string SkillName { get; set; } = string.Empty;

    /// <summary>Optional link to the Skill catalogue. Null when the candidate typed free text.</summary>
    public Guid? SkillId { get; set; }

    public ProficiencyLevel? Proficiency { get; set; }

    [Range(0, 60)]
    public int? YearsOfExperience { get; set; }

    public bool IsCertified { get; set; }

    [MaxLength(200)]
    public string? CertificationName { get; set; }

    // Round 3, lane C1. Ignored (cleared) unless IsCertified is true.
    [MaxLength(100)]
    public string? CertificationNumber { get; set; }

    [MaxLength(200)]
    public string? CertifyingBody { get; set; }

    public DateTime? CertificationExpiryDate { get; set; }
}

/// <summary>A language spoken/written by an external candidate.</summary>
public class ExternalLanguageDto
{
    /// <summary>Guid.Empty for new records; existing DB row Id for updates.</summary>
    public Guid Id { get; set; }

    /// <summary>Catalogue row (round 3, lane C1). Either this or <see cref="LanguageName"/> is required.</summary>
    public Guid? LanguageId { get; set; }

    /// <summary>Free text when no catalogue row fits; mirrored from the catalogue when <see cref="LanguageId"/> is set.</summary>
    [MaxLength(100)]
    public string? LanguageName { get; set; }

    public ErpSystem.Core.Enums.LanguageProficiency Proficiency { get; set; } = ErpSystem.Core.Enums.LanguageProficiency.ProfessionalWorking;
}

/// <summary>Read DTO for a candidate language record.</summary>
public class JobCandidateLanguageDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid JobCandidateId { get; set; }
    public Guid? LanguageId { get; set; }
    public string? LanguageCode { get; set; }
    public string LanguageName { get; set; } = string.Empty;
    public ErpSystem.Core.Enums.LanguageProficiency Proficiency { get; set; }
    public string ProficiencyName => Proficiency.ToString();
}

/// <summary>
/// HR-side write of a candidate language (round 3, lane C1). Either the catalogue id or a name;
/// with an id the name is mirrored from the catalogue and any name sent is ignored.
/// </summary>
public class CreateJobCandidateLanguageDto : CreateDtoBase
{
    [Required]
    public Guid JobCandidateId { get; set; }

    public Guid? LanguageId { get; set; }

    [MaxLength(100)]
    public string? LanguageName { get; set; }

    public ErpSystem.Core.Enums.LanguageProficiency Proficiency { get; set; } = ErpSystem.Core.Enums.LanguageProficiency.ProfessionalWorking;
}

public class UpdateJobCandidateLanguageDto : UpdateDtoBase
{
    public Guid? LanguageId { get; set; }

    [MaxLength(100)]
    public string? LanguageName { get; set; }

    public ErpSystem.Core.Enums.LanguageProficiency Proficiency { get; set; }
}

/// <summary>
/// Returned to an external candidate immediately after a successful application.
/// Contains the tracking token so they can check status later.
/// </summary>
public class ExternalApplicationConfirmationDto
{
    public string  ApplicationNumber  { get; set; } = string.Empty;
    public string  TrackingToken      { get; set; } = string.Empty;
    public string  CandidateName      { get; set; } = string.Empty;
    public string  JobTitle           { get; set; } = string.Empty;
    public string  VacancyNumber      { get; set; } = string.Empty;
    public DateTime SubmittedAt       { get; set; }
}

/// <summary>
/// Handed back by <c>POST /api/public/cv-upload</c>. Carries a single-use token the applicant
/// echoes on their application, never a storage path or record id.
/// </summary>
public class PublicCvUploadTicketDto
{
    /// <summary>Opaque single-use token. Send this back as <c>CvUploadToken</c> when applying.</summary>
    public string UploadToken { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }

    /// <summary>After this the upload is swept and the applicant must upload again.</summary>
    public DateTime ExpiresAtUtc { get; set; }
}

/// <summary>
/// Public-facing application status DTO returned when a candidate polls with their tracking token.
/// Never exposes internal scoring, shortlisting notes, or recruiter details.
/// </summary>
public class PublicApplicationStatusDto
{
    public string ApplicationNumber  { get; set; } = string.Empty;
    public string CandidateName      { get; set; } = string.Empty;
    public string JobTitle           { get; set; } = string.Empty;
    public string VacancyNumber      { get; set; } = string.Empty;
    public DateTime SubmittedAt      { get; set; }

    /// <summary>Candidate-visible status label (no internal process terminology).</summary>
    public string StatusLabel        { get; set; } = string.Empty;

    /// <summary>User-friendly description of what this status means to the candidate.</summary>
    public string StatusDescription  { get; set; } = string.Empty;

    public bool CanWithdraw          { get; set; }
    public DateTime? WithdrawnAt     { get; set; }
    public DateTime? RejectedAt      { get; set; }

    /// <summary>Optional message shown to candidate (e.g. interview invite information).</summary>
    public string? MessageToCandidate { get; set; }
}

public class ExternalWithdrawDto
{
    [Required]
    [MaxLength(100)]
    public string TrackingToken { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

#endregion

#region Candidate Portal Account — Profile DTOs

// The auth DTOs (register/login/verify/forgot/reset/change-password) were deleted 2026-08-30
// with the candidate portal's own auth surface — candidates now self-register on the main JWT
// scheme with the Candidate role, through the standard AuthController flows. The profile,
// application and dashboard DTOs below survive: the main-scheme candidate surface reuses them.

// ── Profile ───────────────────────────────────────────────────────────────────

public class CandidatePortalProfileDto
{
    public Guid AccountId { get; set; }
    public Guid? CandidateId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public ErpSystem.Core.Enums.Gender? Gender { get; set; }
    public string? City { get; set; }
    /// <summary>Round 4, lane A — the resolved tier-1 name, so the portal can echo the full address back.</summary>
    public string? Region { get; set; }
    /// <summary>Round 4, lane A — the candidate re-opens their cascade from this on the next visit.</summary>
    public Guid? GeoAreaId { get; set; }
    public Guid? CountryId { get; set; }
    public string? CountryName { get; set; }
    public string? PostalAddress { get; set; }
    public string? DigitalAddress { get; set; }
    public string? LinkedInProfile { get; set; }
    public string? PortfolioUrl { get; set; }
    public string? GitHubUrl { get; set; }
    // Professional profile
    public string? Headline { get; set; }
    public string? ProfessionalSummary { get; set; }
    public string? CurrentJobTitle { get; set; }
    public string? CurrentEmployer { get; set; }
    public int? TotalYearsExperience { get; set; }
    // Availability & preferences
    public int? NoticePeriodDays { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public ErpSystem.Core.Enums.PreferredWorkArrangement PreferredWorkArrangement { get; set; }
    // Compensation
    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }
    public string? ExpectedSalaryCurrency { get; set; }
    // Compliance
    public ErpSystem.Core.Enums.WorkAuthorizationStatus WorkAuthorizationStatus { get; set; }
    // National identity (round 3, lane C1)
    public Guid? NationalIdTypeId { get; set; }
    public string? NationalIdTypeName { get; set; }
    public string? NationalIdNumber { get; set; }
    public DateTime? NationalIdExpiryDate { get; set; }
    // Documents
    public string? CvFilePath { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    /// <summary>Round 3, lane C2: whether a photograph is on file — the profile page fetches it through <c>GET profile/photo</c> only then.</summary>
    public bool HasPhoto { get; set; }
    public bool IsInTalentPool { get; set; }
    public List<JobCandidateWorkHistoryDto> WorkHistories { get; set; } = new();
    public List<JobCandidateQualificationDto> Qualifications { get; set; } = new();
    public List<JobCandidateRefereeDto> Referees { get; set; } = new();
    public List<JobCandidateSkillDto> Skills { get; set; } = new();
    public List<JobCandidateLanguageDto> Languages { get; set; } = new();
    public List<JobCandidateInterestDto> Interests { get; set; } = new();
    public List<JobCandidateDocumentDto> Documents { get; set; } = new();
}

public class UpdateCandidatePortalProfileDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? MiddleName { get; set; }
    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    [Required, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? AlternatePhone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public ErpSystem.Core.Enums.Gender? Gender { get; set; }
    /// <summary>
    /// ⚠ Overwritten by the resolved town or district when <see cref="GeoAreaId"/> is supplied —
    /// round 4, lane A. It remains the only address a candidate in a country with no geography
    /// scheme can give, which is most of the world.
    /// </summary>
    [MaxLength(100)]
    public string? City { get; set; }

    /// <summary>
    /// Where the candidate lives, from the shared geography tree — the lowest tier they chose.
    /// </summary>
    /// <remarks>
    /// ⚠ The careers form only shows the cascade when the chosen country has a published scheme;
    /// the endpoint answers 204 otherwise and the form falls back to the free-text city. Null here
    /// therefore means "no area", never "the candidate skipped a required step".
    /// </remarks>
    public Guid? GeoAreaId { get; set; }

    /// <summary>
    /// Optional, matching the entity. ⚠ Until 2026-09-14 this was a non-nullable <c>Guid</c> and
    /// the careers page sent <c>Guid.Empty</c> as a sentinel meaning "no country" — a handshake
    /// codified in <c>types/hr/careers.ts</c>. The sentinel is retired: send null, or omit it.
    /// A supplied country is checked against the tenant's live list, which this door never did.
    /// </summary>
    public Guid? CountryId { get; set; }
    [MaxLength(200)]
    public string? PostalAddress { get; set; }
    [MaxLength(30)]
    public string? DigitalAddress { get; set; }
    [MaxLength(200), Url]
    public string? LinkedInProfile { get; set; }
    [MaxLength(200), Url]
    public string? PortfolioUrl { get; set; }
    [MaxLength(200), Url]
    public string? GitHubUrl { get; set; }
    // Professional profile
    [MaxLength(300)]
    public string? Headline { get; set; }
    [MaxLength(4000)]
    public string? ProfessionalSummary { get; set; }
    [MaxLength(200)]
    public string? CurrentJobTitle { get; set; }
    [MaxLength(200)]
    public string? CurrentEmployer { get; set; }
    [Range(0, 60)]
    public int? TotalYearsExperience { get; set; }
    // Availability & preferences
    [Range(0, 1825)]
    public int? NoticePeriodDays { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public ErpSystem.Core.Enums.PreferredWorkArrangement PreferredWorkArrangement { get; set; }
    // Compensation
    [Range(0, 100_000_000)]
    public decimal? ExpectedSalaryMin { get; set; }
    [Range(0, 100_000_000)]
    public decimal? ExpectedSalaryMax { get; set; }
    [MaxLength(10)]
    public string? ExpectedSalaryCurrency { get; set; }
    // Compliance
    public ErpSystem.Core.Enums.WorkAuthorizationStatus WorkAuthorizationStatus { get; set; }
    // National identity (round 3, lane C1)
    public Guid? NationalIdTypeId { get; set; }
    [MaxLength(50)]
    public string? NationalIdNumber { get; set; }
    public DateTime? NationalIdExpiryDate { get; set; }
    // Documents
    // ⚠ No ProfilePhotoUrl. The candidate sets their photo by uploading it through the gate,
    // which stores a scanned upload record and leaves the legacy public URL null. Accepting a
    // URL here let an external user write an arbitrary string onto a record HR then reads.
    public bool IsInTalentPool { get; set; }
    public List<ExternalWorkHistoryDto> WorkHistories { get; set; } = new();
    public List<ExternalQualificationDto> Qualifications { get; set; } = new();
    public List<ExternalRefereeDto> Referees { get; set; } = new();
    public List<ExternalSkillDto> Skills { get; set; } = new();
    public List<ExternalLanguageDto> Languages { get; set; } = new();
    public List<ExternalInterestDto> Interests { get; set; } = new();
}

/// <summary>A professional interest or hobby declared by the candidate.</summary>
public class ExternalInterestDto
{
    /// <summary>Guid.Empty for new records; existing DB row Id for updates.</summary>
    public Guid Id { get; set; }

    [Required, MaxLength(500)]
    public string Detail { get; set; } = string.Empty;
}

// ── Applications ──────────────────────────────────────────────────────────────

public class CandidatePortalApplicationSummaryDto
{
    public Guid ApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string? TrackingToken { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string VacancyNumber { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? LocationName { get; set; }
    public string EmploymentTypeLabel { get; set; } = string.Empty;
    public ErpSystem.Core.Enums.ApplicationStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime ApplicationDate { get; set; }
    public DateTime? ShortlistedDate { get; set; }
    public DateTime? WithdrawnDate { get; set; }
    public DateTime? RejectedDate { get; set; }
    public bool CanWithdraw { get; set; }
}

public class CandidatePortalWithdrawDto
{
    [MaxLength(1000)]
    public string? Reason { get; set; }
}

/// <summary>Payload for saving a draft application (no submission yet).</summary>
public class CandidatePortalSaveDraftDto
{
    [Required]
    public Guid VacancyId { get; set; }
    [MaxLength(5000)]
    public string? CoverLetter { get; set; }
    public int? YearsOfExperience { get; set; }
    public DateTime? AvailableFrom { get; set; }
    /// <summary>
    /// ⚠ Not read (round 3, lane A). The careers surface is the company website; an application made
    /// from a posting link takes its source from the posting's channel. Kept so old clients bind.
    /// </summary>
    public ErpSystem.Core.Enums.ApplicationSource Source { get; set; } = ErpSystem.Core.Enums.ApplicationSource.CompanyWebsite;
    /// <summary>The advert the candidate came through (`/careers/{vacancyId}?posting={id}`); must belong to the vacancy.</summary>
    public Guid? JobPostingId { get; set; }
    public bool AddToTalentPool { get; set; }
}

/// <summary>Payload for submitting an existing draft application.</summary>
public class CandidatePortalSubmitDraftDto
{
    /// <summary>Optional final edits applied at submit time.</summary>
    [MaxLength(5000)]
    public string? CoverLetter { get; set; }
    public int? YearsOfExperience { get; set; }
    public DateTime? AvailableFrom { get; set; }
}

public class CandidatePortalDashboardDto
{
    public CandidatePortalProfileDto Profile { get; set; } = new();
    public List<CandidatePortalApplicationSummaryDto> Applications { get; set; } = new();
    public int TotalApplications { get; set; }
    public int ActiveApplications { get; set; }
    public int ShortlistedCount { get; set; }
}

#endregion

// ============================================================================
// SECTION — ENTERPRISE TALENT POOL MANAGEMENT
// ============================================================================

#region Talent Pool — Segments

public class CandidateTalentSegmentDto : BaseDto
{
    public Guid   TenantId    { get; set; }
    public string Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color       { get; set; }
    public bool   IsActive    { get; set; }
    /// <summary>
    /// Live memberships. ⚠ Only correct when the segment was read with its <c>Memberships</c>
    /// included — every read path before round 3 lane V answered 0 here for that reason.
    /// </summary>
    public int    MemberCount { get; set; }

    // ── Ownership and intent (round 3, lane V; D-6) ──────────────────────────
    public Guid?   OwnerEmployeeId      { get; set; }
    /// <summary>Resolved from the navigation; null when no owner is named.</summary>
    public string? OwnerEmployeeName    { get; set; }
    public string? Purpose              { get; set; }
    public Guid?   TargetPositionId     { get; set; }
    public string? TargetPositionTitle  { get; set; }
    public Guid?   JobFamilyId          { get; set; }
    public string? JobFamilyName        { get; set; }
}

public class CreateCandidateTalentSegmentDto
{
    [Required, MaxLength(150)]
    public string  Name        { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Description { get; set; }
    [MaxLength(30)]
    public string? Color       { get; set; }

    // Lane V (D-6). All optional; each is refused unless it is this tenant's live row.
    public Guid?   OwnerEmployeeId  { get; set; }
    [MaxLength(1000)]
    public string? Purpose          { get; set; }
    public Guid?   TargetPositionId { get; set; }
    public Guid?   JobFamilyId      { get; set; }
}

/// <summary>
/// Updating a talent segment.
/// </summary>
/// <remarks>
/// <para>⚠ <b>This is a whole-record payload, not a patch (G-13.4).</b> Every field is assigned
/// unconditionally by <c>UpdateEntity</c>, so <b>a field you omit is a field you clear</b> —
/// including <c>OwnerEmployeeId</c>, <c>Purpose</c>, <c>TargetPositionId</c> and
/// <c>JobFamilyId</c>. That is deliberate: clearing a segment's owner on the form has to be
/// possible, and a null that means "leave it alone" cannot also mean "remove it".</para>
///
/// <para>The risk is a client that does not know. The talent-pool page handles this correctly and
/// says why, but the contract lived only in that page's comment — so any other caller, or a future
/// partial-update screen, would silently wipe a segment's owner and purpose while changing its
/// colour. Stated here because this is what a new client reads.</para>
///
/// <para><b>Always send every field.</b> Load the segment, change what you mean to change, send
/// the whole thing back. Same convention as the other replace-set payloads in this module.</para>
/// </remarks>
public class UpdateCandidateTalentSegmentDto
{
    [Required]
    public Guid   Id          { get; set; }
    [Required, MaxLength(150)]
    public string  Name        { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? Description { get; set; }
    [MaxLength(30)]
    public string? Color       { get; set; }
    public bool   IsActive    { get; set; }

    // Lane V (D-6). Sent every time: a null clears the owner / target, it does not mean "leave it".
    public Guid?   OwnerEmployeeId  { get; set; }
    [MaxLength(1000)]
    public string? Purpose          { get; set; }
    public Guid?   TargetPositionId { get; set; }
    public Guid?   JobFamilyId      { get; set; }
}

public class CandidateSegmentMembershipDto : BaseDto
{
    public Guid    TenantId    { get; set; }
    public Guid    JobCandidateId { get; set; }
    public Guid    SegmentId   { get; set; }
    public string  SegmentName { get; set; } = string.Empty;
    public string? SegmentColor { get; set; }
    public DateTime AddedDate  { get; set; }
    public string? Notes       { get; set; }
}

public class AddCandidateToSegmentDto
{
    [Required]
    public Guid    SegmentId  { get; set; }
    [MaxLength(500)]
    public string? Notes      { get; set; }
}

#endregion

#region Talent Pool — Engagement Events

public class CandidateEngagementEventDto : BaseDto
{
    public Guid     TenantId        { get; set; }
    public Guid     JobCandidateId  { get; set; }
    public string   CandidateName   { get; set; } = string.Empty;
    public ErpSystem.Core.Enums.CandidateEngagementEventType EventType { get; set; }
    public string   EventTypeName   => EventType.ToString();
    public DateTime EventDate       { get; set; }
    public string?  Subject         { get; set; }
    public string?  Notes           { get; set; }
    public string?  RecordedByName  { get; set; }
    public bool     IsInternal      { get; set; }
}

public class CreateCandidateEngagementEventDto
{
    [Required]
    public Guid     JobCandidateId { get; set; }
    [Required]
    public ErpSystem.Core.Enums.CandidateEngagementEventType EventType { get; set; }
    [Required]
    public DateTime EventDate      { get; set; }
    [MaxLength(300)]
    public string?  Subject        { get; set; }
    [MaxLength(2000)]
    public string?  Notes          { get; set; }
    public bool     IsInternal     { get; set; }
}

#endregion

#region Talent Pool — Rich Pool DTOs

/// <summary>
/// Rich read DTO for a candidate in the talent pool — extends JobCandidateDto
/// with pool-specific fields, segment names, and engagement summary.
/// </summary>
public class TalentPoolCandidateDto : JobCandidateDto
{
    public ErpSystem.Core.Enums.TalentPoolEntrySource      TalentPoolSource       { get; set; }
    public string   TalentPoolSourceName  => TalentPoolSource.ToString();
    public ErpSystem.Core.Enums.TalentPoolCandidateStatus  TalentPoolStatus       { get; set; }
    public string   TalentPoolStatusName  => TalentPoolStatus.ToString();
    public string?  TalentPoolNotes       { get; set; }
    public DateTime? TalentPoolReviewDate { get; set; }
    /// <summary>Why they last left the pool — shown when a removed candidate is looked up again.</summary>
    public string?  TalentPoolRemovalReason { get; set; }
    public DateTime? LastEngagedDate      { get; set; }
    public int      DaysInPool            { get; set; }
    public int      EngagementCount       { get; set; }
    public bool     IsOverdueForReview    => TalentPoolReviewDate.HasValue && TalentPoolReviewDate.Value < DateTime.UtcNow;
    public List<CandidateSegmentMembershipDto> Segments { get; set; } = new();
}

/// <summary>Filter criteria for querying the talent pool.</summary>
public class TalentPoolFilterDto
{
    public string?  Search              { get; set; }
    public List<Guid> SegmentIds        { get; set; } = new();
    public ErpSystem.Core.Enums.TalentPoolCandidateStatus? Status          { get; set; }
    public ErpSystem.Core.Enums.TalentPoolEntrySource?     Source          { get; set; }
    public ErpSystem.Core.Enums.PreferredWorkArrangement?  WorkArrangement { get; set; }
    public int?     MinExperienceYears  { get; set; }
    public int?     MaxExperienceYears  { get; set; }
    public DateTime? AvailableBefore    { get; set; }
    public bool?    OverdueForReview    { get; set; }
    public int?     DormantMoreThanDays { get; set; }

    /// <summary>
    /// Where they are, by the geography tree: an area matches candidates recorded in it AND
    /// anywhere beneath it (round 4, lane B2).
    /// </summary>
    /// <remarks>
    /// <para>Subtree containment, not equality, for the same reason the Location criterion works
    /// that way: "who do we have in Greater Accra?" must find the person recorded in Tema, and
    /// nobody types the region when the cascade offers them the district.</para>
    ///
    /// <para>&#9888; A candidate with only a typed city is NOT matched. The text fallback belongs
    /// to scoring, where a criterion carries the area's name to compare against; a filter has no
    /// such name and guessing at one would quietly widen the answer. Use the search box for a
    /// typed city.</para>
    /// </remarks>
    public Guid?    GeoAreaId           { get; set; }

    public int      PageNumber          { get; set; } = 1;
    public int      PageSize            { get; set; } = 25;
    public string   SortBy              { get; set; } = "LastName";
    public bool     SortDescending      { get; set; }
}

/// <summary>Analytics / summary statistics for the talent pool dashboard.</summary>
public class TalentPoolAnalyticsDto
{
    public int    TotalInPool          { get; set; }
    public int    Active               { get; set; }
    public int    Passive              { get; set; }
    public int    Dormant              { get; set; }
    public int    OverdueForReview     { get; set; }
    public int    ConvertedThisYear    { get; set; }
    public int    AddedThisMonth       { get; set; }
    public int    AddedThisYear        { get; set; }
    public double AvgDaysInPool        { get; set; }
    public List<TalentPoolSourceBreakdownDto>  BySource  { get; set; } = new();
    public List<TalentPoolSegmentBreakdownDto> BySegment { get; set; } = new();
}

public class TalentPoolSourceBreakdownDto
{
    public string SourceName { get; set; } = string.Empty;
    public int    Count      { get; set; }
}

public class TalentPoolSegmentBreakdownDto
{
    public Guid   SegmentId   { get; set; }
    public string SegmentName { get; set; } = string.Empty;
    public string? SegmentColor { get; set; }
    public int    Count       { get; set; }
}

/// <summary>Enhanced DTO for adding a candidate to the talent pool with full metadata.</summary>
public class AddToTalentPoolDto
{
    public ErpSystem.Core.Enums.TalentPoolEntrySource Source { get; set; } = ErpSystem.Core.Enums.TalentPoolEntrySource.RecruiterAdded;
    public List<Guid> SegmentIds { get; set; } = new();
    [MaxLength(2000)]
    public string?   Notes      { get; set; }
    public DateTime? ReviewDate { get; set; }
}

/// <summary>DTO for removing a candidate from the talent pool with a reason.</summary>
public class RemoveFromTalentPoolDto
{
    [MaxLength(1000)]
    public string? Reason { get; set; }
    [MaxLength(2000)]
    public string? Notes  { get; set; }
}

/// <summary>
/// Wrapped body for the pool-status PATCH. A raw JSON enum scalar worked but was easy to get
/// wrong from a typed client and left nowhere to hang a future reason field.
/// </summary>
public class UpdateTalentPoolStatusDto
{
    [Required]
    public ErpSystem.Core.Enums.TalentPoolCandidateStatus Status { get; set; }
}

/// <summary>Wrapped body for the review-date PATCH; same reasoning as the status wrapper.</summary>
public class UpdateTalentPoolReviewDateDto
{
    [Required]
    public DateTime ReviewDate { get; set; }
}

/// <summary>Bulk operation against multiple talent pool candidates.</summary>
public class BulkTalentPoolOperationDto
{
    [Required, MinLength(1)]
    public List<Guid> CandidateIds { get; set; } = new();
    [Required]
    public ErpSystem.Core.Enums.BulkTalentPoolOperation Operation { get; set; }
    // Used when Operation = AssignSegment | RemoveSegment
    public Guid?  SegmentId { get; set; }
    // Used when Operation = SetStatus
    public ErpSystem.Core.Enums.TalentPoolCandidateStatus? Status { get; set; }
    [MaxLength(1000)]
    public string? Notes    { get; set; }
}

/// <summary>Result row when matching talent pool candidates to an open vacancy.</summary>
public class TalentPoolVacancyMatchResultDto
{
    public Guid     CandidateId     { get; set; }
    public string   CandidateName   { get; set; } = string.Empty;
    public string   CandidateNumber { get; set; } = string.Empty;
    public string?  Headline        { get; set; }
    public int?     TotalYearsExperience          { get; set; }
    public string?  PreferredWorkArrangementName  { get; set; }
    public DateTime? AvailableFrom  { get; set; }
    public int      MatchScore      { get; set; }

    /// <summary>The highest score the rubric can award, so a reader knows what 65 means (G-13.2).</summary>
    /// <remarks>The score was rendered bare, with no denominator and no unit, so nobody could tell
    /// whether it was out of 90 or 100 or what a good one looked like. Sent rather than hardcoded
    /// client-side: the weights live in the service and a mirrored constant would drift.</remarks>
    public int      MatchScoreMax   { get; set; } = 90;
    public List<string> MatchReasons { get; set; } = new();
}

/// <summary>Result row when suggesting open vacancies that match a specific candidate's profile.</summary>
public class CandidateVacancyMatchResultDto
{
    public Guid      VacancyId          { get; set; }
    public string    VacancyNumber      { get; set; } = string.Empty;
    public string    JobTitle           { get; set; } = string.Empty;
    public JobVacancyStatus VacancyStatus { get; set; }
    public string    VacancyStatusName  { get; set; } = string.Empty;
    public DateTime? ApplicationDeadline { get; set; }
    public string?   HiringManagerName  { get; set; }
    public int       NumberOfPositions  { get; set; }
    public int       MatchScore         { get; set; }

    /// <inheritdoc cref="TalentPoolVacancyMatchResultDto.MatchScoreMax"/>
    public int       MatchScoreMax      { get; set; } = 90;
    public List<string> MatchReasons    { get; set; } = new();
}

/// <summary>Paged result wrapper for talent pool candidates.</summary>
public class TalentPoolPagedResultDto
{
    public List<TalentPoolCandidateDto> Items      { get; set; } = new();
    public int  TotalCount  { get; set; }
    public int  Page        { get; set; }
    public int  PageSize    { get; set; }
    public int  TotalPages  => (int)Math.Ceiling((double)TotalCount / PageSize);
}

// -- Screening the pool by real criteria (round 4, lane B) ---------------------------------------

/// <summary>
/// One criterion in an ad-hoc screen - the same shape a vacancy's criterion has, minus the vacancy.
/// </summary>
/// <remarks>
/// Resolved and validated by the SAME <c>IShortlistingCriteriaResolver</c> a saved criterion goes
/// through, so a mandatory Gender, a numeric criterion with no bound, a list criterion with no
/// values and an area that is not on the tree are refused here exactly as they are on a vacancy.
/// Nothing is written: the rows exist for the length of the request.
/// </remarks>
public class AdHocScreeningCriterionDto
{
    [Required]
    [MaxLength(100)]
    public string CriteriaName { get; set; } = string.Empty;

    [Required]
    public JobShortlistingCriteriaType Type { get; set; }

    [MaxLength(500)]
    public string? RequiredValue { get; set; }

    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsMandatory { get; set; }

    public MandatoryMatchMode MatchMode { get; set; } = MandatoryMatchMode.AnyMatched;
    public ValueMatchStrategy MatchStrategy { get; set; } = ValueMatchStrategy.Exact;

    [Range(1, 100)]
    public int Weight { get; set; } = 1;

    public ShortlistingComparisonOperator? ComparisonOperator { get; set; }

    /// <summary>The accepted values, as the whole set - same rules as a saved criterion.</summary>
    public List<ShortlistingCriteriaValueInputDto>? Values { get; set; }
}

/// <summary>
/// What to screen: which slice of the pool, how many rows back, and - for the ad-hoc door - the
/// criteria to screen by.
/// </summary>
/// <remarks>
/// <para>The filter is the ordinary <see cref="TalentPoolFilterDto"/> the pool list already uses,
/// so "screen the people I am looking at" needs no second filter vocabulary. Its paging fields are
/// ignored: screening reads the whole matching set and <see cref="TopN"/> trims the ANSWER, which
/// is a different question from paging the list.</para>
///
/// <para>&#9888; <see cref="IncludeNonMatching"/> defaults to true deliberately. A screen that
/// silently dropped everyone who failed a mandatory criterion would answer "nobody in the pool is
/// close", and a recruiter would have no way to see the near miss they might waive. The rows carry
/// the verdict; hiding them is the reader's choice, not the engine's.</para>
/// </remarks>
public class TalentPoolScreenRequestDto
{
    public TalentPoolFilterDto? Filter { get; set; }

    [Range(1, 500)]
    public int TopN { get; set; } = 50;

    public bool IncludeNonMatching { get; set; } = true;

    /// <summary>
    /// The ad-hoc criteria, for <c>POST api/talent-pool/screen</c>. Ignored by the by-vacancy door,
    /// which reads the vacancy's own live criteria - the whole point being that the pool is judged
    /// by what the vacancy actually says.
    /// </summary>
    public List<AdHocScreeningCriterionDto>? Criteria { get; set; }
}

/// <summary>One pool member, scored against the criteria the request named.</summary>
public class TalentPoolScreenRowDto
{
    public Guid    CandidateId     { get; set; }
    public string  CandidateName   { get; set; } = string.Empty;
    public string  CandidateNumber { get; set; } = string.Empty;
    public string  Email           { get; set; } = string.Empty;
    public string? Headline        { get; set; }
    public string? City            { get; set; }
    public Guid?   GeoAreaId       { get; set; }
    public int?    TotalYearsExperience { get; set; }
    public string? PreferredWorkArrangementName { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public bool    HasPhoto        { get; set; }
    public bool    IsInTalentPool  { get; set; }

    /// <summary>
    /// The criteria score out of <see cref="CriteriaScoreMax"/>, or null when nothing about this
    /// candidate could be measured.
    /// </summary>
    /// <remarks>
    /// &#9888; Null and 0 mean different things and are rendered differently. 0 is "measured, and
    /// missed everything"; null is "the criteria asked questions this record cannot answer".
    /// Collapsing them would put a candidate nobody knows anything about at the bottom of the list
    /// beside one who was checked and genuinely does not fit - or, if the fallback went the other
    /// way, at the top. Both are the fault this module keeps having to remove.
    /// </remarks>
    public decimal? CriteriaScore   { get; set; }

    public decimal CriteriaScoreMax { get; set; } = 100m;

    /// <summary>False when a criterion marked mandatory was missed - the score is then 0.</summary>
    public bool AllMandatoryPassed  { get; set; } = true;

    /// <summary>The weight actually measured; criteria left out of the score do not count here.</summary>
    public decimal TotalWeight      { get; set; }

    /// <summary>Whether this candidate already has an application against the screened vacancy.</summary>
    public bool AlreadyApplied      { get; set; }

    public List<CriterionScoreResult> Breakdown { get; set; } = new();
}

/// <summary>The answer to a screen: what was screened, against what, and who came back.</summary>
public class TalentPoolScreenResultDto
{
    /// <summary>Null for an ad-hoc screen, which is not about any one vacancy.</summary>
    public Guid?   VacancyId     { get; set; }
    public string? VacancyNumber { get; set; }
    public string? JobTitle      { get; set; }

    /// <summary>How many pool members the filter selected, before <c>TopN</c> trimmed the answer.</summary>
    public int ScreenedCount  { get; set; }

    /// <summary>How many were scored at all - a member nothing could be measured about is not one.</summary>
    public int ScoredCount    { get; set; }

    /// <summary>How many met every mandatory criterion.</summary>
    public int QualifiedCount { get; set; }

    /// <summary>
    /// The criteria the screen actually ran, named and weighted, so a reader can see what the score
    /// is made of without opening the vacancy.
    /// </summary>
    public List<ScreeningCriterionSummaryDto> Criteria { get; set; } = new();

    public List<TalentPoolScreenRowDto> Rows { get; set; } = new();
}

/// <summary>A criterion as it was applied to a screen - what it measures, and how heavily.</summary>
public class ScreeningCriterionSummaryDto
{
    public Guid?  CriteriaId   { get; set; }
    public string CriteriaName { get; set; } = string.Empty;
    public JobShortlistingCriteriaType Type { get; set; }
    public string TypeName     => Type.ToString();
    public bool   IsMandatory  { get; set; }
    public int    Weight       { get; set; }
    public string? AcceptedValues { get; set; }
}

/// <summary>Invite pool members to apply for a vacancy - the first of lane B's two bulk acts.</summary>
public class TalentPoolInviteToApplyDto
{
    [Required]
    public Guid JobVacancyId { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> CandidateIds { get; set; } = new();

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Whether the candidate is emailed. Default true; the send is best-effort and never fails the
    /// application it belongs to, exactly as the other recruitment notifications are.
    /// </summary>
    public bool SendEmail { get; set; } = true;
}

/// <summary>Book pool members into an existing interview session - the second bulk act.</summary>
/// <remarks>
/// &#9888; Requires each candidate to already have an application against that interview's vacancy.
/// Decision Q2: shortlisting and interviewing belong to an application, and manufacturing one
/// silently here would lose the trail of who decided to consider this person and when. A candidate
/// with no application is skipped with a reason that says to invite them first.
/// </remarks>
public class TalentPoolBookInterviewDto
{
    [Required]
    public Guid JobInterviewId { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> CandidateIds { get; set; } = new();

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// OFFER LETTER (generated document)
// ============================================================================

/// <summary>
/// A rendered offer-of-employment letter for a <c>JobOffer</c>, produced by
/// <c>IOfferLetterService</c> from the offer terms enriched with job-description, salary-breakdown,
/// benefit and pre-employment-condition data, merged into the HR-editable "OfferLetter" template.
/// Delivered as a portal/print view (print-to-PDF) and as the offer email body.
/// </summary>
public class OfferLetterDto
{
    public Guid OfferId { get; set; }
    public string OfferNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>Rendered subject line (used when the letter is emailed).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Rendered, self-contained HTML document body suitable for display and print-to-PDF.</summary>
    public string HtmlBody { get; set; } = string.Empty;
}

// ── The printed interview paper (round 4, lane F) ───────────────────────────────────────────────

/// <summary>Which paper to print.</summary>
public enum InterviewPaperVariant
{
    /// <summary>One sheet per candidate per panelist, with score boxes. The thing that gets signed.</summary>
    ScoreSheet = 1,

    /// <summary>
    /// The drawn questions alone, for the panel to read beforehand. No score boxes.
    /// </summary>
    /// <remarks>
    /// ⚠ This one reveals the questions, so it stays behind the same per-record gate as everything
    /// else on the interview. A blank scoring sheet leaks nothing; a question list does.
    /// </remarks>
    Questions = 2,

    /// <summary>The cover page — panel and timetable — followed by every scoring sheet.</summary>
    Pack = 3,
}

/// <summary>The rendered paper. HTML the client prints; there is no PDF by design.</summary>
public class InterviewPaperDto
{
    public Guid InterviewId { get; set; }
    public string InterviewNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public InterviewPaperVariant Variant { get; set; }

    /// <summary>How many physical sheets this will print — so the screen can say so before it does.</summary>
    public int SheetCount { get; set; }

    /// <summary>
    /// The whole document. Each sheet is a <c>&lt;section class='interview-paper-sheet'&gt;</c>
    /// carrying its own page break, so the print stylesheet has something to hang on.
    /// </summary>
    public string HtmlBody { get; set; } = string.Empty;
}

// ── The panelist's own scorecard worklist (round 4, lane F5) ────────────────────────────────────

/// <summary>
/// Where one of the caller's scorecards has got to. Derived, not stored.
/// </summary>
public enum PanelistScorecardState
{
    /// <summary>Nothing saved at all.</summary>
    NotStarted = 1,

    /// <summary>A private draft exists. Nobody else can see it, and it does not count as filed.</summary>
    Draft = 2,

    /// <summary>A scorecard is saved and visible to the panel, but not yet signed off.</summary>
    Saved = 3,

    /// <summary>Signed off. It can no longer be changed.</summary>
    SignedOff = 4,
}

/// <summary>One candidate the caller has to score, and how far they have got.</summary>
/// <remarks>
/// ⚠ Carries the <b>caller's own</b> card only. A colleague's mark never appears here, whatever the
/// blind-scoring rule would allow elsewhere — this is a to-do list, and a to-do list showing
/// somebody else's answer is the anchoring problem with a different shape.
/// </remarks>
public class PanelistScorecardCandidateDto
{
    public Guid IntervieweeId { get; set; }
    public Guid JobApplicationId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string ApplicationNumber { get; set; } = string.Empty;

    /// <summary>Their slot, when the day was apportioned. Null means "sometime in the session".</summary>
    public TimeSpan? SlotStartTime { get; set; }
    public TimeSpan? SlotEndTime { get; set; }

    /// <summary>Whether they turned up. Null before the day.</summary>
    public bool? CandidateAttended { get; set; }

    public Guid? ScoreSummaryId { get; set; }
    public PanelistScorecardState State { get; set; } = PanelistScorecardState.NotStarted;
    public decimal? TotalWeightedScore { get; set; }
    public JobInterviewRecommendation? Recommendation { get; set; }
    public string? RecommendationName => Recommendation?.ToString();

    /// <summary>True once nothing further is owed for this candidate.</summary>
    public bool IsComplete => State == PanelistScorecardState.SignedOff;
}

/// <summary>
/// One interview the caller sits on, with the candidates they owe a scorecard for.
/// </summary>
/// <remarks>
/// <para><b>Why this exists</b> (round 4, lane F5). A panelist's diary listed interview <i>numbers</i>
/// and nothing else, while describing itself as "the scorecards you owe" — so the only route to a
/// scorecard ran through HR's desk screen and a tab. This is the read that lets the portal keep
/// that promise: who you are seeing, when, and what you still owe.</para>
///
/// <para>⚠ <b>Takes the employee from the token</b>, like <c>me/panelist-slots</c>. The id-bearing
/// twin would mean the client fetching its own employee id and handing it back, which is the shape
/// that produced this module's authorization holes.</para>
/// </remarks>
public class PanelistScorecardWorklistDto
{
    public Guid InterviewId { get; set; }
    public string InterviewNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string VacancyNumber { get; set; } = string.Empty;
    public int Round { get; set; }

    public JobInterviewType Type { get; set; }
    public string TypeName => Type.ToString();
    public InterviewMode Mode { get; set; }
    public string ModeName => Mode.ToString();
    public JobInterviewStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateOnly ScheduledDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? LocationOrLink { get; set; }

    /// <summary>The caller's own seat on this panel — the id the scorecard is filed against.</summary>
    public Guid PanelistId { get; set; }
    public JobInterviewPanelistRole Role { get; set; }
    public string RoleName => Role.ToString();
    public bool IsRequired { get; set; }
    public bool IsConfirmed { get; set; }

    /// <summary>Whether this session has a question plan the panel can be held to.</summary>
    public bool HasQuestionPlan { get; set; }

    public List<PanelistScorecardCandidateDto> Candidates { get; set; } = new();

    /// <summary>How many scorecards are still owed. Zero is the state a panelist is working towards.</summary>
    public int OutstandingCount => Candidates.Count(c => !c.IsComplete);

    /// <summary>
    /// Whether anything can still be filed. A cancelled or completed session is read-only, and a
    /// screen that offers a scorecard on one is offering a 422.
    /// </summary>
    public bool IsOpen => Status != JobInterviewStatus.Cancelled && Status != JobInterviewStatus.Completed;
}

// ── The offer defaults proposal (round 4, lane G) ───────────────────────────────────────────────

/// <summary>
/// What the system proposes for a new offer, and — for every value — where it came from.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> The offer create path already filled several gaps
/// <i>server-side at save time</i>: probation and notice months fell back to the position's, weekly
/// hours to 20 or 40, the position snapshot and the benefit list were seeded outright. All of that
/// happened <b>after</b> HR pressed Save, invisibly — so the form showed empty boxes, HR typed
/// values that were then silently overridden or silently defaulted, and nobody could see which
/// number was theirs and which was the system's.</para>
///
/// <para>This read moves the proposal to <b>before</b> the form is filled in. Every value is
/// editable; every value carries its source in <see cref="Sources"/> so the screen can say
/// <i>"14 days — from the offer validity policy"</i> under the box.</para>
///
/// <para>⚠ <b><see cref="Sources"/> carries an entry only where a value was actually resolved.</b>
/// A field the system could not derive comes back null with <b>no</b> source line, rather than a
/// sentence explaining a number that is not there. Rendering a source for an absent value would be
/// the same fault as an empty criterion scoring full marks: an unanswerable question dressed up as
/// an answer.</para>
/// </remarks>
public class JobOfferDefaultsDto
{
    public Guid JobApplicationId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public string VacancyNumber { get; set; } = string.Empty;
    public string PositionTitle { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string EmploymentTypeName { get; set; } = string.Empty;

    // ── placement ──
    public Guid? LocationLevelId { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }

    // ── terms ──
    public int? ContractDurationMonths { get; set; }
    public int? ProbationPeriodMonths { get; set; }
    public int? NoticePeriodMonths { get; set; }
    public int? AnnualLeaveDays { get; set; }
    public decimal? WeeklyHours { get; set; }
    public bool IsConditional { get; set; }
    public DateOnly? ProposedStartDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    // ── money ──
    public Guid? SalaryGradeId { get; set; }
    public string? SalaryGradeName { get; set; }
    public decimal? SalaryGradeMin { get; set; }
    public decimal? SalaryGradeMax { get; set; }
    public Guid? SalaryLevelId { get; set; }
    public string? SalaryLevelName { get; set; }
    public Guid? SalaryNotchId { get; set; }
    public int? SalaryNotchNumber { get; set; }
    public decimal? BaseSalary { get; set; }
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// camelCase field name → the one-line explanation of where that value came from. Only
    /// populated for values that were actually resolved.
    /// </summary>
    public Dictionary<string, string> Sources { get; set; } = new();

    /// <summary>
    /// Things the system could not propose and the reason, so the screen can say what HR has to
    /// supply by hand rather than leaving a silently empty box.
    /// </summary>
    public List<string> Unresolved { get; set; } = new();
}
