using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// One-to-ones and interim conversations held during a cycle, and the goal updates that come
/// out of them. A goal update recorded here is applied to the goal itself, so flagging a goal
/// at risk in a check-in puts it in the at-risk reports.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CheckInsController : ControllerBase
{
    private readonly ICheckInService _checkInService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CheckInsController> _logger;

    public CheckInsController(
        ICheckInService checkInService,
        ICurrentUserService currentUserService,
        ILogger<CheckInsController> logger)
    {
        _checkInService = checkInService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>Check-ins the signed-in employee is the subject of.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(IEnumerable<CheckInDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMine([FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _checkInService.GetByEmployeeIdAsync(employeeId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving check-ins");
        }
    }

    /// <summary>Check-ins the signed-in employee is conducting — the manager's list.</summary>
    [HttpGet("me/conducting")]
    [ProducesResponseType(typeof(IEnumerable<CheckInDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMineAsConductor([FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetEmployeeId(out var employeeId, out var problem)) return problem!;

        try
        {
            var result = await _checkInService.GetByConductedByIdAsync(employeeId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins conducted by {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving check-ins");
        }
    }

    private bool TryGetEmployeeId(out Guid employeeId, out IActionResult? problem)
    {
        var id = _currentUserService.EmployeeId;
        if (id is null || id == Guid.Empty)
        {
            employeeId = Guid.Empty;
            problem = BadRequest(new { message = "Your account is not linked to an employee record, so it cannot have check-ins." });
            return false;
        }

        employeeId = id.Value;
        problem = null;
        return true;
    }

    /// <summary>
    /// Business rules are answered with 422 and the rule's own message — "check-ins are not
    /// enabled for this appraisal cycle" is something a user hits, not a server fault.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning(ex, "Check-in rule rejected while {Action}", action);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>Get check-ins with pagination</summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<CheckInDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.GetPagedAsync(pageNumber, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged check-ins");
            return StatusCode(500, "An error occurred while retrieving check-ins");
        }
    }

    /// <summary>Get a check-in by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CheckInDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-in {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the check-in");
        }
    }

    /// <summary>Get check-ins for an employee, optionally filtered by cycle</summary>
    [HttpGet("by-employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<CheckInDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEmployee(Guid employeeId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.GetByEmployeeIdAsync(employeeId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving check-ins");
        }
    }

    /// <summary>Get check-ins conducted by a specific person, optionally filtered by cycle</summary>
    [HttpGet("by-conductor/{conductedById:guid}")]
    [ProducesResponseType(typeof(IEnumerable<CheckInDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByConductedBy(Guid conductedById, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.GetByConductedByIdAsync(conductedById, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins conducted by {ConductedById}", conductedById);
            return StatusCode(500, "An error occurred while retrieving check-ins");
        }
    }

    /// <summary>Get upcoming check-ins for an employee within the next N days</summary>
    [HttpGet("upcoming/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<CheckInDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcoming(Guid employeeId, [FromQuery] int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.GetUpcomingAsync(employeeId, daysAhead, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upcoming check-ins for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving upcoming check-ins");
        }
    }

    /// <summary>Create a new check-in</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CheckInDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCheckInDto createDto, CancellationToken cancellationToken = default)
    {
        // Whoever schedules a check-in is normally the one holding it, and the client has no
        // employee id of its own — the token is the only place it exists. An omitted (empty)
        // ConductedById therefore resolves to the caller rather than failing the FK. An
        // explicit id is honoured, so HR scheduling on someone else's behalf still works.
        if (createDto.ConductedById == Guid.Empty)
        {
            if (!TryGetEmployeeId(out var conductorId, out var problem)) return problem!;
            createDto.ConductedById = conductorId;
        }

        try
        {
            var result = await _checkInService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "creating the check-in");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating check-in");
            return StatusCode(500, "An error occurred while creating the check-in");
        }
    }

    /// <summary>Update an existing check-in</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CheckInDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCheckInDto updateDto, CancellationToken cancellationToken = default)
    {
        // The service keys off the body's Id, so without this a mismatch silently edits another row.
        if (id != updateDto.Id)
            return BadRequest(new { message = "ID mismatch" });

        try
        {
            var result = await _checkInService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating check-in {Id}", id);
            return StatusCode(500, "An error occurred while updating the check-in");
        }
    }

    /// <summary>Delete a check-in</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Check-in not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting check-in {Id}", id);
            return StatusCode(500, "An error occurred while deleting the check-in");
        }
    }

    /// <summary>Mark a check-in as complete</summary>
    [HttpPost("{checkInId:guid}/complete")]
    [ProducesResponseType(typeof(CheckInDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid checkInId, [FromBody] CompleteCheckInRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.CompleteAsync(checkInId, request.SharedNotes, request.PrivateNotes, request.ActionItems, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "completing the check-in");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while completing the check-in");
        }
    }

    // ── Goal updates ──────────────────────────────────────────────────────

    /// <summary>Add a goal update to a check-in</summary>
    [HttpPost("{checkInId:guid}/goal-updates")]
    [ProducesResponseType(typeof(CheckInGoalUpdateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddGoalUpdate(Guid checkInId, [FromBody] CreateCheckInGoalUpdateDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.AddGoalUpdateAsync(checkInId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding goal update to check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while adding the goal update");
        }
    }

    /// <summary>Get goal updates for a check-in</summary>
    [HttpGet("{checkInId:guid}/goal-updates")]
    [ProducesResponseType(typeof(IEnumerable<CheckInGoalUpdateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGoalUpdates(Guid checkInId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.GetGoalUpdatesAsync(checkInId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goal updates for check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while retrieving goal updates");
        }
    }

    /// <summary>Update a goal update record</summary>
    [HttpPut("{checkInId:guid}/goal-updates/{updateId:guid}")]
    [ProducesResponseType(typeof(CheckInGoalUpdateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGoalUpdate(Guid checkInId, Guid updateId, [FromBody] UpdateCheckInGoalUpdateDto dto, CancellationToken cancellationToken = default)
    {
        if (updateId != dto.Id)
            return BadRequest(new { message = "ID mismatch" });

        try
        {
            var result = await _checkInService.UpdateGoalUpdateAsync(checkInId, dto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating goal update {UpdateId} for check-in {CheckInId}", updateId, checkInId);
            return StatusCode(500, "An error occurred while updating the goal update");
        }
    }

    /// <summary>Delete a goal update from a check-in</summary>
    [HttpDelete("{checkInId:guid}/goal-updates/{updateId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGoalUpdate(Guid checkInId, Guid updateId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.DeleteGoalUpdateAsync(checkInId, updateId, cancellationToken);
            if (!result) return NotFound(new { message = "Goal update not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting goal update {UpdateId} from check-in {CheckInId}", updateId, checkInId);
            return StatusCode(500, "An error occurred while deleting the goal update");
        }
    }

    // ── Attachments ───────────────────────────────────────────────────────

    /// <summary>Add an attachment to a check-in</summary>
    [HttpPost("{checkInId:guid}/attachments")]
    [ProducesResponseType(typeof(AppraisalAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAttachment(Guid checkInId, [FromBody] CreateAppraisalAttachmentDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.AddAttachmentAsync(checkInId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding attachment to check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while adding the attachment");
        }
    }

    /// <summary>Get attachments for a check-in</summary>
    [HttpGet("{checkInId:guid}/attachments")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalAttachmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttachments(Guid checkInId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.GetAttachmentsAsync(checkInId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachments for check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while retrieving attachments");
        }
    }

    /// <summary>Delete an attachment from a check-in</summary>
    [HttpDelete("{checkInId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid checkInId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _checkInService.DeleteAttachmentAsync(checkInId, attachmentId, cancellationToken);
            if (!result) return NotFound(new { message = "Attachment not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attachment {AttachmentId} from check-in {CheckInId}", attachmentId, checkInId);
            return StatusCode(500, "An error occurred while deleting the attachment");
        }
    }
}

/// <summary>Request body for completing a check-in</summary>
public record CompleteCheckInRequest(string? SharedNotes, string? PrivateNotes, string? ActionItems);
