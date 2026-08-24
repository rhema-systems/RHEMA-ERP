using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
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
public class UnitGoalsController : ControllerBase
{
    private readonly IUnitGoalService _unitGoalService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UnitGoalsController> _logger;

    public UnitGoalsController(
        IUnitGoalService unitGoalService,
        ICurrentUserService currentUserService,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorageService,
        ApplicationDbContext db,
        ILogger<UnitGoalsController> logger)
    {
        _unitGoalService = unitGoalService;
        _currentUserService = currentUserService;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorageService = fileStorageService;
        _db = db;
        _logger = logger;
    }

    /// <summary>Get unit goals with pagination, optionally filtered by cycle</summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<UnitGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _unitGoalService.GetPagedAsync(pageNumber, pageSize, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged unit goals");
            return StatusCode(500, "An error occurred while retrieving unit goals");
        }
    }

    /// <summary>
    /// Dashboard-filtered, projection-based paged list for the alignment management UI.
    /// Supports search, priority, orgUnit, linked/unlinked, and manager-scoped access.
    /// </summary>
    [HttpGet("dashboard/paged")]
    [ProducesResponseType(typeof(PagedResult<UnitGoalListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardPaged(
        [FromQuery] Guid cycleId,
        [FromQuery] string? search       = null,
        [FromQuery] int?    priority     = null,
        [FromQuery] Guid?   orgUnitId    = null,
        [FromQuery] bool?   isLinked     = null,
        [FromQuery] int     pageNumber   = 1,
        [FromQuery] int     pageSize     = 12,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Manager role scope: auto-filter to goals created by this manager
            Guid? managerEmployeeId = null;
            if (User.IsInRole("Manager"))
            {
                var empClaim = User.FindFirst("employee_id")?.Value;
                if (Guid.TryParse(empClaim, out var empId))
                    managerEmployeeId = empId;
            }

            GoalPriority? priorityEnum = priority.HasValue && Enum.IsDefined(typeof(GoalPriority), priority.Value)
                ? (GoalPriority)priority.Value
                : null;

            var result = await _unitGoalService.GetDashboardPagedAsync(
                cycleId, search, priorityEnum, orgUnitId, isLinked,
                managerEmployeeId, pageNumber, pageSize, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard paged unit goals for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving unit goals");
        }
    }

    /// <summary>Aggregated alignment metrics for a cycle's dashboard header tiles</summary>
    [HttpGet("dashboard/metrics")]
    [ProducesResponseType(typeof(UnitGoalDashboardMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardMetrics([FromQuery] Guid cycleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _unitGoalService.GetDashboardMetricsAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unit goal metrics for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving metrics");
        }
    }

    /// <summary>Get a unit goal by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UnitGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _unitGoalService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unit goal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the unit goal");
        }
    }

    /// <summary>Get unit goals by cycle</summary>
    [HttpGet("by-cycle/{cycleId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<UnitGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCycleId(Guid cycleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _unitGoalService.GetByCycleIdAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unit goals for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while retrieving unit goals");
        }
    }

    /// <summary>Get unit goals by organisation unit</summary>
    [HttpGet("by-org-unit/{orgUnitId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<UnitGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByOrganizationUnitId(Guid orgUnitId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _unitGoalService.GetByOrganizationUnitIdAsync(orgUnitId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unit goals for org unit {OrgUnitId}", orgUnitId);
            return StatusCode(500, "An error occurred while retrieving unit goals");
        }
    }

    /// <summary>Get unit goals created by a specific manager, optionally filtered by cycle</summary>
    [HttpGet("by-manager/{managerId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<UnitGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByManager(Guid managerId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _unitGoalService.GetByCreatedByManagerIdAsync(managerId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unit goals for manager {ManagerId}", managerId);
            return StatusCode(500, "An error occurred while retrieving unit goals");
        }
    }

    /// <summary>Get unit goals linked to a parent company goal</summary>
    [HttpGet("by-company-goal/{companyGoalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<UnitGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByParentCompanyGoal(Guid companyGoalId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _unitGoalService.GetByParentCompanyGoalAsync(companyGoalId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unit goals for company goal {CompanyGoalId}", companyGoalId);
            return StatusCode(500, "An error occurred while retrieving unit goals");
        }
    }

    private bool IsHr =>
        User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr);

    /// <summary>
    /// Who may change a unit goal: HR, the manager who raised it, or the head of the org unit it
    /// belongs to.
    /// </summary>
    /// <remarks>
    /// <para><b>Reads stay open to the tenant deliberately.</b> A unit goal is a departmental
    /// target and the entire point of the cascade is that people can see what their unit is aiming
    /// at and align to it — restricting reads would defeat the feature.</para>
    ///
    /// <para><b>Writes were open too, which was a hole.</b> Every endpoint on this controller was
    /// plain <c>[Authorize]</c>, so any authenticated employee could rewrite or delete any
    /// department's goals. Visibility and authorship are different questions.</para>
    /// </remarks>
    private async Task<bool> CanManageGoalAsync(Guid goalId, CancellationToken ct)
    {
        if (IsHr) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<UnitGoal>()
            .AsNoTracking()
            .Where(g => g.Id == goalId && g.TenantId == tenantId)
            .AnyAsync(g => g.CreatedByManagerId == me || g.OrganizationUnit.HeadEmployeeId == me, ct);
    }

    /// <summary>Create a new unit goal</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UnitGoalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateUnitGoalDto createDto, CancellationToken cancellationToken = default)
    {
        // The author is the caller. CreatedByManagerId arrives on the payload, so without this
        // anyone could raise a goal in someone else's name. HR may still name a manager explicitly
        // when raising one on their behalf.
        if (!IsHr)
        {
            if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty)
                return Forbid();
            createDto.CreatedByManagerId = me;
        }

        try
        {
            var result = await _unitGoalService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating unit goal");
            return StatusCode(500, "An error occurred while creating the unit goal");
        }
    }

    /// <summary>Update an existing unit goal</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UnitGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUnitGoalDto updateDto, CancellationToken cancellationToken = default)
    {
        // The service updates the body's id, so without this a PUT to one goal's URL could edit another.
        if (id != updateDto.Id)
            return BadRequest(new { message = "Route id does not match body id." });

        if (!await CanManageGoalAsync(id, cancellationToken)) return Forbid();

        try
        {
            var result = await _unitGoalService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating unit goal {Id}", id);
            return StatusCode(500, "An error occurred while updating the unit goal");
        }
    }

    /// <summary>Delete a unit goal</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageGoalAsync(id, cancellationToken)) return Forbid();

        try
        {
            var result = await _unitGoalService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Unit goal not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting unit goal {Id}", id);
            return StatusCode(500, "An error occurred while deleting the unit goal");
        }
    }

    // ── Attachments ───────────────────────────────────────────────────────

    /// <summary>
    /// Attach a file to a unit goal, through the controlled-upload gate.
    /// </summary>
    /// <remarks>
    /// Replaces a JSON endpoint that took a caller-supplied <c>filePath</c> and could not have
    /// worked in any case — see <c>UnitGoalService.AddAttachmentAsync</c>.
    ///
    /// <para>⚠ <b>Attaching</b> follows the write rule (HR, the goal's author, or the org-unit
    /// head); <b>reading</b> stays open to the tenant, matching the goal itself. So evidence on a
    /// unit goal is tenant-wide readable — a weaker rule than the appraisal and check-in
    /// attachments next door, and deliberately so, because a departmental target is not personal
    /// data. Do not attach anything personal to one.</para>
    /// </remarks>
    [HttpPost("{goalId:guid}/attachments")]
    [ProducesResponseType(typeof(AppraisalAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddAttachment(
        Guid goalId, IFormFile file, [FromForm] string? description, CancellationToken cancellationToken = default)
    {
        if (!await CanManageGoalAsync(goalId, cancellationToken)) return Forbid();

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, _currentUserService, _logger, file,
            sourceEntityType: "UnitGoal",
            sourceRecordId: goalId,
            sourceLabel: "Unit goal attachment",
            documentType: "UnitGoalAttachment",
            description: description,
            persist: (uploadedById, document) => _unitGoalService.AddAttachmentAsync(
                goalId, uploadedById, document.OriginalFileName, document.FileSize, description,
                cancellationToken,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId),
            cancellationToken);
    }

    /// <summary>Streams a unit-goal attachment — the file lives outside the web root.</summary>
    [HttpGet("{goalId:guid}/attachments/{attachmentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid goalId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        if (_currentUserService.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        var attachment = await _db.Set<AppraisalAttachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                a => a.Id == attachmentId && a.UnitGoalId == goalId && a.TenantId == tenantId && !a.IsDeleted,
                cancellationToken);

        if (attachment is null)
            return NotFound(new { message = "Attachment not found" });

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorageService, _db, tenantId,
            attachment.DocumentRecordId, attachment.DocumentVersionId,
            attachment.FileUploadRecordId, attachment.FilePath,
            attachment.FileName, fallbackContentType: null,
            inline: false, cancellationToken);
    }

    /// <summary>Get attachments for a unit goal</summary>
    [HttpGet("{goalId:guid}/attachments")]
    [ProducesResponseType(typeof(IEnumerable<AppraisalAttachmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttachments(Guid goalId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _unitGoalService.GetAttachmentsAsync(goalId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving attachments for unit goal {GoalId}", goalId);
            return StatusCode(500, "An error occurred while retrieving attachments");
        }
    }

    /// <summary>Get projected employee goal summaries cascaded from this unit goal</summary>
    [HttpGet("{id:guid}/employee-goals")]
    [ProducesResponseType(typeof(IEnumerable<UnitGoalEmployeeGoalSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployeeGoalSummaries(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var summaries = await _unitGoalService.GetEmployeeGoalSummariesAsync(id, cancellationToken);
            return Ok(summaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching employee goal summaries for unit goal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving employee goal summaries");
        }
    }

    /// <summary>Cascade integrity check — employee goal count for the create/edit page</summary>
    [HttpGet("{id:guid}/cascade-stats")]
    [ProducesResponseType(typeof(UnitGoalCascadeStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCascadeStats(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _unitGoalService.GetCascadeStatsAsync(id, cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching cascade stats for unit goal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving cascade stats");
        }
    }

    /// <summary>Delete an attachment from a unit goal</summary>
    [HttpDelete("{goalId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid goalId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        if (!await CanManageGoalAsync(goalId, cancellationToken)) return Forbid();

        try
        {
            var result = await _unitGoalService.DeleteAttachmentAsync(goalId, attachmentId, cancellationToken);
            if (!result) return NotFound(new { message = "Attachment not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attachment {AttachmentId} from unit goal {GoalId}", attachmentId, goalId);
            return StatusCode(500, "An error occurred while deleting the attachment");
        }
    }
}
