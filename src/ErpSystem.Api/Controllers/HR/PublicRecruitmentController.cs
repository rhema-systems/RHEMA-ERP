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
    private readonly ICountryService       _countryService;
    private readonly ISkillService         _skillService;
    private readonly IQualificationCatalogueService _qualificationCatalogueService;

    public PublicRecruitmentController(
        IJobVacancyService vacancyService,
        ICountryService countryService,
        ISkillService skillService,
        IQualificationCatalogueService qualificationCatalogueService)
    {
        _vacancyService              = vacancyService;
        _countryService              = countryService;
        _skillService                = skillService;
        _qualificationCatalogueService = qualificationCatalogueService;
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

    // The anonymous APPLY, TRACK and WITHDRAW endpoints were retired 2026-08-30: applying now
    // requires a registered candidate account (main JWT scheme, Candidate role) and lives on
    // api/candidate/applications, where "my applications" replaces token-based tracking. The
    // board below stays anonymous by design - browse public, apply logged-in.

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
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });

        var countries = await _countryService.GetActiveCountriesAsync(tenantId);
        return Ok(countries.Select(c => new { c.Id, c.Name, c.Code, c.Alpha2Code }));
    }

    /// <summary>
    /// Returns active skills from the catalogue for autocomplete in the application form.
    /// </summary>
    [HttpGet("catalogue/skills")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalogueSkills(CancellationToken ct = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });

        var skills = await _skillService.GetActiveSkillsAsync(tenantId);
        return Ok(skills.Select(s => new { s.Id, s.Name }));
    }

    /// <summary>
    /// Returns active qualifications from the catalogue for autocomplete in the application form.
    /// </summary>
    [HttpGet("catalogue/qualifications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalogueQualifications(CancellationToken ct = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });

        var quals = await _qualificationCatalogueService.GetActiveAsync(tenantId);
        return Ok(quals.Select(q => new { q.Id, q.Name }));
    }

    // The anonymous two-phase CV upload (cv-upload -> single-use ticket -> apply) was retired
    // with the anonymous apply, 2026-08-30. A registered candidate's CV rides their profile and
    // documents on api/candidate. The ticket table, minting service and sweeper survive only to
    // drain pre-retirement rows.

}
