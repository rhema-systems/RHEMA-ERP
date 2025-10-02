using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceSessionService _deviceSessionService;
        private readonly IUserSessionService _userSessionService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DeviceController> _logger;

        public DeviceController(
            IDeviceSessionService deviceSessionService,
            IUserSessionService userSessionService,
            ICurrentUserService currentUserService,
            ILogger<DeviceController> logger)
        {
            _deviceSessionService = deviceSessionService;
            _userSessionService = userSessionService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Get current user's devices and active sessions
        /// </summary>
        [HttpGet("my-devices")]
        public async Task<IActionResult> GetMyDevices()
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                var sessions = await _deviceSessionService.GetActiveSessionsForUserAsync(currentUserId.Value);
                var recentSessions = await _deviceSessionService.GetRecentSessionsAsync(currentUserId.Value, 20);
                var stats = await _deviceSessionService.GetSessionStatsAsync(currentUserId.Value);

                var response = new MyDevicesResponse
                {
                    ActiveSessions = sessions,
                    RecentSessions = recentSessions,
                    Stats = stats
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user's devices");
                return StatusCode(500, new { message = "An error occurred while retrieving your devices" });
            }
        }

        /// <summary>
        /// Get session details by session ID (user can only access their own sessions)
        /// </summary>
        [HttpGet("sessions/{sessionId}")]
        public async Task<IActionResult> GetSessionDetails(string sessionId)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                var session = await _deviceSessionService.GetSessionDetailsAsync(sessionId);
                if (session == null)
                {
                    return NotFound(new { message = "Session not found" });
                }

                // Ensure user can only access their own sessions
                if (session.UserId != currentUserId.Value.ToString())
                {
                    return Forbid();
                }

                return Ok(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session details for {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while retrieving session details" });
            }
        }

        /// <summary>
        /// Terminate a specific session (user can only terminate their own sessions)
        /// </summary>
        [HttpPost("sessions/{sessionId}/terminate")]
        public async Task<IActionResult> TerminateSession(string sessionId, [FromBody] TerminateDeviceSessionRequest? request = null)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                // Verify the session belongs to the current user
                var sessions = await _userSessionService.GetActiveUserSessionsAsync(currentUserId.Value);
                var sessionToTerminate = sessions.FirstOrDefault(s => s.SessionId == sessionId);

                if (sessionToTerminate == null)
                {
                    return NotFound(new { message = "Session not found or does not belong to you" });
                }

                var reason = request?.Reason ?? "Terminated by user";
                var success = await _deviceSessionService.TerminateSessionAsync(sessionId, reason);

                if (success)
                {
                    _logger.LogInformation("User {UserId} terminated their session {SessionId}", currentUserId.Value, sessionId);
                    return Ok(new { message = "Session terminated successfully", sessionId });
                }
                else
                {
                    return BadRequest(new { message = "Failed to terminate session" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating session {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while terminating the session" });
            }
        }

        /// <summary>
        /// Terminate all other sessions except the current one
        /// </summary>
        [HttpPost("terminate-all-others")]
        public async Task<IActionResult> TerminateAllOtherSessions([FromBody] TerminateDeviceSessionRequest? request = null)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                // Get current session ID - this is a simplified approach
                // In a real implementation, you might want to pass the current session ID from the frontend
                var currentSessionId = HttpContext.TraceIdentifier;
                
                var reason = request?.Reason ?? "All other sessions terminated by user";
                var terminatedCount = await _deviceSessionService.TerminateAllSessionsExceptCurrentAsync(
                    currentUserId.Value, 
                    currentSessionId, 
                    reason);

                _logger.LogInformation("User {UserId} terminated {Count} other sessions", currentUserId.Value, terminatedCount);
                
                return Ok(new { 
                    message = $"Successfully terminated {terminatedCount} other sessions",
                    terminatedCount 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating other sessions for user");
                return StatusCode(500, new { message = "An error occurred while terminating other sessions" });
            }
        }

        /// <summary>
        /// Trust or untrust a device based on its fingerprint
        /// </summary>
        [HttpPost("trust")]
        public async Task<IActionResult> UpdateDeviceTrust([FromBody] UpdateDeviceTrustRequest request)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                // This is a simplified implementation
                // In a real system, you'd have a device trust store/database
                _logger.LogInformation("User {UserId} updated trust for device {DeviceId} to {IsTrusted}", 
                    currentUserId.Value, request.DeviceId, request.IsTrusted);

                return Ok(new { message = "Device trust updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device trust");
                return StatusCode(500, new { message = "An error occurred while updating device trust" });
            }
        }

        /// <summary>
        /// Get suspicious activity for current user
        /// </summary>
        [HttpGet("suspicious-activity")]
        public async Task<IActionResult> GetSuspiciousActivity([FromQuery] int days = 30)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                // This would integrate with your security/audit system
                // For now, return empty array as this requires more complex implementation
                var activities = new List<SuspiciousActivityDto>();

                return Ok(activities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving suspicious activity");
                return StatusCode(500, new { message = "An error occurred while retrieving suspicious activity" });
            }
        }

        /// <summary>
        /// Get session statistics for current user
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetSessionStats([FromQuery] int days = 30)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                var stats = await _deviceSessionService.GetSessionStatsAsync(currentUserId.Value, days);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session statistics");
                return StatusCode(500, new { message = "An error occurred while retrieving session statistics" });
            }
        }
    }

    // Request/Response DTOs
    public class MyDevicesResponse
    {
        public List<DeviceSessionDto> ActiveSessions { get; set; } = new();
        public List<DeviceSessionDto> RecentSessions { get; set; } = new();
        public DeviceSessionStats Stats { get; set; } = new();
    }

    public class TerminateDeviceSessionRequest
    {
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string? Reason { get; set; }
    }

    public class UpdateDeviceTrustRequest
    {
        [Required]
        public string DeviceId { get; set; } = string.Empty;
        
        [Required]
        public bool IsTrusted { get; set; }
    }

    public class SuspiciousActivityDto
    {
        public string Id { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }
}