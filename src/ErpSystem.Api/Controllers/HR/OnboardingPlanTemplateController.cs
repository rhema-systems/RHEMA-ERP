using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>Onboarding checklist authoring — HR only.</summary>
[ApiController]
[Route("api/onboarding-plan-templates")]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
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

    // ⚠ Removed: GET position/{positionId}. It took a position id and ignored it, returning every
    // active template — OnboardingPlanTemplate has no link to a position, and the only position on the
    // graph (OnboardingTaskTemplate.OwnerPositionId) says who *performs* a task, not who a template is
    // *for*. Answering it properly needs template applicability rules, modelled on the
    // OrientationAudienceRule shape this module already uses (TargetType / TargetEntityId /
    // IsInclusive), so that onboarding can be scoped by grade, org unit and location too — not just
    // position. That endpoint should arrive as GET /applicable?positionId=&orgUnitId=&gradeId=, so
    // reinstating this one-dimensional route now would only bake in the wrong shape.
    // Callers pick a template from `all`, or fall back to `default`.

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
