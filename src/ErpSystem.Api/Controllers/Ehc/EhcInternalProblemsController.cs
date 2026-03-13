using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/problems")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskAgent + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.Manager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin + "," +
    Constants.Roles.Employee)]
public sealed class EhcInternalProblemsController : ControllerBase
{
    private readonly IEhcProblemService _problemService;
    private readonly ILogger<EhcInternalProblemsController> _logger;

    public EhcInternalProblemsController(IEhcProblemService problemService, ILogger<EhcInternalProblemsController> logger)
    {
        _problemService = problemService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? q = null,
        [FromQuery] EhcProblemStatus? status = null,
        [FromQuery] EhcTicketPriority? priority = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] Guid? ownerUserId = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _problemService.GetProblemsAsync(page, pageSize, q, status, priority, departmentId, ownerUserId, createdFrom, createdTo, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing EHC problems");
            return StatusCode(500, new { success = false, message = "Failed to load problems" });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _problemService.GetProblemByIdAsync(id, cancellationToken);
            if (result == null) return NotFound(new { success = false, message = "Problem not found" });
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EHC problem {ProblemId}", id);
            return StatusCode(500, new { success = false, message = "Failed to load problem" });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateEhcProblemRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _problemService.CreateProblemAsync(request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EHC problem");
            return StatusCode(500, new { success = false, message = "Failed to create problem" });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateEhcProblemRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _problemService.UpdateProblemAsync(id, request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Problem not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating EHC problem {ProblemId}", id);
            return StatusCode(500, new { success = false, message = "Failed to update problem" });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _problemService.DeleteProblemAsync(id, cancellationToken);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Problem not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting EHC problem {ProblemId}", id);
            return StatusCode(500, new { success = false, message = "Failed to delete problem" });
        }
    }

    [HttpPost("{id:guid}/tickets")]
    public async Task<ActionResult> LinkTicket(Guid id, [FromBody] LinkEhcTicketToProblemRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await _problemService.LinkTicketAsync(id, request, cancellationToken);
            return Ok(new { success = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking ticket to problem {ProblemId}", id);
            return StatusCode(500, new { success = false, message = "Failed to link ticket" });
        }
    }

    [HttpDelete("{id:guid}/tickets/{ticketId:guid}")]
    public async Task<ActionResult> UnlinkTicket(Guid id, Guid ticketId, CancellationToken cancellationToken)
    {
        try
        {
            await _problemService.UnlinkTicketAsync(id, ticketId, cancellationToken);
            return Ok(new { success = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking ticket from problem {ProblemId}", id);
            return StatusCode(500, new { success = false, message = "Failed to unlink ticket" });
        }
    }

    [HttpPost("{id:guid}/tasks")]
    public async Task<ActionResult> CreateTask(Guid id, [FromBody] CreateEhcCapaTaskRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _problemService.CreateCapaTaskAsync(id, request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating CAPA task for problem {ProblemId}", id);
            return StatusCode(500, new { success = false, message = "Failed to create task" });
        }
    }

    [HttpPut("{id:guid}/tasks/{taskId:guid}")]
    public async Task<ActionResult> UpdateTask(Guid id, Guid taskId, [FromBody] UpdateEhcCapaTaskRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _problemService.UpdateCapaTaskAsync(id, taskId, request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating CAPA task {TaskId} for problem {ProblemId}", taskId, id);
            return StatusCode(500, new { success = false, message = "Failed to update task" });
        }
    }

    [HttpDelete("{id:guid}/tasks/{taskId:guid}")]
    public async Task<ActionResult> DeleteTask(Guid id, Guid taskId, CancellationToken cancellationToken)
    {
        try
        {
            await _problemService.DeleteCapaTaskAsync(id, taskId, cancellationToken);
            return Ok(new { success = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting CAPA task {TaskId} for problem {ProblemId}", taskId, id);
            return StatusCode(500, new { success = false, message = "Failed to delete task" });
        }
    }
}

