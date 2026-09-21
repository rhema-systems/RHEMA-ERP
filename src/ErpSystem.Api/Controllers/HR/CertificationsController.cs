using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Services.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The certification catalogue — the credentials each certifying body issues (demo feedback
/// round 2, lane C2; plan § 1.3 and § 6.3).
/// </summary>
/// <remarks>
/// Lives beside the certifying bodies under <c>api/hr/reference</c> because it is reference data
/// of the same family; the skill, position and employee reads that consume it live with those
/// records. Same tiers as the bodies: read for the list, write to add or change, admin to remove.
/// </remarks>
[ApiController]
[Route("api/hr/reference/certifications")]
[Authorize(Policy = "InternalOnly")]
public class CertificationsController : ControllerBase
{
    private readonly ICertificationService _service;
    private readonly ILogger<CertificationsController> _logger;

    public CertificationsController(ICertificationService service, ILogger<CertificationsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private ActionResult Rejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Certification rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>The catalogue, optionally one body's, optionally active only, optionally searched.</summary>
    [HttpGet]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<CertificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CertificationDto>>> GetAll(
        [FromQuery] Guid? bodyId = null, [FromQuery] bool activeOnly = false, [FromQuery] string? search = null,
        CancellationToken ct = default)
        => Ok(await _service.GetCertificationsAsync(bodyId, activeOnly, search, ct));

    /// <summary>The credentials one body issues — the second half of the body → certification cascade.</summary>
    [HttpGet("/api/hr/reference/certifying-bodies/{bodyId:guid}/certifications")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<CertificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CertificationDto>>> GetForBody(
        Guid bodyId, [FromQuery] bool activeOnly = false, CancellationToken ct = default)
        => Ok(await _service.GetCertificationsAsync(bodyId, activeOnly, null, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(CertificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CertificationDto>> GetById(Guid id, CancellationToken ct)
    {
        try { return Ok(await _service.GetCertificationAsync(id, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(CertificationDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CertificationDto>> Create([FromBody] CreateCertificationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var created = await _service.CreateCertificationAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "creating a certification"); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(CertificationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CertificationDto>> Update(Guid id, [FromBody] UpdateCertificationDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest(new { message = "ID mismatch." });
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.UpdateCertificationAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating a certification"); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _service.DeleteCertificationAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "removing a certification"); }
    }
}
