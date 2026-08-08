using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PeerNominationController : ControllerBase
{
    private readonly IPeerNominationService _nominationService;
    private readonly ILogger<PeerNominationController> _logger;

    public PeerNominationController(IPeerNominationService nominationService, ILogger<PeerNominationController> logger)
    {
        _nominationService = nominationService;
        _logger = logger;
    }

    /// <summary>
    /// Nomination rules — "already nominated", "maximum of N peer evaluators", "cannot nominate
    /// the appraisee as their own peer", "nominations are locked" — are answered with 422 and
    /// the rule's own message, matching the rest of the appraisal run, so a client can read
    /// <c>.message</c> off a consistent body shape.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex)
    {
        _logger.LogWarning(ex, "Peer nomination rule rejected");
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>
    /// Get peer nomination by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PeerNominationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _nominationService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving peer nomination with {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the peer nomination");
        }
    }

    /// <summary>
    /// Get peer nominations by appraisal ID
    /// </summary>
    [HttpGet("appraisal/{appraisalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PeerNominationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByAppraisalId(Guid appraisalId)
    {
        try
        {
            var response = await _nominationService.GetByAppraisalIdAsync(appraisalId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving peer nominations for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving peer nominations");
        }
    }

    /// <summary>
    /// Get peer nominations by peer employee ID
    /// </summary>
    [HttpGet("peer/{peerEmployeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PeerNominationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPeerEmployeeId(Guid peerEmployeeId)
    {
        try
        {
            var response = await _nominationService.GetByPeerEmployeeIdAsync(peerEmployeeId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving peer nominations for peer employee {PeerEmployeeId}", peerEmployeeId);
            return StatusCode(500, "An error occurred while retrieving peer nominations");
        }
    }

    /// <summary>
    /// Get pending peer nominations for an employee
    /// </summary>
    [HttpGet("pending/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PeerNominationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingNominations(Guid employeeId)
    {
        try
        {
            var response = await _nominationService.GetPendingNominationsAsync(employeeId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending peer nominations for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving pending peer nominations");
        }
    }

    /// <summary>
    /// Create a new peer nomination
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PeerNominationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePeerNominationDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _nominationService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating peer nomination");
            return StatusCode(500, "An error occurred while creating the peer nomination");
        }
    }

    /// <summary>
    /// Update an existing peer nomination
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PeerNominationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePeerNominationDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _nominationService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating peer nomination with Id {NominationId}", id);
            return StatusCode(500, "An error occurred while updating the peer nomination");
        }
    }

    /// <summary>
    /// Send peer evaluation invitation
    /// </summary>
    [HttpPost("{id:guid}/send-invitation")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendInvitation(Guid id)
    {
        try
        {
            var invitationDto = new SendPeerEvaluationInvitationDto { PeerNominationId = id };
            var response = await _nominationService.SendInvitationAsync(invitationDto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending invitation for peer nomination {NominationId}", id);
            return StatusCode(500, "An error occurred while sending the invitation");
        }
    }

    /// <summary>
    /// Delete a peer nomination
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _nominationService.DeleteAsync(id);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting peer nomination with Id {NominationId}", id);
            return StatusCode(500, "An error occurred while deleting the peer nomination");
        }
    }
}
