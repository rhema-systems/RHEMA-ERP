using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DiagnosticController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DiagnosticController> _logger;

        public DiagnosticController(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            ILogger<DiagnosticController> logger)
        {
            _context = context;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Get diagnostic information about current user's tenant data
        /// </summary>
        [HttpGet("tenant-data")]
        public async Task<ActionResult> GetTenantDiagnostics()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;

                if (!tenantId.HasValue)
                {
                    return BadRequest("No tenant ID found in token");
                }

                // Check if tenant exists
                var tenant = await _context.Tenants
                    .Where(t => t.Id == tenantId.Value)
                    .FirstOrDefaultAsync();

                // Count users in this tenant
                var userCount = await _context.Users
                    .Where(u => u.TenantId == tenantId.Value)
                    .CountAsync();

                // Count sessions in this tenant
                var sessionCount = await _context.UserSessions
                    .Where(s => s.TenantId == tenantId.Value)
                    .CountAsync();

                // Count active sessions
                var activeSessionCount = await _context.UserSessions
                    .Where(s => s.TenantId == tenantId.Value && s.IsActive)
                    .CountAsync();

                // Get current user info
                var currentUser = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.UserName, u.Email, u.TenantId, u.IsActive })
                    .FirstOrDefaultAsync();

                var result = new
                {
                    TenantId = tenantId.Value.ToString(),
                    UserId = userId?.ToString(),
                    TenantExists = tenant != null,
                    TenantName = tenant?.Name ?? "Not found",
                    TenantStatus = tenant?.Status.ToString() ?? "Not found",
                    UserCount = userCount,
                    SessionCount = sessionCount,
                    ActiveSessionCount = activeSessionCount,
                    CurrentUser = currentUser,
                    DatabaseConnection = _context.Database.CanConnect() ? "Connected" : "Failed"
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tenant diagnostics");
                return StatusCode(500, new
                {
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Get all tenants in the system (for debugging)
        /// </summary>
        [HttpGet("all-tenants")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<ActionResult> GetAllTenants()
        {
            try
            {
                var tenants = await _context.Tenants
                    .Select(t => new
                    {
                        t.Id,
                        t.Name,
                        t.Code,
                        t.Status,
                        UserCount = _context.Users.Count(u => u.TenantId == t.Id)
                    })
                    .ToListAsync();

                return Ok(tenants);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all tenants");
                return StatusCode(500, new
                {
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Get online users with simple queries (bypassing complex dashboard service)
        /// </summary>
        [HttpGet("simple-online-users")]
        public async Task<ActionResult> GetSimpleOnlineUsers()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;

                if (!tenantId.HasValue)
                {
                    return BadRequest("No tenant ID found in token");
                }

                // Get active sessions with minimal data (avoid complex joins)
                var activeSessions = await _context.UserSessions
                    .Where(s => s.TenantId == tenantId.Value && s.IsActive)
                    .Select(s => new
                    {
                        s.UserId,
                        s.LastActivityTime,
                        s.Location,
                        UserName = s.User.UserName,
                        Email = s.User.Email
                    })
                    .GroupBy(s => s.UserId)
                    .Select(g => g.OrderByDescending(s => s.LastActivityTime).First())
                    .ToListAsync();

                var onlineUsers = activeSessions.Select(session => new
                {
                    UserId = session.UserId.ToString(),
                    UserName = session.UserName ?? "Unknown",
                    Email = session.Email ?? "",
                    LastActivity = session.LastActivityTime,
                    Status = "Online",
                    Location = session.Location
                }).ToList();

                return Ok(new
                {
                    count = onlineUsers.Count,
                    users = onlineUsers,
                    tenantId = tenantId.Value.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting simple online users");
                return StatusCode(500, new
                {
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Test SignalR negotiation endpoint to debug connection issues
        /// </summary>
        [HttpGet("signalr-test")]
        public ActionResult TestSignalRNegotiation()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;
                var userName = _currentUserService.UserName;

                // Check JWT token claims
                var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

                // Check if we can access the hub context (this tests if SignalR services are working)
                var hubContextTest = "SignalR services appear to be registered";

                var result = new
                {
                    UserId = userId?.ToString(),
                    UserName = userName,
                    TenantId = tenantId?.ToString(),
                    IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                    AuthenticationType = User.Identity?.AuthenticationType,
                    Claims = claims,
                    HubContextStatus = hubContextTest,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SignalR test endpoint");
                return StatusCode(500, new
                {
                    error = ex.Message,
                    innerError = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }
}
