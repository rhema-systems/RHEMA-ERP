using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/signs")]
[Authorize]
public class SafetySignageController : SheApiControllerBase
{
    private readonly ISafetySignageService _service;

    public SafetySignageController(ISafetySignageService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SafetySignDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SafetySignDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("code/{signCode}")]
    public async Task<ActionResult<SafetySignDto?>> GetByCode(string signCode)
        => Ok(await _service.GetByCodeAsync(signCode));

    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetySignDto>>> GetByLocation(Guid locationId)
        => Ok(await _service.GetByLocationAsync(locationId));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<SafetySignDto>>> GetByType(SheSafetySignType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SafetySignDto>>> GetByStatus(SheSafetySignStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<SafetySignDto>>> GetActive()
        => Ok(await _service.GetActiveAsync());

    [HttpGet("due-for-inspection")]
    public async Task<ActionResult<IEnumerable<SafetySignDto>>> GetDueForInspection([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetDueForInspectionAsync(daysAhead));

    [HttpPost]
    public async Task<ActionResult<SafetySignDto>> Create([FromBody] CreateSafetySignDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SafetySignDto>> Update(Guid id, [FromBody] UpdateSafetySignDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}
