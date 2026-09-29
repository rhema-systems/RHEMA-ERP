using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Models;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The candidate's own recruitment surface — dashboard, profile, applications, documents and
/// offers — for self-registered careers accounts on the main JWT scheme (Candidate role).
/// Replaces the retired <c>api/portal</c> controller (PortalBearer scheme), 2026-08-30.
/// </summary>
/// <remarks>
/// Two fences sit in front of every action here: the <c>CandidateOnly</c> policy (only the
/// Candidate role gets in) and <c>CandidateAccessMiddleware</c> (the same tokens get NOTHING
/// outside the candidate allowlist). Ownership inside is by <c>JobCandidate.UserId</c> — every
/// lookup scopes through the caller's own candidate row, so a guessed id is a miss, not a
/// disclosure. The offer-respond action carries the ownership pre-check the service contract
/// demands (<c>RecordPortalCandidateResponseAsync</c> validates nothing itself — deleting the
/// old controller without re-implementing that check would have left an unguarded mutation).
/// </remarks>
[ApiController]
[Route("api/candidate")]
[Authorize(Policy = "CandidateOnly")]
public class CandidateController : ControllerBase
{
    private readonly ICandidatePortalService _portalService;
    private readonly IFileStorageService _fileStorage;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ApplicationDbContext _db;
    private readonly IJobOfferService _offerService;
    private readonly IRecruitmentTestService _testService;
    private readonly IOfferLetterService _offerLetter;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _email;
    private readonly CandidatePortalOptions _portalOptions;
    private readonly ILogger<CandidateController> _logger;

    public CandidateController(
        ICandidatePortalService portalService,
        IFileStorageService fileStorage,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        ApplicationDbContext db,
        IJobOfferService offerService,
        IRecruitmentTestService testService,
        IOfferLetterService offerLetter,
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        IEmailService email,
        IOptions<CandidatePortalOptions> portalOptions,
        ILogger<CandidateController> logger)
    {
        _portalService = portalService;
        _fileStorage   = fileStorage;
        _hrDocuments   = hrDocuments;
        _centralDocuments = centralDocuments;
        _db            = db;
        _offerService  = offerService;
        _offerLetter   = offerLetter;
        _userManager   = userManager;
        _currentUser   = currentUser;
        _email         = email;
        _testService   = testService;
        _portalOptions = portalOptions.Value;
        _logger        = logger;
    }

    // ── Identity helpers ───────────────────────────────────────────────────────

    private Guid GetTenantId() =>
        _currentUser.TenantId ?? throw new InvalidOperationException("Tenant context could not be resolved.");

    private async Task<ApplicationUser> GetAccountUserAsync()
    {
        var userId = _currentUser.UserId;
        var user = userId == null ? null : await _userManager.FindByIdAsync(userId);
        return user ?? throw new InvalidOperationException("Account not found.");
    }

    /// <summary>
    /// The identity the service works from. EmailConfirmed comes from the Identity STORE, not a
    /// token claim — profile adoption keys off it, and a stale claim minted before verification
    /// must not open that door.
    /// </summary>
    private async Task<CandidateAccountContext> GetAccountContextAsync()
    {
        var user = await GetAccountUserAsync();
        return new CandidateAccountContext(user.Id, user.Email ?? string.Empty, user.EmailConfirmed);
    }

    /// <summary>
    /// The caller's own candidate profile, or null when they have not completed one. Scoping
    /// every file lookup through this is what stops one candidate reading another's documents.
    /// </summary>
    private async Task<JobCandidate?> LoadOwnCandidateAsync(Guid tenantId, CancellationToken ct)
    {
        var user = await GetAccountUserAsync();
        return await _db.Set<JobCandidate>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.UserId == user.Id && item.TenantId == tenantId && !item.IsDeleted, ct);
    }

    // ── Dashboard ──────────────────────────────────────────────────────────────
    /// <summary>Returns the candidate dashboard — profile summary + all applications.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(CandidatePortalDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CandidatePortalDashboardDto>> GetDashboard(CancellationToken ct)
    {
        var result = await _portalService.GetDashboardAsync(await GetAccountContextAsync(), GetTenantId(), ct);
        return Ok(result);
    }

    // ── Profile ────────────────────────────────────────────────────────────────
    /// <summary>Returns the full candidate profile.</summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(CandidatePortalProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CandidatePortalProfileDto>> GetProfile(CancellationToken ct)
    {
        var result = await _portalService.GetProfileAsync(await GetAccountContextAsync(), GetTenantId(), ct);
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

        try
        {
            var result = await _portalService.SaveProfileAsync(await GetAccountContextAsync(), dto, GetTenantId(), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Email confirmation ─────────────────────────────────────────────────────
    // Registration activates the account by SMS OTP; the mailbox is proved separately here.
    // Confirming the email is what unlocks ADOPTING an existing candidate profile that carries
    // this address — the link hands over that profile's application history.

    /// <summary>Sends the email-confirmation link to the account's own address.</summary>
    [HttpPost("confirm-email/send")]
    [EnableRateLimiting("SensitivePolicy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendEmailConfirmation(CancellationToken ct)
    {
        var user = await GetAccountUserAsync();
        if (user.EmailConfirmed)
            return Ok(new { message = "Your email address is already confirmed." });
        if (string.IsNullOrWhiteSpace(user.Email))
            return BadRequest(new { message = "The account has no email address." });

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = $"{_portalOptions.PortalUrl.TrimEnd('/')}/careers/verify-email" +
                   $"?uid={user.Id}&token={Uri.EscapeDataString(token)}";

        var sent = await _email.SendEmailAsync(new EmailDto
        {
            To      = user.Email,
            Subject = "Confirm your email address",
            IsHtml  = true,
            Body    = $"""
                       <p>Hello {user.FirstName},</p>
                       <p>Confirm the email address on your careers account by clicking the link below:</p>
                       <p><a href="{link}">Confirm my email address</a></p>
                       <p>If you did not create this account, you can ignore this message.</p>
                       """,
        });

        if (!sent)
        {
            _logger.LogWarning("Email-confirmation send failed for candidate user {UserId}", user.Id);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "The confirmation email could not be sent. Try again later." });
        }

        return Ok(new { message = "A confirmation link has been sent to your email address." });
    }

    public sealed class ConfirmEmailRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>Confirms the account's email address with the token from the emailed link.</summary>
    [HttpPost("confirm-email")]
    [EnableRateLimiting("SensitivePolicy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { message = "A confirmation token is required." });

        var user = await GetAccountUserAsync();
        if (user.EmailConfirmed)
            return Ok(new { message = "Your email address is already confirmed." });

        var result = await _userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
            return BadRequest(new { message = "The confirmation link is invalid or has expired. Request a new one." });

        return Ok(new { message = "Your email address is confirmed." });
    }

    // ── Applications ───────────────────────────────────────────────────────────
    /// <summary>Returns all applications submitted by this candidate.</summary>
    [HttpGet("applications")]
    [ProducesResponseType(typeof(List<CandidatePortalApplicationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CandidatePortalApplicationSummaryDto>>> GetApplications(
        CancellationToken ct)
    {
        var user = await GetAccountUserAsync();
        var result = await _portalService.GetApplicationsAsync(user.Id, GetTenantId(), ct);
        return Ok(result);
    }

    /// <summary>Saves (or updates) a draft application without submitting it.</summary>
    [HttpPost("applications/draft")]
    [ProducesResponseType(typeof(CandidatePortalApplicationSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CandidatePortalApplicationSummaryDto>> SaveDraft(
        [FromBody] CandidatePortalSaveDraftDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            var user = await GetAccountUserAsync();
            return Ok(await _portalService.SaveDraftAsync(user.Id, dto, GetTenantId(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Submits a previously saved draft.</summary>
    [HttpPost("applications/{applicationId:guid}/submit")]
    [ProducesResponseType(typeof(CandidatePortalApplicationSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CandidatePortalApplicationSummaryDto>> SubmitDraft(
        Guid applicationId, [FromBody] CandidatePortalSubmitDraftDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        try
        {
            return Ok(await _portalService.SubmitDraftAsync(
                await GetAccountContextAsync(), applicationId, dto, GetTenantId(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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
            var ctx = await GetAccountContextAsync();
            var draft = await _portalService.SaveDraftAsync(ctx.UserId, dto, GetTenantId(), ct);
            var submitDto = new CandidatePortalSubmitDraftDto
            {
                CoverLetter       = dto.CoverLetter,
                YearsOfExperience = dto.YearsOfExperience,
                AvailableFrom     = dto.AvailableFrom,
            };
            var result = await _portalService.SubmitDraftAsync(
                ctx, draft.ApplicationId, submitDto, GetTenantId(), ct);
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
            var user = await GetAccountUserAsync();
            await _portalService.WithdrawApplicationAsync(user.Id, applicationId, dto, GetTenantId(), ct);
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
        var user = await GetAccountUserAsync();

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                // The caller is a real Identity user now — the retired portal's anonymous actor
                // constant no longer applies.
                ActorUserId = user.Id,
                ActorName = user.UserName ?? "candidate",
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
                user.Id, document.FileUploadRecordId, tenantId, ct);
        }
        catch (Exception ex)
        {
            await _hrDocuments.RollbackAsync(document, tenantId, user.Id, ct);
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

    // ── Documents ──────────────────────────────────────────────────────────────
    /// <summary>Returns all documents uploaded by this candidate.</summary>
    [HttpGet("documents")]
    [ProducesResponseType(typeof(List<JobCandidateDocumentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JobCandidateDocumentDto>>> GetDocuments(CancellationToken ct)
    {
        var user = await GetAccountUserAsync();
        var result = await _portalService.GetDocumentsAsync(user.Id, GetTenantId(), ct);
        return Ok(result);
    }

    /// <summary>Upload a document and register it against the candidate profile.</summary>
    [HttpPost("documents")]
    [ProducesResponseType(typeof(JobCandidateDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadDocument(
        IFormFile file,
        [FromForm] JobCandidateDocumentType documentType,
        [FromForm] string? description,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (!Enum.IsDefined(typeof(JobCandidateDocumentType), documentType))
            return BadRequest(new { message = "Invalid document type." });

        var tenantId = GetTenantId();
        var user = await GetAccountUserAsync();

        // The DMS needs a source record, and it also means an account without a saved profile
        // is rejected before any bytes are stored.
        Guid candidateId;
        try
        {
            candidateId = await _portalService.RequireCandidateIdAsync(user.Id, tenantId, ct);
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
                ActorUserId = user.Id,
                ActorName = user.UserName ?? "candidate",
                Category = ControlledFileUploadCategories.HrCandidateDocuments,
                File = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = "Candidate document",
                    SourceEntityType = nameof(JobCandidate),
                    SourceRecordId = candidateId,
                    Title = Path.GetFileName(file.FileName),
                    DocumentType = documentType.ToString(),
                    ChangeSummary = "Uploaded by the candidate through the careers surface."
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
                user.Id, documentType, document.OriginalFileName, string.Empty, tenantId, ct,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId,
                description);
            return Ok(result);
        }
        catch (Exception ex)
        {
            await _hrDocuments.RollbackAsync(document, tenantId, user.Id, ct);
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
            var user = await GetAccountUserAsync();
            await _portalService.DeleteDocumentAsync(user.Id, documentId, GetTenantId(), ct);
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

    // ── Offer ──────────────────────────────────────────────────────────────────

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
        if (!await OwnsApplicationAsync(applicationId, ct))
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
        if (!await OwnsApplicationAsync(applicationId, ct))
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

        // ⚠ RecordPortalCandidateResponseAsync validates NOTHING itself — its contract says
        // ownership is the calling controller's job. This check is load-bearing.
        if (!await OwnsApplicationAsync(applicationId, ct))
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

    // ── Assessments (round 4, lane E) ──────────────────────────────────────────
    //
    // ⚠ Everything here is served by the CANDIDATE projections: the question and option types the
    // service returns have no IsCorrect, no ExpectedAnswer and no Explanation on them at all. The
    // guard is the type, not a remembered omission.
    //
    // ⚠ These actions map the service's own exceptions themselves rather than borrowing
    // [RecruitmentBusinessRules]: putting that filter on this controller would change the status
    // code every OTHER candidate endpoint answers with, and the careers pages read those.

    /// <summary>Every assessment the caller has been set, and whether they can start it.</summary>
    [HttpGet("assessments")]
    [ProducesResponseType(typeof(IEnumerable<CandidateAssessmentSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CandidateAssessmentSummaryDto>>> GetAssessments(
        CancellationToken ct)
        => await RunAssessmentAsync(async user =>
            await _testService.GetMyAssessmentsAsync(user.Id, GetTenantId(), ct));

    /// <summary>
    /// Opens an attempt — or hands back the one already running, which does not consume another.
    /// </summary>
    /// <remarks>
    /// ⚠ The clock starts HERE and is stored; it is not restarted by reopening the page. The
    /// response carries the session token that the save and submit calls must present.
    /// </remarks>
    [HttpPost("assessments/{assignmentId:guid}/start")]
    [EnableRateLimiting("SensitivePolicy")]
    [ProducesResponseType(typeof(CandidateSittingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CandidateSittingDto>> StartAssessment(
        Guid assignmentId, CancellationToken ct)
        => await RunAssessmentAsync(async user =>
            await _testService.StartSittingAsync(user.Id, GetTenantId(), assignmentId, ct));

    /// <summary>Resumes an attempt: the paper, the remaining time, and everything already answered.</summary>
    [HttpGet("assessments/sittings/{sittingId:guid}")]
    [ProducesResponseType(typeof(CandidateSittingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CandidateSittingDto>> GetSitting(Guid sittingId, CancellationToken ct)
        => await RunAssessmentAsync(async user =>
            await _testService.GetMySittingAsync(user.Id, GetTenantId(), sittingId, ct));

    /// <summary>Stores what has been answered so far, unmarked.</summary>
    [HttpPut("assessments/sittings/{sittingId:guid}/progress")]
    [ProducesResponseType(typeof(CandidateSittingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CandidateSittingDto>> SaveProgress(
        Guid sittingId, [FromBody] SubmitSittingDto dto, CancellationToken ct)
    {
        if (sittingId != dto.SittingId)
            return BadRequest(new { message = "The route id and the payload id do not match." });

        return await RunAssessmentAsync(async user =>
            await _testService.SaveProgressAsync(user.Id, GetTenantId(), dto, ct));
    }

    /// <summary>Submits the attempt. One submit — it cannot be reopened afterwards.</summary>
    [HttpPost("assessments/sittings/{sittingId:guid}/submit")]
    [EnableRateLimiting("SensitivePolicy")]
    [ProducesResponseType(typeof(CandidateSittingResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CandidateSittingResultDto>> SubmitSitting(
        Guid sittingId, [FromBody] SubmitSittingDto dto, CancellationToken ct)
    {
        if (sittingId != dto.SittingId)
            return BadRequest(new { message = "The route id and the payload id do not match." });

        return await RunAssessmentAsync(async user =>
            await _testService.SubmitSittingAsync(user.Id, GetTenantId(), dto, ct));
    }

    /// <summary>
    /// Runs an assessment call and answers the service's own rules the way the rest of recruitment
    /// does: "not found" is 404, a refused rule is 422 carrying its own sentence.
    /// </summary>
    /// <remarks>
    /// ⚠ A sitting that belongs to somebody else is reported as MISSING, not forbidden — the service
    /// raises the same "could not be found" for a wrong id and for another candidate's, so a guessed
    /// id cannot be used to discover that it exists.
    /// </remarks>
    private async Task<ActionResult<T>> RunAssessmentAsync<T>(Func<ApplicationUser, Task<T>> work)
    {
        try
        {
            return Ok(await work(await GetAccountUserAsync()));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status422UnprocessableEntity, new { message = ex.Message });
        }
    }

    private async Task<bool> OwnsApplicationAsync(Guid applicationId, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        var candidate = await LoadOwnCandidateAsync(tenantId, ct);
        if (candidate is null)
            return false;

        return await _db.Set<JobApplication>()
            .AsNoTracking()
            .AnyAsync(a => a.Id == applicationId &&
                           a.TenantId == tenantId &&
                           a.JobCandidateId == candidate.Id &&
                           !a.IsDeleted, ct);
    }
}
