using System.Security.Claims;
using ErpSystem.Api.Services;
using ErpSystem.Core.DTOs.Dashboard;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly EnterpriseDashboardService _enterpriseDashboardService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            IDashboardService dashboardService,
            EnterpriseDashboardService enterpriseDashboardService,
            ICurrentUserService currentUserService,
            ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _enterpriseDashboardService = enterpriseDashboardService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Get comprehensive dashboard data for the current user's tenant
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<DashboardDataDto>> GetDashboardData()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                // Check if user is SuperAdmin
                var isSuperAdmin = User.IsInRole("SuperAdmin");
                _logger.LogInformation("Dashboard data requested by user with SuperAdmin role: {IsSuperAdmin}", isSuperAdmin);

                var data = await _dashboardService.GetDashboardDataAsync(tenantId.Value.ToString(), isSuperAdmin);
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard data");
                return StatusCode(500, "An error occurred while retrieving dashboard data");
            }
        }

        /// <summary>
        /// Get the enterprise business dashboard aggregate used by the main dashboard page.
        /// </summary>
        [HttpGet("enterprise")]
        [Authorize(Policy = "InternalOnly")]
        [Authorize(Policy = "dashboard.read")]
        public async Task<ActionResult<EnterpriseDashboardDto>> GetEnterpriseDashboard(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? warehouseId = null,
            [FromQuery] Guid? locationId = null)
        {
            if (startDate.HasValue != endDate.HasValue)
            {
                return BadRequest("startDate and endDate must be supplied together.");
            }

            if (startDate.HasValue && endDate.HasValue)
            {
                if (startDate.Value.Date > endDate.Value.Date)
                {
                    return BadRequest("startDate cannot be later than endDate.");
                }

                if ((endDate.Value.Date - startDate.Value.Date).TotalDays > 3660)
                {
                    return BadRequest("The dashboard date range cannot exceed 10 years.");
                }
            }

            if (locationId.HasValue && !warehouseId.HasValue)
            {
                return BadRequest("warehouseId is required when locationId is supplied.");
            }

            try
            {
                var data = await _enterpriseDashboardService.GetEnterpriseDashboardAsync(
                    startDate, endDate, warehouseId, locationId);
                return Ok(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving enterprise dashboard data");
                return StatusCode(500, "An error occurred while retrieving enterprise dashboard data");
            }
        }

        /// <summary>
        /// Get dashboard metrics only
        /// </summary>
        [HttpGet("metrics")]
        public async Task<ActionResult<DashboardMetricsDto>> GetDashboardMetrics()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var isSuperAdmin = User.IsInRole("SuperAdmin");
                var data = await _dashboardService.GetDashboardDataAsync(tenantId.Value.ToString(), isSuperAdmin);
                return Ok(data.Metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard metrics");
                return StatusCode(500, "An error occurred while retrieving dashboard metrics");
            }
        }

        /// <summary>
        /// Get recent activities only
        /// </summary>
        [HttpGet("activities")]
        public async Task<ActionResult<List<RecentActivityDto>>> GetRecentActivities()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var isSuperAdmin = User.IsInRole("SuperAdmin");
                var data = await _dashboardService.GetDashboardDataAsync(tenantId.Value.ToString(), isSuperAdmin);
                return Ok(data.RecentActivities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent activities");
                return StatusCode(500, "An error occurred while retrieving recent activities");
            }
        }

        /// <summary>
        /// Get online users only
        /// </summary>
        [HttpGet("online-users")]
        public async Task<ActionResult<List<OnlineUserDto>>> GetOnlineUsers()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var isSuperAdmin = User.IsInRole("SuperAdmin");
                var data = await _dashboardService.GetDashboardDataAsync(tenantId.Value.ToString(), isSuperAdmin);
                return Ok(data.OnlineUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving online users");
                return StatusCode(500, "An error occurred while retrieving online users");
            }
        }

        /// <summary>
        /// Get system status only
        /// </summary>
        [HttpGet("system-status")]
        public async Task<ActionResult<SystemStatusDto>> GetSystemStatus()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var isSuperAdmin = User.IsInRole("SuperAdmin");
                var data = await _dashboardService.GetDashboardDataAsync(tenantId.Value.ToString(), isSuperAdmin);
                return Ok(data.SystemStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system status");
                return StatusCode(500, "An error occurred while retrieving system status");
            }
        }

        /// <summary>
        /// Trigger manual dashboard update broadcast (for testing or admin purposes)
        /// </summary>
        [HttpPost("broadcast-update")]
        [Authorize(Roles = "SuperAdmin,TenantAdmin")]
        public async Task<ActionResult> BroadcastDashboardUpdate()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var isSuperAdmin = User.IsInRole("SuperAdmin");
                var data = await _dashboardService.GetDashboardDataAsync(tenantId.Value.ToString(), isSuperAdmin);
                await _dashboardService.BroadcastDashboardUpdateAsync(tenantId.Value.ToString(), data);

                return Ok(new { message = "Dashboard update broadcasted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting dashboard update");
                return StatusCode(500, "An error occurred while broadcasting dashboard update");
            }
        }

        /// <summary>
        /// Send test notification to current user
        /// </summary>
        [HttpPost("send-test-notification")]
        public async Task<ActionResult> SendTestNotification([FromBody] NotificationDto notification)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest("UserId not found in token");
                }

                // Set some default values for test notification
                notification.Id = Guid.NewGuid().ToString();
                notification.Timestamp = DateTime.UtcNow;

                await _dashboardService.BroadcastNotificationAsync(userId, notification);

                return Ok(new { message = "Test notification sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test notification");
                return StatusCode(500, "An error occurred while sending test notification");
            }
        }
    }
}
