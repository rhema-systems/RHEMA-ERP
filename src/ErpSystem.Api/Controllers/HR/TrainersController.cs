using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/trainers")]
[Authorize]
[TrainingBusinessRulesAttribute]
public class TrainersController : ControllerBase
{
    private readonly ITrainerService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainersController(ITrainerService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // TRAINER PROFILE QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrainerProfileSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<TrainerProfileSummaryDto>>> GetActive(CancellationToken ct)
        => Ok(await _service.GetActiveAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainerProfileDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("vendor/{vendorId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainerProfileSummaryDto>>> GetByVendorId(Guid vendorId, CancellationToken ct)
        => Ok(await _service.GetByVendorIdAsync(vendorId, ct));

    [HttpGet("available")]
    public async Task<ActionResult<IEnumerable<TrainerProfileSummaryDto>>> GetAvailableForDateRange(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken ct = default)
        => Ok(await _service.GetAvailableForDateRangeAsync(from, to, ct));

    // =========================================================================
    // TRAINER PROFILE CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<TrainerProfileDto>> Create([FromBody] CreateTrainerProfileDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TrainerProfileDto>> Update(Guid id, [FromBody] UpdateTrainerProfileDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // SKILLS SUB-OPERATIONS
    // =========================================================================

    [HttpPost("{id:guid}/skills")]
    public async Task<ActionResult<TrainerSkillDto>> AddSkill(Guid id, [FromBody] CreateTrainerSkillDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.TrainerProfileId = id;
        return Ok(await _service.AddSkillAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/skills")]
    public async Task<ActionResult<IEnumerable<TrainerSkillDto>>> GetSkills(Guid id, CancellationToken ct)
        => Ok(await _service.GetSkillsAsync(id, ct));

    [HttpPut("skills/{skillId:guid}")]
    public async Task<ActionResult<TrainerSkillDto>> UpdateSkill(Guid skillId, [FromBody] UpdateTrainerSkillDto dto, CancellationToken ct)
    {
        if (skillId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateSkillAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("skills/{skillId:guid}")]
    public async Task<IActionResult> DeleteSkill(Guid skillId, CancellationToken ct)
    {
        await _service.DeleteSkillAsync(skillId, ct);
        return NoContent();
    }

    // =========================================================================
    // AVAILABILITY SUB-OPERATIONS
    // =========================================================================

    [HttpPost("{id:guid}/availability")]
    public async Task<ActionResult<TrainerAvailabilityDto>> AddAvailability(Guid id, [FromBody] CreateTrainerAvailabilityDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.TrainerProfileId = id;
        return Ok(await _service.AddAvailabilityAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/availability")]
    public async Task<ActionResult<IEnumerable<TrainerAvailabilityDto>>> GetAvailability(Guid id, CancellationToken ct)
        => Ok(await _service.GetAvailabilityAsync(id, ct));

    [HttpPut("availability/{availabilityId:guid}")]
    public async Task<ActionResult<TrainerAvailabilityDto>> UpdateAvailability(Guid availabilityId, [FromBody] UpdateTrainerAvailabilityDto dto, CancellationToken ct)
    {
        if (availabilityId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAvailabilityAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("availability/{availabilityId:guid}")]
    public async Task<IActionResult> DeleteAvailability(Guid availabilityId, CancellationToken ct)
    {
        await _service.DeleteAvailabilityAsync(availabilityId, ct);
        return NoContent();
    }
}
