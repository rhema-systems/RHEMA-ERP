using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/tender-assignments")]
[Authorize]
public class TenderAssignmentsController : ControllerBase
{
    private readonly ITenderAssignmentService _assignmentService;
    private readonly ILogger<TenderAssignmentsController> _logger;

    public TenderAssignmentsController(
        ITenderAssignmentService assignmentService,
        ILogger<TenderAssignmentsController> logger)
    {
        _assignmentService = assignmentService;
        _logger = logger;
    }

    /// <summary>
    /// Get all assignments for a tender
    /// </summary>
    [HttpGet("tender/{tenderId}")]
    public async Task<ActionResult<IEnumerable<TenderAssignmentDto>>> GetByTenderId(Guid tenderId)
    {
        try
        {
            var assignments = await _assignmentService.GetByTenderIdAsync(tenderId);
            return Ok(assignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assignments for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while retrieving assignments");
        }
    }

    /// <summary>
    /// Get all tenders assigned to a user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<TenderAssignmentDto>>> GetByUserId(Guid userId)
    {
        try
        {
            var assignments = await _assignmentService.GetByUserIdAsync(userId);
            return Ok(assignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assignments for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving assignments");
        }
    }

    /// <summary>
    /// Get all tenders assigned to a business partner
    /// </summary>
    [HttpGet("business-partner/{businessPartnerId}")]
    public async Task<ActionResult<IEnumerable<TenderAssignmentDto>>> GetByBusinessPartnerId(Guid businessPartnerId)
    {
        try
        {
            var assignments = await _assignmentService.GetByBusinessPartnerIdAsync(businessPartnerId);
            return Ok(assignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assignments for business partner {BusinessPartnerId}", businessPartnerId);
            return StatusCode(500, "An error occurred while retrieving assignments");
        }
    }

    /// <summary>
    /// Create a new tender assignment
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TenderAssignmentDto>> Create([FromBody] CreateTenderAssignmentDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var assignment = await _assignmentService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetByTenderId), new { tenderId = assignment.TenderId }, assignment);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation while creating tender assignment");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tender assignment");
            return StatusCode(500, "An error occurred while creating the assignment");
        }
    }

    /// <summary>
    /// Delete a tender assignment
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _assignmentService.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender assignment {Id}", id);
            return StatusCode(500, "An error occurred while deleting the assignment");
        }
    }

    /// <summary>
    /// Check if a user has access to a tender
    /// </summary>
    [HttpGet("has-access")]
    public async Task<ActionResult<bool>> HasAccess([FromQuery] Guid userId, [FromQuery] Guid tenderId)
    {
        try
        {
            var hasAccess = await _assignmentService.HasAccessAsync(userId, tenderId);
            return Ok(hasAccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking access for user {UserId} to tender {TenderId}", userId, tenderId);
            return StatusCode(500, "An error occurred while checking access");
        }
    }

    /// <summary>
    /// Get accessible tender IDs for a user
    /// </summary>
    [HttpGet("accessible-tenders")]
    public async Task<ActionResult<IEnumerable<Guid>>> GetAccessibleTenders([FromQuery] Guid userId, [FromQuery] Guid businessPartnerId)
    {
        try
        {
            var tenderIds = await _assignmentService.GetAccessibleTenderIdsAsync(userId, businessPartnerId);
            return Ok(tenderIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting accessible tenders for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving accessible tenders");
        }
    }
}

