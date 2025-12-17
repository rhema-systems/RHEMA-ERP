using System.Security.Claims;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/partner-blacklist")]
[Authorize]
public class PartnerBlacklistController : ControllerBase
{
    private readonly IPartnerBlacklistService _blacklistService;
    private readonly IBlacklistAppealService _appealService;
    private readonly IBlacklistHistoryService _historyService;
    private readonly ILogger<PartnerBlacklistController> _logger;

    public PartnerBlacklistController(
        IPartnerBlacklistService blacklistService,
        IBlacklistAppealService appealService,
        IBlacklistHistoryService historyService,
        ILogger<PartnerBlacklistController> logger)
    {
        _blacklistService = blacklistService;
        _appealService = appealService;
        _historyService = historyService;
        _logger = logger;
    }

    /// <summary>
    /// Blacklists a partner
    /// </summary>
    [HttpPost("partners/{partnerId:guid}/blacklist")]
    public async Task<IActionResult> BlacklistPartner(Guid partnerId, [FromBody] BlacklistPartnerRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _blacklistService.AddToBlacklistAsync(partnerId, request.Reason, userId, request.BlacklistUntil);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blacklisting partner {PartnerId}", partnerId);
            return StatusCode(500, "An error occurred while blacklisting the partner");
        }
    }

    /// <summary>
    /// Removes a partner from blacklist
    /// </summary>
    [HttpPost("partners/{partnerId:guid}/remove-from-blacklist")]
    public async Task<IActionResult> RemoveFromBlacklist(Guid partnerId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _blacklistService.RemoveFromBlacklistAsync(partnerId, userId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing partner {PartnerId} from blacklist", partnerId);
            return StatusCode(500, "An error occurred while removing the partner from blacklist");
        }
    }

    /// <summary>
    /// Checks if a partner is blacklisted
    /// </summary>
    [HttpGet("partners/{partnerId:guid}/is-blacklisted")]
    public async Task<ActionResult<bool>> IsPartnerBlacklisted(Guid partnerId)
    {
        try
        {
            var isBlacklisted = await _blacklistService.IsBlacklistedAsync(partnerId);
            return Ok(isBlacklisted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking blacklist status for partner {PartnerId}", partnerId);
            return StatusCode(500, "An error occurred while checking blacklist status");
        }
    }

    /// <summary>
    /// Gets all blacklisted partners
    /// </summary>
    [HttpGet("blacklisted-partners")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerDto>>> GetBlacklistedPartners()
    {
        try
        {
            var partners = await _blacklistService.GetBlacklistedPartnersAsync();
            return Ok(partners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blacklisted partners");
            return StatusCode(500, "An error occurred while getting blacklisted partners");
        }
    }

    /// <summary>
    /// Gets partners with expiring blacklists
    /// </summary>
    [HttpGet("expiring-blacklists")]
    public async Task<ActionResult<IEnumerable<BusinessPartnerDto>>> GetExpiringBlacklists([FromQuery] int daysAhead = 30)
    {
        try
        {
            var partners = await _blacklistService.GetPartnersWithExpiringBlacklistAsync(daysAhead);
            return Ok(partners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring blacklists");
            return StatusCode(500, "An error occurred while getting expiring blacklists");
        }
    }

    // ============================================================================
    // BLACKLIST APPEALS
    // ============================================================================

    /// <summary>
    /// Creates a blacklist appeal
    /// </summary>
    [HttpPost("partners/{partnerId:guid}/appeal")]
    public async Task<ActionResult<BlacklistAppealDto>> CreateAppeal(Guid partnerId, [FromBody] CreateBlacklistAppealDto createDto)
    {
        try
        {
            createDto.BusinessPartnerId = partnerId;
            var appeal = await _appealService.CreateAppealAsync(createDto);
            return CreatedAtAction(nameof(GetAppealById), new { id = appeal.Id }, appeal);
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
            _logger.LogError(ex, "Error creating blacklist appeal for partner {PartnerId}", partnerId);
            return StatusCode(500, "An error occurred while creating the blacklist appeal");
        }
    }

    /// <summary>
    /// Gets a blacklist appeal by ID
    /// </summary>
    [HttpGet("appeals/{id:guid}")]
    public async Task<ActionResult<BlacklistAppealDto>> GetAppealById(Guid id)
    {
        try
        {
            var appeal = await _appealService.GetByIdAsync(id);
            if (appeal == null)
            {
                return NotFound();
            }
            return Ok(appeal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blacklist appeal {AppealId}", id);
            return StatusCode(500, "An error occurred while getting the blacklist appeal");
        }
    }

    /// <summary>
    /// Gets all appeals for a business partner
    /// </summary>
    [HttpGet("partners/{partnerId:guid}/appeals")]
    public async Task<ActionResult<IEnumerable<BlacklistAppealDto>>> GetAppealsByPartner(Guid partnerId)
    {
        try
        {
            var appeals = await _appealService.GetByBusinessPartnerAsync(partnerId);
            return Ok(appeals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting appeals for partner {PartnerId}", partnerId);
            return StatusCode(500, "An error occurred while getting the appeals");
        }
    }

    /// <summary>
    /// Gets all pending appeals
    /// </summary>
    [HttpGet("appeals/pending")]
    public async Task<ActionResult<IEnumerable<BlacklistAppealDto>>> GetPendingAppeals()
    {
        try
        {
            var appeals = await _appealService.GetPendingAppealsAsync();
            return Ok(appeals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending appeals");
            return StatusCode(500, "An error occurred while getting pending appeals");
        }
    }

    /// <summary>
    /// Reviews a blacklist appeal
    /// </summary>
    [HttpPost("appeals/{id:guid}/review")]
    public async Task<ActionResult<BlacklistAppealDto>> ReviewAppeal(Guid id, [FromBody] ReviewBlacklistAppealDto reviewDto)
    {
        try
        {
            var appeal = await _appealService.ReviewAppealAsync(id, reviewDto);
            return Ok(appeal);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing blacklist appeal {AppealId}", id);
            return StatusCode(500, "An error occurred while reviewing the appeal");
        }
    }

    /// <summary>
    /// Approves a blacklist appeal
    /// </summary>
    [HttpPost("appeals/{id:guid}/approve")]
    public async Task<ActionResult<BlacklistAppealDto>> ApproveAppeal(Guid id, [FromBody] ApproveBlacklistAppealDto approveDto)
    {
        try
        {
            var appeal = await _appealService.ApproveAppealAsync(id, approveDto);
            return Ok(appeal);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving blacklist appeal {AppealId}", id);
            return StatusCode(500, "An error occurred while approving the appeal");
        }
    }

    /// <summary>
    /// Rejects a blacklist appeal
    /// </summary>
    [HttpPost("appeals/{id:guid}/reject")]
    public async Task<ActionResult<BlacklistAppealDto>> RejectAppeal(Guid id, [FromBody] RejectBlacklistAppealDto rejectDto)
    {
        try
        {
            var appeal = await _appealService.RejectAppealAsync(id, rejectDto);
            return Ok(appeal);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting blacklist appeal {AppealId}", id);
            return StatusCode(500, "An error occurred while rejecting the appeal");
        }
    }

    // ============================================================================
    // BLACKLIST HISTORY
    // ============================================================================

    /// <summary>
    /// Gets blacklist history for a business partner
    /// </summary>
    [HttpGet("partners/{partnerId:guid}/history")]
    public async Task<ActionResult<IEnumerable<BlacklistHistoryDto>>> GetPartnerHistory(Guid partnerId)
    {
        try
        {
            var history = await _historyService.GetByBusinessPartnerAsync(partnerId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blacklist history for partner {PartnerId}", partnerId);
            return StatusCode(500, "An error occurred while getting the blacklist history");
        }
    }
}

// Request models
public record BlacklistPartnerRequest(string Reason, DateTime? BlacklistUntil);

