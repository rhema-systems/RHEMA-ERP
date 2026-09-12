using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The identity behind a candidate call, as the controller resolved it from the main-scheme
/// token and the Identity store. Passed as a value rather than looked up here so this service
/// never depends on ASP.NET Identity — and so <see cref="EmailConfirmed"/> is always the
/// store's answer, not a claim minted before the mailbox was proved.
/// </summary>
public sealed record CandidateAccountContext(Guid UserId, string Email, bool EmailConfirmed);

/// <summary>
/// Candidate self-service — profile data, applications, withdrawals and documents for
/// self-registered careers accounts (main-scheme Identity users in the Candidate role).
/// Until 2026-08-30 this keyed on the retired portal's <c>CandidatePortalAccount</c>; it now
/// resolves the caller's <c>JobCandidate</c> through <c>JobCandidate.UserId</c>.
/// </summary>
public interface ICandidatePortalService
{
    /// <summary>Get the full candidate profile for the authenticated careers account.</summary>
    Task<CandidatePortalProfileDto> GetProfileAsync(CandidateAccountContext account, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Create or update the candidate profile. On the first call this creates a
    /// <c>JobCandidate</c> owned by the account — or, when an unlinked candidate with the
    /// account's email already exists in the tenant, ADOPTS it, which is allowed only once
    /// <see cref="CandidateAccountContext.EmailConfirmed"/> is true: the link hands over the
    /// candidate's application history, and only mailbox proof earns that.
    /// Subsequent calls replace the profile data and child collections.
    /// </summary>
    Task<CandidatePortalProfileDto> SaveProfileAsync(CandidateAccountContext account, UpdateCandidatePortalProfileDto dto, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Save a draft application (status = Draft). Can be called multiple times to update.
    /// No email is sent. Returns the application summary including the ApplicationId for future calls.
    /// </summary>
    Task<CandidatePortalApplicationSummaryDto> SaveDraftAsync(Guid userId, CandidatePortalSaveDraftDto dto, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Submit a previously saved draft (transitions Draft → Submitted).
    /// Sends the application-received confirmation email to the account's address.
    /// </summary>
    Task<CandidatePortalApplicationSummaryDto> SubmitDraftAsync(CandidateAccountContext account, Guid applicationId, CandidatePortalSubmitDraftDto dto, Guid tenantId, CancellationToken ct = default);

    /// <summary>Withdraw an active application.</summary>
    Task WithdrawApplicationAsync(Guid userId, Guid applicationId, CandidatePortalWithdrawDto dto, Guid tenantId, CancellationToken ct = default);

    /// <summary>Get all applications submitted by this account's candidate.</summary>
    Task<List<CandidatePortalApplicationSummaryDto>> GetApplicationsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Get the full dashboard summary (profile + application stats + applications).</summary>
    Task<CandidatePortalDashboardDto> GetDashboardAsync(CandidateAccountContext account, Guid tenantId, CancellationToken ct = default);

    // ── Documents ──────────────────────────────────────────────────────────────

    /// <summary>Get all documents uploaded by this account's candidate.</summary>
    Task<List<JobCandidateDocumentDto>> GetDocumentsAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the candidate profile behind the account, throwing when the account has not
    /// completed one yet. Used to name the source record for central-DMS registration.
    /// </summary>
    Task<Guid> RequireCandidateIdAsync(Guid userId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Registers a newly uploaded document for this candidate. The upload/DMS identifiers come
    /// from <c>IHrControlledDocumentService</c>; <paramref name="filePath"/> stays empty for new
    /// rows and is only populated on pre-migration data.
    /// </summary>
    Task<JobCandidateDocumentDto> AddDocumentAsync(Guid userId, JobCandidateDocumentType documentType,
        string fileName, string filePath, Guid tenantId, CancellationToken ct = default,
        Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null);

    /// <summary>Delete a document that belongs to this candidate.</summary>
    Task DeleteDocumentAsync(Guid userId, Guid documentId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Points the candidate profile at a stored, scanned photo.</summary>
    Task UpdateProfilePhotoAsync(Guid userId, Guid fileUploadRecordId, Guid tenantId, CancellationToken ct = default);
}
