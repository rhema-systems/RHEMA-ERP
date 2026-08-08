using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/onboarding-plan-templates")]
[Authorize]
public class OnboardingPlanTemplateController : ControllerBase
{
    private readonly IOnboardingPlanTemplateService _service;
    private readonly ICurrentUserService _currentUser;

    public OnboardingPlanTemplateController(
        IOnboardingPlanTemplateService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // TEMPLATE QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OnboardingPlanTemplateDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<OnboardingPlanTemplateSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}/with-tasks")]
    public async Task<ActionResult<OnboardingPlanTemplateDetailDto>> GetWithTaskTemplates(Guid id)
        => Ok(await _service.GetWithTaskTemplatesAsync(id));

    [HttpGet("position/{positionId:guid}")]
    public async Task<ActionResult<IEnumerable<OnboardingPlanTemplateSummaryDto>>> GetByPosition(Guid positionId)
        => Ok(await _service.GetByPositionIdAsync(positionId));

    [HttpGet("default")]
    public async Task<ActionResult<OnboardingPlanTemplateDto?>> GetDefault()
        => Ok(await _service.GetDefaultAsync());

    // =========================================================================
    // TEMPLATE CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<OnboardingPlanTemplateDto>> Create(
        [FromBody] CreateOnboardingPlanTemplateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OnboardingPlanTemplateDto>> Update(
        Guid id, [FromBody] UpdateOnboardingPlanTemplateDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // TASK TEMPLATES
    // =========================================================================

    [HttpGet("{planTemplateId:guid}/task-templates")]
    public async Task<ActionResult<IEnumerable<OnboardingTaskTemplateDto>>> GetTaskTemplates(Guid planTemplateId)
        => Ok(await _service.GetTaskTemplatesAsync(planTemplateId));

    [HttpPost("{planTemplateId:guid}/task-templates")]
    public async Task<ActionResult<OnboardingTaskTemplateDto>> AddTaskTemplate(
        Guid planTemplateId, [FromBody] CreateOnboardingTaskTemplateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.AddTaskTemplateAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("task-templates/{taskTemplateId:guid}")]
    public async Task<ActionResult<OnboardingTaskTemplateDto>> UpdateTaskTemplate(
        Guid taskTemplateId, [FromBody] UpdateOnboardingTaskTemplateDto dto)
    {
        if (taskTemplateId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateTaskTemplateAsync(dto, employeeId.Value));
    }

    [HttpDelete("task-templates/{taskTemplateId:guid}")]
    public async Task<IActionResult> DeleteTaskTemplate(Guid taskTemplateId)
    {
        await _service.DeleteTaskTemplateAsync(taskTemplateId);
        return NoContent();
    }
}
