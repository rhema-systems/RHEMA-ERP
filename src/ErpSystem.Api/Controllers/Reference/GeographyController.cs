using ErpSystem.Core.DTOs.Reference;
using ErpSystem.Core.Services.Reference;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Reference;

/// <summary>
/// Administrative geography — the Region / District / Town reference tree every module's addresses
/// resolve through. See docs/GEOGRAPHY-REFERENCE-DESIGN.md.
/// </summary>
/// <remarks>
/// <para><b>Reads are <c>InternalOnly</c> with no further gate, on purpose.</b> Every module's
/// address form lists regions and districts; requiring a permission to read them would break an
/// address dropdown for anyone outside the reference-data desk. The sensitivity in an address lives
/// on the record that carries it, which keeps its own module's gate.</para>
///
/// <para>Writes are gated on <see cref="ReferenceDataPermissions"/>: maintaining the tree is Write,
/// deleting from it is Admin. Deletion separates because it is the one act a later correction
/// cannot undo — records pointing at a deleted area lose what they said. End-dating is the
/// reversible alternative and sits with Write.</para>
/// </remarks>
[ApiController]
[Route("api/reference/geo")]
[Authorize(Policy = "InternalOnly")]
public class GeographyController : ControllerBase
{
    private readonly IGeographyService _service;
    private readonly ILogger<GeographyController> _logger;

    public GeographyController(IGeographyService service, ILogger<GeographyController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <remarks>
    /// ⚠ Surfaces the rule's own message. Without this the middleware maps
    /// <see cref="InvalidOperationException"/> to a fixed string and DISCARDS it, so "that tier
    /// still holds 261 areas" would reach the screen as a generic error.
    /// </remarks>
    private ActionResult Rejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Geography rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    // ── Schemes ─────────────────────────────────────────────────────────────

    [HttpGet("schemes")]
    [ProducesResponseType(typeof(IEnumerable<GeoSchemeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GeoSchemeDto>>> GetSchemes(
        [FromQuery] bool activeOnly = false, CancellationToken ct = default)
        => Ok(await _service.GetSchemesAsync(activeOnly, ct));

    [HttpGet("schemes/{id:guid}")]
    [ProducesResponseType(typeof(GeoSchemeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GeoSchemeDetailDto>> GetScheme(Guid id, CancellationToken ct = default)
    {
        try { return Ok(await _service.GetSchemeAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// The scheme an address form should render for a country. <c>204</c> — not <c>404</c> — when
    /// the country has no scheme: that is a normal state the widget answers by falling back to free
    /// text, not an error worth logging.
    /// </summary>
    [HttpGet("schemes/by-country/{countryId:guid}")]
    [ProducesResponseType(typeof(GeoSchemeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<GeoSchemeDetailDto>> GetSchemeForCountry(
        Guid countryId, CancellationToken ct = default)
    {
        var scheme = await _service.GetSchemeForCountryAsync(countryId, ct);
        return scheme == null ? NoContent() : Ok(scheme);
    }

    [HttpPost("schemes")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyWritePolicy)]
    [ProducesResponseType(typeof(GeoSchemeDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<GeoSchemeDto>> CreateScheme(
        [FromBody] CreateGeoSchemeDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.CreateSchemeAsync(dto, ct);
            return CreatedAtAction(nameof(GetScheme), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "creating a division scheme"); }
    }

    [HttpPut("schemes/{id:guid}")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyWritePolicy)]
    [ProducesResponseType(typeof(GeoSchemeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GeoSchemeDto>> UpdateScheme(
        Guid id, [FromBody] UpdateGeoSchemeDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest(new { message = "ID mismatch." });
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateSchemeAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating a division scheme"); }
    }

    [HttpDelete("schemes/{id:guid}")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteScheme(Guid id, CancellationToken ct = default)
    {
        try { await _service.DeleteSchemeAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "deleting a division scheme"); }
    }

    // ── Levels (tiers) ──────────────────────────────────────────────────────

    [HttpGet("schemes/{schemeId:guid}/levels")]
    [ProducesResponseType(typeof(IEnumerable<GeoLevelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GeoLevelDto>>> GetLevels(
        Guid schemeId, [FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        try { return Ok(await _service.GetLevelsAsync(schemeId, activeOnly, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("levels")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyWritePolicy)]
    [ProducesResponseType(typeof(GeoLevelDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<GeoLevelDto>> CreateLevel(
        [FromBody] CreateGeoLevelDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.CreateLevelAsync(dto, ct);
            return CreatedAtAction(nameof(GetLevels), new { schemeId = created.SchemeId }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "creating a tier"); }
    }

    [HttpPut("levels/{id:guid}")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyWritePolicy)]
    [ProducesResponseType(typeof(GeoLevelDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GeoLevelDto>> UpdateLevel(
        Guid id, [FromBody] UpdateGeoLevelDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest(new { message = "ID mismatch." });
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateLevelAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating a tier"); }
    }

    [HttpDelete("levels/{id:guid}")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteLevel(Guid id, CancellationToken ct = default)
    {
        try { await _service.DeleteLevelAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "deleting a tier"); }
    }

    // ── Areas ───────────────────────────────────────────────────────────────

    [HttpGet("schemes/{schemeId:guid}/areas")]
    [ProducesResponseType(typeof(IEnumerable<GeoAreaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GeoAreaDto>>> GetAreas(
        Guid schemeId,
        [FromQuery] Guid? levelId = null,
        [FromQuery] Guid? parentId = null,
        [FromQuery] bool includeHistorical = false,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        try { return Ok(await _service.GetAreasAsync(schemeId, levelId, parentId, includeHistorical, search, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>The whole scheme as a nested tree — one request instead of one per tier.</summary>
    [HttpGet("schemes/{schemeId:guid}/tree")]
    [ProducesResponseType(typeof(IEnumerable<GeoAreaTreeNodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GeoAreaTreeNodeDto>>> GetAreaTree(
        Guid schemeId, [FromQuery] bool includeHistorical = false, CancellationToken ct = default)
    {
        try { return Ok(await _service.GetAreaTreeAsync(schemeId, includeHistorical, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("areas/{id:guid}")]
    [ProducesResponseType(typeof(GeoAreaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GeoAreaDto>> GetArea(Guid id, CancellationToken ct = default)
    {
        try { return Ok(await _service.GetAreaAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// The cascade an address form drives: areas at one tier, under one parent. Historical areas
    /// are never returned here — a form must not offer a district that no longer exists.
    /// </summary>
    [HttpGet("levels/{levelId:guid}/options")]
    [ProducesResponseType(typeof(IEnumerable<GeoAreaOptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GeoAreaOptionDto>>> GetAreaOptions(
        Guid levelId, [FromQuery] Guid? parentId = null, CancellationToken ct = default)
        => Ok(await _service.GetAreaOptionsAsync(levelId, parentId, ct));

    /// <summary>Broadest-first ancestors of an area, including the area itself.</summary>
    [HttpGet("areas/{id:guid}/ancestors")]
    [ProducesResponseType(typeof(IEnumerable<GeoAreaOptionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GeoAreaOptionDto>>> GetAncestors(
        Guid id, CancellationToken ct = default)
    {
        try { return Ok(await _service.GetAncestorsAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Resolve a name to an area, falling back to alternate names. What the employee import uses
    /// per row, and what a paste-in-a-name search box uses.
    /// </summary>
    [HttpGet("resolve")]
    [ProducesResponseType(typeof(IEnumerable<GeoAreaResolutionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GeoAreaResolutionDto>>> Resolve(
        [FromQuery] string name,
        [FromQuery] Guid? schemeId = null,
        [FromQuery] Guid? levelId = null,
        [FromQuery] Guid? parentId = null,
        CancellationToken ct = default)
        => Ok(await _service.ResolveAsync(name, schemeId, levelId, parentId, ct));

    [HttpPost("areas")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyWritePolicy)]
    [ProducesResponseType(typeof(GeoAreaDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<GeoAreaDto>> CreateArea(
        [FromBody] CreateGeoAreaDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.CreateAreaAsync(dto, ct);
            return CreatedAtAction(nameof(GetArea), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "creating an area"); }
    }

    [HttpPut("areas/{id:guid}")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyWritePolicy)]
    [ProducesResponseType(typeof(GeoAreaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GeoAreaDto>> UpdateArea(
        Guid id, [FromBody] UpdateGeoAreaDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest(new { message = "ID mismatch." });
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateAreaAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating an area"); }
    }

    [HttpDelete("areas/{id:guid}")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteArea(Guid id, CancellationToken ct = default)
    {
        try { await _service.DeleteAreaAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "deleting an area"); }
    }

    // ── Aliases ─────────────────────────────────────────────────────────────

    [HttpGet("areas/{areaId:guid}/aliases")]
    [ProducesResponseType(typeof(IEnumerable<GeoAreaAliasDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GeoAreaAliasDto>>> GetAliases(
        Guid areaId, CancellationToken ct = default)
    {
        try { return Ok(await _service.GetAliasesAsync(areaId, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("aliases")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyWritePolicy)]
    [ProducesResponseType(typeof(GeoAreaAliasDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<GeoAreaAliasDto>> CreateAlias(
        [FromBody] CreateGeoAreaAliasDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.CreateAliasAsync(dto, ct);
            return CreatedAtAction(nameof(GetAliases), new { areaId = created.GeoAreaId }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "creating an alternate name"); }
    }

    [HttpPut("aliases/{id:guid}")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyWritePolicy)]
    [ProducesResponseType(typeof(GeoAreaAliasDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GeoAreaAliasDto>> UpdateAlias(
        Guid id, [FromBody] UpdateGeoAreaAliasDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest(new { message = "ID mismatch." });
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateAliasAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating an alternate name"); }
    }

    [HttpDelete("aliases/{id:guid}")]
    [Authorize(Policy = ReferenceDataPermissions.GeographyAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAlias(Guid id, CancellationToken ct = default)
    {
        try { await _service.DeleteAliasAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "deleting an alternate name"); }
    }
}
