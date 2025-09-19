using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Services;
using ErpSystem.Core.Entities;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditLogController> _logger;

    public AuditLogController(IAuditLogService auditLogService, ILogger<AuditLogController> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Get audit logs with pagination
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<AuditLogDto>>> GetAuditLogs(
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 100)
    {
        try
        {
            if (pageSize > 500) pageSize = 500; // Limit page size for performance
            
            var auditLogs = await _auditLogService.GetAuditLogsAsync(pageNumber, pageSize);
            var auditLogDtos = auditLogs.Select(MapToDto).ToList();

            return Ok(auditLogDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            return StatusCode(500, "An error occurred while retrieving audit logs");
        }
    }

    /// <summary>
    /// Get audit logs by user
    /// </summary>
    [HttpGet("user/{userId}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<AuditLogDto>>> GetAuditLogsByUser(
        Guid userId,
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 100)
    {
        try
        {
            if (pageSize > 500) pageSize = 500;
            
            var auditLogs = await _auditLogService.GetAuditLogsByUserAsync(userId, pageNumber, pageSize);
            var auditLogDtos = auditLogs.Select(MapToDto).ToList();

            return Ok(auditLogDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving audit logs");
        }
    }

    /// <summary>
    /// Get audit logs by resource
    /// </summary>
    [HttpGet("resource/{resource}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<AuditLogDto>>> GetAuditLogsByResource(
        string resource,
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 100)
    {
        try
        {
            if (pageSize > 500) pageSize = 500;
            
            var auditLogs = await _auditLogService.GetAuditLogsByResourceAsync(resource, pageNumber, pageSize);
            var auditLogDtos = auditLogs.Select(MapToDto).ToList();

            return Ok(auditLogDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for resource {Resource}", resource);
            return StatusCode(500, "An error occurred while retrieving audit logs");
        }
    }

    /// <summary>
    /// Get audit logs by date range
    /// </summary>
    [HttpGet("daterange")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<IEnumerable<AuditLogDto>>> GetAuditLogsByDateRange(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 100)
    {
        try
        {
            if (pageSize > 500) pageSize = 500;
            
            var auditLogs = await _auditLogService.GetAuditLogsByDateRangeAsync(from, to, pageNumber, pageSize);
            var auditLogDtos = auditLogs.Select(MapToDto).ToList();

            return Ok(auditLogDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for date range {From} to {To}", from, to);
            return StatusCode(500, "An error occurred while retrieving audit logs");
        }
    }

    /// <summary>
    /// Get audit log by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = Constants.Roles.TenantAdmin + "," + Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<AuditLogDto>> GetAuditLog(Guid id)
    {
        try
        {
            var auditLog = await _auditLogService.GetAuditLogByIdAsync(id);
            if (auditLog == null)
            {
                return NotFound($"Audit log with ID {id} not found");
            }

            return Ok(MapToDto(auditLog));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log {AuditLogId}", id);
            return StatusCode(500, "An error occurred while retrieving the audit log");
        }
    }

    /// <summary>
    /// Delete old audit logs (cleanup operation)
    /// </summary>
    [HttpDelete("cleanup")]
    [Authorize(Roles = Constants.Roles.SuperAdmin)]
    public async Task<IActionResult> DeleteOldAuditLogs([FromQuery] DateTime beforeDate)
    {
        try
        {
            await _auditLogService.DeleteOldAuditLogsAsync(beforeDate);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting old audit logs before {Date}", beforeDate);
            return StatusCode(500, "An error occurred while deleting old audit logs");
        }
    }

    private static AuditLogDto MapToDto(AuditLog auditLog)
    {
        return new AuditLogDto
        {
            Id = auditLog.Id.ToString(),
            UserId = auditLog.UserId.ToString(),
            Username = auditLog.Username,
            Action = auditLog.Action,
            Resource = auditLog.Resource,
            ResourceId = auditLog.ResourceId,
            OldValues = auditLog.OldValues != null ? System.Text.Json.JsonSerializer.Deserialize<object>(auditLog.OldValues) : null,
            NewValues = auditLog.NewValues != null ? System.Text.Json.JsonSerializer.Deserialize<object>(auditLog.NewValues) : null,
            IpAddress = auditLog.IpAddress,
            UserAgent = auditLog.UserAgent,
            Timestamp = auditLog.Timestamp
        };
    }
}

// DTOs
public class AuditLogDto
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public object? OldValues { get; set; }
    public object? NewValues { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
}