using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Salary Levels API (tenant-aware), read-only.
///
/// Levels are synthesized by the projection from the payroll-defined salary structure (payroll is
/// 2-tier, HR is 3-tier), so they cannot be edited here. See <see cref="SalaryGradesController"/>.
/// </summary>
[ApiController]
[Route("api/hr/salary-levels")]
[Authorize]
public class SalaryLevelsController : ControllerBase
{
    private const string ReadOnlyMessage =
        "Salary levels are derived from the payroll-defined salary structure and cannot be edited in HR. " +
        "Edit the grade in Payroll (Administration → HR → Payroll → Grades Setup).";

    private readonly ISalaryLevelService _salaryLevelService;
    private readonly ISalaryNotchService _salaryNotchService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SalaryLevelsController> _logger;

    public SalaryLevelsController(
        ISalaryLevelService salaryLevelService,
        ISalaryNotchService salaryNotchService,
        ICurrentUserService currentUserService,
        ILogger<SalaryLevelsController> logger)
    {
        _salaryLevelService = salaryLevelService;
        _salaryNotchService = salaryNotchService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a salary level by ID including its notches.
    /// </summary>
    [HttpGet("{levelId:guid}")]
    [ProducesResponseType(typeof(SalaryLevelDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryLevelDetailDto>> GetById(Guid levelId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var result = await _salaryLevelService.GetLevelDetailAsync(tenantId, levelId, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving salary level with ID {LevelId}", levelId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the salary level");
        }
    }

    /// <summary>
    /// Retrieves salary notches for a salary level.
    /// </summary>
    [HttpGet("{levelId:guid}/notches")]
    [ProducesResponseType(typeof(IReadOnlyList<SalaryNotchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SalaryNotchDto>>> GetNotchesByLevel(Guid levelId, [FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var result = await _salaryNotchService.GetNotchesByLevelAsync(tenantId, levelId, includeInactive, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving salary notches for LevelId {LevelId}", levelId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving salary notches");
        }
    }

    /// <summary>
    /// Not supported — levels are derived from the payroll grade structure.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Create([FromBody] CreateSalaryLevelDto dto) => MirrorIsReadOnly();

    /// <summary>
    /// Not supported — levels are derived from the payroll grade structure.
    /// </summary>
    [HttpPut("{levelId:guid}")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Update(Guid levelId, [FromBody] UpdateSalaryLevelDto dto) => MirrorIsReadOnly();

    /// <summary>
    /// Not supported — a level's active state follows its payroll grade.
    /// </summary>
    [HttpPut("{levelId:guid}/active")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult SetActive(Guid levelId, [FromQuery] bool isActive) => MirrorIsReadOnly();

    /// <summary>
    /// Not supported — remove the grade in Payroll; the mirror deactivates it on the next sync.
    /// </summary>
    [HttpDelete("{levelId:guid}")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Delete(Guid levelId) => MirrorIsReadOnly();

    private ObjectResult MirrorIsReadOnly() => Conflict(new { message = ReadOnlyMessage });

    private Guid GetTenantIdOrThrow()
        => _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
}
