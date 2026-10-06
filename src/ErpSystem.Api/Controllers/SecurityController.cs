using ErpSystem.Api.Services;
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
    public class SecurityController : ControllerBase
    {
        private readonly ISecurityService _securityService;
        private readonly ISecurityOperationsService _securityOperationsService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SecurityController> _logger;

        public SecurityController(
            ISecurityService securityService,
            ISecurityOperationsService securityOperationsService,
            ICurrentUserService currentUserService,
            ILogger<SecurityController> logger)
        {
            _securityService = securityService;
            _securityOperationsService = securityOperationsService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Consolidated tenant-scoped security operations overview backed by persisted evidence.
        /// </summary>
        [HttpGet("operations/overview")]
        [Authorize(Policy = "SecurityManagementRead")]
        public async Task<IActionResult> GetOperationsOverview(
            [FromQuery] string range = "24h",
            [FromQuery] DateTime? startUtc = null,
            [FromQuery] DateTime? endUtc = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return Ok(await _securityOperationsService.GetOverviewAsync(
                    range,
                    startUtc,
                    endUtc,
                    cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid security analytics range",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        /// <summary>
        /// Server-filtered and paged tenant security events.
        /// </summary>
        [HttpGet("operations/events")]
        [Authorize(Policy = "SecurityManagementRead")]
        public async Task<IActionResult> GetOperationsEvents(
            [FromQuery] SecurityEventQueryDto query,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _securityOperationsService.GetEventsAsync(query, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid security event query",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        /// <summary>
        /// Get security metrics for the current tenant
        /// </summary>
        [HttpGet("metrics")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetSecurityMetrics()
        {
            try
            {
                var metrics = await _securityService.GetSecurityMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security metrics");
                return StatusCode(500, new { message = "An error occurred while retrieving security metrics" });
            }
        }

        /// <summary>
        /// Recalculate and cache security metrics
        /// </summary>
        [HttpPost("metrics/recalculate")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin}")]
        public async Task<IActionResult> RecalculateSecurityMetrics()
        {
            try
            {
                var metrics = await _securityService.CalculateAndCacheSecurityMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating security metrics");
                return StatusCode(500, new { message = "An error occurred while recalculating security metrics" });
            }
        }

        /// <summary>
        /// Get security alerts for the current tenant
        /// </summary>
        [HttpGet("alerts")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetSecurityAlerts([FromQuery] bool dismissed = false)
        {
            try
            {
                var alerts = await _securityService.GetSecurityAlertsAsync(dismissed);
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security alerts");
                return StatusCode(500, new { message = "An error occurred while retrieving security alerts" });
            }
        }

        /// <summary>
        /// Create a new security alert
        /// </summary>
        [HttpPost("alerts")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin}")]
        public async Task<IActionResult> CreateSecurityAlert([FromBody] CreateSecurityAlertRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var alert = await _securityService.CreateSecurityAlertAsync(request);
                return CreatedAtAction(nameof(GetSecurityAlerts), new { id = alert.Id }, alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating security alert");
                return StatusCode(500, new { message = "An error occurred while creating the security alert" });
            }
        }

        /// <summary>
        /// Dismiss a security alert
        /// </summary>
        [HttpPost("alerts/{alertId}/dismiss")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> DismissSecurityAlert(string alertId)
        {
            try
            {
                if (!Guid.TryParse(alertId, out var alertGuid))
                {
                    return BadRequest(new { message = "Invalid alert ID format" });
                }

                await _securityService.DismissSecurityAlertAsync(alertGuid);
                return Ok(new { message = "Alert dismissed successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dismissing security alert {AlertId}", alertId);
                return StatusCode(500, new { message = "An error occurred while dismissing the security alert" });
            }
        }

        /// <summary>
        /// Get audit logs with filtering and pagination
        /// </summary>
        [HttpGet("audit-logs")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogFilterDto filter)
        {
            try
            {
                var auditLogs = await _securityService.GetAuditLogsAsync(filter ?? new AuditLogFilterDto());
                return Ok(auditLogs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs");
                return StatusCode(500, new { message = "An error occurred while retrieving audit logs" });
            }
        }

        /// <summary>
        /// Get device sessions for security monitoring
        /// </summary>
        [HttpGet("device-sessions")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetDeviceSessions()
        {
            try
            {
                var deviceSessions = await _securityService.GetDeviceSessionsAsync();
                return Ok(deviceSessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device sessions");
                return StatusCode(500, new { message = "An error occurred while retrieving device sessions" });
            }
        }

        /// <summary>
        /// Get security health score and recommendations
        /// </summary>
        [HttpGet("health-score")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetSecurityHealthScore()
        {
            try
            {
                var healthScore = await _securityService.GetSecurityHealthScoreAsync();
                return Ok(healthScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security health score");
                return StatusCode(500, new { message = "An error occurred while calculating security health score" });
            }
        }

        /// <summary>
        /// Get security dashboard summary (combines metrics, alerts, and health score)
        /// </summary>
        [HttpGet("dashboard")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetSecurityDashboard()
        {
            try
            {
                var metrics = await _securityService.GetSecurityMetricsAsync();
                var alerts = await _securityService.GetSecurityAlertsAsync(false); // Get only non-dismissed alerts
                var healthScore = await _securityService.GetSecurityHealthScoreAsync();

                var dashboard = new
                {
                    Metrics = metrics,
                    Alerts = alerts.Take(5).ToList(), // Only latest 5 alerts for dashboard
                    HealthScore = healthScore,
                    Summary = new
                    {
                        TotalAlerts = alerts.Count,
                        CriticalAlerts = alerts.Count(a => a.Type == "critical"),
                        OverallHealthScore = healthScore.Overall,
                        LastUpdated = DateTime.UtcNow.ToString("o")
                    }
                };

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security dashboard");
                return StatusCode(500, new { message = "An error occurred while retrieving security dashboard" });
            }
        }

        /// <summary>
        /// Get security statistics for reporting
        /// </summary>
        [HttpGet("statistics")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin}")]
        public async Task<IActionResult> GetSecurityStatistics([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            try
            {
                var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
                var to = toDate ?? DateTime.UtcNow;

                var filter = new AuditLogFilterDto
                {
                    StartDate = from,
                    EndDate = to,
                    PageSize = 1000 // Get more for statistics
                };

                var auditLogs = await _securityService.GetAuditLogsAsync(filter);
                var metrics = await _securityService.GetSecurityMetricsAsync();
                var alerts = await _securityService.GetSecurityAlertsAsync();

                var statistics = new
                {
                    Period = new { From = from, To = to },
                    AuditActivity = new
                    {
                        TotalEvents = auditLogs.Total,
                        SuccessfulEvents = auditLogs.Entries.Count(e => e.Result == "success"),
                        FailedEvents = auditLogs.Entries.Count(e => e.Result == "failure"),
                        HighRiskEvents = auditLogs.Entries.Count(e => e.Risk == "high")
                    },
                    SecurityMetrics = metrics,
                    AlertSummary = new
                    {
                        TotalAlerts = alerts.Count,
                        DismissedAlerts = alerts.Count(a => a.Dismissed),
                        ActiveAlerts = alerts.Count(a => !a.Dismissed),
                        CriticalAlerts = alerts.Count(a => a.Type == "critical"),
                        WarningAlerts = alerts.Count(a => a.Type == "warning"),
                        InfoAlerts = alerts.Count(a => a.Type == "info")
                    }
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security statistics");
                return StatusCode(500, new { message = "An error occurred while retrieving security statistics" });
            }
        }

        /// <summary>
        /// Get threat detections
        /// </summary>
        [HttpGet("threats")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetThreatDetections()
        {
            try
            {
                var threats = await _securityService.GetThreatDetectionsAsync();
                return Ok(threats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting threat detections");
                return StatusCode(500, new { message = "An error occurred while retrieving threat detections" });
            }
        }

        /// <summary>
        /// Update threat detection status
        /// </summary>
        [HttpPatch("threats/{threatId}")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> UpdateThreatStatus(string threatId, [FromBody] UpdateThreatStatusRequest request)
        {
            try
            {
                if (!Guid.TryParse(threatId, out var threatGuid))
                {
                    return BadRequest(new { message = "Invalid threat ID format" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                await _securityService.UpdateThreatStatusAsync(threatGuid, request);
                return Ok(new { message = "Threat status updated successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating threat status {ThreatId}", threatId);
                return StatusCode(500, new { message = "An error occurred while updating threat status" });
            }
        }

        /// <summary>
        /// Get Two-Factor Authentication settings for current user
        /// </summary>
        [HttpGet("two-factor")]
        public async Task<IActionResult> GetTwoFactorSettings()
        {
            try
            {
                var settings = await _securityService.GetTwoFactorSettingsAsync();
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting two-factor settings");
                return StatusCode(500, new { message = "An error occurred while retrieving two-factor settings" });
            }
        }

        /// <summary>
        /// Enable Two-Factor Authentication for current user
        /// </summary>
        [HttpPost("two-factor/enable")]
        public async Task<IActionResult> EnableTwoFactor([FromBody] EnableTwoFactorRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var result = await _securityService.EnableTwoFactorAsync(request);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation failed during two-factor authentication setup");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling two-factor authentication");
                return StatusCode(500, new { message = "An error occurred while enabling two-factor authentication" });
            }
        }

        /// <summary>
        /// Disable Two-Factor Authentication for current user
        /// </summary>
        [HttpPost("two-factor/disable")]
        public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                await _securityService.DisableTwoFactorAsync(request);
                return Ok(new { message = "Two-factor authentication disabled successfully" });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation failed during two-factor authentication disable");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling two-factor authentication");
                return StatusCode(500, new { message = "An error occurred while disabling two-factor authentication" });
            }
        }

        /// <summary>
        /// Get security policies for current tenant
        /// </summary>
        [HttpGet("policies")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetSecurityPolicies()
        {
            try
            {
                var policies = await _securityService.GetSecurityPoliciesAsync();
                return Ok(policies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting security policies");
                return StatusCode(500, new { message = "An error occurred while retrieving security policies" });
            }
        }

        /// <summary>
        /// Create a new security policy
        /// </summary>
        [HttpPost("policies")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin}")]
        public async Task<IActionResult> CreateSecurityPolicy([FromBody] CreateSecurityPolicyRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var policy = await _securityService.CreateSecurityPolicyAsync(request);
                return CreatedAtAction(nameof(GetSecurityPolicies), new { id = policy.Id }, policy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating security policy");
                return StatusCode(500, new { message = "An error occurred while creating the security policy" });
            }
        }

        /// <summary>
        /// Update a security policy
        /// </summary>
        [HttpPut("policies/{policyId}")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin}")]
        public async Task<IActionResult> UpdateSecurityPolicy(string policyId, [FromBody] UpdateSecurityPolicyRequest request)
        {
            try
            {
                if (!Guid.TryParse(policyId, out var policyGuid))
                {
                    return BadRequest(new { message = "Invalid policy ID format" });
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var policy = await _securityService.UpdateSecurityPolicyAsync(policyGuid, request);
                return Ok(policy);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating security policy {PolicyId}", policyId);
                return StatusCode(500, new { message = "An error occurred while updating the security policy" });
            }
        }

        /// <summary>
        /// Get detailed information about a specific device session
        /// </summary>
        [HttpGet("device-sessions/{sessionId}")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetDeviceSessionDetails(string sessionId)
        {
            try
            {
                var sessionDetails = await _securityService.GetDeviceSessionDetailsAsync(sessionId);
                if (sessionDetails == null)
                {
                    return NotFound(new { message = "Device session not found" });
                }

                return Ok(sessionDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device session details {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while retrieving device session details" });
            }
        }

        /// <summary>
        /// Terminate a specific device session
        /// </summary>
        [HttpDelete("device-sessions/{sessionId}")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> TerminateDeviceSession(string sessionId, [FromQuery] string reason = "Manual termination")
        {
            try
            {
                var success = await _securityService.TerminateDeviceSessionAsync(sessionId, reason);
                if (!success)
                {
                    return NotFound(new { message = "Device session not found or could not be terminated" });
                }

                return Ok(new { message = "Device session terminated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating device session {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while terminating the device session" });
            }
        }

        /// <summary>
        /// Terminate all device sessions except the current one
        /// </summary>
        [HttpPost("device-sessions/terminate-others")]
        [Authorize]
        public async Task<IActionResult> TerminateAllOtherDeviceSessions([FromBody] TerminateOtherSessionsRequest request)
        {
            try
            {
                var terminatedCount = await _securityService.TerminateAllDeviceSessionsExceptCurrentAsync(
                    request.CurrentSessionId,
                    request.Reason ?? "Terminate other sessions");

                return Ok(new
                {
                    message = $"Successfully terminated {terminatedCount} device sessions",
                    terminatedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating other device sessions");
                return StatusCode(500, new { message = "An error occurred while terminating other device sessions" });
            }
        }

        /// <summary>
        /// Get device session statistics for the current user
        /// </summary>
        [HttpGet("device-sessions/stats")]
        [Authorize]
        public async Task<IActionResult> GetDeviceSessionStats([FromQuery] int days = 30)
        {
            try
            {
                var stats = await _securityService.GetDeviceSessionStatsAsync(days);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting device session stats");
                return StatusCode(500, new { message = "An error occurred while retrieving device session statistics" });
            }
        }

        /// <summary>
        /// Get recent device sessions for the current user
        /// </summary>
        [HttpGet("device-sessions/recent")]
        [Authorize]
        public async Task<IActionResult> GetRecentDeviceSessions([FromQuery] int count = 10)
        {
            try
            {
                var recentSessions = await _securityService.GetRecentDeviceSessionsAsync(count);
                return Ok(recentSessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent device sessions");
                return StatusCode(500, new { message = "An error occurred while retrieving recent device sessions" });
            }
        }
    }

    public class TerminateOtherSessionsRequest
    {
        public string CurrentSessionId { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}
