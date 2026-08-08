using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Salary Notches API (tenant-aware), read-only.
///
/// Notches mirror the payroll grade notches and carry the amounts HR reads for basic pay. They are
/// maintained by the projection, not edited here. See <see cref="SalaryGradesController"/>.
/// </summary>
[ApiController]
[Route("api/hr/salary-notches")]
[Authorize]
public class SalaryNotchesController : ControllerBase
{
    private const string ReadOnlyMessage =
        "Salary notches are defined in Payroll and mirrored into HR. Edit them in Payroll " +
        "(Administration → HR → Payroll → Grades Setup); changes appear here automatically.";

    private readonly ISalaryNotchService _salaryNotchService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SalaryNotchesController> _logger;

    public SalaryNotchesController(
        ISalaryNotchService salaryNotchService,
        ICurrentUserService currentUserService,
        ILogger<SalaryNotchesController> logger)
    {
        _salaryNotchService = salaryNotchService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a salary notch by ID.
    /// </summary>
    [HttpGet("{notchId:guid}")]
    [ProducesResponseType(typeof(SalaryNotchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SalaryNotchDto>> GetById(Guid notchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = GetTenantIdOrThrow();
            var result = await _salaryNotchService.GetNotchByIdAsync(tenantId, notchId, cancellationToken);
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
            _logger.LogError(ex, "Error retrieving salary notch with ID {NotchId}", notchId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the salary notch");
        }
    }

    /// <summary>
    /// Not supported — add the notch in Payroll instead.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Create([FromBody] CreateSalaryNotchDto dto) => MirrorIsReadOnly();

    /// <summary>
    /// Not supported — change the notch amount in Payroll instead.
    /// </summary>
    [HttpPut("{notchId:guid}")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Update(Guid notchId, [FromBody] UpdateSalaryNotchDto dto) => MirrorIsReadOnly();

    /// <summary>
    /// Not supported — a notch's active state follows its payroll definition.
    /// </summary>
    [HttpPut("{notchId:guid}/active")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult SetActive(Guid notchId, [FromQuery] bool isActive) => MirrorIsReadOnly();

    /// <summary>
    /// Not supported — remove the notch in Payroll; the mirror deactivates it on the next sync.
    /// </summary>
    [HttpDelete("{notchId:guid}")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Delete(Guid notchId) => MirrorIsReadOnly();

    private ObjectResult MirrorIsReadOnly() => Conflict(new { message = ReadOnlyMessage });

    private Guid GetTenantIdOrThrow()
        => _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context");
}
