using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using ErpSystem.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/approvals")]
[StaffTravelBusinessRules]
[Authorize(Policy = HrPermissions.TravelReadPolicy)]
public class StaffTravelApprovalsController : HrControllerBase
{
    private readonly IStaffTravelApprovalService _service;

    public StaffTravelApprovalsController(IStaffTravelApprovalService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    // =========================================================================
    // WORKFLOW TEMPLATES
    // =========================================================================

    [HttpGet("templates")]
    public async Task<ActionResult<IEnumerable<StaffTravelApprovalWorkflowTemplateSummaryDto>>> GetAllTemplates()
        => Ok(await _service.GetAllTemplatesAsync());

    [HttpGet("templates/active")]
    public async Task<ActionResult<IEnumerable<StaffTravelApprovalWorkflowTemplateSummaryDto>>> GetActiveTemplates()
        => Ok(await _service.GetActiveTemplatesAsync());

    [HttpGet("templates/{id:guid}")]
    public async Task<ActionResult<StaffTravelApprovalWorkflowTemplateDto>> GetTemplateById(Guid id)
        => Ok(await _service.GetTemplateByIdAsync(id));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("templates")]
    public async Task<ActionResult<StaffTravelApprovalWorkflowTemplateDto>> CreateTemplate([FromBody] CreateStaffTravelApprovalWorkflowTemplateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        var created = await _service.CreateTemplateAsync(dto, tenantId, userId);
        return CreatedAtAction(nameof(GetTemplateById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("templates/{id:guid}")]
    public async Task<ActionResult<StaffTravelApprovalWorkflowTemplateDto>> UpdateTemplate(Guid id, [FromBody] UpdateStaffTravelApprovalWorkflowTemplateDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        return Ok(await _service.UpdateTemplateAsync(dto, userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("templates/{id:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        await _service.DeleteTemplateAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW STEPS
    // =========================================================================

    [HttpGet("templates/{templateId:guid}/steps")]
    public async Task<ActionResult<IEnumerable<StaffTravelApprovalWorkflowStepDto>>> GetSteps(Guid templateId)
        => Ok(await _service.GetStepsAsync(templateId));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("templates/{templateId:guid}/steps")]
    public async Task<ActionResult<StaffTravelApprovalWorkflowStepDto>> AddStep(Guid templateId, [FromBody] CreateStaffTravelApprovalWorkflowStepDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        dto.WorkflowTemplateId = templateId;
        return Ok(await _service.AddStepAsync(dto, tenantId, userId));
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("steps/{stepId:guid}")]
    public async Task<ActionResult<StaffTravelApprovalWorkflowStepDto>> UpdateStep(Guid stepId, [FromBody] UpdateStaffTravelApprovalWorkflowStepDto dto)
    {
        if (stepId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        return Ok(await _service.UpdateStepAsync(dto, userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("steps/{stepId:guid}")]
    public async Task<IActionResult> DeleteStep(Guid stepId)
    {
        await _service.DeleteStepAsync(stepId);
        return NoContent();
    }

    // =========================================================================
    // APPROVAL INSTANCES
    // =========================================================================

    [HttpGet("instances/{id:guid}")]
    public async Task<ActionResult<StaffTravelApprovalInstanceDto>> GetInstanceById(Guid id)
        => Ok(await _service.GetInstanceByIdAsync(id));

    [HttpGet("instances/request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelApprovalInstanceSummaryDto>>> GetInstancesByRequest(Guid requestId)
        => Ok(await _service.GetInstancesByRequestAsync(requestId));

    [HttpGet("instances/request/{requestId:guid}/active")]
    public async Task<ActionResult<StaffTravelApprovalInstanceDto?>> GetActiveInstanceForRequest(Guid requestId)
        => Ok(await _service.GetActiveInstanceForRequestAsync(requestId));

    [HttpGet("instances/status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffTravelApprovalInstanceSummaryDto>>> GetInstancesByStatus(TravelApprovalInstanceStatus status)
        => Ok(await _service.GetInstancesByStatusAsync(status));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("instances")]
    public async Task<ActionResult<StaffTravelApprovalInstanceDto>> Initiate([FromBody] CreateStaffTravelApprovalInstanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        var created = await _service.InitiateAsync(dto, tenantId, userId);
        return CreatedAtAction(nameof(GetInstanceById), new { id = created.Id }, created);
    }

    // =========================================================================
    // DECISIONS
    // =========================================================================

    [HttpGet("instances/{instanceId:guid}/decisions")]
    public async Task<ActionResult<IEnumerable<StaffTravelApprovalDecisionDto>>> GetDecisions(Guid instanceId)
        => Ok(await _service.GetDecisionsForInstanceAsync(instanceId));

    [HttpGet("decisions/pending/{approverId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelApprovalDecisionDto>>> GetPendingDecisions(Guid approverId)
        => Ok(await _service.GetPendingDecisionsForApproverAsync(approverId));

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("instances/{instanceId:guid}/decisions")]
    public async Task<ActionResult<StaffTravelApprovalDecisionDto>> RecordDecision(Guid instanceId, [FromBody] RecordStaffTravelApprovalDecisionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        dto.ApprovalInstanceId = instanceId;
        if (dto.ApproverId == Guid.Empty) dto.ApproverId = userId;
        return Ok(await _service.RecordDecisionAsync(dto, tenantId));
    }
}
