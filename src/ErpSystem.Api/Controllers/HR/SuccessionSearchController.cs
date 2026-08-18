using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Criteria-based candidate search for talent pools and succession plans — returns employees
/// with computed age, service-years-left, latest appraisal, competency gaps and a fit score.
/// </summary>
[ApiController]
[Route("api/succession")]
[Authorize(Policy = HrPermissions.SuccessionReadPolicy)]
public class SuccessionSearchController : ControllerBase
{
    private readonly ISuccessionCandidateSearchService _searchService;
    private readonly ILogger<SuccessionSearchController> _logger;

    public SuccessionSearchController(
        ISuccessionCandidateSearchService searchService,
        ILogger<SuccessionSearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    [HttpPost("candidate-search")]
    [ProducesResponseType(typeof(IReadOnlyList<SuccessionCandidateSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] SuccessionCandidateSearchDto criteria, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var results = await _searchService.SearchAsync(criteria, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running succession candidate search");
            return StatusCode(500, "An error occurred while searching for candidates.");
        }
    }

    /// <summary>Fit scores + suggested ranking for a plan's existing candidates.</summary>
    [HttpGet("plans/{planId:guid}/candidate-fit")]
    [ProducesResponseType(typeof(IReadOnlyList<CandidateFitScoreDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ScorePlanCandidates(Guid planId, CancellationToken cancellationToken)
    {
        try
        {
            var results = await _searchService.ScorePlanCandidatesAsync(planId, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scoring plan candidates for {PlanId}", planId);
            return StatusCode(500, "An error occurred while scoring candidates.");
        }
    }
}
