using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>Onboarding checklist authoring — HR only.</summary>
[ApiController]
[OrientationBusinessRules]
[Route("api/onboarding-plan-templates")]
[Authorize(Policy = "InternalOnly")]
public class OnboardingPlanTemplateController : ControllerBase
{
    private readonly IOnboardingPlanTemplateService _service;
    private readonly IOnboardingTemplateApplicabilityService _applicability;
    private readonly ICurrentUserService _currentUser;

    public OnboardingPlanTemplateController(
        IOnboardingPlanTemplateService service,
        IOnboardingTemplateApplicabilityService applicability,
        ICurrentUserService currentUser)
    {
        _service = service;
        _applicability = applicability;
        _currentUser = currentUser;
    }

    // =========================================================================
    // TEMPLATE QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<OnboardingPlanTemplateDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("all")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OnboardingPlanTemplateSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}/with-tasks")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<OnboardingPlanTemplateDetailDto>> GetWithTaskTemplates(Guid id)
        => Ok(await _service.GetWithTaskTemplatesAsync(id));

    // ⚠ Removed: GET position/{positionId}. It took a position id and ignored it, returning every
    // active template — OnboardingPlanTemplate had no link to a position. This comment asked for
    // template applicability rules on the OrientationAudienceRule shape and a GET /applicable; round 4
    // lane I4 built both, below. The one change from what it asked: an organisation LEVEL rather than
    // a grade, because grade is payroll's axis and not one the shared HR audience model has.

    /// <summary>
    /// Which template a placement would get, and why — every candidate with its score, so the
    /// answer can be argued with. Pass <c>employeeId</c> to use a person's current placement;
    /// otherwise any of the four (an offer may not know the location yet).
    /// </summary>
    [HttpGet("applicable")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<OnboardingTemplateApplicabilityDto>> GetApplicable(
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? positionId,
        [FromQuery] Guid? organizationUnitId,
        [FromQuery] Guid? organizationLevelId,
        [FromQuery] Guid? locationId)
    {
        if (_currentUser.TenantId is not { } tenantId) return BadRequest("Tenant context could not be resolved.");
        return Ok(employeeId is { } id
            ? await _applicability.FindApplicableForEmployeeAsync(tenantId, id)
            : await _applicability.FindApplicableAsync(tenantId, positionId, organizationUnitId, organizationLevelId, locationId));
    }

    /// <summary>Who a template is for. None means it is chosen by hand only, unless it is the default.</summary>
    [HttpGet("{id:guid}/audiences")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IReadOnlyList<OnboardingPlanTemplateAudienceDto>>> GetAudiences(Guid id)
        => Ok(await _applicability.GetAudiencesAsync(id));

    [HttpPost("{id:guid}/audiences")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OnboardingPlanTemplateAudienceDto>> AddAudience(
        Guid id, [FromBody] CreateOnboardingPlanTemplateAudienceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!Guid.TryParse(_currentUser.UserId, out var userId)) return BadRequest("Your user could not be resolved.");
        return Ok(await _applicability.AddAudienceAsync(id, dto, userId));
    }

    [HttpDelete("{id:guid}/audiences/{audienceId:guid}")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<IActionResult> RemoveAudience(Guid id, Guid audienceId)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId)) return BadRequest("Your user could not be resolved.");
        await _applicability.RemoveAudienceAsync(id, audienceId, userId);
        return NoContent();
    }

    [HttpGet("default")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<OnboardingPlanTemplateDto?>> GetDefault()
        => Ok(await _service.GetDefaultAsync());

    // =========================================================================
    // TEMPLATE CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
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
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
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
    [Authorize(Policy = HrPermissions.OrientationAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // TASK TEMPLATES
    // =========================================================================

    [HttpGet("{planTemplateId:guid}/task-templates")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OnboardingTaskTemplateDto>>> GetTaskTemplates(Guid planTemplateId)
        => Ok(await _service.GetTaskTemplatesAsync(planTemplateId));

    [HttpPost("{planTemplateId:guid}/task-templates")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
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
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
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
    [Authorize(Policy = HrPermissions.OrientationAdminPolicy)]
    public async Task<IActionResult> DeleteTaskTemplate(Guid taskTemplateId)
    {
        await _service.DeleteTaskTemplateAsync(taskTemplateId);
        return NoContent();
    }
}
