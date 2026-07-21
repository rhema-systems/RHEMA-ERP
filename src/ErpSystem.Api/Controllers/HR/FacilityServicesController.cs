using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/facility-services")]
[Authorize]
public class FacilityServicesController : MedicalControllerBase
{
    private readonly IHealthcareFacilityService _service;

    public FacilityServicesController(IHealthcareFacilityService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FacilityServiceDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetFacilityServiceByIdAsync(id, ct));

    [HttpGet("facility/{facilityId:guid}")]
    public async Task<ActionResult<IEnumerable<FacilityServiceDto>>> GetByFacility(Guid facilityId, CancellationToken ct)
        => Ok(await _service.GetFacilityServicesAsync(facilityId, ct));

    [HttpPost]
    public async Task<ActionResult<FacilityServiceDto>> Create([FromBody] CreateFacilityServiceDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateFacilityServiceAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FacilityServiceDto>> Update(Guid id, [FromBody] UpdateFacilityServiceDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateFacilityServiceAsync(dto, userId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteFacilityServiceAsync(id, ct);
        return NoContent();
    }
}
