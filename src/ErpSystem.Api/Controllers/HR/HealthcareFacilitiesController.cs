using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/healthcare-facilities")]
[Authorize]
public class HealthcareFacilitiesController : MedicalControllerBase
{
    private readonly IHealthcareFacilityService _service;

    public HealthcareFacilitiesController(IHealthcareFacilityService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<HealthcareFacilitySummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllFacilitiesAsync(ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<HealthcareFacilitySummaryDto>>> GetActive(CancellationToken ct)
        => Ok(await _service.GetActiveFacilitiesAsync(ct));

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<HealthcareFacilitySummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => Ok(await _service.GetFacilitiesPagedAsync(pageNumber, pageSize, search, ct));

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<HealthcareFacilitySummaryDto>>> Search(
        [FromQuery] string term,
        CancellationToken ct)
        => Ok(await _service.SearchFacilitiesAsync(term, ct));

    [HttpGet("type/{facilityType}")]
    public async Task<ActionResult<IEnumerable<HealthcareFacilitySummaryDto>>> GetByType(
        HealthFacilityType facilityType,
        CancellationToken ct)
        => Ok(await _service.GetFacilitiesByTypeAsync(facilityType, ct));

    [HttpGet("nhis")]
    public async Task<ActionResult<IEnumerable<HealthcareFacilitySummaryDto>>> GetAcceptingNHIS(CancellationToken ct)
        => Ok(await _service.GetFacilitiesAcceptingNHISAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<HealthcareFacilityDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetFacilityByIdAsync(id, ct));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<HealthcareFacilityDetailDto>> GetWithDetails(Guid id, CancellationToken ct)
        => Ok(await _service.GetFacilityWithDetailsAsync(id, ct));

    [HttpGet("code/{facilityCode}")]
    public async Task<ActionResult<HealthcareFacilityDto?>> GetByCode(string facilityCode, CancellationToken ct)
        => Ok(await _service.GetFacilityByCodeAsync(facilityCode, ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<HealthcareFacilityDto>> Create([FromBody] CreateHealthcareFacilityDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateFacilityAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<HealthcareFacilityDto>> Update(Guid id, [FromBody] UpdateHealthcareFacilityDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateFacilityAsync(dto, userId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteFacilityAsync(id, ct);
        return NoContent();
    }
}
