using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class AppraisalCycleController : ControllerBase
{
    private readonly IAppraisalCycleService _cycleService;
    private readonly ICycleCoverageService _coverageService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AppraisalCycleController> _logger;

    public AppraisalCycleController(
        IAppraisalCycleService cycleService,
        ICycleCoverageService coverageService,
        ICurrentUserService currentUser,
        ILogger<AppraisalCycleController> logger)
    {
        _cycleService = cycleService;
        _coverageService = coverageService;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Get all appraisal cycles
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCycleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _cycleService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all appraisal cycles");
            return StatusCode(500, "An error occurred while retrieving appraisal cycles");
        }
    }

    /// <summary>
    /// Get appraisal cycles with pagination
    /// </summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<AppraisalCycleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _cycleService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged appraisal cycles");
            return StatusCode(500, "An error occurred while retrieving appraisal cycles");
        }
    }

    /// <summary>
    /// Get appraisal cycle by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalCycleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _cycleService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal cycle with {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the appraisal cycle");
        }
    }

    /// <summary>
    /// Get appraisal cycles by year
    /// </summary>
    [HttpGet("year/{year}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCycleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByYear(int year)
    {
        try
        {
            var response = await _cycleService.GetByYearAsync(year);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal cycles for year {Year}", year);
            return StatusCode(500, "An error occurred while retrieving appraisal cycles");
        }
    }

    /// <summary>
    /// Get appraisal cycles by type
    /// </summary>
    [HttpGet("type/{type}")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCycleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByType(AppraisalType type)
    {
        try
        {
            var response = await _cycleService.GetByTypeAsync(type);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving appraisal cycles by type {Type}", type);
            return StatusCode(500, "An error occurred while retrieving appraisal cycles");
        }
    }

    /// <summary>
    /// Get active appraisal cycles
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCycleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveCycles()
    {
        try
        {
            var response = await _cycleService.GetActiveCyclesAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active appraisal cycles");
            return StatusCode(500, "An error occurred while retrieving active appraisal cycles");
        }
    }

    /// <summary>
    /// Create a new appraisal cycle
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppraisalCycleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Create([FromBody] CreateAppraisalCycleDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _cycleService.CreateAsync(createDto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating appraisal cycle");
            return StatusCode(500, "An error occurred while creating the appraisal cycle");
        }
    }

    /// <summary>
    /// Update an existing appraisal cycle
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppraisalCycleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppraisalCycleDto updateDto)
    {
        try
        {
            if (id != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _cycleService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating appraisal cycle with Id {CycleId}", id);
            return StatusCode(500, "An error occurred while updating the appraisal cycle");
        }
    }

    /// <summary>
    /// Open an appraisal cycle
    /// </summary>
    [HttpPost("{id:guid}/open")]
    [ProducesResponseType(typeof(AppraisalCycleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> OpenCycle(Guid id)
    {
        try
        {
            var employeeId = _currentUser.EmployeeId;
            if (employeeId == null)
                return Unauthorized("Unable to determine current employee — ensure your account is linked to an employee record.");

            var openDto = new OpenAppraisalCycleDto { CycleId = id };
            var success = await _cycleService.OpenCycleAsync(openDto, employeeId.Value);
            
            if (success)
            {
                // Fetch and return the updated cycle
                var updatedCycle = await _cycleService.GetByIdAsync(id);
                return Ok(updatedCycle);
            }
            
            return BadRequest("Failed to open cycle");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening appraisal cycle with Id {CycleId}", id);
            return StatusCode(500, "An error occurred while opening the appraisal cycle");
        }
    }

    /// <summary>
    /// Close an appraisal cycle
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(typeof(AppraisalCycleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> CloseCycle(Guid id)
    {
        try
        {
            var employeeId = _currentUser.EmployeeId;
            if (employeeId == null)
                return Unauthorized("Unable to determine current employee — ensure your account is linked to an employee record.");

            var closeDto = new CloseAppraisalCycleDto { CycleId = id };
            var success = await _cycleService.CloseCycleAsync(closeDto, employeeId.Value);
            
            if (success)
            {
                // Fetch and return the updated cycle
                var updatedCycle = await _cycleService.GetByIdAsync(id);
                return Ok(updatedCycle);
            }
            
            return BadRequest("Failed to close cycle");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing appraisal cycle with Id {CycleId}", id);
            return StatusCode(500, "An error occurred while closing the appraisal cycle");
        }
    }

    /// <summary>
    /// Generate appraisal instances for all in-scope employees of an open cycle.
    /// Must be called after opening the cycle. Uses coverage-preview scope alignment.
    /// </summary>
    [HttpPost("{id:guid}/generate-appraisals")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> GenerateAppraisals(Guid id)
    {
        try
        {
            var employeeId = _currentUser.EmployeeId;
            if (employeeId == null)
                return Unauthorized("Unable to determine current employee — ensure your account is linked to an employee record.");

            var (created, evaluations, reviewEvents) = await _cycleService.GenerateAppraisalsAsync(id, employeeId.Value);
            var updatedCycle = await _cycleService.GetByIdAsync(id);
            return Ok(new { AppraisalsCreated = created, EvaluationsCreated = evaluations, ReviewEventsCreated = reviewEvents, Cycle = updatedCycle });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating appraisals for cycle {CycleId}", id);
            return StatusCode(500, "An error occurred while generating appraisals");
        }
    }

    /// <summary>
    /// Raise in-app deadline reminders for every phase of this cycle that is overdue or
    /// closing soon, addressed to the employees in scope. Safe to run more than once —
    /// an identical unread reminder is not duplicated.
    /// </summary>
    [HttpPost("{id:guid}/deadline-reminders")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> SendDeadlineReminders(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var raised = await _cycleService.SendDeadlineRemindersAsync(id, cancellationToken);
            return Ok(new { NotificationsRaised = raised });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending deadline reminders for cycle {CycleId}", id);
            return StatusCode(500, "An error occurred while sending deadline reminders");
        }
    }

    /// <summary>
    /// Get comprehensive progress metrics for an appraisal cycle
    /// </summary>
    [HttpGet("{id:guid}/progress")]
    [ProducesResponseType(typeof(AppraisalCycleProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<IActionResult> GetCycleProgress(Guid id)
    {
        try
        {
            var progress = await _cycleService.GetCycleProgressAsync(id);
            return Ok(progress);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting progress for appraisal cycle with Id {CycleId}", id);
            return StatusCode(500, "An error occurred while retrieving cycle progress");
        }
    }

    /// <summary>
    /// Get the appraisal calendar (derived activity dates) for a cycle
    /// </summary>
    [HttpGet("{id:guid}/calendar")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCalendarEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalendar(Guid id)
    {
        try
        {
            var calendar = await _cycleService.GetCalendarAsync(id);
            return Ok(calendar);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting calendar for appraisal cycle {CycleId}", id);
            return StatusCode(500, "An error occurred while retrieving the appraisal calendar");
        }
    }

    /// <summary>
    /// Get employees in scope for an appraisal cycle
    /// </summary>
    [HttpGet("{id:guid}/employees")]
    [ProducesResponseType(typeof(IEnumerable<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<IActionResult> GetEmployeesInScope(Guid id)
    {
        try
        {
            var response = await _cycleService.GetEmployeesInScopeAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employees in scope for cycle {CycleId}", id);
            return StatusCode(500, "An error occurred while retrieving employees in scope");
        }
    }

    /// <summary>
    /// Delete an appraisal cycle
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _cycleService.DeleteAsync(id);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting appraisal cycle with Id {CycleId}", id);
            return StatusCode(500, "An error occurred while deleting the appraisal cycle");
        }
    }

    #region Cycle Target Operations

    /// <summary>
    /// Add a target to an appraisal cycle
    /// </summary>
    [HttpPost("{cycleId:guid}/targets")]
    [ProducesResponseType(typeof(AppraisalCycleTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> AddCycleTarget(Guid cycleId, [FromBody] CreateAppraisalCycleTargetDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _cycleService.AddCycleTargetAsync(cycleId, createDto);
            return Ok(response);
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
            _logger.LogError(ex, "Error adding target to appraisal cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while adding the cycle target");
        }
    }

    /// <summary>
    /// Get all targets for an appraisal cycle
    /// </summary>
    [HttpGet("{cycleId:guid}/targets")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalCycleTargetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCycleTargets(Guid cycleId)
    {
        try
        {
            var response = await _cycleService.GetCycleTargetsAsync(cycleId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving targets for appraisal cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving cycle targets");
        }
    }

    /// <summary>
    /// Update a cycle target
    /// </summary>
    [HttpPut("{cycleId:guid}/targets/{targetId:guid}")]
    [ProducesResponseType(typeof(AppraisalCycleTargetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> UpdateCycleTarget(Guid cycleId, Guid targetId, [FromBody] UpdateAppraisalCycleTargetDto updateDto)
    {
        try
        {
            if (targetId != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _cycleService.UpdateCycleTargetAsync(cycleId, updateDto);
            return Ok(response);
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
            _logger.LogError(ex, "Error updating cycle target {TargetId} for cycle {CycleId}", targetId, cycleId);
            return StatusCode(500, "An error occurred while updating the cycle target");
        }
    }

    /// <summary>
    /// Delete a cycle target
    /// </summary>
    [HttpDelete("{cycleId:guid}/targets/{targetId:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> RemoveCycleTarget(Guid cycleId, Guid targetId)
    {
        try
        {
            var response = await _cycleService.RemoveCycleTargetAsync(cycleId, targetId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cycle target {TargetId} from cycle {CycleId}", targetId, cycleId);
            return StatusCode(500, "An error occurred while removing the cycle target");
        }
    }

    #endregion

    #region Coverage Preview

    /// <summary>
    /// Simulates appraisal generation and returns a full coverage analysis.
    /// Read-only — no records are created.
    /// </summary>
    [HttpGet("{cycleId:guid}/coverage-preview")]
    [ProducesResponseType(typeof(CoveragePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<IActionResult> GetCoveragePreview(
        Guid cycleId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 200)
    {
        try
        {
            var result = await _coverageService.GetCoveragePreviewAsync(cycleId, pageNumber, pageSize);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating coverage preview for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while generating the coverage preview");
        }
    }

    #endregion
}
