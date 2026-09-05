using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The services a healthcare facility offers, and what they cost.
/// </summary>
/// <remarks>
/// Reads are open to any authenticated user for the same reason as
/// <see cref="HealthcareFacilitiesController"/> — this is facility reference data, and an employee
/// filing their own claim needs it. Writes and deletes are HR work.
/// </remarks>
[ApiController]
[Route("api/facility-services")]
[Authorize(Policy = "InternalOnly")]
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

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<FacilityServiceDto>> Create([FromBody] CreateFacilityServiceDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } error) return error;

        var created = await _service.CreateFacilityServiceAsync(dto, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.MedicalWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FacilityServiceDto>> Update(Guid id, [FromBody] UpdateFacilityServiceDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out _, out var userId) is { } error) return error;

        return Ok(await _service.UpdateFacilityServiceAsync(dto, userId, ct));
    }

    [Authorize(Policy = HrPermissions.MedicalAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteFacilityServiceAsync(id, ct);
        return NoContent();
    }
}
