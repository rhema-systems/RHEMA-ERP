using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[Authorize]
[ApiController]
[Route("api/procurement/[controller]")]
public class TenderAwardsController : ControllerBase
{
    private readonly ITenderAwardService _awardService;
    private readonly ILogger<TenderAwardsController> _logger;

    public TenderAwardsController(
        ITenderAwardService awardService,
        ILogger<TenderAwardsController> logger)
    {
        _awardService = awardService;
        _logger = logger;
    }

    /// <summary>
    /// Get all awards
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<IEnumerable<TenderAwardDto>>> GetAwards()
    {
        try
        {
            // Service interface requires (int page, int pageSize, string? search, string? status)
            var awards = await _awardService.GetAwardsAsync(1, 100, null, null);
            return Ok(awards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting awards");
            return StatusCode(500, "An error occurred while retrieving awards");
        }
    }

    /// <summary>
    /// Get award by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderAwardDto>> GetAward(Guid id)
    {
        try
        {
            var award = await _awardService.GetAwardByIdAsync(id);
            if (award == null)
            {
                return NotFound($"Award with ID {id} not found");
            }

            return Ok(award);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting award {AwardId}", id);
            return StatusCode(500, "An error occurred while retrieving the award");
        }
    }

    /// <summary>
    /// Get award by tender ID
    /// </summary>
    [HttpGet("by-tender/{tenderId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderAwardDto>> GetAwardByTender(Guid tenderId)
    {
        try
        {
            var award = await _awardService.GetAwardByTenderIdAsync(tenderId);
            if (award == null)
            {
                return NotFound($"Award for tender {tenderId} not found");
            }

            return Ok(award);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting award for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while retrieving the award");
        }
    }

    /// <summary>
    /// Get award by bid ID (for external portal - suppliers can view their own award)
    /// </summary>
    [HttpGet("by-bid/{bidId}")]
    public async Task<ActionResult<TenderAwardDto>> GetAwardByBid(Guid bidId)
    {
        try
        {
            var award = await _awardService.GetAwardByBidIdAsync(bidId);
            if (award == null)
            {
                return NotFound($"Award for bid {bidId} not found");
            }

            return Ok(award);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting award for bid {BidId}", bidId);
            return StatusCode(500, "An error occurred while retrieving the award");
        }
    }

    /// <summary>
    /// Generate award recommendation
    /// </summary>
    [HttpGet("recommendation/{tenderId}")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<AwardRecommendationDto>> GenerateAwardRecommendation(Guid tenderId)
    {
        try
        {
            var recommendation = await _awardService.GenerateAwardRecommendationAsync(tenderId);
            return Ok(recommendation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating award recommendation for tender {TenderId}", tenderId);
            return StatusCode(500, "An error occurred while generating the award recommendation");
        }
    }

    /// <summary>
    /// Create award
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderAwardDto>> CreateAward([FromBody] CreateAwardDto dto)
    {
        try
        {
            // Service interface requires (Guid tenderId, CreateAwardDto dto)
            var award = await _awardService.CreateAwardAsync(dto.TenderId, dto);
            return CreatedAtAction(nameof(GetAward), new { id = award.Id }, award);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating award");
            return StatusCode(500, "An error occurred while creating the award");
        }
    }

    /// <summary>
    /// Approve award
    /// </summary>
    [HttpPost("{id}/approve")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderAwardDto>> ApproveAward(Guid id, [FromBody] ApproveAwardDto dto)
    {
        try
        {
            // TODO: Service interface doesn't have ApproveAwardAsync method
            // Commenting out until the method is implemented
            // var award = await _awardService.ApproveAwardAsync(id, dto);
            // return Ok(award);
            return StatusCode(501, "Approve award functionality not yet implemented");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving award {AwardId}", id);
            return StatusCode(500, "An error occurred while approving the award");
        }
    }

    /// <summary>
    /// Reject award
    /// </summary>
    [HttpPost("{id}/reject")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderAwardDto>> RejectAward(Guid id, [FromBody] RejectAwardDto dto)
    {
        try
        {
            // TODO: Service interface doesn't have RejectAwardAsync method
            // Commenting out until the method is implemented
            // var award = await _awardService.RejectAwardAsync(id, dto);
            // return Ok(award);
            return StatusCode(501, "Reject award functionality not yet implemented");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting award {AwardId}", id);
            return StatusCode(500, "An error occurred while rejecting the award");
        }
    }

    /// <summary>
    /// Cancel award
    /// </summary>
    [HttpPost("{id}/cancel")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<TenderAwardDto>> CancelAward(Guid id, [FromBody] CancelAwardDto dto)
    {
        try
        {
            // Service method returns Task (void), not Task<TenderAwardDto>
            await _awardService.CancelAwardAsync(id, dto);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling award {AwardId}", id);
            return StatusCode(500, "An error occurred while cancelling the award");
        }
    }

    /// <summary>
    /// Generate award notification
    /// </summary>
    [HttpGet("{id}/notification")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<AwardNotificationDto>> GenerateAwardNotification(Guid id)
    {
        try
        {
            // TODO: Service interface doesn't have GenerateAwardNotificationAsync method
            // Commenting out until the method is implemented
            // var notification = await _awardService.GenerateAwardNotificationAsync(id);
            // return Ok(notification);
            return StatusCode(501, "Generate award notification functionality not yet implemented");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating award notification for award {AwardId}", id);
            return StatusCode(500, "An error occurred while generating the award notification");
        }
    }

    /// <summary>
    /// Send award notifications to supplier (email with PDF + in-app notification)
    /// </summary>
    [HttpPost("{id}/notify")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult> SendAwardNotification(Guid id, [FromBody] AwardNotificationDto dto)
    {
        try
        {
            // Get the award to find the tender ID
            var award = await _awardService.GetAwardByIdAsync(id);
            if (award == null)
            {
                return NotFound($"Award with ID {id} not found");
            }

            await _awardService.SendAwardNotificationsAsync(award.TenderId, dto);

            _logger.LogInformation("Sent award notifications for award {AwardId}", id);
            return Ok(new { message = "Award notifications sent successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending award notifications for award {AwardId}", id);
            return StatusCode(500, "An error occurred while sending award notifications");
        }
    }

    /// <summary>
    /// Create a purchase order from a tender award
    /// </summary>
    [HttpPost("create-purchase-order")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin,Manager")]
    public async Task<ActionResult<PurchaseOrderFromAwardResponseDto>> CreatePurchaseOrderFromAward([FromBody] CreatePurchaseOrderFromAwardDto dto)
    {
        try
        {
            var result = await _awardService.CreatePurchaseOrderFromAwardAsync(dto);
            _logger.LogInformation("Created purchase order {OrderNumber} from tender award {AwardId}",
                result.OrderNumber, dto.TenderAwardId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating purchase order from tender award {AwardId}", dto.TenderAwardId);
            return StatusCode(500, "An error occurred while creating the purchase order");
        }
    }
}
