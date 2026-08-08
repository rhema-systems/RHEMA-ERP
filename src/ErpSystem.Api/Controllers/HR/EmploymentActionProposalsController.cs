using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,HR")]
public class EmploymentActionProposalsController : ControllerBase
{
    private readonly IEmploymentActionProposalService _service;
    private readonly ILogger<EmploymentActionProposalsController> _logger;

    public EmploymentActionProposalsController(IEmploymentActionProposalService service, ILogger<EmploymentActionProposalsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>List employment action proposals (optionally filtered by status)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmploymentActionProposalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] EmploymentActionProposalStatus? status = null, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.GetAllAsync(status, cancellationToken)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing employment action proposals");
            return StatusCode(500, "An error occurred while retrieving proposals");
        }
    }

    /// <summary>Set the status of an employment action proposal (e.g., Approved / Rejected / Actioned)</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(EmploymentActionProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] EmploymentActionProposalStatus status, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _service.SetStatusAsync(id, status, cancellationToken)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employment action proposal {Id}", id);
            return StatusCode(500, "An error occurred while updating the proposal");
        }
    }
}
