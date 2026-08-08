using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PerformanceLinksController : ControllerBase
{
    private readonly IPerformanceLinkService _service;
    private readonly ILogger<PerformanceLinksController> _logger;

    public PerformanceLinksController(IPerformanceLinkService service, ILogger<PerformanceLinksController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Get the required skills (competencies) for an employee goal</summary>
    [HttpGet("goals/{goalId:guid}/required-skills")]
    [ProducesResponseType(typeof(IEnumerable<GoalRequiredSkillDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGoalRequiredSkills(Guid goalId, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.GetGoalRequiredSkillsAsync(goalId, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting required skills for goal {GoalId}", goalId);
            return StatusCode(500, "An error occurred while retrieving required skills");
        }
    }

    /// <summary>Replace the required skills for an employee goal</summary>
    [HttpPut("goals/{goalId:guid}/required-skills")]
    [ProducesResponseType(typeof(IEnumerable<GoalRequiredSkillDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetGoalRequiredSkills(Guid goalId, [FromBody] List<SetGoalRequiredSkillDto> skills, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.SetGoalRequiredSkillsAsync(goalId, skills ?? new(), cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting required skills for goal {GoalId}", goalId);
            return StatusCode(500, "An error occurred while saving required skills");
        }
    }

    /// <summary>Get development-plan skill suggestions for an employee in a cycle (from their goals' required skills)</summary>
    [HttpGet("employees/{employeeId:guid}/cycles/{cycleId:guid}/skill-suggestions")]
    [ProducesResponseType(typeof(IEnumerable<DevelopmentSkillSuggestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevelopmentSkillSuggestions(Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.GetDevelopmentSkillSuggestionsAsync(employeeId, cycleId, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting skill suggestions for employee {EmployeeId} cycle {CycleId}", employeeId, cycleId);
            return StatusCode(500, "An error occurred while retrieving skill suggestions");
        }
    }

    /// <summary>Get the yearly objectives linked to a check-in</summary>
    [HttpGet("check-ins/{checkInId:guid}/objectives")]
    [ProducesResponseType(typeof(IEnumerable<CheckInObjectiveLinkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCheckInObjectives(Guid checkInId, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.GetCheckInObjectivesAsync(checkInId, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting objectives for check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while retrieving objectives");
        }
    }

    /// <summary>Replace the yearly objectives linked to a check-in</summary>
    [HttpPut("check-ins/{checkInId:guid}/objectives")]
    [ProducesResponseType(typeof(IEnumerable<CheckInObjectiveLinkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetCheckInObjectives(Guid checkInId, [FromBody] List<Guid> companyGoalIds, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.SetCheckInObjectivesAsync(checkInId, companyGoalIds ?? new(), cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting objectives for check-in {CheckInId}", checkInId);
            return StatusCode(500, "An error occurred while saving objectives");
        }
    }
}
