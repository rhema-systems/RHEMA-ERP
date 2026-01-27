using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/performance-reviews")]
[Authorize]
public class PerformanceReviewsController : ControllerBase
{
    private readonly IPerformanceReviewService _reviewService;
    private readonly ILogger<PerformanceReviewsController> _logger;

    public PerformanceReviewsController(
        IPerformanceReviewService reviewService,
        ILogger<PerformanceReviewsController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PerformanceReviewDto>> GetById(Guid id)
    {
        try
        {
            var review = await _reviewService.GetByIdAsync(id);
            if (review == null)
            {
                return NotFound($"Performance review with ID {id} not found");
            }
            return Ok(review);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance review {ReviewId}", id);
            return StatusCode(500, "An error occurred while retrieving the performance review");
        }
    }

    [HttpGet("number/{reviewNumber}")]
    public async Task<ActionResult<PerformanceReviewDto>> GetByReviewNumber(string reviewNumber)
    {
        try
        {
            var review = await _reviewService.GetByReviewNumberAsync(reviewNumber);
            if (review == null)
            {
                return NotFound($"Performance review with number {reviewNumber} not found");
            }
            return Ok(review);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance review {ReviewNumber}", reviewNumber);
            return StatusCode(500, "An error occurred while retrieving the performance review");
        }
    }

    [HttpGet("business-partner/{businessPartnerId}")]
    public async Task<ActionResult<IEnumerable<PerformanceReviewDto>>> GetByBusinessPartner(Guid businessPartnerId)
    {
        try
        {
            var reviews = await _reviewService.GetByBusinessPartnerAsync(businessPartnerId);
            return Ok(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance reviews for business partner {BusinessPartnerId}", businessPartnerId);
            return StatusCode(500, "An error occurred while retrieving performance reviews");
        }
    }

    [HttpGet("business-partner/{businessPartnerId}/latest")]
    public async Task<ActionResult<PerformanceReviewDto>> GetLatestReview(Guid businessPartnerId)
    {
        try
        {
            var review = await _reviewService.GetLatestReviewAsync(businessPartnerId);
            if (review == null)
            {
                return NotFound($"No performance reviews found for business partner {businessPartnerId}");
            }
            return Ok(review);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest performance review for business partner {BusinessPartnerId}", businessPartnerId);
            return StatusCode(500, "An error occurred while retrieving the latest performance review");
        }
    }

    [HttpGet("period/{reviewPeriod}")]
    public async Task<ActionResult<IEnumerable<PerformanceReviewDto>>> GetByPeriod(string reviewPeriod)
    {
        try
        {
            var reviews = await _reviewService.GetByPeriodAsync(reviewPeriod);
            return Ok(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance reviews for period {ReviewPeriod}", reviewPeriod);
            return StatusCode(500, "An error occurred while retrieving performance reviews");
        }
    }

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<PerformanceReviewDto>>> GetByStatus(string status)
    {
        try
        {
            var reviews = await _reviewService.GetByStatusAsync(status);
            return Ok(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance reviews by status {Status}", status);
            return StatusCode(500, "An error occurred while retrieving performance reviews");
        }
    }

    [HttpPost]
    public async Task<ActionResult<PerformanceReviewDto>> Create([FromBody] CreatePerformanceReviewDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var review = await _reviewService.CreateAsync(createDto);
            return CreatedAtAction(nameof(GetById), new { id = review.Id }, review);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating performance review");
            return StatusCode(500, "An error occurred while creating the performance review");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PerformanceReviewDto>> Update(Guid id, [FromBody] CreatePerformanceReviewDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var review = await _reviewService.UpdateAsync(id, updateDto);
            return Ok(review);
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
            _logger.LogError(ex, "Error updating performance review {ReviewId}", id);
            return StatusCode(500, "An error occurred while updating the performance review");
        }
    }

    [HttpPost("{id}/submit")]
    public async Task<ActionResult<PerformanceReviewDto>> Submit(Guid id)
    {
        try
        {
            var review = await _reviewService.SubmitAsync(id);
            return Ok(review);
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
            _logger.LogError(ex, "Error submitting performance review {ReviewId}", id);
            return StatusCode(500, "An error occurred while submitting the performance review");
        }
    }

    [HttpPost("{id}/acknowledge")]
    public async Task<ActionResult<PerformanceReviewDto>> Acknowledge(Guid id, [FromBody] string? supplierComments = null)
    {
        try
        {
            var review = await _reviewService.AcknowledgeAsync(id, supplierComments);
            return Ok(review);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging performance review {ReviewId}", id);
            return StatusCode(500, "An error occurred while acknowledging the performance review");
        }
    }

    [HttpPost("{id}/finalize")]
    public async Task<ActionResult<PerformanceReviewDto>> Finalize(Guid id)
    {
        try
        {
            var review = await _reviewService.FinalizeAsync(id);
            return Ok(review);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing performance review {ReviewId}", id);
            return StatusCode(500, "An error occurred while finalizing the performance review");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            await _reviewService.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting performance review {ReviewId}", id);
            return StatusCode(500, "An error occurred while deleting the performance review");
        }
    }
}

