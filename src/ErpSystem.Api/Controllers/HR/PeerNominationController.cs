using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class PeerNominationController : ControllerBase
{
    private readonly IPeerNominationService _nominationService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PeerNominationController> _logger;

    public PeerNominationController(
        IPeerNominationService nominationService,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<PeerNominationController> logger)
    {
        _nominationService = nominationService;
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    // ── W3 entitlement ────────────────────────────────────────────────────
    //
    // A nomination names who will score whom. The parties — the peer, the appraisee and the
    // appraisee's line manager — may see and (pre-approval) act on it; everyone else needs the
    // performance permission. The service validates the nomination rules but never asked who
    // was calling, so any authenticated user could nominate, reassign or withdraw anyone's peers.

    private async Task<bool> HoldsPolicyAsync(string policy)
    {
        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>The nomination's peer, its appraisee, the appraisee's manager, or a policy holder.</summary>
    private async Task<bool> CanAccessNominationAsync(Guid nominationId, string policy)
    {
        if (await HoldsPolicyAsync(policy)) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<PeerNomination>()
            .AsNoTracking()
            .Where(n => n.Id == nominationId && n.TenantId == tenantId)
            .AnyAsync(n => n.PeerEmployeeId == me
                        || n.Appraisal.EmployeeId == me
                        || n.Appraisal.Employee.ManagerId == me,
                HttpContext.RequestAborted);
    }

    /// <summary>The appraisal's subject, their line manager, or a policy holder.</summary>
    private async Task<bool> CanAccessAppraisalAsync(Guid appraisalId, string policy)
    {
        if (await HoldsPolicyAsync(policy)) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<PerformanceAppraisal>()
            .AsNoTracking()
            .Where(a => a.Id == appraisalId && a.TenantId == tenantId)
            .AnyAsync(a => a.EmployeeId == me || a.Employee.ManagerId == me,
                HttpContext.RequestAborted);
    }

    /// <summary>The named employee is the caller, or the caller holds the policy.</summary>
    private async Task<bool> SelfOrPolicyAsync(Guid employeeId, string policy)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == employeeId) return true;
        return await HoldsPolicyAsync(policy);
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
        if (!await CanAccessNominationAsync(id, HrPermissions.PerformanceReadPolicy)) return Forbid();

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
        if (!await CanAccessAppraisalAsync(appraisalId, HrPermissions.PerformanceReadPolicy)) return Forbid();

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
        if (!await SelfOrPolicyAsync(peerEmployeeId, HrPermissions.PerformanceReadPolicy)) return Forbid();

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
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.PerformanceReadPolicy)) return Forbid();

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
        if (!await CanAccessAppraisalAsync(createDto.AppraisalId, HrPermissions.PerformanceWritePolicy)) return Forbid();

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
        if (!await CanAccessNominationAsync(id, HrPermissions.PerformanceWritePolicy)) return Forbid();

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
        if (!await CanAccessNominationAsync(id, HrPermissions.PerformanceWritePolicy)) return Forbid();

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
        // Withdrawing a nomination pre-approval is the parties' act; the service guards status.
        if (!await CanAccessNominationAsync(id, HrPermissions.PerformanceWritePolicy)) return Forbid();

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
