using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/approvals")]
[Authorize]
public class StaffTravelApprovalsController : ControllerBase
{
    private readonly IStaffTravelApprovalService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffTravelApprovalsController(IStaffTravelApprovalService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
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

    [HttpPost("templates")]
    public async Task<ActionResult<StaffTravelApprovalWorkflowTemplateDto>> CreateTemplate([FromBody] CreateStaffTravelApprovalWorkflowTemplateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.EmployeeId;
        if (tenantId is null) return BadRequest("Tenant context could not be resolved.");
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        var created = await _service.CreateTemplateAsync(dto, tenantId.Value, userId.Value);
        return CreatedAtAction(nameof(GetTemplateById), new { id = created.Id }, created);
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<ActionResult<StaffTravelApprovalWorkflowTemplateDto>> UpdateTemplate(Guid id, [FromBody] UpdateStaffTravelApprovalWorkflowTemplateDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = _currentUser.EmployeeId;
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateTemplateAsync(dto, userId.Value));
    }

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

    [HttpPost("templates/{templateId:guid}/steps")]
    public async Task<ActionResult<StaffTravelApprovalWorkflowStepDto>> AddStep(Guid templateId, [FromBody] CreateStaffTravelApprovalWorkflowStepDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.EmployeeId;
        if (tenantId is null) return BadRequest("Tenant context could not be resolved.");
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        dto.WorkflowTemplateId = templateId;
        return Ok(await _service.AddStepAsync(dto, tenantId.Value, userId.Value));
    }

    [HttpPut("steps/{stepId:guid}")]
    public async Task<ActionResult<StaffTravelApprovalWorkflowStepDto>> UpdateStep(Guid stepId, [FromBody] UpdateStaffTravelApprovalWorkflowStepDto dto)
    {
        if (stepId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = _currentUser.EmployeeId;
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateStepAsync(dto, userId.Value));
    }

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

    [HttpPost("instances")]
    public async Task<ActionResult<StaffTravelApprovalInstanceDto>> Initiate([FromBody] CreateStaffTravelApprovalInstanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.EmployeeId;
        if (tenantId is null) return BadRequest("Tenant context could not be resolved.");
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        var created = await _service.InitiateAsync(dto, tenantId.Value, userId.Value);
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

    [HttpPost("instances/{instanceId:guid}/decisions")]
    public async Task<ActionResult<StaffTravelApprovalDecisionDto>> RecordDecision(Guid instanceId, [FromBody] RecordStaffTravelApprovalDecisionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.EmployeeId;
        if (tenantId is null) return BadRequest("Tenant context could not be resolved.");
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        dto.ApprovalInstanceId = instanceId;
        if (dto.ApproverId == Guid.Empty) dto.ApproverId = userId.Value;
        return Ok(await _service.RecordDecisionAsync(dto, tenantId.Value));
    }
}
