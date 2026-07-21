using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Candidate portal service — manages profile data, vacancy browsing, applications
/// and withdrawals for authenticated portal accounts.
/// </summary>
public interface ICandidatePortalService
{
    /// <summary>Get the full candidate profile for an authenticated portal account.</summary>
    Task<CandidatePortalProfileDto> GetProfileAsync(Guid accountId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Create or update the candidate profile.
    /// On the first call this creates a <c>JobCandidate</c> record and links it to the account.
    /// Subsequent calls replace the profile data and child collections.
    /// </summary>
    Task<CandidatePortalProfileDto> SaveProfileAsync(Guid accountId, UpdateCandidatePortalProfileDto dto, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Save a draft application (status = Draft). Can be called multiple times to update.
    /// No email is sent. Returns the application summary including the ApplicationId for future calls.
    /// </summary>
    Task<CandidatePortalApplicationSummaryDto> SaveDraftAsync(Guid accountId, CandidatePortalSaveDraftDto dto, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Submit a previously saved draft (transitions Draft → Submitted).
    /// Sends the application-received confirmation email.
    /// </summary>
    Task<CandidatePortalApplicationSummaryDto> SubmitDraftAsync(Guid accountId, Guid applicationId, CandidatePortalSubmitDraftDto dto, Guid tenantId, CancellationToken ct = default);

    /// <summary>Withdraw an active application.</summary>
    Task WithdrawApplicationAsync(Guid accountId, Guid applicationId, CandidatePortalWithdrawDto dto, Guid tenantId, CancellationToken ct = default);

    /// <summary>Get all applications submitted by this portal account.</summary>
    Task<List<CandidatePortalApplicationSummaryDto>> GetApplicationsAsync(Guid accountId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Get the full dashboard summary (profile + application stats + applications).</summary>
    Task<CandidatePortalDashboardDto> GetDashboardAsync(Guid accountId, Guid tenantId, CancellationToken ct = default);

    // ── Documents ──────────────────────────────────────────────────────────────

    /// <summary>Get all documents uploaded by this portal account.</summary>
    Task<List<JobCandidateDocumentDto>> GetDocumentsAsync(Guid accountId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Register a newly uploaded document file for this candidate.</summary>
    Task<JobCandidateDocumentDto> AddDocumentAsync(Guid accountId, JobCandidateDocumentType documentType,
        string fileName, string filePath, Guid tenantId, CancellationToken ct = default);

    /// <summary>Delete a document that belongs to this candidate.</summary>
    Task DeleteDocumentAsync(Guid accountId, Guid documentId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Update the profile photo URL for this candidate.</summary>
    Task<string> UpdateProfilePhotoAsync(Guid accountId, string photoUrl, Guid tenantId, CancellationToken ct = default);
}
