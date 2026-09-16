using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// JOB OFFER SERVICE
// ============================================================================

public interface IJobOfferService
{
    // Queries
    Task<JobOfferDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JobOfferDto?> GetByOfferNumberAsync(string offerNumber, CancellationToken cancellationToken = default);
    Task<JobOfferDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JobOfferDto?> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobOfferSummaryDto>> GetAllSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the tenant's offers, newest first.
    /// </summary>
    /// <remarks>
    /// ⚠ G-10.3 (2026-09-15). The offers list's <b>default</b> view called
    /// <see cref="GetAllSummaryAsync"/>, which returns every offer in the tenant in one response.
    /// That is worse than the requisitions and vacancies lists, where the unpaged read only fires
    /// when a status filter is chosen — here it was what loaded when the screen was opened, with
    /// no pager, no total and no disclosure. This was the only recruitment list with no bound at
    /// all. <see cref="GetAllSummaryAsync"/> is kept for callers that genuinely need every row.
    /// </remarks>
    Task<PagedResult<JobOfferSummaryDto>> GetPagedSummaryAsync(
        int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobOfferSummaryDto>> GetByStatusAsync(JobOfferStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobOfferSummaryDto>> GetExpiringOffersAsync(int daysAhead = 7, CancellationToken cancellationToken = default);

    // CRUD
    Task<JobOfferDto> CreateAsync(CreateJobOfferDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<JobOfferDto> UpdateAsync(UpdateJobOfferDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    Task<bool> SubmitForApprovalAsync(Guid offerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The preparer withdrawing an offer that is out for approval, returning it to Draft.
    /// ⚠ The generic recall button in <c>WorkflowApprovalActions</c> calls the engine directly, so
    /// any rule added here is enforced for API callers but bypassed by that button — the same split
    /// PIP and requisitions have.
    /// </summary>
    /// <summary>
    /// Withdraws an offer awaiting approval back to Draft.
    /// </summary>
    /// <param name="recalledByEmployeeId">
    /// The caller's <b>Employee</b> id, compared against <c>PreparedById</c> so only the person who
    /// prepared the offer can take it back. Added 2026-09-15 with G-10.1: with no workflow
    /// definition published there is no engine instance to enforce its own requester-only rule, so
    /// the check has to be made here. On the configured path the engine still enforces it too.
    /// </param>
    Task<bool> RecallApprovalAsync(Guid offerId, Guid recalledByEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> ApproveAsync(ApproveJobOfferDto dto, Guid approvedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RejectApprovalAsync(RejectJobOfferDto dto, Guid rejectedByUserId, CancellationToken cancellationToken = default);
    Task<bool> IssueAsync(IssueJobOfferDto dto, Guid issuedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RecordResponseAsync(RecordOfferResponseDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(RevokeJobOfferDto dto, Guid revokedByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobOfferSummaryDto>> GetByPreparedByAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a sent/negotiating offer to <see cref="JobOfferStatus.ConditionallyAccepted"/>.
    /// Sets <c>IsConditional = true</c> and records the acceptance date.
    /// After this call, HR should create a <see cref="PreEmploymentCheck"/> package against the offer.
    /// </summary>
    Task<JobOfferDto> AcceptConditionallyAsync(Guid offerId, Guid updatedByUserId, string? candidateResponseNotes = null, CancellationToken cancellationToken = default);

    // Benefits
    Task<JobOfferBenefitDto> AddBenefitAsync(CreateJobOfferBenefitDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobOfferBenefitDto>> GetBenefitsAsync(Guid offerId, CancellationToken cancellationToken = default);
    Task<JobOfferBenefitDto> UpdateBenefitAsync(UpdateJobOfferBenefitDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteBenefitAsync(Guid benefitId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Suggests benefits for an offer by reading the EmployeePositionBenefits attached
    /// to the vacancy's target position.  Returns unsaved DTOs — caller may review and
    /// then persist them individually via AddBenefitAsync.
    /// </summary>
    Task<IEnumerable<JobOfferBenefitDto>> SuggestBenefitsFromPositionAsync(Guid offerId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Copies the position's defined benefits into the offer as confirmed benefit lines.
    /// Idempotent: skips lines whose BenefitName already exists on this offer.
    /// </summary>
    Task<IEnumerable<JobOfferBenefitDto>> ImportBenefitsFromPositionAsync(Guid offerId, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);

    // Notes
    Task<JobOfferNoteDto> AddNoteAsync(CreateJobOfferNoteDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobOfferNoteDto>> GetNotesAsync(Guid offerId, CancellationToken cancellationToken = default);

    // Public candidate response (token-based, no auth)
    /// <summary>Validates a candidate token and returns a public-safe offer summary.</summary>
    Task<CandidateOfferSummaryDto> ValidateCandidateTokenAsync(Guid token, CancellationToken cancellationToken = default);
    /// <summary>Records a candidate's accept/negotiate/decline response via their token link.</summary>
    Task<bool> RecordCandidateResponseAsync(CandidateResponseDto dto, CancellationToken cancellationToken = default);
    /// <summary>
    /// Records a candidate's response (accept / negotiate / decline) submitted through
    /// the authenticated candidate portal.  Ownership of the application is validated
    /// by the calling controller before this method is invoked.
    /// </summary>
    Task<bool> RecordPortalCandidateResponseAsync(Guid applicationId, CandidatePortalOfferResponseDto dto, CancellationToken cancellationToken = default);

    // File uploads
    /// <summary>
    /// Points the offer at an already-stored, already-scanned offer letter.
    /// </summary>
    /// <remarks>
    /// This replaces the previous stream-based <c>UploadOfferLetterAsync</c>, which called
    /// <c>IFileUploadService</c> — registered as a stub whose <c>ValidateFile</c> always
    /// returned true and which never actually wrote the file, so the stored path pointed at
    /// nothing. Uploading is now the controller's job via <c>IHrControlledDocumentService</c>.
    /// </remarks>
    Task RecordOfferLetterAsync(Guid offerId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Points the offer at an already-stored, already-scanned countersigned letter.
    /// See <see cref="RecordOfferLetterAsync"/>.
    /// </summary>
    Task RecordSignedLetterAsync(Guid offerId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId, CancellationToken cancellationToken = default);

    // Offer versioning
    /// <summary>
    /// Issues a revised offer in response to a counter-offer.
    /// Marks the previous offer's IsLatestVersion = false and creates a new offer
    /// with Version incremented, PreviousOfferId set, and OfferStatus = Draft.
    /// </summary>
    Task<JobOfferDto> ReviseOfferAsync(ReviseJobOfferDto dto, Guid revisedByUserId, CancellationToken cancellationToken = default);
}

// ============================================================================
// JOB HIRE SERVICE
// ============================================================================

public interface IJobHireService
{
    // Queries
    Task<JobHireRecordDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JobHireRecordDto?> GetByHireNumberAsync(string hireNumber, CancellationToken cancellationToken = default);
    Task<JobHireRecordDto?> GetByApplicationIdAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<JobHireRecordDto?> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobHireRecordSummaryDto>> GetByStatusAsync(JobHireStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobHireRecordSummaryDto>> GetWithStartDateApproachingAsync(int daysAhead = 14, CancellationToken cancellationToken = default);

    // CRUD
    Task<JobHireRecordDto> CreateAsync(CreateJobHireRecordDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<JobHireRecordDto> UpdateStatusAsync(UpdateJobHireRecordStatusDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Workflow
    Task<bool> ConfirmStartAsync(Guid hireRecordId, DateTime actualStartDate, Guid? employeeId, Guid confirmedByUserId, CancellationToken cancellationToken = default);
}
