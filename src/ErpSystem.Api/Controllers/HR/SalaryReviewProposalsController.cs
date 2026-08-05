using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,HR")]
public class SalaryReviewProposalsController : ControllerBase
{
    private readonly ISalaryReviewProposalService _service;
    private readonly ILogger<SalaryReviewProposalsController> _logger;

    public SalaryReviewProposalsController(ISalaryReviewProposalService service, ILogger<SalaryReviewProposalsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>List salary review proposals (optionally filtered by status)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SalaryReviewProposalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] SalaryReviewProposalStatus? status = null, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.GetAllAsync(status, cancellationToken)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing salary review proposals");
            return StatusCode(500, "An error occurred while retrieving proposals");
        }
    }

    /// <summary>Set the status of a salary review proposal (e.g., Approved / Rejected / Applied)</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(SalaryReviewProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SalaryReviewProposalStatus status, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.SetStatusAsync(id, status, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating salary review proposal {Id}", id);
            return StatusCode(500, "An error occurred while updating the proposal");
        }
    }
}
