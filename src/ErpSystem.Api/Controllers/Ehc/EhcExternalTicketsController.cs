using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.Ehc;

[ApiController]
[Route("api/ehc/external/tickets")]
[Authorize(Policy = "ExternalOnly")]
[EnableRateLimiting("ApiPolicy")]
public sealed class EhcExternalTicketsController : ControllerBase
{
    private readonly IEhcTicketService _ticketService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICaptchaVerificationService _captchaVerificationService;
    private readonly ILogger<EhcExternalTicketsController> _logger;

    public EhcExternalTicketsController(
        IEhcTicketService ticketService,
        ICurrentUserService currentUserService,
        ICaptchaVerificationService captchaVerificationService,
        ILogger<EhcExternalTicketsController> logger)
    {
        _ticketService = ticketService;
        _currentUserService = currentUserService;
        _captchaVerificationService = captchaVerificationService;
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<ActionResult<EhcTicketDetailDto>> Create([FromBody] CreateEhcTicketRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "Tenant context is required." });
            }

            var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            var host = !string.IsNullOrWhiteSpace(forwardedHost) ? forwardedHost : Request.Host.Host;
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            await _captchaVerificationService.EnsureCaptchaValidAsync(tenantId, request.CaptchaToken, host, remoteIp, cancellationToken);

            var result = await _ticketService.CreateExternalTicketAsync(request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (CaptchaVerificationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating external EHC ticket");
            return StatusCode(500, new { success = false, message = "Failed to create ticket" });
        }
    }

    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? q = null,
        [FromQuery] EhcTicketStatus? status = null,
        [FromQuery] EhcTicketType? ticketType = null,
        [FromQuery] EhcTicketPriority? priority = null,
        [FromQuery] EhcTicketSource? source = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _ticketService.GetMyTicketsAsync(
                page,
                pageSize,
                q: q,
                status: status,
                ticketType: ticketType,
                priority: priority,
                source: source,
                categoryId: categoryId,
                createdFrom: createdFrom,
                createdTo: createdTo,
                cancellationToken: cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing external EHC tickets");
            return StatusCode(500, new { success = false, message = "Failed to load tickets" });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.GetMyTicketByIdAsync(id, cancellationToken);
            if (result == null)
            {
                return NotFound(new { success = false, message = "Ticket not found" });
            }

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving external EHC ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to load ticket" });
        }
    }

    [HttpGet("{id:guid}/links")]
    public async Task<ActionResult> GetLinks(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var links = await _ticketService.GetMyTicketLinksAsync(id, cancellationToken);
            return Ok(new { success = true, data = links });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving external EHC ticket links {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to load ticket links" });
        }
    }

    [HttpPost("{id:guid}/feedback")]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<ActionResult> SubmitFeedback(Guid id, [FromBody] SubmitEhcTicketFeedbackRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            request ??= new SubmitEhcTicketFeedbackRequestDto();
            var result = await _ticketService.SubmitMyTicketFeedbackAsync(id, request, cancellationToken);
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
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting feedback for EHC ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to submit feedback" });
        }
    }

    [HttpPost("{id:guid}/messages")]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<ActionResult> AddMessage(Guid id, [FromBody] AddEhcTicketMessageRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.AddExternalMessageAsync(id, request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding external message to ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to add message" });
        }
    }

    [HttpPost("{id:guid}/attachments")]
    [EnableRateLimiting("SensitivePolicy")]
    public async Task<ActionResult> AddAttachment(Guid id, [FromBody] AddEhcTicketAttachmentRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ticketService.AddExternalAttachmentAsync(id, request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Ticket not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding external attachment to ticket {TicketId}", id);
            return StatusCode(500, new { success = false, message = "Failed to add attachment" });
        }
    }
}
