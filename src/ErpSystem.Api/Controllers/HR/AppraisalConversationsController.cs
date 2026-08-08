using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppraisalConversationsController : ControllerBase
{
    private readonly IAppraisalConversationService _conversationService;
    private readonly ILogger<AppraisalConversationsController> _logger;

    public AppraisalConversationsController(IAppraisalConversationService conversationService, ILogger<AppraisalConversationsController> logger)
    {
        _conversationService = conversationService;
        _logger = logger;
    }

    /// <summary>Get an appraisal conversation by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
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
    public async Task<IActionResult> GetByAppraisal(Guid appraisalId, CancellationToken cancellationToken = default)
    {
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
    public async Task<IActionResult> GetByType(Guid appraisalId, ConversationType type, CancellationToken cancellationToken = default)
    {
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

    /// <summary>Get scheduled conversations for a manager</summary>
    [HttpGet("scheduled/{managerId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalConversationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScheduledByManager(Guid managerId, CancellationToken cancellationToken = default)
    {
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
    public async Task<IActionResult> Create([FromBody] CreateAppraisalConversationDto createDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _conversationService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalConversationDto updateDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _conversationService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
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

    /// <summary>Mark a conversation as complete</summary>
    [HttpPost("{conversationId:guid}/complete")]
    [ProducesResponseType(typeof(AppraisalConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(Guid conversationId, [FromBody] CompleteConversationRequest request, CancellationToken cancellationToken = default)
    {
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
            return BadRequest(new { message = ex.Message });
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
