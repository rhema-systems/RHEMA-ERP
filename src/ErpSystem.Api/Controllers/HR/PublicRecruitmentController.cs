using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
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
    private readonly ILanguageService _languageService;
    private readonly IIdentificationTypeService _identificationTypeService;
    private readonly IUnitOfWork _unitOfWork;

    public PublicRecruitmentController(
        IJobVacancyService vacancyService,
        ICountryService countryService,
        ISkillService skillService,
        IQualificationCatalogueService qualificationCatalogueService,
        ILanguageService languageService,
        IIdentificationTypeService identificationTypeService,
        IUnitOfWork unitOfWork)
    {
        _vacancyService              = vacancyService;
        _countryService              = countryService;
        _skillService                = skillService;
        _qualificationCatalogueService = qualificationCatalogueService;
        _languageService             = languageService;
        _identificationTypeService   = identificationTypeService;
        _unitOfWork                  = unitOfWork;
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
        // Round 3, lane C2: the careers form filters the catalogue by the row's kind (Education,
        // Certification, …), so the kind rides along as its enum name.
        return Ok(quals.Select(q => new { q.Id, q.Name, Type = q.Type.ToString() }));
    }

    // Round 3, lane C1. Three more catalogues the careers profile form picks from. Each is the
    // tenant's own active rows and nothing else: a language name, an identity document type, a
    // currency code — public facts, read through explicit-tenant paths because there is no user.

    /// <summary>Active languages from the HR catalogue, for the profile's languages section.</summary>
    [HttpGet("catalogue/languages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalogueLanguages(CancellationToken ct = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });

        var languages = await _languageService.GetActiveForTenantAsync(tenantId, ct);
        return Ok(languages.Select(l => new { l.Id, l.Name, l.Code }));
    }

    /// <summary>Active identity document types (Ghana Card, passport, …), for the national-ID trio.</summary>
    [HttpGet("catalogue/identification-types")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalogueIdentificationTypes(CancellationToken ct = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });

        var types = await _identificationTypeService.GetActiveForTenantAsync(tenantId, ct);
        return Ok(types.Select(t => new { t.Id, t.Name, t.Code }));
    }

    /// <summary>
    /// Active currencies as {code, name, symbol}, for the expected-salary field. Read straight off
    /// Finance's Currency rows for the header tenant: <c>ICurrencyService</c> takes its tenant from
    /// the signed-in user, and there is none here.
    /// </summary>
    [HttpGet("catalogue/currencies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCatalogueCurrencies(CancellationToken ct = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });

        var currencies = await _unitOfWork.Repository<Currency>().GetQueryable()
            .Where(c => c.TenantId == tenantId && c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.CurrencyCode)
            .Select(c => new { Code = c.CurrencyCode, Name = c.CurrencyName, Symbol = c.CurrencySymbol })
            .ToListAsync(ct);
        return Ok(currencies);
    }

    // =========================================================================
    // GEOGRAPHY — round 4, lane A
    // =========================================================================
    //
    // ⚠ Why these exist at all, rather than the careers pages calling api/reference/geo.
    // That controller is [Authorize(Policy = "InternalOnly")], and InternalOnly is a BLOCKLIST that
    // excludes exactly three roles: ExternalUser, Candidate and ConsultantClient. So a signed-in
    // candidate filling in their own address is refused by it — and refused silently, because a
    // react-query default of [] renders as an empty dropdown rather than an error. The cascade
    // would have looked like "this country has no scheme" for every candidate, forever.
    //
    // These three mirror GeographyController's cascade reads exactly, scoped to the header tenant
    // and returning nothing but what an address form needs. Read-only, rate-limited with the rest
    // of this controller. Historical areas are excluded: nobody should be asked to file a new
    // address in a district that no longer exists.

    /// <summary>
    /// The administrative scheme a country uses, with its tiers — or <b>204</b> when it has none.
    /// </summary>
    /// <remarks>
    /// 204 rather than 404 on purpose, matching <c>GeographyController</c>: a country with no
    /// scheme is the NORMAL case (most of the world), and the address widget reads it as "fall back
    /// to the free-text city", not as an error to show the candidate.
    /// </remarks>
    [HttpGet("catalogue/geo-scheme")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetGeoSchemeForCountry(
        [FromQuery] Guid countryId, CancellationToken ct = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });
        if (countryId == Guid.Empty) return NoContent();

        var scheme = await _unitOfWork.Repository<ErpSystem.Core.Entities.Reference.GeoScheme>()
            .GetQueryable()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive && s.CountryId == countryId)
            .OrderByDescending(s => s.IsDefault)
            .Select(s => new { s.Id, s.Name, s.Code })
            .FirstOrDefaultAsync(ct);
        if (scheme is null) return NoContent();

        var levels = await _unitOfWork.Repository<ErpSystem.Core.Entities.Reference.GeoLevel>()
            .GetQueryable()
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.IsActive && l.SchemeId == scheme.Id)
            .OrderBy(l => l.LevelNumber)
            .Select(l => new { l.Id, l.Name, l.Code, l.LevelNumber, l.IsRequiredInAddress, l.AllowsAddressAssignment })
            .ToListAsync(ct);

        return Ok(new { scheme.Id, scheme.Name, scheme.Code, Levels = levels });
    }

    /// <summary>The areas offered at one tier, under one parent — the cascade's driver.</summary>
    [HttpGet("catalogue/geo-areas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGeoAreaOptions(
        [FromQuery] Guid levelId, [FromQuery] Guid? parentId, CancellationToken ct = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });
        if (levelId == Guid.Empty) return Ok(Array.Empty<object>());

        var query = _unitOfWork.Repository<ErpSystem.Core.Entities.Reference.GeoArea>()
            .GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && a.IsActive && a.GeoLevelId == levelId);

        // A null parent means the broadest tier, where no area has one. Filtering on
        // "ParentAreaId == null" is therefore correct rather than "any parent".
        query = parentId is { } pid
            ? query.Where(a => a.ParentAreaId == pid)
            : query.Where(a => a.ParentAreaId == null);

        var areas = await query
            .OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.Name, a.Code, a.GeoLevelId, a.ParentAreaId })
            .ToListAsync(ct);
        return Ok(areas);
    }

    /// <summary>
    /// One area's ancestors, broadest first and including the area itself — what lets a returning
    /// candidate's saved address re-open the cascade at the right tier on every visit.
    /// </summary>
    [HttpGet("catalogue/geo-areas/{areaId:guid}/ancestors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGeoAreaAncestors(Guid areaId, CancellationToken ct = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return BadRequest(new { message = "A valid X-Tenant-Id header is required." });

        var repo = _unitOfWork.Repository<ErpSystem.Core.Entities.Reference.GeoArea>();
        var area = await repo.GetQueryable()
            .Where(a => a.Id == areaId && a.TenantId == tenantId && !a.IsDeleted)
            .Select(a => new { a.Id, a.Path })
            .FirstOrDefaultAsync(ct);
        if (area is null) return Ok(Array.Empty<object>());

        // Path is "/root/child/leaf" and already includes this area as its last segment, so the
        // whole chain comes out of one parse and one IN query rather than a walk up the tree.
        var ids = (area.Path ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue).Select(g => g!.Value)
            .ToList();
        if (ids.Count == 0) ids.Add(areaId);

        var rows = await repo.GetQueryable()
            .Where(a => ids.Contains(a.Id) && a.TenantId == tenantId && !a.IsDeleted)
            .Select(a => new { a.Id, a.Name, a.Code, a.GeoLevelId, a.ParentAreaId })
            .ToListAsync(ct);

        // Ordered by the path, not by the database — the path IS the ancestry.
        var byId = rows.ToDictionary(r => r.Id);
        var ordered = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        return Ok(ordered);
    }

    // The anonymous two-phase CV upload (cv-upload -> single-use ticket -> apply) was retired
    // with the anonymous apply, 2026-08-30. A registered candidate's CV rides their profile and
    // documents on api/candidate. The ticket table, minting service and sweeper survive only to
    // drain pre-retirement rows.

}
