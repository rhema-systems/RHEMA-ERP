using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Appraisal conversations — the kick-off, mid-year, quarterly and final review meetings held
/// against an appraisal, and the notes that come out of them.
///
/// <para>Distinct from check-ins: a check-in is an ad-hoc one-to-one inside a cycle, while these
/// are the cycle's own scheduled conversations and hang off the appraisal record.</para>
///
/// <para>Entitlement follows the appraisal: HR, the appraisee, the appraisee's line manager, and
/// whoever scheduled or is holding the conversation. <c>/mine</c> and <c>/my-diary</c> take the
/// employee from the token so no screen has to pass an employee id — passing one is how the
/// earlier version let anybody read anybody's diary.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class AppraisalConversationsController : ControllerBase
{
    private readonly IAppraisalConversationService _conversationService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AppraisalConversationsController> _logger;

    public AppraisalConversationsController(
        IAppraisalConversationService conversationService,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<AppraisalConversationsController> logger)
    {
        _conversationService = conversationService;
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private bool IsHr =>
        User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr);

    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Conversation rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>HR, the appraisee, or the appraisee's line manager.</summary>
    private async Task<bool> CanAccessAppraisalAsync(Guid appraisalId, CancellationToken ct = default)
    {
        if (IsHr) return true;
        if (_currentUserService.EmployeeId is not Guid me) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<PerformanceAppraisal>()
            .AsNoTracking()
            .Where(a => a.Id == appraisalId && a.TenantId == tenantId)
            .AnyAsync(a => a.EmployeeId == me || a.Employee.ManagerId == me, ct);
    }

    /// <summary>As above, plus whoever scheduled or is holding this particular conversation.</summary>
    private async Task<bool> CanAccessConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        if (IsHr) return true;
        if (_currentUserService.EmployeeId is not Guid me) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<AppraisalConversation>()
            .AsNoTracking()
            .Where(c => c.Id == conversationId && c.TenantId == tenantId)
            .AnyAsync(c => c.ScheduledById == me
                        || c.ConductedById == me
                        || c.Appraisal.EmployeeId == me
                        || c.Appraisal.Employee.ManagerId == me, ct);
    }

    /// <summary>Get an appraisal conversation by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessConversationAsync(id, cancellationToken)) return Forbid();

        try
        {
            var result = await _conversationService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal conversation {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the conversation");
        }
    }

    /// <summary>Get conversations for an appraisal</summary>
    [HttpGet("by-appraisal/{appraisalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByAppraisal(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, cancellationToken)) return Forbid();

        try
        {
            var result = await _conversationService.GetByAppraisalIdAsync(appraisalId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversations for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving conversations");
        }
    }

    /// <summary>Get conversations of a specific type for an appraisal</summary>
    [HttpGet("by-appraisal/{appraisalId:guid}/type/{type}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByType(Guid appraisalId, ConversationType type, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, cancellationToken)) return Forbid();

        try
        {
            var result = await _conversationService.GetByTypeAsync(appraisalId, type, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversations of type {Type} for appraisal {AppraisalId}", type, appraisalId);
            return StatusCode(500, "An error occurred while retrieving conversations");
        }
    }

    /// <summary>Conversations about the signed-in employee.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalConversationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken = default)
    {
        if (_currentUserService.EmployeeId is not Guid me)
            return Ok(Array.Empty<AppraisalConversationDto>());

        try
        {
            return Ok(await _conversationService.GetByAppraiseeAsync(me, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving the caller's conversations");
            return StatusCode(500, "An error occurred while retrieving your conversations");
        }
    }

    /// <summary>
    /// The signed-in manager's outstanding conversations — everything they scheduled or are down
    /// to hold and have not yet held, overdue ones included.
    /// </summary>
    [HttpGet("my-diary")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalConversationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyDiary(CancellationToken cancellationToken = default)
    {
        if (_currentUserService.EmployeeId is not Guid me)
            return Ok(Array.Empty<AppraisalConversationDto>());

        try
        {
            return Ok(await _conversationService.GetScheduledByManagerAsync(me, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving the caller's conversation diary");
            return StatusCode(500, "An error occurred while retrieving your scheduled conversations");
        }
    }

    /// <summary>Get scheduled conversations for a manager</summary>
    [HttpGet("scheduled/{managerId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetScheduledByManager(Guid managerId, CancellationToken cancellationToken = default)
    {
        // Another manager's diary is not this caller's to read; use /my-diary for your own.
        if (!IsHr && _currentUserService.EmployeeId != managerId) return Forbid();

        try
        {
            var result = await _conversationService.GetScheduledByManagerAsync(managerId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scheduled conversations for manager {ManagerId}", managerId);
            return StatusCode(500, "An error occurred while retrieving scheduled conversations");
        }
    }

    /// <summary>Create a new appraisal conversation</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppraisalConversationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalConversationDto createDto, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessAppraisalAsync(createDto.AppraisalId, cancellationToken)) return Forbid();

        // Whoever books the conversation is the one holding it unless they say otherwise, and the
        // client has no employee id of its own to send.
        if (_currentUserService.EmployeeId is Guid me)
        {
            if (createDto.ScheduledById is null || createDto.ScheduledById == Guid.Empty)
                createDto.ScheduledById = me;
            if (createDto.ConductedById == Guid.Empty)
                createDto.ConductedById = null;
        }

        try
        {
            var result = await _conversationService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "creating");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appraisal conversation");
            return StatusCode(500, "An error occurred while creating the conversation");
        }
    }

    /// <summary>Update an existing appraisal conversation</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalConversationDto updateDto, CancellationToken cancellationToken = default)
    {
        // The service keys off the body's Id, so a mismatch would silently edit another meeting.
        if (id != updateDto.Id)
            return BadRequest(new { message = "The id in the route does not match the id in the body." });
        if (!await CanAccessConversationAsync(id, cancellationToken)) return Forbid();

        try
        {
            var result = await _conversationService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "updating");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appraisal conversation {Id}", id);
            return StatusCode(500, "An error occurred while updating the conversation");
        }
    }

    /// <summary>Delete an appraisal conversation</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessConversationAsync(id, cancellationToken)) return Forbid();

        try
        {
            var result = await _conversationService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Conversation not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting appraisal conversation {Id}", id);
            return StatusCode(500, "An error occurred while deleting the conversation");
        }
    }

    /// <summary>
    /// Mark a conversation as complete. This is the only way it closes — an update cannot do it,
    /// because completing stamps the held date and tells the employee the notes are up.
    /// </summary>
    [HttpPost("{conversationId:guid}/complete")]
    [ProducesResponseType(typeof(AppraisalConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Complete(Guid conversationId, [FromBody] CompleteConversationRequest request, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessConversationAsync(conversationId, cancellationToken)) return Forbid();

        try
        {
            var result = await _conversationService.CompleteAsync(conversationId, request.PostMeetingNotes, request.KeyTakeaways, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "completing");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing conversation {ConversationId}", conversationId);
            return StatusCode(500, "An error occurred while completing the conversation");
        }
    }
}

/// <summary>Request body for completing a conversation</summary>
public record CompleteConversationRequest(string? PostMeetingNotes, string? KeyTakeaways);
