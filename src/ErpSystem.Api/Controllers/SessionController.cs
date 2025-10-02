using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Core.Entities;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Data;
using ErpSystem.Shared;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SessionController : ControllerBase
    {
        private readonly IUserSessionService _userSessionService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SessionController> _logger;

        public SessionController(
            IUserSessionService userSessionService,
            ICurrentUserService currentUserService,
            ApplicationDbContext context,
            ILogger<SessionController> logger)
        {
            _userSessionService = userSessionService;
            _currentUserService = currentUserService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all active sessions across the system (admin only)
        /// </summary>
        [HttpGet("active")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetActiveSessions([FromQuery] ActiveSessionsQuery query)
        {
            try
            {
                var currentTenantId = _currentUserService.GetTenantId();
                if (!currentTenantId.HasValue)
                {
                    return BadRequest(new { message = "No tenant context found" });
                }

                var sessionsQuery = _context.UserSessions
                    .Include(s => s.User)
                        .ThenInclude(u => u.UserRoles)
                            .ThenInclude(ur => ur.Role)
                    .Where(s => s.IsActive && s.TenantId == currentTenantId.Value);

                // Apply filters
                if (!string.IsNullOrEmpty(query.Username))
                {
                    sessionsQuery = sessionsQuery.Where(s => 
                        s.User != null && s.User.UserName.Contains(query.Username));
                }

                if (!string.IsNullOrEmpty(query.IpAddress))
                {
                    sessionsQuery = sessionsQuery.Where(s => s.IpAddress.Contains(query.IpAddress));
                }

                if (!string.IsNullOrEmpty(query.DeviceType))
                {
                    sessionsQuery = sessionsQuery.Where(s => s.DeviceType.Contains(query.DeviceType));
                }

                if (query.LoginTimeAfter.HasValue)
                {
                    sessionsQuery = sessionsQuery.Where(s => s.LoginTime >= query.LoginTimeAfter.Value);
                }

                if (query.LastActivityAfter.HasValue)
                {
                    sessionsQuery = sessionsQuery.Where(s => s.LastActivityTime >= query.LastActivityAfter.Value);
                }

                var sessions = await sessionsQuery
                    .OrderByDescending(s => s.LastActivityTime)
                    .Select(s => new ActiveSessionDto
                    {
                        SessionId = s.SessionId,
                        UserId = s.UserId,
                        Username = s.User != null ? s.User.UserName : "Unknown",
                        Role = s.User != null ? s.User.UserRoles.OrderByDescending(ur => ur.Role.Name == "SuperAdmin" ? 10 : 
                                                    ur.Role.Name == "TenantAdmin" ? 9 : 
                                                    ur.Role.Name == "Manager" ? 8 : 
                                                    ur.Role.Name == "Employee" ? 7 : 0)
                                                .Select(ur => ur.Role.Name).FirstOrDefault() ?? "Unknown" : "Unknown",
                        Email = s.User != null ? s.User.Email : "Unknown",
                        IpAddress = s.IpAddress,
                        UserAgent = s.UserAgent,
                        DeviceType = s.DeviceType,
                        Browser = s.Browser,
                        OperatingSystem = s.OperatingSystem,
                        Location = s.Location,
                        LoginTime = s.LoginTime,
                        LastActivityTime = s.LastActivityTime,
                        SessionDuration = DateTime.UtcNow - s.LoginTime
                    })
                    .ToListAsync();

                return Ok(sessions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active sessions");
                return StatusCode(500, new { message = "An error occurred while retrieving sessions" });
            }
        }

        /// <summary>
        /// Get all active sessions for a specific user
        /// </summary>
        [HttpGet("user/{userId}")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetUserSessions(Guid userId)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                
                // Role-based access is handled by the [Authorize] attribute
                // Additional logic can be added here if needed

                var sessions = await _userSessionService.GetActiveUserSessionsAsync(userId);
                
                var sessionDtos = sessions.Select(s => new ActiveSessionDto
                {
                    SessionId = s.SessionId,
                    UserId = s.UserId,
                    Username = "Current User", // We don't need to query user again
                    Role = "Current User", // This would need proper role lookup if needed
                    Email = "Current User", // This would need proper email lookup if needed
                    IpAddress = s.IpAddress,
                    UserAgent = s.UserAgent,
                    DeviceType = s.DeviceType,
                    Browser = s.Browser,
                    OperatingSystem = s.OperatingSystem,
                    Location = s.Location,
                    LoginTime = s.LoginTime,
                    LastActivityTime = s.LastActivityTime,
                    SessionDuration = DateTime.UtcNow - s.LoginTime
                }).ToList();

                return Ok(sessionDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user sessions for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while retrieving user sessions" });
            }
        }

        /// <summary>
        /// Terminate a specific session
        /// </summary>
        [HttpPost("{sessionId}/terminate")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin}")]
        public async Task<IActionResult> TerminateSession(string sessionId, [FromBody] TerminateSessionRequest request)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                var currentUsername = _currentUserService.GetUsername();

                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                await _userSessionService.TerminateSessionAsync(sessionId, request.Reason ?? "Terminated by administrator");

                _logger.LogInformation("Session {SessionId} terminated by admin {AdminId} ({AdminUsername}). Reason: {Reason}", 
                    sessionId, currentUserId.Value, currentUsername, request.Reason);

                return Ok(new { message = "Session terminated successfully", sessionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating session {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while terminating the session" });
            }
        }

        /// <summary>
        /// Terminate all sessions for a specific user
        /// </summary>
        [HttpPost("user/{userId}/terminate-all")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin}")]
        public async Task<IActionResult> TerminateAllUserSessions(Guid userId, [FromBody] TerminateSessionRequest request)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                var currentUsername = _currentUserService.GetUsername();

                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                await _userSessionService.TerminateAllUserSessionsAsync(userId, null, 
                    request.Reason ?? "All sessions terminated by administrator");

                _logger.LogInformation("All sessions for user {UserId} terminated by admin {AdminId} ({AdminUsername}). Reason: {Reason}", 
                    userId, currentUserId.Value, currentUsername, request.Reason);

                return Ok(new { message = "All user sessions terminated successfully", userId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating all sessions for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while terminating user sessions" });
            }
        }

        /// <summary>
        /// Bulk terminate multiple sessions
        /// </summary>
        [HttpPost("bulk-terminate")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin}")]
        public async Task<IActionResult> BulkTerminateSessions([FromBody] BulkTerminateSessionsRequest request)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                var currentUsername = _currentUserService.GetUsername();

                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                if (request.SessionIds == null || !request.SessionIds.Any())
                {
                    return BadRequest(new { message = "No session IDs provided" });
                }

                var results = new List<BulkSessionOperationResult>();
                var reason = request.Reason ?? "Bulk terminated by administrator";

                foreach (var sessionId in request.SessionIds)
                {
                    try
                    {
                        await _userSessionService.TerminateSessionAsync(sessionId, reason);
                        results.Add(new BulkSessionOperationResult
                        {
                            SessionId = sessionId,
                            Success = true,
                            Message = "Session terminated successfully"
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to terminate session {SessionId} during bulk operation", sessionId);
                        results.Add(new BulkSessionOperationResult
                        {
                            SessionId = sessionId,
                            Success = false,
                            Message = ex.Message
                        });
                    }
                }

                var successCount = results.Count(r => r.Success);
                var failureCount = results.Count(r => !r.Success);

                _logger.LogInformation("Bulk session termination completed by admin {AdminId} ({AdminUsername}). " +
                    "Success: {SuccessCount}, Failures: {FailureCount}. Reason: {Reason}", 
                    currentUserId.Value, currentUsername, successCount, failureCount, reason);

                return Ok(new BulkSessionOperationResponse
                {
                    TotalRequested = request.SessionIds.Count(),
                    Successful = successCount,
                    Failed = failureCount,
                    Results = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk session termination");
                return StatusCode(500, new { message = "An error occurred during bulk session termination" });
            }
        }

        /// <summary>
        /// Terminate sessions by criteria (IP range, device type, etc.)
        /// </summary>
        [HttpPost("terminate-by-criteria")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin}")]
        public async Task<IActionResult> TerminateSessionsByCriteria([FromBody] TerminateSessionsByCriteriaRequest request)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                var currentUsername = _currentUserService.GetUsername();
                var currentTenantId = _currentUserService.GetTenantId();

                if (!currentUserId.HasValue || !currentTenantId.HasValue)
                {
                    return Unauthorized();
                }

                // Build query for sessions matching criteria
                var sessionsQuery = _context.UserSessions
                    .Where(s => s.IsActive && s.TenantId == currentTenantId.Value);

                if (!string.IsNullOrEmpty(request.IpAddressPattern))
                {
                    sessionsQuery = sessionsQuery.Where(s => s.IpAddress.Contains(request.IpAddressPattern));
                }

                if (!string.IsNullOrEmpty(request.DeviceType))
                {
                    sessionsQuery = sessionsQuery.Where(s => s.DeviceType == request.DeviceType);
                }

                if (!string.IsNullOrEmpty(request.Browser))
                {
                    sessionsQuery = sessionsQuery.Where(s => s.Browser.Contains(request.Browser));
                }

                if (request.LoginTimeBefore.HasValue)
                {
                    sessionsQuery = sessionsQuery.Where(s => s.LoginTime < request.LoginTimeBefore.Value);
                }

                if (request.LastActivityBefore.HasValue)
                {
                    sessionsQuery = sessionsQuery.Where(s => s.LastActivityTime < request.LastActivityBefore.Value);
                }

                // Exclude the current user's session unless explicitly requested
                if (!request.IncludeCurrentUser)
                {
                    sessionsQuery = sessionsQuery.Where(s => s.UserId != currentUserId.Value);
                }

                var matchingSessions = await sessionsQuery.Select(s => s.SessionId).ToListAsync();

                if (!matchingSessions.Any())
                {
                    return Ok(new BulkSessionOperationResponse
                    {
                        TotalRequested = 0,
                        Successful = 0,
                        Failed = 0,
                        Results = new List<BulkSessionOperationResult>(),
                        Message = "No sessions matched the specified criteria"
                    });
                }

                var results = new List<BulkSessionOperationResult>();
                var reason = request.Reason ?? $"Terminated by criteria: {request.GetCriteriaDescription()}";

                foreach (var sessionId in matchingSessions)
                {
                    try
                    {
                        await _userSessionService.TerminateSessionAsync(sessionId, reason);
                        results.Add(new BulkSessionOperationResult
                        {
                            SessionId = sessionId,
                            Success = true,
                            Message = "Session terminated successfully"
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to terminate session {SessionId} during criteria-based termination", sessionId);
                        results.Add(new BulkSessionOperationResult
                        {
                            SessionId = sessionId,
                            Success = false,
                            Message = ex.Message
                        });
                    }
                }

                var successCount = results.Count(r => r.Success);
                var failureCount = results.Count(r => !r.Success);

                _logger.LogInformation("Criteria-based session termination completed by admin {AdminId} ({AdminUsername}). " +
                    "Criteria: {Criteria}, Success: {SuccessCount}, Failures: {FailureCount}. Reason: {Reason}", 
                    currentUserId.Value, currentUsername, request.GetCriteriaDescription(), successCount, failureCount, reason);

                return Ok(new BulkSessionOperationResponse
                {
                    TotalRequested = matchingSessions.Count,
                    Successful = successCount,
                    Failed = failureCount,
                    Results = results,
                    Message = $"Processed {matchingSessions.Count} sessions matching criteria"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during criteria-based session termination");
                return StatusCode(500, new { message = "An error occurred during criteria-based session termination" });
            }
        }

        /// <summary>
        /// Get current user's active sessions (user can view own sessions)
        /// </summary>
        [HttpGet("my-sessions")]
        public async Task<IActionResult> GetMyActiveSessions()
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                
                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                var sessions = await _userSessionService.GetActiveUserSessionsAsync(currentUserId.Value);
                
                var sessionDtos = sessions.Select(s => new UserSessionDto
                {
                    SessionId = s.SessionId,
                    IpAddress = s.IpAddress,
                    UserAgent = s.UserAgent,
                    DeviceType = s.DeviceType,
                    Browser = s.Browser,
                    OperatingSystem = s.OperatingSystem,
                    Location = s.Location,
                    LoginTime = s.LoginTime,
                    LastActivityTime = s.LastActivityTime,
                    SessionDuration = DateTime.UtcNow - s.LoginTime,
                    IsCurrentSession = s.SessionId == HttpContext.TraceIdentifier // This is a simple check, might need refinement
                }).OrderByDescending(s => s.LastActivityTime).ToList();

                return Ok(sessionDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user's own active sessions");
                return StatusCode(500, new { message = "An error occurred while retrieving your sessions" });
            }
        }

        /// <summary>
        /// Terminate one of the current user's own sessions
        /// </summary>
        [HttpPost("my-sessions/{sessionId}/terminate")]
        public async Task<IActionResult> TerminateMySession(string sessionId)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                
                if (!currentUserId.HasValue)
                {
                    return Unauthorized();
                }

                // Verify that the session belongs to the current user
                var userSessions = await _userSessionService.GetActiveUserSessionsAsync(currentUserId.Value);
                var sessionToTerminate = userSessions.FirstOrDefault(s => s.SessionId == sessionId);
                
                if (sessionToTerminate == null)
                {
                    return NotFound(new { message = "Session not found or does not belong to you" });
                }

                await _userSessionService.TerminateSessionAsync(sessionId, "Terminated by user");

                _logger.LogInformation("User {UserId} terminated their own session {SessionId}", 
                    currentUserId.Value, sessionId);

                return Ok(new { message = "Session terminated successfully", sessionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating user's own session {SessionId}", sessionId);
                return StatusCode(500, new { message = "An error occurred while terminating the session" });
            }
        }

        /// <summary>
        /// Get session history for a user (includes terminated sessions)
        /// </summary>
        [HttpGet("user/{userId}/history")]
        [Authorize(Roles = $"{Constants.Roles.SuperAdmin},{Constants.Roles.TenantAdmin},{Constants.Roles.Manager}")]
        public async Task<IActionResult> GetUserSessionHistory(Guid userId, [FromQuery] int days = 30)
        {
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                
                // Role-based access is handled by the [Authorize] attribute
                // Additional logic can be added here if needed

                var sessions = await _userSessionService.GetUserSessionHistoryAsync(userId, days);
                
                var sessionDtos = sessions.Select(s => new SessionHistoryDto
                {
                    SessionId = s.SessionId,
                    UserId = s.UserId,
                    IpAddress = s.IpAddress,
                    UserAgent = s.UserAgent,
                    DeviceType = s.DeviceType,
                    Browser = s.Browser,
                    OperatingSystem = s.OperatingSystem,
                    Location = s.Location,
                    LoginTime = s.LoginTime,
                    LogoutTime = s.LogoutTime,
                    LastActivityTime = s.LastActivityTime,
                    IsActive = s.IsActive,
                    TerminationReason = s.TerminationReason,
                    WasTerminatedByConcurrentLogin = s.WasTerminatedByConcurrentLogin,
                    SessionDuration = s.GetSessionDuration()
                }).ToList();

                return Ok(sessionDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session history for user {UserId}", userId);
                return StatusCode(500, new { message = "An error occurred while retrieving session history" });
            }
        }
    }

    // DTOs and request models
    public class ActiveSessionsQuery
    {
        public string? Username { get; set; }
        public string? IpAddress { get; set; }
        public string? DeviceType { get; set; }
        public DateTime? LoginTimeAfter { get; set; }
        public DateTime? LastActivityAfter { get; set; }
    }

    public class ActiveSessionDto
    {
        public string SessionId { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string Browser { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public TimeSpan SessionDuration { get; set; }
    }

    public class SessionHistoryDto : ActiveSessionDto
    {
        public DateTime? LogoutTime { get; set; }
        public bool IsActive { get; set; }
        public string? TerminationReason { get; set; }
        public bool WasTerminatedByConcurrentLogin { get; set; }
    }

    public class UserSessionDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string Browser { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public TimeSpan SessionDuration { get; set; }
        public bool IsCurrentSession { get; set; }
    }

    public class TerminateSessionRequest
    {
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string? Reason { get; set; }
    }

    public class BulkTerminateSessionsRequest
    {
        [Required]
        public IEnumerable<string> SessionIds { get; set; } = new List<string>();
        
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string? Reason { get; set; }
    }

    public class TerminateSessionsByCriteriaRequest
    {
        public string? IpAddressPattern { get; set; }
        public string? DeviceType { get; set; }
        public string? Browser { get; set; }
        public DateTime? LoginTimeBefore { get; set; }
        public DateTime? LastActivityBefore { get; set; }
        public bool IncludeCurrentUser { get; set; } = false;
        
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string? Reason { get; set; }

        public string GetCriteriaDescription()
        {
            var criteria = new List<string>();
            
            if (!string.IsNullOrEmpty(IpAddressPattern))
                criteria.Add($"IP contains '{IpAddressPattern}'");
            if (!string.IsNullOrEmpty(DeviceType))
                criteria.Add($"Device type is '{DeviceType}'");
            if (!string.IsNullOrEmpty(Browser))
                criteria.Add($"Browser contains '{Browser}'");
            if (LoginTimeBefore.HasValue)
                criteria.Add($"Login before {LoginTimeBefore.Value:yyyy-MM-dd HH:mm}");
            if (LastActivityBefore.HasValue)
                criteria.Add($"Last activity before {LastActivityBefore.Value:yyyy-MM-dd HH:mm}");

            return criteria.Any() ? string.Join(", ", criteria) : "No criteria specified";
        }
    }

    public class BulkSessionOperationResult
    {
        public string SessionId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class BulkSessionOperationResponse
    {
        public int TotalRequested { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public string? Message { get; set; }
        public IEnumerable<BulkSessionOperationResult> Results { get; set; } = new List<BulkSessionOperationResult>();
    }
}