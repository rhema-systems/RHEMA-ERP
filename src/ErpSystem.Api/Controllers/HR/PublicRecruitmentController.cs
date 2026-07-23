using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Public-facing career portal endpoints — no authentication required.
/// All endpoints are rate-limited and return only safe, publicly appropriate data.
/// Tenant is resolved from the <c>X-Tenant-Id</c> request header.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting("PublicPortalPolicy")]
public class PublicRecruitmentController : ControllerBase
{
    private readonly IJobVacancyService    _vacancyService;
    private readonly IJobApplicationService _applicationService;
    private readonly ICountryService       _countryService;
    private readonly IFileStorageService   _fileStorage;
    private readonly ISkillService         _skillService;
    private readonly IQualificationCatalogueService _qualificationCatalogueService;
    private readonly ILogger<PublicRecruitmentController> _logger;

    public PublicRecruitmentController(
        IJobVacancyService vacancyService,
        IJobApplicationService applicationService,
        ICountryService countryService,
        IFileStorageService fileStorage,
        ISkillService skillService,
        IQualificationCatalogueService qualificationCatalogueService,
        ILogger<PublicRecruitmentController> logger)
    {
        _vacancyService              = vacancyService;
        _applicationService          = applicationService;
        _countryService              = countryService;
        _fileStorage                 = fileStorage;
        _skillService                = skillService;
        _qualificationCatalogueService = qualificationCatalogueService;
        _logger                      = logger;
    }

    // =========================================================================
    // VACANCY LISTING
    // =========================================================================

    /// <summary>
    /// Returns the list of currently published vacancies available for external application.
    /// Supports optional search, employment-type, and work-mode filters.
    /// </summary>
    [HttpGet("vacancies")]
    [ProducesResponseType(typeof(IEnumerable<PublicVacancyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublicVacancyDto>>> GetPublishedVacancies(
        [FromQuery] string?        search     = null,
        [FromQuery] EmploymentType? empType   = null,
        [FromQuery] WorkMode?       workMode  = null,
        CancellationToken ct = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });

        var vacancies = await _vacancyService.GetPublishedForExternalPortalAsync(
            tenantId:   tenantId,
            searchTerm: search,
            empType:    empType,
            workMode:   workMode,
            cancellationToken: ct);

        return Ok(vacancies);
    }

    /// <summary>
    /// Returns the public details for a single published vacancy.
    /// Returns 404 when the vacancy is not found or is not in Published status.
    /// </summary>
    [HttpGet("vacancies/{id:guid}")]
    [ProducesResponseType(typeof(PublicVacancyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicVacancyDto>> GetVacancyById(
        Guid id,
        CancellationToken ct = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });

        var vacancy = await _vacancyService.GetPublicVacancyByIdAsync(tenantId, id, ct);
        if (vacancy == null)
            return NotFound(new { message = "Vacancy not found or no longer available." });

        return Ok(vacancy);
    }

    /// <summary>
    /// Extracts a usable tenant id from the <c>X-Tenant-Id</c> header. The value is passed explicitly into
    /// the vacancy service/repository query — public vacancy queries are NOT covered by a DbContext global
    /// tenant filter (it is inactive for the DI-created context), so without threading this id through, the
    /// listing would span every tenant's published adverts.
    /// </summary>
    private bool TryGetTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        return Request.Headers.TryGetValue("X-Tenant-Id", out var value)
            && Guid.TryParse(value.ToString(), out tenantId)
            && tenantId != Guid.Empty;
    }

    // =========================================================================
    // APPLICATION SUBMISSION
    // =========================================================================

    /// <summary>
    /// Submits an external candidate application.
    /// Resolves or creates a candidate profile keyed by email address.
    /// Returns a tracking token that the candidate can use to check their status.
    /// </summary>
    [HttpPost("apply")]
    [EnableRateLimiting("PublicApplyPolicy")]
    [ProducesResponseType(typeof(ExternalApplicationConfirmationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ExternalApplicationConfirmationDto>> Apply(
        [FromBody] ExternalApplicationDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Resolve tenant from the X-Tenant-Id header (required for public portal)
        if (!Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader)
            || !Guid.TryParse(tenantHeader, out var tenantId))
        {
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });
        }

        try
        {
            var confirmation = await _applicationService.ExternalApplyAsync(dto, tenantId, ct);
            return CreatedAtAction(
                nameof(GetApplicationStatus),
                new { token = confirmation.TrackingToken },
                confirmation);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already submitted"))
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    // =========================================================================
    // APPLICATION STATUS TRACKING
    // =========================================================================

    /// <summary>
    /// Returns the public status of an application identified by tracking token.
    /// No personally sensitive scoring or HR-internal data is disclosed.
    /// </summary>
    [HttpGet("track/{token}")]
    [ProducesResponseType(typeof(PublicApplicationStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicApplicationStatusDto>> GetApplicationStatus(
        [FromRoute] string token,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
            return BadRequest(new { message = "Invalid tracking token." });

        try
        {
            var status = await _applicationService.GetApplicationStatusByTokenAsync(token, ct);
            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            // Return 404 without revealing whether the token format is invalid vs. not found
            return NotFound(new { message = "No application found for the provided tracking token." });
        }
    }

    /// <summary>
    /// Allows an external candidate to withdraw their application using only their tracking token.
    /// </summary>
    [HttpPost("track/{token}/withdraw")]
    [EnableRateLimiting("PublicApplyPolicy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> WithdrawApplication(
        [FromRoute] string token,
        [FromBody] ExternalWithdrawDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!string.Equals(token, dto.TrackingToken, StringComparison.Ordinal))
            return BadRequest(new { message = "Token in URL and body must match." });

        try
        {
            await _applicationService.WithdrawByTokenAsync(token, dto.Reason, ct);
            return Ok(new { message = "Your application has been withdrawn successfully." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "No application found for the provided tracking token." });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
    }

    // =========================================================================
    // REFERENCE DATA
    // =========================================================================

    /// <summary>
    /// Returns the list of active countries for use in the public application form.
    /// </summary>
    [HttpGet("countries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCountries(CancellationToken ct = default)
    {
        var countries = await _countryService.GetActiveCountriesAsync();
        return Ok(countries.Select(c => new { c.Id, c.Name, c.Code, c.Alpha2Code }));
    }

    /// <summary>
    /// Returns active skills from the catalogue for autocomplete in the application form.
    /// </summary>
    [HttpGet("catalogue/skills")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalogueSkills(CancellationToken ct = default)
    {
        var skills = await _skillService.GetActiveSkillsAsync();
        return Ok(skills.Select(s => new { s.Id, s.Name }));
    }

    /// <summary>
    /// Returns active qualifications from the catalogue for autocomplete in the application form.
    /// </summary>
    [HttpGet("catalogue/qualifications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalogueQualifications(CancellationToken ct = default)
    {
        var quals = await _qualificationCatalogueService.GetActiveAsync();
        return Ok(quals.Select(q => new { q.Id, q.Name }));
    }

    // =========================================================================
    // CV UPLOAD
    // =========================================================================

    private static readonly string[] AllowedCvExtensions = { ".pdf", ".doc", ".docx" };
    private const long MaxCvSizeBytes = 5 * 1024 * 1024; // 5 MB

    /// <summary>
    /// Accepts a CV/résumé file upload and stores it, returning the file path.
    /// The caller should include the returned path in the subsequent apply payload.
    /// </summary>
    [HttpPost("cv-upload")]
    [EnableRateLimiting("PublicApplyPolicy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadCv(
        IFormFile file,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (file.Length > MaxCvSizeBytes)
            return BadRequest(new { message = "File size must not exceed 5 MB." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedCvExtensions.Contains(ext))
            return BadRequest(new { message = "Only PDF, DOC, and DOCX files are accepted." });

        try
        {
            await using var stream = file.OpenReadStream();
            var safeFileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = await _fileStorage.UploadFileAsync(stream, safeFileName, "cv-uploads");
            return Ok(new { filePath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading CV file {FileName}", file.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Failed to upload the file. Please try again." });
        }
    }
}
