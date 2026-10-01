using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/tickets")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskAgent + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.Manager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin + "," +
    Constants.Roles.Employee)]
public sealed class EhcInternalTicketsController : ControllerBase
{
    private readonly IEhcTicketService _ticketService;
    private readonly IEhcProblemService _problemService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EhcInternalTicketsController> _logger;

    public EhcInternalTicketsController(
        IEhcTicketService ticketService,
        IEhcProblemService problemService,
        ICurrentUserService currentUserService,
        ILogger<EhcInternalTicketsController> logger)
    {
        _ticketService = ticketService;
        _problemService = problemService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] EhcTicketStatus? status = null,
        [FromQuery] EhcTicketType? ticketType = null,
        [FromQuery] EhcTicketPriority? priority = null,
        [FromQuery] EhcTicketSource? source = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? assignedDepartmentId = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _ticketService.GetTicketsAsync(
                page,
                pageSize,
                status,
                ticketType,
                priority,
                source,
                categoryId,
                assignedDepartmentId,
                createdFrom,
                createdTo,
                cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing internal EHC tickets");
            return StatusCode(500, new { success = false, message = "Failed to load tickets" });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateEhcTicketRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.CreateInternalTicketAsync(request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating internal EHC ticket");
            return StatusCode(500, new { success = false, message = "Failed to create ticket" });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.GetTicketByIdAsync(id, cancellationToken);
            if (result == null)
            {
                return NotFound(new { success = false, message = "Ticket not found" });
            }

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving internal EHC ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to load ticket" });
        }
    }

    [HttpGet("{id:guid}/allowed-transitions")]
    public async Task<ActionResult> AllowedTransitions(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.GetAllowedTransitionsAsync(id, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving allowed transitions for EHC ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to load allowed transitions" });
        }
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult> Assign(Guid id, [FromQuery] Guid assignedToUserId, [FromQuery] Guid? assignedOrganizationUnitId, CancellationToken cancellationToken)
    {
        try
        {
            await _ticketService.AssignTicketAsync(id, assignedToUserId, assignedOrganizationUnitId, cancellationToken);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning internal EHC ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to assign ticket" });
        }
    }

    [HttpPost("{id:guid}/route")]
    public async Task<ActionResult> RouteToDepartment(Guid id, [FromQuery] Guid assignedOrganizationUnitId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUserService.UserId, out var actorUserId) || actorUserId == Guid.Empty)
        {
            return Unauthorized(new { success = false, message = "Authenticated user context is required" });
        }

        if (assignedOrganizationUnitId == Guid.Empty)
        {
            return BadRequest(new { success = false, message = "Destination department is required" });
        }

        try
        {
            // This route deliberately derives the assigning/owning user from the authenticated
            // request. The browser supplies only the destination organization unit.
            await _ticketService.AssignTicketAsync(id, actorUserId, assignedOrganizationUnitId, cancellationToken);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing internal EHC ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to route ticket" });
        }
    }

    [HttpPost("{id:guid}/transition")]
    public async Task<ActionResult> Transition(
        Guid id,
        [FromQuery] EhcTicketStatus targetStatus,
        [FromQuery] Guid? workflowTransitionId,
        [FromQuery] string? workflowTransitionName,
        [FromBody] string? notes,
        CancellationToken cancellationToken)
    {
        try
        {
            await _ticketService.TransitionTicketAsync(id, targetStatus, notes, workflowTransitionId, workflowTransitionName, cancellationToken);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transitioning internal EHC ticket {TicketId} to {TargetStatus}", id, targetStatus);
            return StatusCode(500, new { success = false, message = "Failed to transition ticket" });
        }
    }

    [HttpPut("{id:guid}/rca")]
    public async Task<ActionResult> UpdateRca(Guid id, [FromBody] UpdateEhcTicketRcaRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.UpdateTicketRcaAsync(id, request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating RCA details for EHC ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to update RCA" });
        }
    }

    [HttpPost("{id:guid}/convert-to-problem")]
    public async Task<ActionResult> ConvertToProblem(Guid id, [FromBody] ConvertEhcTicketToProblemRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _problemService.ConvertTicketToProblemAsync(id, request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting ticket {TicketId} to problem", id);
            return StatusCode(500, new { success = false, message = "Failed to convert ticket to problem" });
        }
    }

    [HttpGet("{id:guid}/problems")]
    public async Task<ActionResult> TicketProblems(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _problemService.GetTicketProblemsAsync(id, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving problems for ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to load ticket problems" });
        }
    }

    [HttpPost("{id:guid}/internal-comments")]
    public async Task<ActionResult> AddInternalComment(Guid id, [FromBody] AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.AddInternalCommentAsync(id, request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding internal comment to EHC ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to add comment" });
        }
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult> AddAgentMessage(Guid id, [FromBody] AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.AddAgentMessageAsync(id, request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding agent message to EHC ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to add message" });
        }
    }

    [HttpPost("{id:guid}/attachments")]
    public async Task<ActionResult> AddInternalAttachment(Guid id, [FromBody] AddEhcTicketAttachmentRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.AddInternalAttachmentAsync(id, request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding attachment to EHC ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to add attachment" });
        }
    }
}
