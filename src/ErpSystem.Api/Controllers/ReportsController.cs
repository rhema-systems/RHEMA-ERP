using System.Security.Claims;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IReportsService _reportsService;
        private readonly IReportTemplateLifecycleService _reportTemplateLifecycle;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            IReportsService reportsService,
            IReportTemplateLifecycleService reportTemplateLifecycle,
            ICurrentUserService currentUserService,
            ILogger<ReportsController> logger)
        {
            _reportsService = reportsService;
            _reportTemplateLifecycle = reportTemplateLifecycle;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Get all reports for the current user's tenant
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<ReportDefinitionDto>>> GetReports(
            [FromQuery] string? type = null,
            [FromQuery] string? status = null,
            [FromQuery] bool? favoriteOnly = null)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var reports = await _reportsService.GetReportsAsync(
                    tenantId.Value,
                    userId.Value,
                    type,
                    status,
                    favoriteOnly,
                    bypassRoleFiltering: IsReportAdministrator());
                return Ok(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reports");
                return StatusCode(500, "An error occurred while retrieving reports");
            }
        }

        /// <summary>
        /// Get all reports for admin/management purposes (bypasses role filtering)
        /// </summary>
        [HttpGet("admin")]
        [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
        public async Task<ActionResult<List<ReportDefinitionDto>>> GetReportsForAdmin(
            [FromQuery] string? type = null,
            [FromQuery] string? status = null)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                // Admin context: bypass role filtering, only filter by tenant
                var reports = await _reportsService.GetReportsAsync(tenantId.Value, userId.Value, type, status, null, bypassRoleFiltering: true);
                return Ok(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reports");
                return StatusCode(500, "An error occurred while retrieving reports");
            }
        }

        /// <summary>
        /// Get a specific report by ID
        /// </summary>
        [HttpGet("{reportId:guid}")]
        public async Task<ActionResult<ReportDefinitionDto>> GetReport(Guid reportId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var report = await _reportsService.GetReportAsync(reportId, tenantId.Value, userId.Value, IsReportAdministrator());
                if (report == null)
                {
                    return NotFound();
                }

                return Ok(report);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access to report {ReportId}", reportId);
                return StatusCode(403, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid report definition {ReportId}", reportId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving report {ReportId}", reportId);
                return StatusCode(500, "An error occurred while retrieving the report");
            }
        }

        /// <summary>
        /// Create a new report
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ReportDefinitionDto>> CreateReport(CreateReportDto createReportDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var report = await _reportsService.CreateReportAsync(createReportDto, tenantId.Value, userId.Value);

                return CreatedAtAction(nameof(GetReport), new { reportId = report.Id }, report);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Rejected report creation for a reserved system identifier");
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating report");
                return StatusCode(500, "An error occurred while creating the report");
            }
        }

        /// <summary>
        /// Update an existing report
        /// </summary>
        [HttpPut("{reportId:guid}")]
        public async Task<ActionResult<ReportDefinitionDto>> UpdateReport(Guid reportId, UpdateReportDto updateReportDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                // Check if user is admin (SuperAdmin role bypasses role/module filtering)
                var isAdminUser = IsReportAdministrator();

                var report = await _reportsService.UpdateReportAsync(reportId, updateReportDto, tenantId.Value, userId.Value, isAdminUser);

                if (report == null)
                {
                    return NotFound();
                }

                return Ok(report);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Rejected mutation of system report {ReportId}", reportId);
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report {ReportId}", reportId);
                return StatusCode(500, "An error occurred while updating the report");
            }
        }

        /// <summary>
        /// Delete a report
        /// </summary>
        [HttpDelete("{reportId:guid}")]
        public async Task<ActionResult> DeleteReport(Guid reportId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var success = await _reportsService.DeleteReportAsync(reportId, tenantId.Value, userId.Value);

                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Rejected deletion of system report {ReportId}", reportId);
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting report {ReportId}", reportId);
                return StatusCode(500, "An error occurred while deleting the report");
            }
        }

        /// <summary>
        /// Execute a report and get results
        /// </summary>
        [HttpPost("{reportId:guid}/execute")]
        public async Task<ActionResult<ReportResultDto>> ExecuteReport(Guid reportId, ExecuteReportDto executeReportDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                // Check if user is admin (SuperAdmin role bypasses role/module filtering)
                var isAdminUser = IsReportAdministrator();

                var result = await _reportsService.ExecuteReportAsync(reportId, executeReportDto, tenantId.Value, userId.Value, isAdminUser);

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access to report {ReportId} by user {UserId}", reportId, _currentUserService.UserId);
                return StatusCode(403, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation for report {ReportId}", reportId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing report {ReportId}", reportId);
                return StatusCode(500, "An error occurred while executing the report. Please try again or contact support if the issue persists.");
            }
        }

        /// <summary>
        /// Export a report in specified format
        /// </summary>
        [HttpPost("{reportId:guid}/export")]
        public async Task<ActionResult> ExportReport(Guid reportId, ExportReportDto exportReportDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                // Check if user is admin (SuperAdmin role bypasses role/module filtering)
                var isAdminUser = IsReportAdministrator();

                var exportResult = await _reportsService.ExportReportAsync(reportId, exportReportDto, tenantId.Value, userId.Value, isAdminUser);

                return File(
                    exportResult.Data,
                    exportResult.ContentType,
                    exportResult.FileName);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized export of report {ReportId}", reportId);
                return StatusCode(403, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid export request for report {ReportId}", reportId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report {ReportId}", reportId);
                return StatusCode(500, "An error occurred while exporting the report");
            }
        }

        /// <summary>
        /// Schedule a report for automated execution
        /// </summary>
        [HttpPost("{reportId:guid}/schedule")]
        public async Task<ActionResult<ReportScheduleDto>> ScheduleReport(Guid reportId, CreateReportScheduleDto scheduleDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var schedule = await _reportsService.ScheduleReportAsync(reportId, scheduleDto, tenantId.Value, userId.Value);

                return CreatedAtAction(nameof(GetReportSchedule), new { reportId, scheduleId = schedule.Id }, schedule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling report {ReportId}", reportId);
                return StatusCode(500, "An error occurred while scheduling the report");
            }
        }

        /// <summary>
        /// Get report schedule
        /// </summary>
        [HttpGet("{reportId:guid}/schedule/{scheduleId:guid}")]
        public async Task<ActionResult<ReportScheduleDto>> GetReportSchedule(Guid reportId, Guid scheduleId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var schedule = await _reportsService.GetReportScheduleAsync(scheduleId, tenantId.Value);
                if (schedule == null)
                {
                    return NotFound();
                }

                return Ok(schedule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving report schedule {ScheduleId}", scheduleId);
                return StatusCode(500, "An error occurred while retrieving the report schedule");
            }
        }

        [HttpGet("templates")]
        public async Task<ActionResult<IReadOnlyList<ReportTemplateDto>>> GetReportTemplates(
            [FromQuery] string? audience = null,
            [FromQuery] string? cadence = null,
            [FromQuery] string? status = null,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetActor(out var tenantId, out var userId, out var error)) return error!;
            try
            {
                return Ok(await _reportTemplateLifecycle.GetAsync(
                    tenantId, userId, IsReportAdministrator(), audience, cadence, status, cancellationToken));
            }
            catch (Exception exception) { return TemplateFailure(exception, "list"); }
        }

        [HttpGet("templates/{templateId:guid}")]
        public async Task<ActionResult<ReportTemplateDto>> GetReportTemplate(
            Guid templateId, CancellationToken cancellationToken)
        {
            if (!TryGetActor(out var tenantId, out var userId, out var error)) return error!;
            try
            {
                var template = await _reportTemplateLifecycle.GetByIdAsync(
                    templateId, tenantId, userId, IsReportAdministrator(), cancellationToken);
                return template is null ? NotFound() : Ok(template);
            }
            catch (Exception exception) { return TemplateFailure(exception, "read"); }
        }

        [HttpPost("templates")]
        [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
        public async Task<ActionResult<ReportTemplateDto>> CreateReportTemplate(
            CreateReportTemplateDto request, CancellationToken cancellationToken)
        {
            if (!TryGetActor(out var tenantId, out var userId, out var error)) return error!;
            try
            {
                var template = await _reportTemplateLifecycle.CreateAsync(
                    request, tenantId, userId, IsReportAdministrator(), cancellationToken);
                return CreatedAtAction(nameof(GetReportTemplate), new { templateId = template.Id }, template);
            }
            catch (Exception exception) { return TemplateFailure(exception, "create"); }
        }

        [HttpPut("templates/{templateId:guid}")]
        [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
        public async Task<ActionResult<ReportTemplateDto>> UpdateReportTemplate(
            Guid templateId, UpdateReportTemplateDto request, CancellationToken cancellationToken)
        {
            if (!TryGetActor(out var tenantId, out var userId, out var error)) return error!;
            try
            {
                return Ok(await _reportTemplateLifecycle.UpdateAsync(
                    templateId, request, tenantId, userId, IsReportAdministrator(), cancellationToken));
            }
            catch (Exception exception) { return TemplateFailure(exception, "update"); }
        }

        [HttpPost("templates/{templateId:guid}/publish")]
        [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
        public async Task<ActionResult<ReportTemplateDto>> PublishReportTemplate(
            Guid templateId, ReportTemplateLifecycleActionDto request, CancellationToken cancellationToken) =>
            await MutateTemplate(templateId, request, "publish", cancellationToken);

        [HttpPost("templates/{templateId:guid}/archive")]
        [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
        public async Task<ActionResult<ReportTemplateDto>> ArchiveReportTemplate(
            Guid templateId, ReportTemplateLifecycleActionDto request, CancellationToken cancellationToken) =>
            await MutateTemplate(templateId, request, "archive", cancellationToken);

        [HttpPost("templates/{templateId:guid}/clone")]
        [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
        public async Task<ActionResult<ReportTemplateDto>> CloneReportTemplate(
            Guid templateId, CloneReportTemplateDto request, CancellationToken cancellationToken)
        {
            if (!TryGetActor(out var tenantId, out var userId, out var error)) return error!;
            try
            {
                return Ok(await _reportTemplateLifecycle.CloneAsync(
                    templateId, request, tenantId, userId, IsReportAdministrator(), cancellationToken));
            }
            catch (Exception exception) { return TemplateFailure(exception, "clone"); }
        }

        [HttpDelete("templates/{templateId:guid}")]
        [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
        public async Task<IActionResult> DeleteReportTemplate(
            Guid templateId, [FromBody] ReportTemplateLifecycleActionDto request, CancellationToken cancellationToken)
        {
            if (!TryGetActor(out var tenantId, out var userId, out var error)) return error!;
            try
            {
                await _reportTemplateLifecycle.DeleteAsync(
                    templateId, request, tenantId, userId, IsReportAdministrator(), cancellationToken);
                return NoContent();
            }
            catch (Exception exception) { return TemplateFailure(exception, "delete"); }
        }

        [HttpPost("templates/{templateId:guid}/execute")]
        public async Task<ActionResult<ReportResultDto>> ExecuteReportTemplate(
            Guid templateId, GenerateReportTemplateDto request, CancellationToken cancellationToken)
        {
            if (!TryGetActor(out var tenantId, out var userId, out var error)) return error!;
            try
            {
                return Ok(await _reportTemplateLifecycle.ExecuteAsync(
                    templateId, request, tenantId, userId, IsReportAdministrator(), cancellationToken));
            }
            catch (Exception exception) { return TemplateFailure(exception, "execute"); }
        }

        [HttpPost("templates/{templateId:guid}/export")]
        public async Task<IActionResult> ExportReportTemplate(
            Guid templateId, GenerateReportTemplateDto request, CancellationToken cancellationToken)
        {
            if (!TryGetActor(out var tenantId, out var userId, out var error)) return error!;
            try
            {
                var export = await _reportTemplateLifecycle.ExportAsync(
                    templateId, request, tenantId, userId, IsReportAdministrator(), cancellationToken);
                return File(export.Data, export.ContentType, export.FileName);
            }
            catch (Exception exception) { return TemplateFailure(exception, "export"); }
        }

        /// <summary>
        /// Get analytics data
        /// </summary>
        [HttpGet("analytics")]
        public async Task<ActionResult<ReportAnalyticsDto>> GetReportAnalytics(
            [FromQuery] string period = "last-30-days",
            [FromQuery] string? tenantFilter = null)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var isSuperAdmin = User.IsInRole("SuperAdmin");
                var analytics = await _reportsService.GetReportAnalyticsAsync(tenantId.Value, period, tenantFilter, isSuperAdmin);

                return Ok(analytics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving report analytics");
                return StatusCode(500, "An error occurred while retrieving report analytics");
            }
        }

        /// <summary>
        /// Toggle report favorite status
        /// </summary>
        [HttpPost("{reportId:guid}/favorite")]
        public async Task<ActionResult> ToggleFavorite(Guid reportId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var isFavorite = await _reportsService.ToggleFavoriteAsync(reportId, tenantId.Value, userId.Value);

                return Ok(new { isFavorite });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling favorite for report {ReportId}", reportId);
                return StatusCode(500, "An error occurred while updating favorite status");
            }
        }

        /// <summary>
        /// Publish a report
        /// </summary>
        [HttpPost("{reportId:guid}/publish")]
        public async Task<ActionResult> PublishReport(Guid reportId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var result = await _reportsService.PublishReportAsync(reportId, tenantId.Value, userId.Value);

                return Ok(new { reportId, publishedAt = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing report {ReportId}", reportId);
                return StatusCode(500, "An error occurred while publishing the report");
            }
        }

        /// <summary>
        /// Unpublish a report
        /// </summary>
        [HttpPost("{reportId:guid}/unpublish")]
        public async Task<ActionResult> UnpublishReport(Guid reportId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var result = await _reportsService.UnpublishReportAsync(reportId, tenantId.Value, userId.Value);

                return Ok(new { reportId, unpublishedAt = result });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Rejected unpublish of system report {ReportId}", reportId);
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpublishing report {ReportId}", reportId);
                return StatusCode(500, "An error occurred while unpublishing the report");
            }
        }

        /// <summary>
        /// Assign a report to a module
        /// </summary>
        [HttpPost("{reportId:guid}/assign-module")]
        public async Task<ActionResult> AssignReportToModule(Guid reportId, [FromBody] AssignModuleDto assignModuleDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var result = await _reportsService.AssignReportToModuleAsync(reportId, assignModuleDto.ModuleId, tenantId.Value, userId.Value);

                return Ok(new { reportId, moduleId = assignModuleDto.ModuleId, assignedAt = result });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Rejected module reassignment of system report {ReportId}", reportId);
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning report {ReportId} to module {ModuleId}", reportId, assignModuleDto?.ModuleId);
                return StatusCode(500, "An error occurred while assigning the report to module");
            }
        }

        /// <summary>
        /// Unassign a report from its current module
        /// </summary>
        [HttpPost("{reportId:guid}/unassign-module")]
        public async Task<ActionResult> UnassignReportFromModule(Guid reportId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var result = await _reportsService.UnassignReportFromModuleAsync(reportId, tenantId.Value, userId.Value);

                return Ok(new { reportId, unassignedAt = result });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Rejected module removal from system report {ReportId}", reportId);
                return Conflict(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning report {ReportId} from module", reportId);
                return StatusCode(500, "An error occurred while unassigning the report from module");
            }
        }

        private async Task<ActionResult<ReportTemplateDto>> MutateTemplate(
            Guid templateId,
            ReportTemplateLifecycleActionDto request,
            string action,
            CancellationToken cancellationToken)
        {
            if (!TryGetActor(out var tenantId, out var userId, out var error)) return error!;
            try
            {
                var result = action == "publish"
                    ? await _reportTemplateLifecycle.PublishAsync(
                        templateId, request, tenantId, userId, IsReportAdministrator(), cancellationToken)
                    : await _reportTemplateLifecycle.ArchiveAsync(
                        templateId, request, tenantId, userId, IsReportAdministrator(), cancellationToken);
                return Ok(result);
            }
            catch (Exception exception) { return TemplateFailure(exception, action); }
        }

        private bool TryGetActor(out Guid tenantId, out Guid userId, out ActionResult? error)
        {
            tenantId = _currentUserService.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
            {
                userId = Guid.Empty;
                error = BadRequest(new { code = "REPORT_TEMPLATE_TENANT_REQUIRED", message = "TenantId not found in token." });
                return false;
            }

            if (!Guid.TryParse(_currentUserService.UserId, out userId))
            {
                error = BadRequest(new { code = "REPORT_TEMPLATE_USER_REQUIRED", message = "UserId not found in token." });
                return false;
            }

            error = null;
            return true;
        }

        private ObjectResult TemplateFailure(Exception exception, string action)
        {
            _logger.LogWarning(exception, "Report-template {Action} failed", action);
            var (status, code) = exception switch
            {
                KeyNotFoundException => (StatusCodes.Status404NotFound, "REPORT_TEMPLATE_NOT_FOUND"),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "REPORT_TEMPLATE_FORBIDDEN"),
                InvalidOperationException => (StatusCodes.Status409Conflict, "REPORT_TEMPLATE_CONFLICT"),
                _ => (StatusCodes.Status500InternalServerError, "REPORT_TEMPLATE_FAILURE")
            };
            return StatusCode(status, new
            {
                code,
                message = status == StatusCodes.Status500InternalServerError
                    ? "The report-template operation failed."
                    : exception.Message
            });
        }

        private bool IsReportAdministrator() =>
            _currentUserService.IsInRole(Constants.Roles.SuperAdmin) ||
            _currentUserService.IsInRole(Constants.Roles.TenantAdmin);
    }
}
