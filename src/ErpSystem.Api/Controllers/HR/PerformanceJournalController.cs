using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PerformanceJournalController : ControllerBase
{
    private readonly IPerformanceJournalService _journalService;
    private readonly IEmployeeService            _employeeService;
    private readonly ILogger<PerformanceJournalController> _logger;

    public PerformanceJournalController(
        IPerformanceJournalService journalService,
        IEmployeeService employeeService,
        ILogger<PerformanceJournalController> logger)
    {
        _journalService  = journalService;
        _employeeService = employeeService;
        _logger          = logger;
    }

    /// <summary>Get a journal entry by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceJournalEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid requestingEmployeeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _journalService.GetByIdAsync(id, requestingEmployeeId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance journal entry {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the journal entry");
        }
    }

    /// <summary>Get journal entries by owner, optionally filtered by cycle</summary>
    [HttpGet("by-owner/{ownerId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceJournalEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByOwner(Guid ownerId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _journalService.GetByOwnerIdAsync(ownerId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving journal entries for owner {OwnerId}", ownerId);
            return StatusCode(500, "An error occurred while retrieving journal entries");
        }
    }

    /// <summary>Get entries shared with the manager (IsPrivate = false) for an employee</summary>
    [HttpGet("shared/{ownerId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceJournalEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSharedWithManager(Guid ownerId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _journalService.GetSharedWithManagerAsync(ownerId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving shared journal entries for owner {OwnerId}", ownerId);
            return StatusCode(500, "An error occurred while retrieving journal entries");
        }
    }

    /// <summary>Get entries written by a manager about a specific direct report</summary>
    [HttpGet("about/{managerId:guid}/{subjectEmployeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceJournalEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAboutSubject(Guid managerId, Guid subjectEmployeeId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _journalService.GetAboutSubjectAsync(managerId, subjectEmployeeId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving journal entries by manager {ManagerId} about subject {SubjectId}", managerId, subjectEmployeeId);
            return StatusCode(500, "An error occurred while retrieving journal entries");
        }
    }

    /// <summary>Get paged journal entries for an owner</summary>
    [HttpGet("paged/{ownerId:guid}")]
    [ProducesResponseType(typeof(PagedResult<PerformanceJournalEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(Guid ownerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _journalService.GetPagedAsync(ownerId, pageNumber, pageSize, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged journal entries for owner {OwnerId}", ownerId);
            return StatusCode(500, "An error occurred while retrieving journal entries");
        }
    }

    /// <summary>Create a new journal entry</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PerformanceJournalEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePerformanceJournalEntryDto createDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _journalService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id, requestingEmployeeId = result.OwnerId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating journal entry");
            return StatusCode(500, "An error occurred while creating the journal entry");
        }
    }

    /// <summary>Update an existing journal entry</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceJournalEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePerformanceJournalEntryDto updateDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _journalService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating journal entry {Id}", id);
            return StatusCode(500, "An error occurred while updating the journal entry");
        }
    }

    /// <summary>Delete a journal entry</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _journalService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Journal entry not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting journal entry {Id}", id);
            return StatusCode(500, "An error occurred while deleting the journal entry");
        }
    }

    /// <summary>Get team journal entries visible to a manager across their direct reports</summary>
    [HttpGet("team")]
    [ProducesResponseType(typeof(IEnumerable<TeamJournalListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeamJournal(
        [FromQuery] Guid      managerId,
        [FromQuery] Guid?     employeeId        = null,
        [FromQuery] Guid?     appraisalCycleId  = null,
        [FromQuery] DateTime? fromDate          = null,
        [FromQuery] DateTime? toDate            = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var directReports = await _employeeService.GetDirectReportsAsync(managerId, cancellationToken);
            var reportIds     = directReports.Select(e => e.Id).ToList();

            // Optionally narrow to a single direct report
            if (employeeId.HasValue)
            {
                if (!reportIds.Contains(employeeId.Value))
                    return BadRequest(new { message = "The specified employee is not a direct report of this manager." });
                reportIds = new List<Guid> { employeeId.Value };
            }

            var result = await _journalService.GetTeamJournalAsync(
                managerId, reportIds, appraisalCycleId, fromDate, toDate, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team journal for manager {ManagerId}", managerId);
            return StatusCode(500, "An error occurred while retrieving team journal entries");
        }
    }

    /// <summary>Set the privacy flag on a journal entry</summary>
    [HttpPatch("{id:guid}/privacy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrivacy(Guid id, [FromBody] bool isPrivate, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _journalService.SetPrivacyAsync(id, isPrivate, cancellationToken);
            if (!result) return NotFound(new { message = "Journal entry not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting privacy for journal entry {Id}", id);
            return StatusCode(500, "An error occurred while updating the journal entry");
        }
    }
}
