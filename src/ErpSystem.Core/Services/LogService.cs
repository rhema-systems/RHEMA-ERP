using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services;

public interface IAuditLogService
{
    Task<IEnumerable<AuditLog>> GetAuditLogsAsync(int pageNumber = 1, int pageSize = 100);
    Task<IEnumerable<AuditLog>> GetAuditLogsByUserAsync(Guid userId, int pageNumber = 1, int pageSize = 100);
    Task<IEnumerable<AuditLog>> GetAuditLogsByResourceAsync(string resource, int pageNumber = 1, int pageSize = 100);
    Task<IEnumerable<AuditLog>> GetAuditLogsByDateRangeAsync(DateTime from, DateTime to, int pageNumber = 1, int pageSize = 100);
    Task<AuditLog?> GetAuditLogByIdAsync(Guid id);
    Task<AuditLog> CreateAuditLogAsync(AuditLog auditLog);
    Task LogUserActionAsync(Guid userId, string username, string action, string resource, string? resourceId = null, object? oldValues = null, object? newValues = null, string? ipAddress = null, string? userAgent = null);
    Task DeleteOldAuditLogsAsync(DateTime beforeDate);
}

public interface ISecurityLogService
{
    Task<IEnumerable<SecurityLog>> GetSecurityLogsAsync(int pageNumber = 1, int pageSize = 100);
    Task<IEnumerable<SecurityLog>> GetSecurityLogsByUserAsync(Guid userId, int pageNumber = 1, int pageSize = 100);
    Task<IEnumerable<SecurityLog>> GetSecurityLogsByActionAsync(string action, int pageNumber = 1, int pageSize = 100);
    Task<IEnumerable<SecurityLog>> GetSecurityLogsByDateRangeAsync(DateTime from, DateTime to, int pageNumber = 1, int pageSize = 100);
    Task<SecurityLog?> GetSecurityLogByIdAsync(Guid id);
    Task<SecurityLog> CreateSecurityLogAsync(SecurityLog securityLog);
    Task LogSecurityEventAsync(SecurityAction action, bool success, string ipAddress, string? username = null, Guid? userId = null, string? details = null, string? failureReason = null, string? userAgent = null);
    Task DeleteOldSecurityLogsAsync(DateTime beforeDate);
    Task<IEnumerable<SecurityLog>> GetFailedLoginAttemptsAsync(string ipAddress, DateTime since);
    Task<int> GetFailedLoginCountAsync(string username, DateTime since);
}

public class AuditLogService : IAuditLogService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuditLogService> _logger;
    private readonly ICurrentUserService _currentUserService;

    public AuditLogService(
        IUnitOfWork unitOfWork,
        ILogger<AuditLogService> logger,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(int pageNumber = 1, int pageSize = 100)
    {
        try
        {
            var tenantId = GetRequiredAuditTenantId();
            return await _unitOfWork.Repository<AuditLog>().GetPagedAsync(
                pageNumber,
                pageSize,
                a => a.TenantId == tenantId,
                a => a.Timestamp,
                descending: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            throw;
        }
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsByUserAsync(Guid userId, int pageNumber = 1, int pageSize = 100)
    {
        try
        {
            var tenantId = GetRequiredAuditTenantId();
            return await _unitOfWork.Repository<AuditLog>().GetPagedAsync(
                pageNumber,
                pageSize,
                a => a.TenantId == tenantId && a.UserId == userId,
                a => a.Timestamp,
                descending: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for user {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsByResourceAsync(string resource, int pageNumber = 1, int pageSize = 100)
    {
        try
        {
            var tenantId = GetRequiredAuditTenantId();
            return await _unitOfWork.Repository<AuditLog>().GetPagedAsync(
                pageNumber,
                pageSize,
                a => a.TenantId == tenantId && a.Resource == resource,
                a => a.Timestamp,
                descending: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for resource {Resource}", resource);
            throw;
        }
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsByDateRangeAsync(DateTime from, DateTime to, int pageNumber = 1, int pageSize = 100)
    {
        try
        {
            var tenantId = GetRequiredAuditTenantId();
            return await _unitOfWork.Repository<AuditLog>().GetPagedAsync(
                pageNumber,
                pageSize,
                a => a.TenantId == tenantId && a.Timestamp >= from && a.Timestamp <= to,
                a => a.Timestamp,
                descending: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for date range {From} to {To}", from, to);
            throw;
        }
    }

    public async Task<AuditLog?> GetAuditLogByIdAsync(Guid id)
    {
        try
        {
            var tenantId = GetRequiredAuditTenantId();
            return await _unitOfWork.Repository<AuditLog>()
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log {AuditLogId}", id);
            throw;
        }
    }

    public async Task<AuditLog> CreateAuditLogAsync(AuditLog auditLog)
    {
        try
        {
            auditLog.Id = Guid.NewGuid();
            auditLog.CreatedAt = DateTime.UtcNow;
            auditLog.Timestamp = DateTime.UtcNow;

            // Ensure TenantId is set - this is required for proper multi-tenant audit trail
            if (auditLog.TenantId == Guid.Empty)
            {
                _logger.LogError("AuditLog created without TenantId for user {Username}. This should never happen in a multi-tenant system.", auditLog.Username);
                throw new InvalidOperationException($"AuditLog requires a valid TenantId for user {auditLog.Username}");
            }

            var createdAuditLog = await _unitOfWork.Repository<AuditLog>().AddAsync(auditLog);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogDebug("Created audit log for user {Username} action {Action} on {Resource}",
                auditLog.Username, auditLog.Action, auditLog.Resource);

            return createdAuditLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating audit log");
            throw;
        }
    }

    public async Task LogUserActionAsync(Guid userId, string username, string action, string resource,
        string? resourceId = null, object? oldValues = null, object? newValues = null,
        string? ipAddress = null, string? userAgent = null)
    {
        try
        {
            // Get tenant context from current user or try to get from user entity
            var tenantId = _currentUserService.TenantId;

            // If no tenant context from current user (e.g., system operations), we need to get it another way
            if (!tenantId.HasValue)
            {
                // For now, log this as a warning and skip audit logging
                // In production, you might want to look up the user's tenant from the database
                _logger.LogWarning("Cannot create audit log for user {Username} - no tenant context available", username);
                return;
            }

            var auditLog = new AuditLog
            {
                UserId = userId,
                Username = username,
                Action = action,
                Resource = resource,
                ResourceId = resourceId,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                IpAddress = ipAddress ?? "Unknown",
                UserAgent = userAgent,
                Timestamp = DateTime.UtcNow,
                TenantId = tenantId.Value
            };

            await CreateAuditLogAsync(auditLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging user action {Action} for user {Username}", action, username);
            // Don't rethrow as logging failures shouldn't break the main application flow
        }
    }

    public async Task DeleteOldAuditLogsAsync(DateTime beforeDate)
    {
        try
        {
            var tenantId = GetRequiredAuditTenantId();
            var oldLogs = await _unitOfWork.Repository<AuditLog>()
                .FindAsync(a => a.TenantId == tenantId && a.CreatedAt < beforeDate);
            foreach (var log in oldLogs)
            {
                await _unitOfWork.Repository<AuditLog>().DeleteAsync(log.Id);
            }
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted {Count} audit logs older than {Date}", oldLogs.Count(), beforeDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting old audit logs");
            throw;
        }
    }

    private Guid GetRequiredAuditTenantId()
    {
        var tenantId = _currentUserService.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required for audit log access.");
        }

        return tenantId.Value;
    }
}

public class SecurityLogService : ISecurityLogService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SecurityLogService> _logger;
    private readonly ICurrentUserService _currentUserService;

    public SecurityLogService(
        IUnitOfWork unitOfWork,
        ILogger<SecurityLogService> logger,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<IEnumerable<SecurityLog>> GetSecurityLogsAsync(int pageNumber = 1, int pageSize = 100)
    {
        try
        {
            return await _unitOfWork.Repository<SecurityLog>().GetPagedAsync(
                pageNumber,
                pageSize,
                s => s.Timestamp,
                descending: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security logs");
            throw;
        }
    }

    public async Task<IEnumerable<SecurityLog>> GetSecurityLogsByUserAsync(Guid userId, int pageNumber = 1, int pageSize = 100)
    {
        try
        {
            return await _unitOfWork.Repository<SecurityLog>().GetPagedAsync(
                pageNumber,
                pageSize,
                s => s.UserId == userId,
                s => s.Timestamp,
                descending: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security logs for user {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<SecurityLog>> GetSecurityLogsByActionAsync(string action, int pageNumber = 1, int pageSize = 100)
    {
        try
        {
            return await _unitOfWork.Repository<SecurityLog>().GetPagedAsync(
                pageNumber,
                pageSize,
                s => s.Action == action,
                s => s.Timestamp,
                descending: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security logs for action {Action}", action);
            throw;
        }
    }

    public async Task<IEnumerable<SecurityLog>> GetSecurityLogsByDateRangeAsync(DateTime from, DateTime to, int pageNumber = 1, int pageSize = 100)
    {
        try
        {
            return await _unitOfWork.Repository<SecurityLog>().GetPagedAsync(
                pageNumber,
                pageSize,
                s => s.Timestamp >= from && s.Timestamp <= to,
                s => s.Timestamp,
                descending: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security logs for date range {From} to {To}", from, to);
            throw;
        }
    }

    public async Task<SecurityLog?> GetSecurityLogByIdAsync(Guid id)
    {
        try
        {
            return await _unitOfWork.Repository<SecurityLog>().GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving security log {SecurityLogId}", id);
            throw;
        }
    }

    public async Task<SecurityLog> CreateSecurityLogAsync(SecurityLog securityLog)
    {
        try
        {
            securityLog.Id = Guid.NewGuid();
            securityLog.CreatedAt = DateTime.UtcNow;
            securityLog.Timestamp = DateTime.UtcNow;

            // For security logs, TenantId should always be provided for proper multi-tenant security tracking
            if (securityLog.TenantId == Guid.Empty)
            {
                _logger.LogError("SecurityLog created without TenantId for action {Action}. This should never happen in a multi-tenant system.", securityLog.Action);
                throw new InvalidOperationException($"SecurityLog requires a valid TenantId for action {securityLog.Action}");
            }

            var createdSecurityLog = await _unitOfWork.Repository<SecurityLog>().AddAsync(securityLog);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogDebug("Created security log for action {Action} from IP {IpAddress}",
                securityLog.Action, securityLog.IpAddress);

            return createdSecurityLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating security log");
            throw;
        }
    }

    public async Task LogSecurityEventAsync(SecurityAction action, bool success, string ipAddress,
        string? username = null, Guid? userId = null, string? details = null,
        string? failureReason = null, string? userAgent = null)
    {
        try
        {
            // Get tenant context from current user
            var tenantId = _currentUserService.TenantId;

            // If no tenant context, we need to handle this appropriately
            if (!tenantId.HasValue)
            {
                _logger.LogWarning("Cannot create security log for action {Action} - no tenant context available", action);
                return;
            }

            var securityLog = new SecurityLog
            {
                UserId = userId,
                Username = username,
                Action = action.ToString(),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Success = success,
                Details = details,
                FailureReason = failureReason,
                Timestamp = DateTime.UtcNow,
                TenantId = tenantId.Value
            };

            await CreateSecurityLogAsync(securityLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging security event {Action}", action);
            // Don't rethrow as logging failures shouldn't break the main application flow
        }
    }

    public async Task DeleteOldSecurityLogsAsync(DateTime beforeDate)
    {
        try
        {
            var oldLogs = await _unitOfWork.Repository<SecurityLog>().FindAsync(s => s.CreatedAt < beforeDate);
            foreach (var log in oldLogs)
            {
                await _unitOfWork.Repository<SecurityLog>().DeleteAsync(log.Id);
            }
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted {Count} security logs older than {Date}", oldLogs.Count(), beforeDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting old security logs");
            throw;
        }
    }

    public async Task<IEnumerable<SecurityLog>> GetFailedLoginAttemptsAsync(string ipAddress, DateTime since)
    {
        try
        {
            return await _unitOfWork.Repository<SecurityLog>().FindAsync(s =>
                s.IpAddress == ipAddress &&
                s.Action == SecurityAction.LoginFailure.ToString() &&
                s.Timestamp >= since);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving failed login attempts for IP {IpAddress}", ipAddress);
            throw;
        }
    }

    public async Task<int> GetFailedLoginCountAsync(string username, DateTime since)
    {
        try
        {
            var failedAttempts = await _unitOfWork.Repository<SecurityLog>().FindAsync(s =>
                s.Username == username &&
                s.Action == SecurityAction.LoginFailure.ToString() &&
                s.Timestamp >= since);

            return failedAttempts.Count();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting failed login attempts for user {Username}", username);
            throw;
        }
    }
}
