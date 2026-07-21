using ErpSystem.Api.Models;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Route("api/Pip")]
[Authorize]
public class PerformanceImprovementPlansController : ControllerBase
{
    private readonly IPerformanceImprovementPlanService _improvementPlanService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly ILogger<PerformanceImprovementPlansController> _logger;

    public PerformanceImprovementPlansController(
        IPerformanceImprovementPlanService improvementPlanService,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUserService,
        IGenericRepository<Employee> employeeRepository,
        ILogger<PerformanceImprovementPlansController> logger)
    {
        _improvementPlanService = improvementPlanService;
        _fileStorageService = fileStorageService;
        _currentUserService = currentUserService;
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all performance improvement plans
    /// </summary>
    [HttpGet]
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
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
    /// Get PIPs by employee ID
    /// </summary>
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<PerformanceImprovementPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEmployeeId(Guid employeeId)
    {
        try
        {
            var response = await _improvementPlanService.GetByEmployeeIdAsync(employeeId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance improvement plans");
            return StatusCode(500, "An error occurred while retrieving performance improvement plans");
        }
    }

    /// <summary>
    /// Get PIPs by status
    /// </summary>
    [HttpGet("status/{status}")]
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
    /// Create a new PIP
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
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
    [ProducesResponseType(typeof(PerformanceImprovementPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PipUpdateRequest req)
    {
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
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating performance improvement plan with Id {PipId}", id);
            return StatusCode(500, "An error occurred while updating the performance improvement plan");
        }
    }

    /// <summary>
    /// Update PIP status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdatePipStatusDto statusDto)
    {
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
            return NotFound(ex.Message);
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
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            return NotFound(ex.Message);
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
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var response = await _improvementPlanService.DeleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
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
    [ProducesResponseType(typeof(PipPrepareResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Prepare([FromQuery] Guid employeeId, [FromQuery] Guid? appraisalId)
    {
        try
        {
            var employee = await _employeeRepository.GetQueryable(e => e.Id == employeeId)
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id)
    {
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
    public async Task<IActionResult> GetGoals(Guid pipId)
    {
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
    [ProducesResponseType(typeof(PipGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddGoal(Guid pipId, [FromBody] PipGoalRequest req)
    {
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
    [ProducesResponseType(typeof(PipGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGoal(Guid goalId, [FromBody] PipGoalRequest req)
    {
        try
        {
            var existingGoal = await _improvementPlanService.GetGoalByIdAsync(goalId);
            if (existingGoal == null)
                return NotFound("Goal not found");

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
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGoal(Guid goalId)
    {
        try
        {
            var existingGoal = await _improvementPlanService.GetGoalByIdAsync(goalId);
            if (existingGoal == null)
                return NotFound("Goal not found");

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
    [ProducesResponseType(typeof(PipAttachmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadAttachment(Guid pipId, IFormFile file, [FromForm] string? description)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided");

            var uploadedById = _currentUserService.EmployeeId ?? Guid.Empty;
            if (uploadedById == Guid.Empty)
                return Unauthorized("User employee context not found");

            await using var stream = file.OpenReadStream();
            var uploadRequest = new FileUploadRequest
            {
                FileStream  = stream,
                FileName    = file.FileName,
                ContentType = file.ContentType,
                FileSize    = file.Length,
                Category    = "pip-attachments",
                TenantId    = _currentUserService.TenantId?.ToString(),
            };

            var storageResult = await _fileStorageService.UploadFileAsync(uploadRequest);
            if (!storageResult.Success)
                return StatusCode(500, storageResult.ErrorMessage ?? "File upload failed");

            var dto = await _improvementPlanService.CreatePipAttachmentAsync(
                pipId, uploadedById, file.FileName, storageResult.FilePath,
                storageResult.PublicUrl, file.Length, description);

            return Ok(new PipAttachmentResponse
            {
                AttachmentId   = dto.Id,
                FileName       = dto.FileName,
                FileSizeBytes  = dto.FileSizeBytes,
                Description    = dto.Description,
                UploadDate     = dto.UploadDate,
                UploadedByName = dto.UploadedByName,
                PublicUrl      = storageResult.PublicUrl,
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading attachment for PIP {PipId}", pipId);
            return StatusCode(500, "An error occurred while uploading the attachment");
        }
    }

    [HttpDelete("attachments/{attachmentId:guid}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid attachmentId)
    {
        try
        {
            var attachment = await _improvementPlanService.GetAttachmentByIdAsync(attachmentId);
            if (attachment == null)
                return NotFound("Attachment not found");

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

    [HttpPost("{pipId:guid}/outcome")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordOutcome(Guid pipId, [FromBody] PipOutcomeRequest req)
    {
        try
        {
            var completeDto = new CompletePipDto
            {
                PipId        = pipId,
                Outcome      = (PipOutcome)req.Outcome,
                OutcomeNotes = req.Notes ?? string.Empty,
            };

            var result = await _improvementPlanService.CompletePipAsync(completeDto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording outcome for PIP {PipId}", pipId);
            return StatusCode(500, "An error occurred while recording the outcome");
        }
    }

    // ── Employee search ───────────────────────────────────────────────────────

    [HttpGet("employees/search")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeSearchResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchEmployees([FromQuery] string? q)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(Array.Empty<EmployeeSearchResult>());

            var term = q.Trim().ToLower();
            var employees = await _employeeRepository.GetQueryable()
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
    [ProducesResponseType(typeof(PipReviewMeetingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddReviewMeeting(Guid pipId, [FromBody] CreatePipReviewMeetingDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var response = await _improvementPlanService.AddReviewMeetingAsync(pipId, createDto);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
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
    public async Task<IActionResult> GetReviewMeetings(Guid pipId)
    {
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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestReviewMeeting(Guid pipId)
    {
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
    [ProducesResponseType(typeof(PipReviewMeetingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReviewMeeting(Guid pipId, Guid meetingId, [FromBody] UpdatePipReviewMeetingDto updateDto)
    {
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
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReviewMeeting(Guid pipId, Guid meetingId)
    {
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
