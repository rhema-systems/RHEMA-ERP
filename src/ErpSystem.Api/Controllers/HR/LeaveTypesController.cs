using ErpSystem.Core.DTOs.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Leave type configuration: types, sub-types, allocations, eligibility rules, accrual policies
/// </summary>
/// <remarks>
/// W3 slice 5: READS stay on InternalOnly — an employee filing a request has to name a leave
/// type, the same reason medical keeps its facility register open. Maintaining the catalogue is
/// the leave WRITE tier (HR's own reference data, matching the admin.hr decision), while
/// deactivation and deletion are ADMIN.
/// </remarks>
[ApiController]
[Route("api/hr/leave-types")]
[Authorize(Policy = "InternalOnly")]
public class LeaveTypesController : ControllerBase
{
    private readonly ILeaveTypeService _service;
    private readonly ILogger<LeaveTypesController> _logger;

    public LeaveTypesController(ILeaveTypeService service, ILogger<LeaveTypesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // ─── Leave Types ─────────────────────────────────────────────────────────

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LeaveTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaveTypeDto>>> GetAll([FromQuery] bool activeOnly = true)
        => Ok(await _service.GetAllLeaveTypesAsync(activeOnly));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LeaveTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveTypeDto>> GetById(Guid id)
    {
        try
        {
            return Ok(await _service.GetLeaveTypeByIdAsync(id));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}/detail")]
    [ProducesResponseType(typeof(LeaveTypeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveTypeDetailDto>> GetDetail(Guid id)
    {
        try
        {
            return Ok(await _service.GetLeaveTypeDetailAsync(id));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
    [ProducesResponseType(typeof(LeaveTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeaveTypeDto>> Create([FromBody] CreateLeaveTypeDto dto)
    {
        try
        {
            var result = await _service.CreateLeaveTypeAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating leave type");
            return StatusCode(500, "An error occurred while creating the leave type");
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
    [ProducesResponseType(typeof(LeaveTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveTypeDto>> Update(Guid id, [FromBody] UpdateLeaveTypeDto dto)
    {
        try
        {
            return Ok(await _service.UpdateLeaveTypeAsync(id, dto));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating leave type {id}", id);
            return StatusCode(500, "An error occurred while updating the leave type");
        }
    }

    [HttpPatch("{id:guid}/deactivate")]
    [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        try
        {
            await _service.DeactivateLeaveTypeAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ─── Sub Types ───────────────────────────────────────────────────────────

    [HttpGet("{leaveTypeId:guid}/sub-types")]
    [ProducesResponseType(typeof(IEnumerable<LeaveSubTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaveSubTypeDto>>> GetSubTypes(
        Guid leaveTypeId, [FromQuery] bool activeOnly = false)
        => Ok(await _service.GetSubTypesAsync(leaveTypeId, activeOnly));

    [HttpPost("sub-types")]
    [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
    [ProducesResponseType(typeof(LeaveSubTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeaveSubTypeDto>> CreateSubType([FromBody] CreateLeaveSubTypeDto dto)
    {
        try
        {
            var result = await _service.CreateSubTypeAsync(dto);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating leave sub-type");
            return StatusCode(500, "An error occurred while creating the leave sub-type");
        }
    }

    [HttpPut("sub-types/{id:guid}")]
    [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
    [ProducesResponseType(typeof(LeaveSubTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveSubTypeDto>> UpdateSubType(Guid id, [FromBody] CreateLeaveSubTypeDto dto)
    {
        try
        {
            return Ok(await _service.UpdateSubTypeAsync(id, dto));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating leave sub-type {id}", id);
            return StatusCode(500, "An error occurred while updating the leave sub-type");
        }
    }

    [HttpDelete("sub-types/{id:guid}")]
    [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSubType(Guid id)
    {
        try
        {
            await _service.DeleteSubTypeAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ─── Category Allocations ────────────────────────────────────────────────

    [HttpGet("{leaveTypeId:guid}/allocations")]
    [ProducesResponseType(typeof(IEnumerable<LeaveCategoryAllocationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaveCategoryAllocationDto>>> GetAllocations(Guid leaveTypeId)
        => Ok(await _service.GetAllocationsAsync(leaveTypeId));

    [HttpPost("allocations")]
    [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
    [ProducesResponseType(typeof(LeaveCategoryAllocationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeaveCategoryAllocationDto>> CreateAllocation([FromBody] CreateLeaveCategoryAllocationDto dto)
    {
        try
        {
            return StatusCode(201, await _service.CreateAllocationAsync(dto));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating leave category allocation");
            return StatusCode(500, "An error occurred while creating the allocation");
        }
    }

    [HttpPut("allocations/{id:guid}")]
    [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
    [ProducesResponseType(typeof(LeaveCategoryAllocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveCategoryAllocationDto>> UpdateAllocation(Guid id, [FromBody] CreateLeaveCategoryAllocationDto dto)
    {
        try
        {
            return Ok(await _service.UpdateAllocationAsync(id, dto));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating leave category allocation {id}", id);
            return StatusCode(500, "An error occurred while updating the allocation");
        }
    }

    [HttpDelete("allocations/{id:guid}")]
    [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAllocation(Guid id)
    {
        try
        {
            await _service.DeleteAllocationAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ─── Eligibility Rules ───────────────────────────────────────────────────

    [HttpGet("{leaveTypeId:guid}/eligibility")]
    [ProducesResponseType(typeof(IEnumerable<LeaveTypeEligibilityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaveTypeEligibilityDto>>> GetEligibilityRules(Guid leaveTypeId)
        => Ok(await _service.GetEligibilityRulesAsync(leaveTypeId));

    [HttpPost("eligibility")]
    [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
    [ProducesResponseType(typeof(LeaveTypeEligibilityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeaveTypeEligibilityDto>> CreateEligibilityRule([FromBody] CreateLeaveTypeEligibilityDto dto)
    {
        try
        {
            return StatusCode(201, await _service.CreateEligibilityRuleAsync(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating eligibility rule");
            return StatusCode(500, "An error occurred while creating the eligibility rule");
        }
    }

    [HttpDelete("eligibility/{id:guid}")]
    [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEligibilityRule(Guid id)
    {
        try
        {
            await _service.DeleteEligibilityRuleAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ─── Accrual Policies ────────────────────────────────────────────────────

    [HttpGet("{leaveTypeId:guid}/accrual-policies")]
    [ProducesResponseType(typeof(IEnumerable<LeaveAccrualPolicyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeaveAccrualPolicyDto>>> GetAccrualPolicies(Guid leaveTypeId)
        => Ok(await _service.GetAccrualPoliciesAsync(leaveTypeId));

    [HttpPost("accrual-policies")]
    [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
    [ProducesResponseType(typeof(LeaveAccrualPolicyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeaveAccrualPolicyDto>> CreateAccrualPolicy([FromBody] CreateLeaveAccrualPolicyDto dto)
    {
        try
        {
            return StatusCode(201, await _service.CreateAccrualPolicyAsync(dto));
        }
        // ⚠ A leave type may have ONE active accrual policy (entitlement plan A2), and the service
        // refuses a second with a sentence naming what to do instead. Without this arm that refusal
        // came back as a 500 and a generic string — the caller lost both the status code and the
        // only part of the message worth reading.
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating accrual policy");
            return StatusCode(500, "An error occurred while creating the accrual policy");
        }
    }

    [HttpPut("accrual-policies/{id:guid}")]
    [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
    [ProducesResponseType(typeof(LeaveAccrualPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaveAccrualPolicyDto>> UpdateAccrualPolicy(Guid id, [FromBody] CreateLeaveAccrualPolicyDto dto)
    {
        try
        {
            return Ok(await _service.UpdateAccrualPolicyAsync(id, dto));
        }
        // Same rule on the edit path: the payload carries a leave type, so an update can MOVE a
        // policy onto a type that already has one.
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating accrual policy {id}", id);
            return StatusCode(500, "An error occurred while updating the accrual policy");
        }
    }

    [HttpDelete("accrual-policies/{id:guid}")]
    [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAccrualPolicy(Guid id)
    {
        try
        {
            await _service.DeleteAccrualPolicyAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }
}
