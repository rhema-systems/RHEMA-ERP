using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-program-groups")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class TrainingProgramGroupsController : ControllerBase
{
    private readonly ITrainingProgramGroupService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingProgramGroupsController(ITrainingProgramGroupService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrainingProgramGroupDto>>> GetAll([FromQuery] bool activeOnly = false, CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(activeOnly, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingProgramGroupDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingProgramGroupDto>> Create([FromBody] CreateTrainingProgramGroupDto dto, CancellationToken ct)
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
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingProgramGroupDto>> Update(Guid id, [FromBody] UpdateTrainingProgramGroupDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(id, dto, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
