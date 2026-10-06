using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImageRules = ErpSystem.Core.Services.HR.CompanySealAssetRules;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The tenant's company (legal-employer) profile — identity, statutory numbers, registered address,
/// contacts and document-presentation details. One record per tenant, GET + PUT.
/// </summary>
/// <remarks>
/// Gated on <c>HR.Company.*</c> (W3 slice 14), read as well as write — previously an
/// SA/TenantAdmin/HR role gate, and before that a bare <c>[Authorize]</c> that slice 0 measured a
/// plain `Employee` rewriting the whole record through. The letterhead itself is not secret —
/// every employee sees it on their own offer or confirmation letter — but the same record carries
/// the tenant's TIN, VAT number and SSNIT employer number.
///
/// Gating the controller does not affect any document: the letter and email services read the
/// profile through <c>ICompanyProfileProvider</c> directly, never through this route.
/// </remarks>
[ApiController]
[Route("api/hr/company-profile")]
[Authorize(Policy = "InternalOnly")]
public class CompanyProfileController : ControllerBase
{
    private readonly ICompanyProfileService _service;
    private readonly ILogger<CompanyProfileController> _logger;
    private readonly ErpSystem.Core.Services.HR.ICompanySealAssetService _seals;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICurrentUserService _currentUser;

    public CompanyProfileController(
        ICompanyProfileService service,
        ILogger<CompanyProfileController> logger,
        ErpSystem.Core.Services.HR.ICompanySealAssetService seals,
        IHrControlledDocumentService hrDocuments,
        ICurrentUserService currentUser)
    {
        _service = service;
        _logger = logger;
        _seals = seals;
        _hrDocuments = hrDocuments;
        _currentUser = currentUser;
    }

    /// <summary>Get the current tenant's company profile (Tenant/config-resolved defaults if none saved yet).</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    [ProducesResponseType(typeof(CompanyProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company profile");
            return StatusCode(500, "An error occurred while retrieving the company profile");
        }
    }

    /// <summary>Update (upsert) the current tenant's company profile.</summary>
    [HttpPut]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    [ProducesResponseType(typeof(CompanyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateCompanyProfileDto dto, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            return Ok(await _service.UpdateAsync(dto, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company profile");
            return StatusCode(500, "An error occurred while updating the company profile");
        }
    }

    // ── The seal, the signature and the logo ─────────────────────────────────
    //
    // Company-schedule final closure lane 4c (D-9, C-50, F-55): the LOGO is the third kind, through the same doors — it is
    // embedded in every letter like the other two, so it is versioned and replaced by the same restricted act, and the
    // free-text CompanyProfile.LogoUrl is retired. All three must be a PNG or JPEG of at most 2 MB (the user's rulings),
    // checked here before the gate stores a byte: the gate's own type list is a tenant setting and admits documents.
    //
    // ⚠ **Gated on Admin, not Write, and that is the point.** HR maintains the company profile —
    // the letterhead, the addresses, the statutory numbers. Replacing the seal is a different act:
    // it is what makes a generated document look authentic, so it sits with the tier that already
    // holds the settings described as "knobs that move trust boundaries". A no new permission was
    // seeded for it; AdministerCompany already excludes the HR role and already means this.
    //
    // ⚠ Until these existed, both images were caller-supplied path STRINGS on the update DTO,
    // substituted straight into rendered offer and probation letters — so an arbitrary value became
    // an image source in a document sent to a candidate.

    /// <summary>The seal, signature and logo in force, and every image used before them.</summary>
    [HttpGet("seal-assets")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<CompanySealAssetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSealAssets(CancellationToken ct)
        => Ok(await _seals.GetHistoryAsync(ct));

    /// <summary>Replaces the seal, the signature or the logo, retiring whatever it supersedes.</summary>
    [HttpPost("seal-assets/{kind}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    [RequestSizeLimit(10_000_000)]
    [ProducesResponseType(typeof(CompanySealAssetDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReplaceSealAsset(
        CompanySealAssetKind kind, IFormFile? file, [FromForm] string? reason, CancellationToken ct)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest(new { message = "Tenant context could not be resolved." });
        if (!Enum.IsDefined(kind))
            return BadRequest(new { message = "Say which image: Seal, Signature or Logo." });
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        // Lane 4c: a PNG or JPEG of at most 2 MB — by its name, its type and its first bytes — before a byte is stored.
        var header = new byte[ImageRules.HeaderLength];
        int read;
        await using (var peek = file.OpenReadStream())
            read = await peek.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        var refusal = ImageRules.RefuseImage(kind, file.FileName, file.ContentType, file.Length, header.AsSpan(0, read));
        if (refusal is not null)
            return BadRequest(new { message = refusal });

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                // ⚠ UserId is a STRING on ICurrentUserService, and the seal deliberately does NOT
                // require a linked employee the way HrAttachmentUpload does — the tier that replaces
                // a seal is an administrator, who often has no employee record.
                ActorUserId = Guid.TryParse(_currentUser.UserId, out var actorId) ? actorId : Guid.Empty,
                ActorName = _currentUser.UserName ?? "administrator",
                Category = ControlledFileUploadCategories.HrCompanySealAssets,
                File = file,
                // ⚠ Registered in the DMS, unlike an avatar. An instrument of authority is a
                // retained document: after a compromise, "which documents carry the seal that
                // leaked?" has to stay answerable.
                Registration = new HrDocumentDmsRegistration
                {
                    SourceEntityType = nameof(Core.Entities.HR.CompanySealAsset),
                    // The company profile is one row per tenant, so the tenant IS the owning record.
                    SourceRecordId = tenantId,
                    SourceLabel = $"Company {kind}",
                    Title = $"Company {kind}",
                    DocumentType = $"Company{kind}",
                    ChangeSummary = reason
                }
            }, ct);
        }
        catch (ControlledFileUploadException ex)
        {
            _logger.LogWarning(ex, "Company {Kind} upload refused by the gate", kind);
            return BadRequest(new { message = ex.Message });
        }

        return Ok(await _seals.ReplaceAsync(
            kind, document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId,
            document.OriginalFileName, document.ContentType, document.FileSize, reason, ct));
    }

    /// <summary>
    /// Withdraws the current seal, signature or logo without replacing it.
    /// </summary>
    /// <remarks>
    /// A compromised seal must be able to stop being used before a replacement exists. Letters then
    /// render without one, which is the right outcome: the alternative is continuing to stamp
    /// documents with an image known to be bad. A withdrawn logo leaves letters on the tenant's own
    /// logo, if it has one (lane 4c).
    /// </remarks>
    [HttpPost("seal-assets/{kind}/retire")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetireSealAsset(
        CompanySealAssetKind kind, [FromBody] RetireCompanySealAssetDto? dto, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind))
            return BadRequest(new { message = "Say which image: Seal, Signature or Logo." });
        return await _seals.RetireCurrentAsync(kind, dto?.Reason, ct)
            ? NoContent()
            : NotFound(new { message = $"There is no {ImageRules.Describe(kind)} in force to withdraw." });
    }
}
