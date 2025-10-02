using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Services;
using System.Security.Claims;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IReportsService _reportsService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            IReportsService reportsService,
            ICurrentUserService currentUserService,
            ILogger<ReportsController> logger)
        {
            _reportsService = reportsService;
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = _currentUserService.GetUserId();
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var reports = await _reportsService.GetReportsAsync(tenantId.Value, userId.Value, type, status, favoriteOnly);
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var report = await _reportsService.GetReportAsync(reportId, tenantId.Value);
                if (report == null)
                {
                    return NotFound();
                }

                return Ok(report);
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = _currentUserService.GetUserId();
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var report = await _reportsService.CreateReportAsync(createReportDto, tenantId.Value, userId.Value);
                
                return CreatedAtAction(nameof(GetReport), new { reportId = report.Id }, report);
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = _currentUserService.GetUserId();
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var report = await _reportsService.UpdateReportAsync(reportId, updateReportDto, tenantId.Value, userId.Value);
                
                if (report == null)
                {
                    return NotFound();
                }

                return Ok(report);
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = _currentUserService.GetUserId();
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = _currentUserService.GetUserId();
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var result = await _reportsService.ExecuteReportAsync(reportId, executeReportDto, tenantId.Value, userId.Value);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing report {ReportId}", reportId);
                return StatusCode(500, "An error occurred while executing the report");
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = _currentUserService.GetUserId();
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var exportResult = await _reportsService.ExportReportAsync(reportId, exportReportDto, tenantId.Value, userId.Value);

                return File(
                    exportResult.Data, 
                    exportResult.ContentType, 
                    exportResult.FileName);
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = _currentUserService.GetUserId();
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
                var tenantId = _currentUserService.GetTenantId();
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

        /// <summary>
        /// Get report templates
        /// </summary>
        [HttpGet("templates")]
        public async Task<ActionResult<List<ReportTemplateDto>>> GetReportTemplates(
            [FromQuery] string? category = null)
        {
            try
            {
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var templates = await _reportsService.GetReportTemplatesAsync(tenantId.Value, category);
                return Ok(templates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving report templates");
                return StatusCode(500, "An error occurred while retrieving report templates");
            }
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
                var tenantId = _currentUserService.GetTenantId();
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = _currentUserService.GetUserId();
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
    }
}