using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SecurityLogController : ControllerBase
{
    private readonly ISecurityLogService _securityLogService;
    private readonly ILogger<SecurityLogController> _logger;

    public SecurityLogController(ISecurityLogService securityLogService, ILogger<SecurityLogController> logger)
    {
        _securityLogService = securityLogService;
        _logger = logger;
    }

    /// <summary>
    /// Get security logs with pagination
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<SecurityLogDto>>> GetSecurityLogs(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100)
    {
        try
        {
            if (pageSize > 500)
            {
                pageSize = 500; // Limit page size for performance
            }

            var securityLogs = await _securityLogService.GetSecurityLogsAsync(pageNumber, pageSize);
            var securityLogDtos = securityLogs.Select(MapToDto).ToList();

            return Ok(securityLogDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security logs");
            return StatusCode(500, "An error occurred while retrieving security logs");
        }
    }

    /// <summary>
    /// Get security logs by user
    /// </summary>
    [HttpGet("user/{userId}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<SecurityLogDto>>> GetSecurityLogsByUser(
        Guid userId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100)
    {
        try
        {
            if (pageSize > 500)
            {
                pageSize = 500;
            }

            var securityLogs = await _securityLogService.GetSecurityLogsByUserAsync(userId, pageNumber, pageSize);
            var securityLogDtos = securityLogs.Select(MapToDto).ToList();

            return Ok(securityLogDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security logs for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving security logs");
        }
    }

    /// <summary>
    /// Get security logs by action
    /// </summary>
    [HttpGet("action/{action}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<SecurityLogDto>>> GetSecurityLogsByAction(
        string action,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100)
    {
        try
        {
            if (pageSize > 500)
            {
                pageSize = 500;
            }

            var securityLogs = await _securityLogService.GetSecurityLogsByActionAsync(action, pageNumber, pageSize);
            var securityLogDtos = securityLogs.Select(MapToDto).ToList();

            return Ok(securityLogDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security logs for action {Action}", action);
            return StatusCode(500, "An error occurred while retrieving security logs");
        }
    }

    /// <summary>
    /// Get security logs by date range
    /// </summary>
    [HttpGet("daterange")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<SecurityLogDto>>> GetSecurityLogsByDateRange(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100)
    {
        try
        {
            if (pageSize > 500)
            {
                pageSize = 500;
            }

            var securityLogs = await _securityLogService.GetSecurityLogsByDateRangeAsync(from, to, pageNumber, pageSize);
            var securityLogDtos = securityLogs.Select(MapToDto).ToList();

            return Ok(securityLogDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security logs for date range {From} to {To}", from, to);
            return StatusCode(500, "An error occurred while retrieving security logs");
        }
    }

    /// <summary>
    /// Get security log by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<SecurityLogDto>> GetSecurityLog(Guid id)
    {
        try
        {
            var securityLog = await _securityLogService.GetSecurityLogByIdAsync(id);
            if (securityLog == null)
            {
                return NotFound($"Security log with ID {id} not found");
            }

            return Ok(MapToDto(securityLog));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security log {SecurityLogId}", id);
            return StatusCode(500, "An error occurred while retrieving the security log");
        }
    }

    /// <summary>
    /// Get failed login attempts by IP address
    /// </summary>
    [HttpGet("failed-logins/{ipAddress}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<SecurityLogDto>>> GetFailedLoginAttempts(
        string ipAddress,
        [FromQuery] DateTime? since = null)
    {
        try
        {
            var sinceDate = since ?? DateTime.UtcNow.AddHours(-24); // Default to last 24 hours
            var failedAttempts = await _securityLogService.GetFailedLoginAttemptsAsync(ipAddress, sinceDate);
            var securityLogDtos = failedAttempts.Select(MapToDto).ToList();

            return Ok(securityLogDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving failed login attempts for IP {IpAddress}", ipAddress);
            return StatusCode(500, "An error occurred while retrieving failed login attempts");
        }
    }

    /// <summary>
    /// Get failed login count for a user
    /// </summary>
    [HttpGet("failed-count/{username}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<int>> GetFailedLoginCount(
        string username,
        [FromQuery] DateTime? since = null)
    {
        try
        {
            var sinceDate = since ?? DateTime.UtcNow.AddHours(-24); // Default to last 24 hours
            var count = await _securityLogService.GetFailedLoginCountAsync(username, sinceDate);

            return Ok(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting failed login attempts for user {Username}", username);
            return StatusCode(500, "An error occurred while counting failed login attempts");
        }
    }

    /// <summary>
    /// Delete old security logs (cleanup operation)
    /// </summary>
    [HttpDelete("cleanup")]
    [Authorize(Roles = Constants.Roles.SuperAdmin)]
    public async Task<IActionResult> DeleteOldSecurityLogs([FromQuery] DateTime beforeDate)
    {
        try
        {
            await _securityLogService.DeleteOldSecurityLogsAsync(beforeDate);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting old security logs before {Date}", beforeDate);
            return StatusCode(500, "An error occurred while deleting old security logs");
        }
    }

    private static SecurityLogDto MapToDto(SecurityLog securityLog)
    {
        return new SecurityLogDto
        {
            Id = securityLog.Id.ToString(),
            UserId = securityLog.UserId?.ToString(),
            Username = securityLog.Username,
            Action = securityLog.Action,
            IpAddress = securityLog.IpAddress,
            UserAgent = securityLog.UserAgent,
            Success = securityLog.Success,
            Details = securityLog.Details,
            FailureReason = securityLog.FailureReason,
            Timestamp = securityLog.Timestamp
        };
    }
}

// DTOs
public class SecurityLogDto
{
    public string Id { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public bool Success { get; set; }
    public string? Details { get; set; }
    public string? FailureReason { get; set; }
    public DateTime Timestamp { get; set; }
}
