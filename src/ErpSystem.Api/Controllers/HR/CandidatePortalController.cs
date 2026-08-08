using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Api.Security;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Authenticated candidate portal endpoints — profile, applications, dashboard.
/// All routes require a valid candidate portal JWT (user_type = portal_candidate).
/// </summary>
[ApiController]
[Route("api/portal")]
[Authorize(Policy = "CandidatePortal", AuthenticationSchemes = PortalAuth.Scheme)]
public class CandidatePortalController : ControllerBase
{
    private readonly ICandidatePortalService _portalService;
    private readonly ICandidatePortalAuthService _authService;
    private readonly IFileStorageService _fileStorage;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ApplicationDbContext _db;
    private readonly IJobOfferService _offerService;
    private readonly IOfferLetterService _offerLetter;

    public CandidatePortalController(
        ICandidatePortalService portalService,
        ICandidatePortalAuthService authService,
        IFileStorageService fileStorage,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        ApplicationDbContext db,
        IJobOfferService offerService,
        IOfferLetterService offerLetter)
    {
        _portalService = portalService;
        _authService   = authService;
        _fileStorage   = fileStorage;
        _hrDocuments   = hrDocuments;
        _centralDocuments = centralDocuments;
        _db            = db;
        _offerService  = offerService;
        _offerLetter   = offerLetter;
    }

    // ── Dashboard ──────────────────────────────────────────────────────────────
    /// <summary>Returns the candidate dashboard — profile summary + all applications.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(CandidatePortalDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CandidatePortalDashboardDto>> GetDashboard(CancellationToken ct)
    {
        var result = await _portalService.GetDashboardAsync(GetAccountId(), GetTenantId(), ct);
        return Ok(result);
    }

    // ── Profile ────────────────────────────────────────────────────────────────
    /// <summary>Returns the full candidate profile.</summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(CandidatePortalProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CandidatePortalProfileDto>> GetProfile(CancellationToken ct)
    {
        var result = await _portalService.GetProfileAsync(GetAccountId(), GetTenantId(), ct);
        return Ok(result);
    }

    /// <summary>Creates or updates the candidate profile, replacing child collections.</summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(CandidatePortalProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CandidatePortalProfileDto>> SaveProfile(
        [FromBody] UpdateCandidatePortalProfileDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _portalService.SaveProfileAsync(GetAccountId(), dto, GetTenantId(), ct);
        return Ok(result);
    }

    /// <summary>Changes the password for the authenticated candidate account.</summary>
    [HttpPost("profile/change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] CandidatePortalChangePasswordDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _authService.ChangePasswordAsync(GetAccountId(), dto, GetTenantId(), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Applications ───────────────────────────────────────────────────────────
    /// <summary>Returns all applications submitted by this candidate.</summary>
    [HttpGet("applications")]
    [ProducesResponseType(typeof(List<CandidatePortalApplicationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CandidatePortalApplicationSummaryDto>>> GetApplications(
        CancellationToken ct)
    {
        var result = await _portalService.GetApplicationsAsync(GetAccountId(), GetTenantId(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Submits a new application for a published vacancy.
    /// Creates a draft and immediately transitions it to Submitted in a single call.
    /// </summary>
    [HttpPost("applications")]
    [ProducesResponseType(typeof(CandidatePortalApplicationSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CandidatePortalApplicationSummaryDto>> Apply(
        [FromBody] CandidatePortalSaveDraftDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            var draft = await _portalService.SaveDraftAsync(GetAccountId(), dto, GetTenantId(), ct);
            var submitDto = new CandidatePortalSubmitDraftDto
            {
                CoverLetter       = dto.CoverLetter,
                YearsOfExperience = dto.YearsOfExperience,
                AvailableFrom     = dto.AvailableFrom,
            };
            var result = await _portalService.SubmitDraftAsync(
                GetAccountId(), draft.ApplicationId, submitDto, GetTenantId(), ct);
            return CreatedAtAction(nameof(GetApplications), result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Withdraws an application by the authenticated candidate.</summary>
    [HttpDelete("applications/{applicationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> WithdrawApplication(
        Guid applicationId,
        [FromBody] CandidatePortalWithdrawDto dto,
        CancellationToken ct)
    {
        try
        {
            await _portalService.WithdrawApplicationAsync(
                GetAccountId(), applicationId, dto, GetTenantId(), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    // ── Profile Photo ──────────────────────────────────────────────────────────
    /// <summary>
    /// Upload and set the candidate profile photo.
    /// </summary>
    /// <remarks>
    /// Returns the route to fetch the photo, not a public URL. A photograph of a named job
    /// applicant is personal data, so it is stored privately and served only through the
    /// authorizing endpoint below — callers must fetch it with their bearer token rather than
    /// putting the value straight into an <c>img src</c>.
    /// </remarks>
    [HttpPost("profile/photo")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadProfilePhoto(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        var tenantId = GetTenantId();
        var accountId = GetAccountId();

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                // The portal session is the candidate's own; there is no internal user behind it.
                ActorUserId = ControlledFileUploadActors.PublicPortalAnonymous,
                ActorName = "candidate-portal",
                Category = ControlledFileUploadCategories.HrCandidatePhotos,
                File = file,
                // Avatars carry no retention value; a DMS record per photo is repository noise.
                Registration = null
            }, ct);
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        try
        {
            await _portalService.UpdateProfilePhotoAsync(
                accountId, document.FileUploadRecordId, tenantId, ct);
        }
        catch (Exception ex)
        {
            await _hrDocuments.RollbackAsync(
                document, tenantId, ControlledFileUploadActors.PublicPortalAnonymous, ct);
            if (ex is InvalidOperationException)
                return BadRequest(new { message = ex.Message });
            throw;
        }

        return Ok(new { url = Url.Action(nameof(GetProfilePhoto)) });
    }

    /// <summary>Streams the authenticated candidate's own profile photo.</summary>
    [HttpGet("profile/photo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfilePhoto(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var candidate = await LoadOwnCandidateAsync(tenantId, ct);
        if (candidate is null)
            return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            documentRecordId: null, documentVersionId: null,
            candidate.ProfilePhotoFileUploadRecordId,
            legacyPath: null,
            fallbackFileName: "profile-photo",
            fallbackContentType: null,
            inline: true, ct);
    }

    /// <summary>
    /// Loads the candidate profile owned by the authenticated portal account, or null when the
    /// account has not completed one. Scoping every lookup through the account's own
    /// <c>JobCandidateId</c> is what stops one candidate reading another's files.
    /// </summary>
    private async Task<JobCandidate?> LoadOwnCandidateAsync(Guid tenantId, CancellationToken ct)
    {
        var accountId = GetAccountId();
        var candidateId = await _db.Set<CandidatePortalAccount>()
            .AsNoTracking()
            .Where(item => item.Id == accountId && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => item.JobCandidateId)
            .SingleOrDefaultAsync(ct);
        if (candidateId is not Guid id)
            return null;

        return await _db.Set<JobCandidate>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, ct);
    }

    // ── Documents ──────────────────────────────────────────────────────────────
    /// <summary>Returns all documents uploaded by this candidate.</summary>
    [HttpGet("documents")]
    [ProducesResponseType(typeof(List<JobCandidateDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JobCandidateDocumentDto>>> GetDocuments(CancellationToken ct)
    {
        var result = await _portalService.GetDocumentsAsync(GetAccountId(), GetTenantId(), ct);
        return Ok(result);
    }

    /// <summary>Upload a document and register it against the candidate profile.</summary>
    [HttpPost("documents")]
    [ProducesResponseType(typeof(JobCandidateDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadDocument(
        IFormFile file,
        [FromForm] JobCandidateDocumentType documentType,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (!Enum.IsDefined(typeof(JobCandidateDocumentType), documentType))
            return BadRequest(new { message = "Invalid document type." });

        var tenantId = GetTenantId();
        var accountId = GetAccountId();

        // The DMS needs a source record, and it also means an account without a saved profile
        // is rejected before any bytes are stored.
        Guid candidateId;
        try
        {
            candidateId = await _portalService.RequireCandidateIdAsync(accountId, tenantId, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                ActorUserId = ControlledFileUploadActors.PublicPortalAnonymous,
                ActorName = "candidate-portal",
                Category = ControlledFileUploadCategories.HrCandidateDocuments,
                File = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = "Candidate document",
                    SourceEntityType = nameof(JobCandidate),
                    SourceRecordId = candidateId,
                    Title = Path.GetFileName(file.FileName),
                    DocumentType = documentType.ToString(),
                    ChangeSummary = "Uploaded by the candidate through the careers portal."
                }
            }, ct);
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        try
        {
            var result = await _portalService.AddDocumentAsync(
                accountId, documentType, document.OriginalFileName, string.Empty, tenantId, ct,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            await _hrDocuments.RollbackAsync(
                document, tenantId, ControlledFileUploadActors.PublicPortalAnonymous, ct);
            if (ex is InvalidOperationException)
                return BadRequest(new { message = ex.Message });
            throw;
        }
    }

    /// <summary>Streams a document belonging to the authenticated candidate.</summary>
    [HttpGet("documents/{documentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(Guid documentId, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var candidate = await LoadOwnCandidateAsync(tenantId, ct);
        if (candidate is null)
            return NotFound();

        // Scoped to the caller's own candidate id, so a guessed document id from another
        // candidate is a lookup miss rather than a disclosure.
        var document = await _db.Set<JobCandidateDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == documentId &&
                        item.TenantId == tenantId &&
                        item.JobCandidateId == candidate.Id &&
                        !item.IsDeleted,
                ct);
        if (document is null)
            return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId, document.FilePath,
            document.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    /// <summary>Streams the authenticated candidate's own CV.</summary>
    [HttpGet("cv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadCv(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var candidate = await LoadOwnCandidateAsync(tenantId, ct);
        if (candidate is null)
            return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            candidate.CvDocumentRecordId, candidate.CvDocumentVersionId,
            candidate.CvFileUploadRecordId, candidate.CvFilePath,
            fallbackFileName: $"cv-{candidate.CandidateNumber}",
            fallbackContentType: null,
            inline: false, ct);
    }

    /// <summary>Delete a document belonging to this candidate.</summary>
    [HttpDelete("documents/{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteDocument(Guid documentId, CancellationToken ct)
    {
        try
        {
            await _portalService.DeleteDocumentAsync(GetAccountId(), documentId, GetTenantId(), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    // ── Offer (portal) ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the latest offer for a specific application, but only if it belongs
    /// to the authenticated candidate.
    /// </summary>
    [HttpGet("applications/{applicationId:guid}/offer")]
    [ProducesResponseType(typeof(CandidatePortalOfferDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CandidatePortalOfferDto>> GetApplicationOffer(
        Guid applicationId, CancellationToken ct)
    {
        var apps = await _portalService.GetApplicationsAsync(GetAccountId(), GetTenantId(), ct);
        if (!apps.Any(a => a.ApplicationId == applicationId))
            return NotFound();

        var offer = await _offerService.GetByApplicationIdAsync(applicationId, ct);
        if (offer == null)
            return NotFound(new { message = "No offer found for this application." });

        return Ok(new CandidatePortalOfferDto
        {
            Id                    = offer.Id,
            OfferNumber           = offer.OfferNumber,
            PositionTitle         = offer.PositionTitle,
            DepartmentName        = offer.DepartmentName,
            ReportsToTitle        = offer.ReportsToTitle,
            GradeTitle            = offer.GradeTitle,
            LocationName          = offer.LocationName,
            EmploymentType        = offer.EmploymentType,
            ContractDurationMonths= offer.ContractDurationMonths,
            WorkMode              = offer.WorkMode,
            BaseSalary            = offer.BaseSalary,
            CurrencyCode          = offer.CurrencyCode,
            Bonus                 = offer.Bonus,
            BonusTerms            = offer.BonusTerms,
            Commission            = offer.Commission,
            CommissionStructure   = offer.CommissionStructure,
            Benefits              = offer.Benefits,
            ProbationPeriodMonths = offer.ProbationPeriodMonths,
            NoticePeriodMonths    = offer.NoticePeriodMonths,
            AnnualLeaveDays       = offer.AnnualLeaveDays,
            WeeklyHours           = offer.WeeklyHours,
            NdaRequired           = offer.NdaRequired,
            ProposedStartDate     = offer.ProposedStartDate,
            ExpiryDate            = offer.ExpiryDate,
            AdditionalTerms       = offer.AdditionalTerms,
            OfferLetterPath       = offer.OfferLetterPath,
            IsConditional         = offer.IsConditional,
            OfferStatus           = offer.OfferStatus,
            CandidateResponseNotes= offer.CandidateResponseNotes,
            AcceptedDate          = offer.AcceptedDate,
            DeclinedDate          = offer.DeclinedDate,
        });
    }

    /// <summary>
    /// Returns the rendered formal offer letter (HTML) for an application's latest offer, for the
    /// candidate to view and print (print-to-PDF). Ownership-checked against the authenticated candidate.
    /// </summary>
    [HttpGet("applications/{applicationId:guid}/offer/letter")]
    [ProducesResponseType(typeof(OfferLetterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OfferLetterDto>> GetApplicationOfferLetter(
        Guid applicationId, CancellationToken ct)
    {
        var apps = await _portalService.GetApplicationsAsync(GetAccountId(), GetTenantId(), ct);
        if (!apps.Any(a => a.ApplicationId == applicationId))
            return NotFound();

        var offer = await _offerService.GetByApplicationIdAsync(applicationId, ct);
        if (offer == null)
            return NotFound(new { message = "No offer found for this application." });

        var letter = await _offerLetter.GenerateAsync(offer.Id, ct);
        return Ok(letter);
    }

    /// <summary>
    /// Records the candidate's accept / negotiate / decline response for an offer.
    /// </summary>
    [HttpPost("applications/{applicationId:guid}/offer/respond")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RespondToOffer(
        Guid applicationId, [FromBody] CandidatePortalOfferResponseDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var apps = await _portalService.GetApplicationsAsync(GetAccountId(), GetTenantId(), ct);
        if (!apps.Any(a => a.ApplicationId == applicationId))
            return NotFound();

        try
        {
            await _offerService.RecordPortalCandidateResponseAsync(applicationId, dto, ct);
            return Ok(new { message = "Your response has been recorded." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private Guid GetAccountId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private Guid GetTenantId() =>
        Guid.Parse(User.FindFirstValue("tenant_id")!);
}
