using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IJobOfferService _offerService;
    private readonly IOfferLetterService _offerLetter;

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] AllowedDocExtensions   = { ".pdf", ".doc", ".docx" };
    private const long MaxPhotoSizeBytes = 5  * 1024 * 1024; // 5 MB
    private const long MaxDocSizeBytes   = 10 * 1024 * 1024; // 10 MB

    public CandidatePortalController(
        ICandidatePortalService portalService,
        ICandidatePortalAuthService authService,
        IFileStorageService fileStorage,
        IJobOfferService offerService,
        IOfferLetterService offerLetter)
    {
        _portalService = portalService;
        _authService   = authService;
        _fileStorage   = fileStorage;
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
    /// <summary>Upload and set the candidate profile photo. Returns the public URL.</summary>
    [HttpPost("profile/photo")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadProfilePhoto(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (file.Length > MaxPhotoSizeBytes)
            return BadRequest(new { message = "Photo must not exceed 5 MB." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            return BadRequest(new { message = "Only JPG, PNG and WebP photos are accepted." });

        await using var stream = file.OpenReadStream();
        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = await _fileStorage.UploadFileAsync(stream, safeFileName, "candidate-photos");
        var publicUrl = await _fileStorage.GetPublicUrlAsync(filePath);

        var url = await _portalService.UpdateProfilePhotoAsync(GetAccountId(), publicUrl, GetTenantId(), ct);
        return Ok(new { url });
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

        if (file.Length > MaxDocSizeBytes)
            return BadRequest(new { message = "Document must not exceed 10 MB." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedDocExtensions.Contains(ext))
            return BadRequest(new { message = "Only PDF, DOC and DOCX files are accepted." });

        if (!Enum.IsDefined(typeof(JobCandidateDocumentType), documentType))
            return BadRequest(new { message = "Invalid document type." });

        await using var stream = file.OpenReadStream();
        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = await _fileStorage.UploadFileAsync(stream, safeFileName, "candidate-documents");

        var result = await _portalService.AddDocumentAsync(
            GetAccountId(), documentType, file.FileName, filePath, GetTenantId(), ct);
        return Ok(result);
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
