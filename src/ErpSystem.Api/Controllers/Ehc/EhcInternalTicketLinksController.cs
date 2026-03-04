using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/internal/tickets/{ticketId:guid}/links")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Roles =
    Constants.Roles.HelpdeskAgent + "," +
    Constants.Roles.HelpdeskSupervisor + "," +
    Constants.Roles.HelpdeskManager + "," +
    Constants.Roles.Manager + "," +
    Constants.Roles.TenantAdmin + "," +
    Constants.Roles.SuperAdmin + "," +
    Constants.Roles.Employee)]
public sealed class EhcInternalTicketLinksController : ControllerBase
{
    private readonly IEhcTicketService _ticketService;
    private readonly ILogger<EhcInternalTicketLinksController> _logger;

    public EhcInternalTicketLinksController(IEhcTicketService ticketService, ILogger<EhcInternalTicketLinksController> logger)
    {
        _ticketService = ticketService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> List(Guid ticketId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.GetTicketLinksAsync(ticketId, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing EHC ticket links for ticket {TicketId}", ticketId);
            return StatusCode(500, new { success = false, message = "Failed to load ticket links" });
        }
    }

    [HttpPost]
    public async Task<ActionResult> Create(Guid ticketId, [FromBody] CreateEhcTicketLinkRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.CreateTicketLinkAsync(ticketId, request, cancellationToken);
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
            _logger.LogError(ex, "Error creating EHC ticket link for ticket {TicketId}", ticketId);
            return StatusCode(500, new { success = false, message = "Failed to create ticket link" });
        }
    }

    [HttpPost("close-duplicates")]
    public async Task<ActionResult> CloseDuplicates(Guid ticketId, [FromBody] CloseEhcDuplicateTicketsRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.CloseDuplicateTicketsAsync(ticketId, request, cancellationToken);
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
            _logger.LogError(ex, "Error closing duplicates for ticket {TicketId}", ticketId);
            return StatusCode(500, new { success = false, message = "Failed to close duplicates" });
        }
    }

    [HttpDelete("{linkId:guid}")]
    public async Task<ActionResult> Delete(Guid ticketId, Guid linkId, CancellationToken cancellationToken)
    {
        try
        {
            await _ticketService.DeleteTicketLinkAsync(ticketId, linkId, cancellationToken);
            return Ok(new { success = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting EHC ticket link {LinkId} for ticket {TicketId}", linkId, ticketId);
            return StatusCode(500, new { success = false, message = "Failed to delete ticket link" });
        }
    }
}
