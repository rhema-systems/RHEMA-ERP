using ErpSystem.Api.Models;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Models;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Performance improvement plans.
///
/// <para><b>Who can see one.</b> A PIP is one of the most sensitive records the HR module holds,
/// and it names an employee who has been told their performance is not good enough. The org-wide
/// reads are HR's; everything about a single plan is reachable by HR, the employee it is about,
/// the named supervisor and the named HR owner, and by nobody else. <c>/mine</c> and
/// <c>/supervising</c> exist so an employee and a manager each have their own list without the
/// org-wide one being opened up.</para>
///
/// <para><b>Approval.</b> Draft → PendingApproval → Active runs on the generic workflow engine, so
/// nothing here is in force until a <c>PerformanceImprovementPlan</c> workflow definition has been
/// published and the plan approved through it.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Route("api/Pip")]
[Authorize(Policy = "InternalOnly")]
public class PerformanceImprovementPlansController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;
    private const string AuthorRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr + "," + Constants.Roles.Manager;

    private readonly IPerformanceImprovementPlanService _improvementPlanService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly ILogger<PerformanceImprovementPlansController> _logger;

    public PerformanceImprovementPlansController(
        IPerformanceImprovementPlanService improvementPlanService,
        IFileStorageService fileStorageService,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IGenericRepository<Employee> employeeRepository,
        ILogger<PerformanceImprovementPlansController> logger)
    {
        _improvementPlanService = improvementPlanService;
        _fileStorageService = fileStorageService;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _db = db;
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    private bool IsHr =>
        User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr);

    /// <summary>
    /// Business rules ("this employee is already on a plan", "recall it before editing") are the
    /// service's <see cref="InvalidOperationException"/>s. Left to the generic handler they came
    /// back as a 500 with a bare string body the client cannot read a message out of, so a rule the
    /// user hits routinely looked like a crash. 422 + the rule's own text, logged as a warning —
    /// matching <c>EmployeeGoalsController</c> and <c>AppraisalCycleTemplatesController</c>.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("PIP rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>
    /// True when the caller is entitled to see this plan: HR, the employee it is about, the named
    /// supervisor, or the named HR owner. Everyone else is refused — before this, every
    /// authenticated user in the tenant could read any improvement plan by id. See
    /// <see cref="PipAccess"/> for the shared rule.
    /// </summary>
    private Task<bool> CanAccessPlanAsync(Guid pipId, CancellationToken ct = default)
        => PipAccess.CanAccessAsync(this, _db, _currentUserService, pipId, ct);

    /// <summary>As <see cref="CanAccessPlanAsync"/>, but the subject employee is a reader only —
    /// the people who may change a plan are HR, the supervisor and the HR owner.</summary>
    private Task<bool> CanManagePlanAsync(Guid pipId, CancellationToken ct = default)
        => PipAccess.CanManageAsync(this, _db, _currentUserService, pipId, ct);

    /// <summary>
    /// Get all performance improvement plans
    /// </summary>
    [HttpGet]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(IEnumerable<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var response = await _improvementPlanService.GetAllAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all performance improvement plans");
            return StatusCode(500, "An error occurred while retrieving performance improvement plans");
        }
    }

    /// <summary>
    /// Get PIPs with pagination
    /// </summary>
    [HttpGet("paged")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(PagedResult<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var response = await _improvementPlanService.GetPagedAsync(pageNumber, pageSize);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged performance improvement plans");
            return StatusCode(500, "An error occurred while retrieving performance improvement plans");
        }
    }

    /// <summary>
    /// Get PIP by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PerformanceImprovementPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!await CanAccessPlanAsync(id)) return Forbid();

        try
        {
            var response = await _improvementPlanService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance improvement plan with Id {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the performance improvement plan");
        }
    }

    /// <summary>
    /// Get PIPs by employee ID. HR, the employee themselves, or that employee's line manager.
    /// </summary>
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByEmployeeId(Guid employeeId, CancellationToken ct = default)
    {
        if (!IsHr)
        {
            if (_currentUserService.EmployeeId is not Guid me) return Forbid();
            if (me != employeeId)
            {
                var managesThem = _currentUserService.TenantId is Guid tenantId
                    && await _employeeRepository.ExistsAsync(
                        e => e.Id == employeeId && e.TenantId == tenantId && e.ManagerId == me);
                if (!managesThem) return Forbid();
            }
        }

        try
        {
            var response = await _improvementPlanService.GetByEmployeeIdAsync(employeeId, ct);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance improvement plans");
            return StatusCode(500, "An error occurred while retrieving performance improvement plans");
        }
    }

    /// <summary>The signed-in employee's own improvement plans.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken ct = default)
    {
        // An account with no employee link has no plans rather than an error — the same shape
        // every other /me route in this module returns.
        if (_currentUserService.EmployeeId is not Guid me)
            return Ok(Array.Empty<PerformanceImprovementPlanDto>());

        try
        {
            return Ok(await _improvementPlanService.GetByEmployeeIdAsync(me, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving the caller's improvement plans");
            return StatusCode(500, "An error occurred while retrieving your improvement plans");
        }
    }

    /// <summary>
    /// Plans the signed-in employee owns as supervisor or HR owner — a manager's worklist, without
    /// opening up the org-wide list.
    /// </summary>
    [HttpGet("supervising")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSupervising(CancellationToken ct = default)
    {
        if (_currentUserService.EmployeeId is not Guid me)
            return Ok(Array.Empty<PerformanceImprovementPlanDto>());

        try
        {
            return Ok(await _improvementPlanService.GetBySupervisorAsync(me, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving supervised improvement plans");
            return StatusCode(500, "An error occurred while retrieving the plans you supervise");
        }
    }

    /// <summary>
    /// Get PIPs by status
    /// </summary>
    [HttpGet("status/{status}")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(IEnumerable<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByStatus(PipStatus status)
    {
        try
        {
            var response = await _improvementPlanService.GetByStatusAsync(status);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance improvement plans");
            return StatusCode(500, "An error occurred while retrieving performance improvement plans");
        }
    }

    /// <summary>
    /// Get all active PIPs
    /// </summary>
    [HttpGet("active")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(IEnumerable<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive()
    {
        try
        {
            var response = await _improvementPlanService.GetActivePipsAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active performance improvement plans");
            return StatusCode(500, "An error occurred while retrieving active performance improvement plans");
        }
    }

    /// <summary>
    /// Create a new PIP. It starts in Draft and is not in force until it has been approved.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] PipCreateRequest req)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createDto = new CreatePerformanceImprovementPlanDto
            {
                EmployeeId        = req.EmployeeId,
                AppraisalId       = req.AppraisalId,
                StartDate         = req.StartDate,
                EndDate           = req.EndDate,
                PerformanceIssues = req.PerformanceIssues,
                ExpectedStandards = req.ExpectedStandards,
                ImprovementActions = req.ImprovementActions,
                SupportProvided   = req.SupportProvided ?? string.Empty,
                MeasurementCriteria = req.MeasurementCriteria ?? string.Empty,
                SupervisorId      = req.SupervisorId,
                HROwnerId         = req.HROwnerId,
                ReviewSchedule    = req.ReviewSchedule,
            };

            var response = await _improvementPlanService.CreateAsync(createDto);
            return Ok(response.Id);
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "creating");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating performance improvement plan");
            return StatusCode(500, "An error occurred while creating the performance improvement plan");
        }
    }

    /// <summary>
    /// Update an existing PIP
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(PerformanceImprovementPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PipUpdateRequest req)
    {
        if (!await CanManagePlanAsync(id)) return Forbid();

        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updateDto = new UpdatePerformanceImprovementPlanDto
            {
                Id                 = id,
                EmployeeId         = req.EmployeeId,
                AppraisalId        = req.AppraisalId,
                StartDate          = req.StartDate,
                EndDate            = req.EndDate,
                Status             = req.Status,
                PerformanceIssues  = req.PerformanceIssues,
                ExpectedStandards  = req.ExpectedStandards,
                ImprovementActions = req.ImprovementActions,
                SupportProvided    = req.SupportProvided ?? string.Empty,
                MeasurementCriteria = req.MeasurementCriteria ?? string.Empty,
                SupervisorId       = req.SupervisorId,
                HROwnerId          = req.HROwnerId,
                ReviewSchedule     = req.ReviewSchedule,
            };

            var response = await _improvementPlanService.UpdateAsync(updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "updating");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance improvement plan with Id {PipId}", id);
            return StatusCode(500, "An error occurred while updating the performance improvement plan");
        }
    }

    // ── Approval workflow ─────────────────────────────────────────────────────
    // Submit / approve / reject / recall run on the generic workflow engine. There is no role
    // attribute on approve and reject on purpose: the authority to approve comes from the published
    // PerformanceImprovementPlan definition, and the service refuses anyone the engine has not
    // routed the step to.
    //
    // ⚠ All four are inoperable until such a definition is published and
    // POST api/Workflow/entity-types/seed has been re-run after this build.

    /// <summary>Send a draft plan out for approval.</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(PerformanceImprovementPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitForApproval(Guid id, CancellationToken ct = default)
    {
        if (!await CanManagePlanAsync(id, ct)) return Forbid();

        try
        {
            return Ok(await _improvementPlanService.SubmitForApprovalAsync(id, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "submitting for approval");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting PIP {PipId} for approval", id);
            return StatusCode(500, "An error occurred while submitting the plan for approval");
        }
    }

    /// <summary>Approve the current workflow step. Puts the plan in force when it is the last one.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(PerformanceImprovementPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct = default)
    {
        try
        {
            return Ok(await _improvementPlanService.ApproveAsync(id, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "approving");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving PIP {PipId}", id);
            return StatusCode(500, "An error occurred while approving the plan");
        }
    }

    /// <summary>Reject the plan. It returns to Draft with the reason kept on the record.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(PerformanceImprovementPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] PipRejectionRequest? request, CancellationToken ct = default)
    {
        try
        {
            return Ok(await _improvementPlanService.RejectAsync(id, request?.Reason, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "rejecting");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting PIP {PipId}", id);
            return StatusCode(500, "An error occurred while rejecting the plan");
        }
    }

    /// <summary>Pull a plan back out of approval so it can be reworked.</summary>
    [HttpPost("{id:guid}/recall")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(PerformanceImprovementPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Recall(Guid id, CancellationToken ct = default)
    {
        if (!await CanManagePlanAsync(id, ct)) return Forbid();

        try
        {
            return Ok(await _improvementPlanService.RecallAsync(id, ct));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "recalling");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalling PIP {PipId}", id);
            return StatusCode(500, "An error occurred while recalling the plan");
        }
    }

    /// <summary>
    /// Update PIP status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdatePipStatusDto statusDto)
    {
        if (!await CanManagePlanAsync(id)) return Forbid();

        try
        {
            if (id != statusDto.PipId)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _improvementPlanService.UpdateStatusAsync(statusDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "changing status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance improvement plan with ID {PipId}", id);
            return StatusCode(500, "An error occurred while updating the performance improvement plan");
        }
    }

    /// <summary>
    /// Complete a PIP
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompletePipDto completeDto)
    {
        try
        {
            if (id != completeDto.PipId)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _improvementPlanService.CompletePipAsync(completeDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "recording the outcome");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing performance improvement plan with Id {PipId}", id);
            return StatusCode(500, "An error occurred while completing the performance improvement plan");
        }
    }

    /// <summary>
    /// Delete a PIP
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _improvementPlanService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "deleting");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting performance improvement plan with Id {PipId}", id);
            return StatusCode(500, "An error occurred while deleting the performance improvement plan");
        }
    }

    #region New Pip-specific Endpoints

    /// <summary>
    /// Prepare a new PIP form with pre-populated employee and optional appraisal data.
    /// </summary>
    [HttpGet("prepare")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(PipPrepareResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Prepare([FromQuery] Guid employeeId, [FromQuery] Guid? appraisalId)
    {
        if (_currentUserService.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        try
        {
            var employee = await _employeeRepository.GetQueryable(e => e.Id == employeeId && e.TenantId == tenantId)
                .Include(e => e.Position)
                .Include(e => e.Department)
                .Include(e => e.Manager)
                .FirstOrDefaultAsync();

            if (employee == null)
                return NotFound("Employee not found");

            var result = new PipPrepareResponse
            {
                EmployeeId         = employee.Id,
                EmployeeName       = employee.FullName,
                EmployeePosition   = employee.Position?.Title ?? string.Empty,
                EmployeeDepartment = employee.Department?.Name ?? string.Empty,
                EmployeePhotoUrl   = employee.PicturePath,
                SupervisorId       = employee.ManagerId ?? Guid.Empty,
                SupervisorName     = employee.Manager?.FullName ?? string.Empty,
                StartDate          = DateTime.Today,
                EndDate            = DateTime.Today.AddDays(90),
            };

            // The appraisal half of "employee and optional appraisal data" was never read: the
            // parameter was accepted and dropped, so the three appraisal fields on the response
            // came back null however the caller asked. They are the whole reason a PIP raised off
            // an appraisal shows what prompted it.
            if (appraisalId is Guid sourceAppraisalId)
            {
                var appraisal = await _db.Set<PerformanceAppraisal>()
                    .AsNoTracking()
                    .Where(a => a.Id == sourceAppraisalId
                             && a.TenantId == tenantId
                             && a.EmployeeId == employeeId)
                    .Select(a => new
                    {
                        a.Id,
                        CycleName = a.AppraisalCycle.CycleName,
                        a.OverallScore,
                        GradeLabel = a.OverallGrade != null ? a.OverallGrade.GradeName : null,
                    })
                    .FirstOrDefaultAsync();

                if (appraisal != null)
                {
                    result.AppraisalId         = appraisal.Id;
                    result.AppraisalCycleName  = appraisal.CycleName;
                    result.AppraisalScore      = appraisal.OverallScore;
                    result.AppraisalGradeLabel = appraisal.GradeLabel;
                }
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing PIP for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while preparing the PIP");
        }
    }

    /// <summary>
    /// Get full PIP detail including goals, meetings, and attachments.
    /// </summary>
    [HttpGet("{id:guid}/detail")]
    [ProducesResponseType(typeof(PipDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        if (!await CanAccessPlanAsync(id)) return Forbid();

        try
        {
            var pip     = await _improvementPlanService.GetByIdAsync(id);
            var goals   = (await _improvementPlanService.GetPipGoalsAsync(id)).ToList();
            var meetings = (await _improvementPlanService.GetReviewMeetingsAsync(id)).ToList();
            var attachments = (await _improvementPlanService.GetPipAttachmentsAsync(id)).ToList();

            var detail = new PipDetailResponse
            {
                PipId               = pip.Id,
                PipNumber           = pip.PipNumber,
                EmployeeId          = pip.EmployeeId,
                EmployeeName        = pip.EmployeeName,
                SupervisorId        = pip.SupervisorId,
                SupervisorName      = pip.SupervisorName,
                HROwnerId           = pip.HROwnerId,
                HROwnerName         = pip.HROwnerName,
                AppraisalId         = pip.AppraisalId,
                StartDate           = pip.StartDate,
                EndDate             = pip.EndDate,
                Status              = pip.Status,
                PerformanceIssues   = pip.PerformanceIssues,
                ExpectedStandards   = pip.ExpectedStandards,
                ImprovementActions  = pip.ImprovementActions,
                SupportProvided     = pip.SupportProvided,
                MeasurementCriteria = pip.MeasurementCriteria,
                ReviewSchedule      = pip.ReviewSchedule,
                Outcome             = pip.Outcome,
                CompletionDate      = pip.CompletionDate,
                OutcomeNotes        = pip.OutcomeNotes,
                Goals = goals.Select(g => new PipGoalResponse
                {
                    GoalId          = g.Id,
                    Title           = g.Title,
                    Description     = g.Description,
                    SuccessCriteria = g.SuccessCriteria,
                    DueDate         = g.DueDate,
                    Status          = g.Status,
                    ProgressPercent = g.ProgressPercent,
                    ProgressNotes   = g.ProgressNotes,
                }).ToList(),
                Attachments = attachments.Select(a => new PipAttachmentResponse
                {
                    AttachmentId    = a.Id,
                    FileName        = a.FileName,
                    FileSizeBytes   = a.FileSizeBytes,
                    Description     = a.Description,
                    UploadDate      = a.UploadDate,
                    UploadedByName  = a.UploadedByName,
                    PublicUrl       = a.PublicUrl,
                }).ToList(),
                ReviewMeetings = meetings.Select(m => new PipMeetingSummaryResponse
                {
                    MeetingId           = m.Id,
                    MeetingDate         = m.MeetingDate,
                    EmployeeAttended    = m.EmployeeAttended,
                    ConductedByName     = m.ConductedByName,
                    ProgressNotesPreview = m.ProgressNotes.Length > 120 ? m.ProgressNotes[..120] + "..." : m.ProgressNotes,
                    IsCompleted         = m.MeetingDate <= DateTime.UtcNow,
                }).ToList(),
            };

            detail.NextScheduledMeeting = detail.ReviewMeetings
                .Where(m => !m.IsCompleted)
                .OrderBy(m => m.MeetingDate)
                .FirstOrDefault();

            return Ok(detail);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving PIP detail for {PipId}", id);
            return StatusCode(500, "An error occurred while retrieving PIP detail");
        }
    }

    // ── Goals ─────────────────────────────────────────────────────────────────

    [HttpGet("{pipId:guid}/goals")]
    [ProducesResponseType(typeof(IEnumerable<PipGoalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetGoals(Guid pipId)
    {
        if (!await CanAccessPlanAsync(pipId)) return Forbid();

        try
        {
            var goals = await _improvementPlanService.GetPipGoalsAsync(pipId);
            return Ok(goals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goals for PIP {PipId}", pipId);
            return StatusCode(500, "An error occurred while retrieving PIP goals");
        }
    }

    [HttpPost("{pipId:guid}/goals")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(PipGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddGoal(Guid pipId, [FromBody] PipGoalRequest req)
    {
        if (!await CanManagePlanAsync(pipId)) return Forbid();

        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var dto = new CreatePipGoalDto
            {
                PipId           = pipId,
                Title           = req.Title,
                Description     = req.Description,
                SuccessCriteria = req.SuccessCriteria,
                DueDate         = req.DueDate,
                Status          = req.Status,
            };

            var result = await _improvementPlanService.AddPipGoalAsync(pipId, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding goal to PIP {PipId}", pipId);
            return StatusCode(500, "An error occurred while adding the goal");
        }
    }

    [HttpPut("goals/{goalId:guid}")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(PipGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGoal(Guid goalId, [FromBody] PipGoalRequest req)
    {
        try
        {
            var existingGoal = await _improvementPlanService.GetGoalByIdAsync(goalId);
            if (existingGoal == null)
                return NotFound("Goal not found");

            if (!await CanManagePlanAsync(existingGoal.PipId)) return Forbid();

            var dto = new UpdatePipGoalDto
            {
                Id              = goalId,
                PipId           = existingGoal.PipId,
                Title           = req.Title,
                Description     = req.Description,
                SuccessCriteria = req.SuccessCriteria,
                DueDate         = req.DueDate,
                Status          = req.Status,
                ProgressPercent = req.ProgressPercent,
                ProgressNotes   = req.ProgressNotes,
            };

            var result = await _improvementPlanService.UpdatePipGoalAsync(existingGoal.PipId, dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating goal {GoalId}", goalId);
            return StatusCode(500, "An error occurred while updating the goal");
        }
    }

    [HttpDelete("goals/{goalId:guid}")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGoal(Guid goalId)
    {
        try
        {
            var existingGoal = await _improvementPlanService.GetGoalByIdAsync(goalId);
            if (existingGoal == null)
                return NotFound("Goal not found");

            if (!await CanManagePlanAsync(existingGoal.PipId)) return Forbid();

            var result = await _improvementPlanService.DeletePipGoalAsync(existingGoal.PipId, goalId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting goal {GoalId}", goalId);
            return StatusCode(500, "An error occurred while deleting the goal");
        }
    }

    // ── Attachments ───────────────────────────────────────────────────────────

    [HttpPost("{pipId:guid}/attachments")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(PipAttachmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadAttachment(
        Guid pipId, IFormFile file, [FromForm] string? description, CancellationToken ct = default)
    {
        if (!await CanManagePlanAsync(pipId, ct)) return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest("No file provided");

        var uploadedById = _currentUserService.EmployeeId ?? Guid.Empty;
        if (uploadedById == Guid.Empty)
            return Unauthorized("User employee context not found");

        if (_currentUserService.TenantId is not Guid tenantId ||
            !Guid.TryParse(_currentUserService.UserId, out var actorUserId))
            return Unauthorized("User context could not be resolved");

        HrControlledDocument document;
        try
        {
            document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                ActorName = _currentUserService.UserName,
                Category = ControlledFileUploadCategories.HrPipAttachments,
                File = file,
                Registration = new HrDocumentDmsRegistration
                {
                    SourceLabel = "Performance improvement plan attachment",
                    SourceEntityType = "PerformanceImprovementPlan",
                    SourceRecordId = pipId,
                    Title = Path.GetFileName(file.FileName),
                    DocumentType = "PipAttachment",
                    ChangeSummary = description
                }
            }, ct);
        }
        catch (ControlledFileUploadException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }

        try
        {
            var dto = await _improvementPlanService.CreatePipAttachmentAsync(
                pipId, uploadedById, document.OriginalFileName, string.Empty,
                publicUrl: null, document.FileSize, description, ct,
                document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId);

            return Ok(new PipAttachmentResponse
            {
                AttachmentId   = dto.Id,
                FileName       = dto.FileName,
                FileSizeBytes  = dto.FileSizeBytes,
                Description    = dto.Description,
                UploadDate     = dto.UploadDate,
                UploadedByName = dto.UploadedByName,
                // No public URL any more: the file lives outside the web root and is only
                // reachable through the authorizing download endpoint below.
                PublicUrl      = null,
            });
        }
        catch (Exception ex)
        {
            await _hrDocuments.RollbackAsync(document, tenantId, actorUserId, ct);

            if (ex is ArgumentException)
                return NotFound(ex.Message);

            _logger.LogError(ex, "Error uploading attachment for PIP {PipId}", pipId);
            return StatusCode(500, "An error occurred while uploading the attachment");
        }
    }

    /// <summary>
    /// Streams a PIP attachment to a caller entitled to see it.
    /// </summary>
    /// <remarks>
    /// Improvement plans are sensitive employment records. Neither this endpoint's helper nor
    /// the DMS performs the entitlement check — that is the ownership test below.
    /// </remarks>
    [HttpGet("attachments/{attachmentId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken ct = default)
    {
        if (_currentUserService.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        var attachment = await _db.Set<AppraisalAttachment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == attachmentId && item.TenantId == tenantId && !item.IsDeleted,
                ct);
        if (attachment?.PipId is not Guid pipId)
            return NotFound("Attachment not found");

        var plan = await _db.Set<PerformanceImprovementPlan>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == pipId && item.TenantId == tenantId, ct);
        if (plan is null)
            return NotFound("Attachment not found");

        // Same entitlement test as every other read on a plan — HR, the subject, the supervisor
        // or the HR owner. The supervisor was missing here, so a manager could open the plan they
        // wrote but not the evidence attached to it.
        if (!await CanAccessPlanAsync(pipId, ct))
            return Forbid();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorageService, _db, tenantId,
            attachment.DocumentRecordId, attachment.DocumentVersionId,
            attachment.FileUploadRecordId, attachment.FilePath,
            attachment.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    [HttpDelete("attachments/{attachmentId:guid}")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId)
    {
        try
        {
            var attachment = await _improvementPlanService.GetAttachmentByIdAsync(attachmentId);
            if (attachment == null)
                return NotFound("Attachment not found");

            if (!await CanManagePlanAsync(attachment.PipId)) return Forbid();

            // Delete file from storage
            if (!string.IsNullOrWhiteSpace(attachment.FilePath))
            {
                try { await _fileStorageService.DeleteFileAsync(attachment.FilePath); }
                catch (Exception storageEx)
                {
                    _logger.LogWarning(storageEx, "Could not delete file from storage for attachment {Id}", attachmentId);
                }
            }

            var result = await _improvementPlanService.DeletePipAttachmentAsync(attachmentId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attachment {AttachmentId}", attachmentId);
            return StatusCode(500, "An error occurred while deleting the attachment");
        }
    }

    // ── Outcome ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Records the outcome. <c>Extended</c> is not a closure — it pushes the end date out to
    /// <c>newEndDate</c> (required for that outcome) and leaves the plan running.
    /// </summary>
    [HttpPost("{pipId:guid}/outcome")]
    [Authorize(Roles = HrRoles)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordOutcome(Guid pipId, [FromBody] PipOutcomeRequest req)
    {
        try
        {
            var completeDto = new CompletePipDto
            {
                PipId        = pipId,
                Outcome      = (PipOutcome)req.Outcome,
                OutcomeNotes = req.Notes ?? string.Empty,
                NewEndDate   = req.NewEndDate,
            };

            var result = await _improvementPlanService.CompletePipAsync(completeDto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "recording the outcome");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording outcome for PIP {PipId}", pipId);
            return StatusCode(500, "An error occurred while recording the outcome");
        }
    }

    // ── Employee search ───────────────────────────────────────────────────────

    [HttpGet("employees/search")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeSearchResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchEmployees([FromQuery] string? q)
    {
        // The DbContext is registered without a tenant, so its query filter is inert: without an
        // explicit predicate this searched every tenant's staff directory.
        if (_currentUserService.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        try
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(Array.Empty<EmployeeSearchResult>());

            var term = q.Trim().ToLower();
            var employees = await _employeeRepository.GetQueryable()
                .Where(e => e.TenantId == tenantId)
                .Include(e => e.Position)
                .Include(e => e.Department)
                .Include(e => e.Manager)
                .Where(e =>
                    e.FirstName.ToLower().Contains(term) ||
                    e.LastName.ToLower().Contains(term) ||
                    e.EmployeeNumber.ToLower().Contains(term) ||
                    (e.MiddleName != null && e.MiddleName.ToLower().Contains(term)))
                .Take(20)
                .ToListAsync();

            var results = employees.Select(e => new EmployeeSearchResult(
                e.Id,
                e.FullName,
                e.Position?.Title ?? string.Empty,
                e.Department?.Name ?? string.Empty,
                e.PicturePath,
                e.ManagerId,
                e.Manager?.FullName
            ));

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching employees with term '{Term}'", q);
            return StatusCode(500, "An error occurred while searching employees");
        }
    }

    #endregion

    #region PIP Review Meeting Operations

    /// <summary>
    /// Add a review meeting to a PIP
    /// </summary>
    [HttpPost("{pipId}/review-meetings")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(PipReviewMeetingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddReviewMeeting(Guid pipId, [FromBody] CreatePipReviewMeetingDto createDto)
    {
        if (!await CanManagePlanAsync(pipId)) return Forbid();

        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Whoever books the meeting is the one holding it unless they name someone else, and
            // the client has no employee id of its own to send.
            if (createDto.ConductedById == Guid.Empty && _currentUserService.EmployeeId is Guid me)
                createDto.ConductedById = me;

            var response = await _improvementPlanService.AddReviewMeetingAsync(pipId, createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "scheduling a review meeting");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding review meeting to PIP {PipId}", pipId);
            return StatusCode(500, "An error occurred while adding the review meeting");
        }
    }

    /// <summary>
    /// Get all review meetings for a PIP
    /// </summary>
    [HttpGet("{pipId}/review-meetings")]
    [ProducesResponseType(typeof(IEnumerable<PipReviewMeetingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReviewMeetings(Guid pipId)
    {
        if (!await CanAccessPlanAsync(pipId)) return Forbid();

        try
        {
            var response = await _improvementPlanService.GetReviewMeetingsAsync(pipId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review meetings for PIP {PipId}", pipId);
            return StatusCode(500, "An error occurred while retrieving review meetings");
        }
    }

    /// <summary>
    /// Get the latest review meeting for a PIP
    /// </summary>
    [HttpGet("{pipId}/review-meetings/latest")]
    [ProducesResponseType(typeof(PipReviewMeetingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestReviewMeeting(Guid pipId)
    {
        if (!await CanAccessPlanAsync(pipId)) return Forbid();

        try
        {
            var response = await _improvementPlanService.GetLatestReviewMeetingAsync(pipId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest review meeting for PIP {PipId}", pipId);
            return StatusCode(500, "An error occurred while retrieving the latest review meeting");
        }
    }

    /// <summary>
    /// Update a review meeting
    /// </summary>
    [HttpPut("{pipId}/review-meetings/{meetingId}")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(PipReviewMeetingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReviewMeeting(Guid pipId, Guid meetingId, [FromBody] UpdatePipReviewMeetingDto updateDto)
    {
        if (!await CanManagePlanAsync(pipId)) return Forbid();

        try
        {
            if (meetingId != updateDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _improvementPlanService.UpdateReviewMeetingAsync(pipId, updateDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating review meeting {MeetingId} for PIP {PipId}", updateDto.Id, pipId);
            return StatusCode(500, "An error occurred while updating the review meeting");
        }
    }

    /// <summary>
    /// Delete a review meeting
    /// </summary>
    [HttpDelete("{pipId}/review-meetings/{meetingId}")]
    [Authorize(Roles = AuthorRoles)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReviewMeeting(Guid pipId, Guid meetingId)
    {
        if (!await CanManagePlanAsync(pipId)) return Forbid();

        try
        {
            var response = await _improvementPlanService.DeleteReviewMeetingAsync(pipId, meetingId);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review meeting {MeetingId} for PIP {PipId}", meetingId, pipId);
            return StatusCode(500, "An error occurred while deleting the review meeting");
        }
    }

    #endregion
}
