using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Maintenance;

[ApiController]
[Route("api/maintenance/safety-protocols")]
[Authorize]
public class SafetyProtocolsController : ControllerBase
{
    private readonly ISafetyProtocolService _protocolService;
    private readonly ILogger<SafetyProtocolsController> _logger;

    public SafetyProtocolsController(
        ISafetyProtocolService protocolService,
        ILogger<SafetyProtocolsController> logger)
    {
        _protocolService = protocolService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of safety protocols with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SafetyProtocolListDto>>> GetProtocols(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? category = null,
        [FromQuery] string? severity = null,
        [FromQuery] string? regulatoryStandard = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isMandatory = null,
        [FromQuery] string? approvalStatus = null,
        [FromQuery] bool? reviewDue = null)
    {
        try
        {
            if (pageSize > 100)
            {
                pageSize = 100;
            }

            var filter = new SafetyProtocolFilterDto
            {
                Page = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                Category = category,
                Severity = severity,
                RegulatoryStandard = regulatoryStandard,
                IsActive = isActive,
                IsMandatory = isMandatory,
                ApprovalStatus = approvalStatus,
                ReviewDue = reviewDue
            };

            var result = await _protocolService.GetProtocolsPagedAsync(filter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving safety protocols");
            return StatusCode(500, "An error occurred while retrieving safety protocols");
        }
    }

    /// <summary>
    /// Gets all active safety protocols
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<SafetyProtocolDto>>> GetActiveProtocols()
    {
        try
        {
            var result = await _protocolService.GetActiveProtocolsAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active safety protocols");
            return StatusCode(500, "An error occurred while retrieving active safety protocols");
        }
    }

    /// <summary>
    /// Gets protocols due for review
    /// </summary>
    [HttpGet("due-for-review")]
    public async Task<ActionResult<IEnumerable<SafetyProtocolDto>>> GetProtocolsDueForReview()
    {
        try
        {
            var result = await _protocolService.GetProtocolsDueForReviewAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving protocols due for review");
            return StatusCode(500, "An error occurred while retrieving protocols due for review");
        }
    }

    /// <summary>
    /// Gets a specific safety protocol by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SafetyProtocolDto>> GetProtocol(Guid id)
    {
        try
        {
            var protocol = await _protocolService.GetProtocolByIdAsync(id);
            if (protocol == null)
            {
                return NotFound($"Safety protocol with ID {id} not found");
            }

            return Ok(protocol);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving safety protocol {ProtocolId}", id);
            return StatusCode(500, $"An error occurred while retrieving safety protocol {id}");
        }
    }

    /// <summary>
    /// Creates a new safety protocol
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SafetyProtocolDto>> CreateProtocol([FromBody] CreateSafetyProtocolDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var protocol = await _protocolService.CreateProtocolAsync(createDto);
            return CreatedAtAction(nameof(GetProtocol), new { id = protocol.Id }, protocol);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating safety protocol");
            return StatusCode(500, "An error occurred while creating the safety protocol");
        }
    }

    /// <summary>
    /// Updates an existing safety protocol
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SafetyProtocolDto>> UpdateProtocol(Guid id, [FromBody] UpdateSafetyProtocolDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var protocol = await _protocolService.UpdateProtocolAsync(id, updateDto);
            return Ok(protocol);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating safety protocol {ProtocolId}", id);
            return StatusCode(500, "An error occurred while updating the safety protocol");
        }
    }

    /// <summary>
    /// Deletes a safety protocol
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProtocol(Guid id)
    {
        try
        {
            await _protocolService.DeleteProtocolAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting safety protocol {ProtocolId}", id);
            return StatusCode(500, "An error occurred while deleting the safety protocol");
        }
    }

    /// <summary>
    /// Submits protocol for approval
    /// </summary>
    [HttpPut("{id:guid}/submit-for-approval")]
    public async Task<ActionResult<SafetyProtocolDto>> SubmitForApproval(Guid id)
    {
        try
        {
            await _protocolService.SubmitForApprovalAsync(id);
            var protocol = await _protocolService.GetProtocolByIdAsync(id);
            return Ok(protocol);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting protocol for approval {ProtocolId}", id);
            return StatusCode(500, "An error occurred while submitting the protocol for approval");
        }
    }

    /// <summary>
    /// Approves a safety protocol
    /// </summary>
    [HttpPut("{id:guid}/approve")]
    public async Task<ActionResult<SafetyProtocolDto>> ApproveProtocol(Guid id, [FromBody] ApprovalRequestDto approvalRequest)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _protocolService.ApproveProtocolAsync(id, approvalRequest);
            var protocol = await _protocolService.GetProtocolByIdAsync(id);
            return Ok(protocol);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving safety protocol {ProtocolId}", id);
            return StatusCode(500, "An error occurred while approving the safety protocol");
        }
    }

    /// <summary>
    /// Gets safety compliance report
    /// </summary>
    [HttpGet("compliance-report")]
    public async Task<ActionResult<SafetyComplianceReportDto>> GetComplianceReport(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-3);
            var end = endDate ?? DateTime.UtcNow;

            var report = await _protocolService.GetComplianceReportAsync(start, end);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating safety compliance report");
            return StatusCode(500, "An error occurred while generating the safety compliance report");
        }
    }

}
