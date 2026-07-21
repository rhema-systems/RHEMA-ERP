using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/medical-physicians")]
[Authorize]
public class MedicalPhysiciansController : MedicalControllerBase
{
    private readonly IHealthcareFacilityService _service;

    public MedicalPhysiciansController(IHealthcareFacilityService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PhysicianSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllPhysiciansAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PhysicianDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetPhysicianByIdAsync(id, ct));

    [HttpGet("facility/{facilityId:guid}")]
    public async Task<ActionResult<IEnumerable<PhysicianSummaryDto>>> GetByFacility(Guid facilityId, CancellationToken ct)
        => Ok(await _service.GetPhysiciansByFacilityAsync(facilityId, ct));

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<PhysicianSummaryDto>>> Search([FromQuery] string term, CancellationToken ct)
        => Ok(await _service.SearchPhysiciansAsync(term, ct));

    [HttpPost]
    public async Task<ActionResult<PhysicianDto>> Create([FromBody] CreatePhysicianDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreatePhysicianAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PhysicianDto>> Update(Guid id, [FromBody] UpdatePhysicianDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdatePhysicianAsync(dto, userId, ct));
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, [FromBody] VerifyPhysicianDto dto, CancellationToken ct)
    {
        dto.PhysicianId = id;
        await _service.VerifyPhysicianAsync(dto, ct);
        return Ok(new { message = "Physician verified." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeletePhysicianAsync(id, ct);
        return NoContent();
    }
}
