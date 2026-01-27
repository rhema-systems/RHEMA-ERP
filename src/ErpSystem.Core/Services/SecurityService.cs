using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services;

public interface ISecurityService
{
    Task<SecurityMetricsDto> GetSecurityMetricsAsync();
    Task<SecurityMetricsDto> CalculateAndCacheSecurityMetricsAsync();

    Task<List<SecurityAlertDto>> GetSecurityAlertsAsync(bool dismissed = false);
    Task<SecurityAlertDto> CreateSecurityAlertAsync(CreateSecurityAlertRequest request);
    Task DismissSecurityAlertAsync(Guid alertId);

    Task<AuditLogResponseDto> GetAuditLogsAsync(AuditLogFilterDto filter);

    Task<List<DeviceSessionDto>> GetDeviceSessionsAsync();
    Task<DeviceSessionDto?> GetDeviceSessionDetailsAsync(string sessionId);
    Task<bool> TerminateDeviceSessionAsync(string sessionId, string reason = "Manual termination");
    Task<int> TerminateAllDeviceSessionsExceptCurrentAsync(string currentSessionId, string reason = "Terminate other sessions");
    Task<DeviceSessionStats> GetDeviceSessionStatsAsync(int days = 30);
    Task<List<DeviceSessionDto>> GetRecentDeviceSessionsAsync(int count = 10);
    Task<SecurityHealthScoreDto> GetSecurityHealthScoreAsync();

    // Threat Detection methods
    Task<List<ThreatDetectionDto>> GetThreatDetectionsAsync();
    Task UpdateThreatStatusAsync(Guid threatId, UpdateThreatStatusRequest request);

    // Two-Factor Authentication methods
    Task<TwoFactorSettingsDto> GetTwoFactorSettingsAsync();
    Task<TwoFactorSetupDto> EnableTwoFactorAsync(EnableTwoFactorRequest request);
    Task DisableTwoFactorAsync(DisableTwoFactorRequest request);

    // Security Policy methods
    Task<List<SecurityPolicyDto>> GetSecurityPoliciesAsync();
    Task<SecurityPolicyDto> CreateSecurityPolicyAsync(CreateSecurityPolicyRequest request);
    Task<SecurityPolicyDto> UpdateSecurityPolicyAsync(Guid policyId, UpdateSecurityPolicyRequest request);
}

public class SecurityService : ISecurityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserService _userService;
    private readonly IUserSessionService _userSessionService;
    private readonly ITwoFactorAuthService _twoFactorAuthService;
    private readonly IDeviceSessionService _deviceSessionService;
    private readonly ILogger<SecurityService> _logger;

    public SecurityService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IUserService userService,
        IUserSessionService userSessionService,
        ITwoFactorAuthService twoFactorAuthService,
        IDeviceSessionService deviceSessionService,
        ILogger<SecurityService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _userService = userService;
        _userSessionService = userSessionService;
        _twoFactorAuthService = twoFactorAuthService;
        _deviceSessionService = deviceSessionService;
        _logger = logger;
    }

    #region Security Metrics

    public async Task<SecurityMetricsDto> GetSecurityMetricsAsync()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            // Try to get cached metrics from today
            var today = DateTime.UtcNow.Date;
            var cachedMetrics = await _unitOfWork.Repository<SecurityMetrics>()
                .FirstOrDefaultAsync(sm => sm.TenantId == tenantId.Value && sm.MetricDate == today);

            if (cachedMetrics != null)
            {
                return MapToSecurityMetricsDto(cachedMetrics);
            }

            // If no cached metrics, calculate and cache them
            return await CalculateAndCacheSecurityMetricsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security metrics");
            throw;
        }
    }

    public async Task<SecurityMetricsDto> CalculateAndCacheSecurityMetricsAsync()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);

            // Get real user data
            var allUsers = await _userService.GetAllUsersAsync();
            var tenantUsers = allUsers.Where(u => u.TenantId == tenantId.Value).ToList();
            var totalUsers = tenantUsers.Count;
            var usersWithTwoFactor = tenantUsers.Count(u => u.TwoFactorEnabled);
            var twoFactorAdoptionRate = totalUsers > 0 ? (int)((double)usersWithTwoFactor / totalUsers * 100) : 0;

            var failedLoginsToday = await _unitOfWork.Repository<SecurityLog>()
                .CountAsync(sl => sl.TenantId == tenantId.Value &&
                                  sl.Timestamp >= today &&
                                  !sl.Success);

            // Get real active sessions count for tenant
            var activeSessionsCount = 0;
            foreach (var user in tenantUsers)
            {
                var userSessions = await _userSessionService.GetActiveUserSessionsAsync(user.Id);
                activeSessionsCount += userSessions.Count;
            }
            var activeSessions = activeSessionsCount;

            var securityIncidentsToday = await _unitOfWork.Repository<SecurityLog>()
                .CountAsync(sl => sl.TenantId == tenantId.Value &&
                                  sl.Timestamp >= today &&
                                  (sl.Action.Contains("SUSPICIOUS") || sl.Action.Contains("BRUTE_FORCE")));

            var auditEventsToday = await _unitOfWork.Repository<AuditLog>()
                .CountAsync(al => al.TenantId == tenantId.Value && al.Timestamp >= today);

            // Calculate password compliance based on user password age and policy
            var passwordCompliance = await CalculatePasswordComplianceAsync(tenantUsers);

            // Get yesterday's metrics for trends
            var yesterdayMetrics = await _unitOfWork.Repository<SecurityMetrics>()
                .FirstOrDefaultAsync(sm => sm.TenantId == tenantId.Value && sm.MetricDate == yesterday);

            var trends = new SecurityTrendsDto();
            if (yesterdayMetrics != null)
            {
                trends.TwoFactorAdoptionRate = twoFactorAdoptionRate - yesterdayMetrics.TwoFactorAdoptionRate;
                trends.FailedLoginAttempts = failedLoginsToday - yesterdayMetrics.FailedLoginAttempts;
                trends.ActiveSessions = activeSessions - yesterdayMetrics.ActiveSessions;
                trends.SecurityIncidents = securityIncidentsToday - yesterdayMetrics.SecurityIncidents;
                trends.PasswordCompliance = passwordCompliance - yesterdayMetrics.PasswordCompliance;
                trends.AuditEventsToday = auditEventsToday - yesterdayMetrics.AuditEventsToday;
            }

            // Save metrics to cache
            var metrics = new SecurityMetrics
            {
                TenantId = tenantId.Value,
                MetricDate = today,
                TwoFactorAdoptionRate = twoFactorAdoptionRate,
                FailedLoginAttempts = failedLoginsToday,
                ActiveSessions = activeSessions,
                SecurityIncidents = securityIncidentsToday,
                PasswordCompliance = passwordCompliance,
                AuditEventsToday = auditEventsToday,
                TotalUsers = totalUsers,
                UsersWithTwoFactorEnabled = usersWithTwoFactor,
                LastUpdated = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserName
            };

            // Check if today's metrics already exist
            var existingMetrics = await _unitOfWork.Repository<SecurityMetrics>()
                .FirstOrDefaultAsync(sm => sm.TenantId == tenantId.Value && sm.MetricDate == today);

            if (existingMetrics != null)
            {
                // Update existing
                existingMetrics.TwoFactorAdoptionRate = twoFactorAdoptionRate;
                existingMetrics.FailedLoginAttempts = failedLoginsToday;
                existingMetrics.ActiveSessions = activeSessions;
                existingMetrics.SecurityIncidents = securityIncidentsToday;
                existingMetrics.PasswordCompliance = passwordCompliance;
                existingMetrics.AuditEventsToday = auditEventsToday;
                existingMetrics.TotalUsers = totalUsers;
                existingMetrics.UsersWithTwoFactorEnabled = usersWithTwoFactor;
                existingMetrics.LastUpdated = DateTime.UtcNow;

                await _unitOfWork.Repository<SecurityMetrics>().UpdateAsync(existingMetrics);
            }
            else
            {
                await _unitOfWork.Repository<SecurityMetrics>().AddAsync(metrics);
            }

            await _unitOfWork.SaveChangesAsync();

            return new SecurityMetricsDto
            {
                TwoFactorAdoptionRate = twoFactorAdoptionRate,
                FailedLoginAttempts = failedLoginsToday,
                ActiveSessions = activeSessions,
                SecurityIncidents = securityIncidentsToday,
                PasswordCompliance = passwordCompliance,
                AuditEventsToday = auditEventsToday,
                LastUpdated = DateTime.UtcNow.ToString("o"),
                Trends = trends
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating security metrics");
            throw;
        }
    }

    private static SecurityMetricsDto MapToSecurityMetricsDto(SecurityMetrics metrics)
    {
        return new SecurityMetricsDto
        {
            TwoFactorAdoptionRate = metrics.TwoFactorAdoptionRate,
            FailedLoginAttempts = metrics.FailedLoginAttempts,
            ActiveSessions = metrics.ActiveSessions,
            SecurityIncidents = metrics.SecurityIncidents,
            PasswordCompliance = metrics.PasswordCompliance,
            AuditEventsToday = metrics.AuditEventsToday,
            LastUpdated = metrics.LastUpdated.ToString("o"),
            Trends = new SecurityTrendsDto
            {
                TwoFactorAdoptionRate = metrics.TwoFactorAdoptionTrend,
                FailedLoginAttempts = metrics.FailedLoginAttemptsTrend,
                ActiveSessions = metrics.ActiveSessionsTrend,
                SecurityIncidents = metrics.SecurityIncidentsTrend,
                PasswordCompliance = metrics.PasswordComplianceTrend,
                AuditEventsToday = metrics.AuditEventsTrend
            }
        };
    }

    #endregion

    #region Security Alerts

    public async Task<List<SecurityAlertDto>> GetSecurityAlertsAsync(bool dismissed = false)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            var alerts = await _unitOfWork.Repository<SecurityAlert>()
                .FindAsync(sa => sa.TenantId == tenantId.Value && sa.Dismissed == dismissed);

            return alerts.Select(MapToSecurityAlertDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security alerts");
            throw;
        }
    }

    public async Task<SecurityAlertDto> CreateSecurityAlertAsync(CreateSecurityAlertRequest request)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            var alert = new SecurityAlert
            {
                TenantId = tenantId.Value,
                Type = request.Type,
                Title = request.Title,
                Message = request.Message,
                Severity = request.Severity,
                Category = request.Category,
                Source = request.Source,
                AffectedUser = request.AffectedUser,
                IpAddress = request.IpAddress,
                Location = request.Location,
                Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
                CreatedBy = _currentUserService.UserName
            };

            var createdAlert = await _unitOfWork.Repository<SecurityAlert>().AddAsync(alert);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created security alert: {Title} ({Type})", alert.Title, alert.Type);

            return MapToSecurityAlertDto(createdAlert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating security alert");
            throw;
        }
    }

    public async Task DismissSecurityAlertAsync(Guid alertId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            var alert = await _unitOfWork.Repository<SecurityAlert>()
                .FirstOrDefaultAsync(sa => sa.Id == alertId && sa.TenantId == tenantId.Value) ?? throw new InvalidOperationException("Security alert not found");
            alert.Dismissed = true;
            alert.DismissedAt = DateTime.UtcNow;
            alert.DismissedBy = _currentUserService.UserName;

            await _unitOfWork.Repository<SecurityAlert>().UpdateAsync(alert);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Dismissed security alert: {AlertId}", alertId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing security alert {AlertId}", alertId);
            throw;
        }
    }

    private static SecurityAlertDto MapToSecurityAlertDto(SecurityAlert alert)
    {
        return new SecurityAlertDto
        {
            Id = alert.Id.ToString(),
            Type = alert.Type,
            Title = alert.Title,
            Message = alert.Message,
            Timestamp = alert.Timestamp,
            Dismissed = alert.Dismissed,
            Severity = alert.Severity,
            Category = alert.Category,
            Source = alert.Source,
            AffectedUser = alert.AffectedUser,
            IpAddress = alert.IpAddress,
            Location = alert.Location,
            Metadata = !string.IsNullOrEmpty(alert.Metadata)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(alert.Metadata)
                : null
        };
    }

    #endregion

    #region Audit Logs

    public async Task<AuditLogResponseDto> GetAuditLogsAsync(AuditLogFilterDto filter)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            var query = _unitOfWork.Repository<AuditLog>().GetQueryable()
                .Where(al => al.TenantId == tenantId.Value);

            // Apply filters
            if (filter.StartDate.HasValue)
            {
                query = query.Where(al => al.Timestamp >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                query = query.Where(al => al.Timestamp <= filter.EndDate.Value);
            }

            if (!string.IsNullOrEmpty(filter.UserName))
            {
                query = query.Where(al => al.Username.Contains(filter.UserName));
            }

            if (!string.IsNullOrEmpty(filter.Action))
            {
                query = query.Where(al => al.Action.Contains(filter.Action));
            }

            if (!string.IsNullOrEmpty(filter.Resource))
            {
                query = query.Where(al => al.Resource.Contains(filter.Resource));
            }

            if (!string.IsNullOrEmpty(filter.IpAddress))
            {
                query = query.Where(al => al.IpAddress.Contains(filter.IpAddress));
            }

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                query = query.Where(al =>
                    al.Action.Contains(filter.SearchTerm) ||
                    al.Username.Contains(filter.SearchTerm) ||
                    al.Resource.Contains(filter.SearchTerm));
            }

            var total = await query.CountAsync();

            // Apply sorting
            if (filter.SortBy.Equals("timestamp", StringComparison.OrdinalIgnoreCase))
            {
                query = filter.SortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase)
                    ? query.OrderBy(al => al.Timestamp)
                    : query.OrderByDescending(al => al.Timestamp);
            }
            else
            {
                query = query.OrderByDescending(al => al.Timestamp);
            }

            // Apply pagination
            var entries = await query
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Include(al => al.User)
                .ToListAsync();

            var totalPages = (int)Math.Ceiling((double)total / filter.PageSize);

            return new AuditLogResponseDto
            {
                Entries = (await Task.WhenAll(entries.Select(async entry => await MapToAuditLogEntryDtoAsync(entry)))).ToList(),
                Total = total,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalPages = totalPages,
                HasNext = filter.Page < totalPages,
                HasPrevious = filter.Page > 1
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs");
            throw;
        }
    }

    private async Task<AuditLogEntryDto> MapToAuditLogEntryDtoAsync(AuditLog auditLog)
    {
        // Map to the expected frontend format
        var result = auditLog.Action.Contains("SUCCESS") || auditLog.Action.Contains("CREATE") || auditLog.Action.Contains("UPDATE")
            ? "success" : "failure";

        var risk = auditLog.Action.Contains("DELETE") || auditLog.Action.Contains("ROLE_CHANGE") ? "high" : "low";

        return new AuditLogEntryDto
        {
            Id = auditLog.Id.ToString(),
            Timestamp = auditLog.Timestamp,
            UserId = auditLog.UserId.ToString(),
            UserName = auditLog.Username,
            UserEmail = auditLog.User?.Email ?? "",
            Action = auditLog.Action,
            Resource = auditLog.Resource,
            Result = result,
            IpAddress = auditLog.IpAddress,
            UserAgent = auditLog.UserAgent ?? "",
            Location = await GetLocationFromIpAsync(auditLog.IpAddress),
            Details = $"{auditLog.Resource} {auditLog.Action}",
            Risk = risk,
            Metadata = new Dictionary<string, object>
            {
                ["resourceId"] = auditLog.ResourceId ?? "",
                ["oldValues"] = auditLog.OldValues ?? "",
                ["newValues"] = auditLog.NewValues ?? ""
            }
        };
    }

    private async Task<string> GetLocationFromIpAsync(string ipAddress)
    {
        try
        {
            if (string.IsNullOrEmpty(ipAddress))
            {
                return "Unknown";
            }

            var geolocation = await _deviceSessionService.GetGeolocationAsync(ipAddress);
            if (geolocation != null)
            {
                return $"{geolocation.City}, {geolocation.Country}";
            }

            return "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get location for IP {IpAddress}", ipAddress);
            return "Unknown";
        }
    }

    #endregion

    #region Device Sessions

    public async Task<List<DeviceSessionDto>> GetDeviceSessionsAsync()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            // Get all users for the current tenant
            var allUsers = await _userService.GetAllUsersAsync();
            var tenantUsers = allUsers.Where(u => u.TenantId == tenantId.Value).ToList();

            var deviceSessions = new List<DeviceSessionDto>();

            foreach (var user in tenantUsers)
            {
                var userDeviceSessions = await _deviceSessionService.GetActiveSessionsForUserAsync(user.Id);
                deviceSessions.AddRange(userDeviceSessions);
            }

            return deviceSessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device sessions");
            throw;
        }
    }

    public async Task<DeviceSessionDto?> GetDeviceSessionDetailsAsync(string sessionId)
    {
        try
        {
            return await _deviceSessionService.GetSessionDetailsAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device session details for {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<bool> TerminateDeviceSessionAsync(string sessionId, string reason = "Manual termination")
    {
        try
        {
            return await _deviceSessionService.TerminateSessionAsync(sessionId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating device session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<int> TerminateAllDeviceSessionsExceptCurrentAsync(string currentSessionId, string reason = "Terminate other sessions")
    {
        try
        {
            var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;
            if (!userId.HasValue)
            {
                throw new InvalidOperationException("User ID is required");
            }

            return await _deviceSessionService.TerminateAllSessionsExceptCurrentAsync(userId.Value, currentSessionId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating all device sessions except current");
            throw;
        }
    }

    public async Task<DeviceSessionStats> GetDeviceSessionStatsAsync(int days = 30)
    {
        try
        {
            var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;
            if (!userId.HasValue)
            {
                throw new InvalidOperationException("User ID is required");
            }

            return await _deviceSessionService.GetSessionStatsAsync(userId.Value, days);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device session stats");
            throw;
        }
    }

    public async Task<List<DeviceSessionDto>> GetRecentDeviceSessionsAsync(int count = 10)
    {
        try
        {
            var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;
            if (!userId.HasValue)
            {
                throw new InvalidOperationException("User ID is required");
            }

            return await _deviceSessionService.GetRecentSessionsAsync(userId.Value, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent device sessions");
            throw;
        }
    }


    #endregion

    #region Security Health Score

    public async Task<SecurityHealthScoreDto> GetSecurityHealthScoreAsync()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            // Calculate health scores for each category
            var passwordPoliciesScore = await CalculatePasswordPoliciesScore(tenantId.Value);
            var twoFactorScore = await CalculateTwoFactorAdoptionScore(tenantId.Value);
            var sessionSecurityScore = await CalculateSessionSecurityScore(tenantId.Value);
            var accessControlsScore = await CalculateAccessControlsScore(tenantId.Value);
            var auditComplianceScore = await CalculateAuditComplianceScore(tenantId.Value);

            var overallScore = (passwordPoliciesScore + twoFactorScore + sessionSecurityScore +
                               accessControlsScore + auditComplianceScore) / 5;

            var recommendations = GenerateSecurityRecommendations(
                passwordPoliciesScore, twoFactorScore, sessionSecurityScore,
                accessControlsScore, auditComplianceScore);

            return new SecurityHealthScoreDto
            {
                Overall = overallScore,
                Categories = new SecurityHealthCategoriesDto
                {
                    PasswordPolicies = passwordPoliciesScore,
                    TwoFactorAdoption = twoFactorScore,
                    SessionSecurity = sessionSecurityScore,
                    AccessControls = accessControlsScore,
                    AuditCompliance = auditComplianceScore
                },
                Recommendations = recommendations,
                LastCalculated = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating security health score");
            throw;
        }
    }

    private async Task<int> CalculatePasswordPoliciesScore(Guid tenantId)
    {
        var security = await _unitOfWork.Repository<Security>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        if (security == null)
        {
            return 50;
        }

        var score = 60; // Base score
        if (security.PasswordMinLength >= 12)
        {
            score += 10;
        }

        if (security.PasswordRequireUppercase)
        {
            score += 5;
        }

        if (security.PasswordRequireLowercase)
        {
            score += 5;
        }

        if (security.PasswordRequireDigits)
        {
            score += 5;
        }

        if (security.PasswordRequireSpecialChars)
        {
            score += 10;
        }

        if (security.PasswordMaxAge.HasValue && security.PasswordMaxAge <= 90)
        {
            score += 5;
        }

        return Math.Min(score, 100);
    }

    private async Task<int> CalculateTwoFactorAdoptionScore(Guid tenantId)
    {
        try
        {
            // Get real user data for tenant
            var allUsers = await _userService.GetAllUsersAsync();
            var tenantUsers = allUsers.Where(u => u.TenantId == tenantId).ToList();

            if (tenantUsers.Count == 0)
            {
                return 50; // No users, return base score
            }

            var usersWithTwoFactor = tenantUsers.Count(u => u.TwoFactorEnabled);
            var adoptionPercentage = (double)usersWithTwoFactor / tenantUsers.Count * 100;

            // Convert percentage to score (0-100)
            var score = (int)Math.Round(adoptionPercentage);

            // Add bonus points for high adoption
            if (adoptionPercentage >= 95)
            {
                score += 5;
            }
            else if (adoptionPercentage >= 80)
            {
                score += 3;
            }

            return Math.Min(score, 100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating two-factor adoption score");
            return 50; // Fallback score
        }
    }

    private async Task<int> CalculateSessionSecurityScore(Guid tenantId)
    {
        var security = await _unitOfWork.Repository<Security>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId);

        if (security == null)
        {
            return 50;
        }

        var score = 50; // Base score
        if (security.SessionTimeoutMinutes <= 60)
        {
            score += 20;
        }

        if (security.JwtTokenLifetimeMinutes <= 120)
        {
            score += 15;
        }

        if (security.PreventConcurrentLogin != ErpSystem.Core.Enums.PreventConcurrentLogin.Disabled)
        {
            score += 15;
        }

        return Math.Min(score, 100);
    }

    private async Task<int> CalculateAccessControlsScore(Guid tenantId)
    {
        try
        {
            var score = 50; // Base score

            // Check role-based access control using UserService
            var allUsers = await _userService.GetAllUsersAsync();
            var tenantUsers = allUsers.Where(u => u.TenantId == tenantId).ToList();

            // Count unique roles across all tenant users
            var uniqueRoles = tenantUsers
                .SelectMany(u => u.UserRoles?.Select(ur => ur.RoleId) ?? new List<Guid>())
                .Distinct()
                .Count();

            // Bonus for having multiple roles (better access control)
            if (uniqueRoles >= 5)
            {
                score += 20;
            }
            else if (uniqueRoles >= 3)
            {
                score += 15;
            }
            else if (uniqueRoles >= 2)
            {
                score += 10;
            }

            if (tenantUsers.Count > 0)
            {
                var usersWithRoles = tenantUsers.Count(u => u.UserRoles != null && u.UserRoles.Any());
                var roleAssignmentPercentage = (double)usersWithRoles / tenantUsers.Count * 100;

                // Bonus for proper role assignment
                if (roleAssignmentPercentage >= 90)
                {
                    score += 15;
                }
                else if (roleAssignmentPercentage >= 75)
                {
                    score += 10;
                }
                else if (roleAssignmentPercentage >= 50)
                {
                    score += 5;
                }
            }

            // Check security policies
            var activePolicies = await _unitOfWork.Repository<SecurityPolicy>()
                .CountAsync(sp => sp.TenantId == tenantId && sp.IsActive);

            // Bonus for having security policies
            if (activePolicies >= 3)
            {
                score += 10;
            }
            else if (activePolicies >= 1)
            {
                score += 5;
            }

            return Math.Min(score, 100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating access controls score");
            return 50; // Fallback score
        }
    }

    private async Task<int> CalculateAuditComplianceScore(Guid tenantId)
    {
        var today = DateTime.UtcNow.Date;
        var last30Days = today.AddDays(-30);

        var auditLogsCount = await _unitOfWork.Repository<AuditLog>()
            .CountAsync(al => al.TenantId == tenantId && al.Timestamp >= last30Days);

        // Score based on audit activity (more activity = better compliance)
        if (auditLogsCount > 1000)
        {
            return 95;
        }

        if (auditLogsCount > 500)
        {
            return 85;
        }

        if (auditLogsCount > 100)
        {
            return 75;
        }

        if (auditLogsCount > 10)
        {
            return 65;
        }

        return 50;
    }

    private static List<SecurityRecommendationDto> GenerateSecurityRecommendations(
        int passwordScore, int twoFactorScore, int sessionScore,
        int accessControlsScore, int auditScore)
    {
        var recommendations = new List<SecurityRecommendationDto>();

        if (twoFactorScore < 95)
        {
            recommendations.Add(new SecurityRecommendationDto
            {
                Category = "Two-Factor Authentication",
                Message = "Increase 2FA adoption rate to reach 95% target",
                Priority = twoFactorScore < 50 ? "high" : "medium",
                ActionRequired = true
            });
        }

        if (sessionScore < 80)
        {
            recommendations.Add(new SecurityRecommendationDto
            {
                Category = "Session Security",
                Message = "Review session timeout policies for better security",
                Priority = "low",
                ActionRequired = false
            });
        }

        if (passwordScore < 90)
        {
            recommendations.Add(new SecurityRecommendationDto
            {
                Category = "Password Policies",
                Message = "Strengthen password requirements to improve security",
                Priority = passwordScore < 60 ? "high" : "medium",
                ActionRequired = true
            });
        }

        return recommendations;
    }

    #endregion

    #region Threat Detection

    public async Task<List<ThreatDetectionDto>> GetThreatDetectionsAsync()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            var threats = await _unitOfWork.Repository<ThreatDetection>()
                .FindAsync(td => td.TenantId == tenantId.Value);

            return threats.Select(MapToThreatDetectionDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threat detections");
            throw;
        }
    }

    public async Task UpdateThreatStatusAsync(Guid threatId, UpdateThreatStatusRequest request)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            var threat = await _unitOfWork.Repository<ThreatDetection>()
                .FirstOrDefaultAsync(td => td.Id == threatId && td.TenantId == tenantId.Value) ?? throw new InvalidOperationException("Threat detection not found");
            threat.Status = Enum.Parse<Core.Enums.ThreatStatus>(request.Status);
            threat.Resolution = request.Resolution;
            threat.ResolvedBy = _currentUserService.UserName;

            if (threat.Status == Core.Enums.ThreatStatus.Resolved)
            {
                threat.ResolvedAt = DateTime.UtcNow;
            }

            await _unitOfWork.Repository<ThreatDetection>().UpdateAsync(threat);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated threat status: {ThreatId} to {Status}", threatId, request.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating threat status {ThreatId}", threatId);
            throw;
        }
    }

    private static ThreatDetectionDto MapToThreatDetectionDto(ThreatDetection threat)
    {
        return new ThreatDetectionDto
        {
            Id = threat.Id.ToString(),
            Type = threat.ThreatType,
            Title = threat.Title,
            Description = threat.Description,
            Severity = threat.Severity.ToString(),
            Status = threat.Status.ToString(),
            Timestamp = threat.DetectedAt,
            Source = threat.IpAddress ?? "Unknown",
            Target = threat.AffectedUserName,
            Resolution = threat.Resolution,
            Indicators = threat.Indicators?.Select(MapToThreatIndicatorDto).ToList() ?? new List<ThreatIndicatorDto>(),
            Metadata = new Dictionary<string, object>
            {
                ["riskScore"] = threat.RiskScore,
                ["isBlocked"] = threat.IsBlocked,
                ["ipAddress"] = threat.IpAddress ?? "",
                ["location"] = threat.Location ?? "",
                ["resolvedAt"] = threat.ResolvedAt?.ToString("o") ?? ""
            }
        };
    }

    private static ThreatIndicatorDto MapToThreatIndicatorDto(ThreatIndicator indicator)
    {
        return new ThreatIndicatorDto
        {
            Key = indicator.IndicatorType,
            Value = indicator.Value,
            Risk = indicator.Confidence
        };
    }

    #endregion

    #region Two-Factor Authentication

    public async Task<TwoFactorSettingsDto> GetTwoFactorSettingsAsync()
    {
        try
        {
            var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;
            if (!userId.HasValue)
            {
                throw new InvalidOperationException("User ID is required");
            }

            var user = await _userService.GetUserByIdAsync(userId.Value) ?? throw new InvalidOperationException("User not found");
            return new TwoFactorSettingsDto
            {
                IsEnabled = user.TwoFactorEnabled,
                RecoveryCodes = new List<string>(), // Don't expose recovery codes in get
                AuthenticatorKey = null, // Don't expose key in get
                EnabledAt = user.TwoFactorEnabled ? user.CreatedAt : null,
                BackupCodesGeneratedAt = null,
                RecoveryCodesRemaining = 0 // TODO: Implement recovery codes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting two-factor settings");
            throw;
        }
    }

    public async Task<TwoFactorSetupDto> EnableTwoFactorAsync(EnableTwoFactorRequest request)
    {
        try
        {
            var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;
            if (!userId.HasValue)
            {
                throw new InvalidOperationException("User ID is required");
            }

            var user = await _userService.GetUserByIdAsync(userId.Value) ?? throw new InvalidOperationException("User not found");
            _logger.LogInformation("EnableTwoFactorAsync called for user {UserId}. User has AuthenticatorKey: {HasKey}, VerificationCode provided: '{Code}'",
                userId.Value, !string.IsNullOrEmpty(user.AuthenticatorKey), request.VerificationCode);

            // ALWAYS verify the password first before doing anything
            var passwordHasher = new PasswordHasher<ApplicationUser>();
            var passwordVerificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (passwordVerificationResult == PasswordVerificationResult.Failed)
            {
                _logger.LogWarning("Password verification failed for user {UserId} during 2FA setup", userId.Value);
                throw new ArgumentException("Incorrect password. Please check your password and try again.");
            }

            _logger.LogInformation("Password verified successfully for user {UserId}", userId.Value);

            // If user already has a secret key and provided a verification code, verify and enable
            if (!string.IsNullOrEmpty(user.AuthenticatorKey) && !string.IsNullOrEmpty(request.VerificationCode))
            {
                _logger.LogInformation("Verifying TOTP code to enable 2FA for user {UserId}", userId.Value);
                var isValidCode = await _twoFactorAuthService.ValidateTotpAsync(user, request.VerificationCode);
                if (!isValidCode)
                {
                    throw new ArgumentException("The verification code you entered is incorrect or has expired. Please try again with a new code from your authenticator app.");
                }

                // Enable 2FA for the user
                user.TwoFactorEnabled = true;
                await _userService.UpdateUserAsync(user);

                _logger.LogInformation("Two-factor authentication enabled for user {UserId}", userId.Value);

                return new TwoFactorSetupDto
                {
                    AuthenticatorKey = user.AuthenticatorKey,
                    QrCodeUrl = _twoFactorAuthService.GenerateQrCodeDataUri(user.AuthenticatorKey, user.Email ?? user.UserName ?? "user"),
                    RecoveryCodes = await _twoFactorAuthService.GenerateRecoveryCodesAsync(user),
                    Instructions = "Two-factor authentication has been enabled successfully."
                };
            }
            // If user has no secret key OR no verification code provided, setup 2FA for the first time
            else
            {
                _logger.LogInformation("Setting up 2FA for the first time for user {UserId}", userId.Value);
                // Setup 2FA for the first time
                var setupResult = await _twoFactorAuthService.SetupTwoFactorAsync(user);

                // Save the authenticator key to the user
                await _userService.UpdateUserAsync(user);

                _logger.LogInformation("2FA setup completed for user {UserId}. Generated secret key: {SecretKey}",
                    userId.Value, setupResult.SecretKey);

                return new TwoFactorSetupDto
                {
                    AuthenticatorKey = setupResult.ManualEntryKey,
                    QrCodeUrl = setupResult.QrCodeDataUri,
                    RecoveryCodes = setupResult.RecoveryCodes,
                    Instructions = "Scan the QR code with your authenticator app, then verify with a code to enable 2FA."
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling two-factor authentication");
            throw;
        }
    }

    public async Task DisableTwoFactorAsync(DisableTwoFactorRequest request)
    {
        try
        {
            var userId = Guid.TryParse(_currentUserService.UserId, out var id) ? id : (Guid?)null;
            if (!userId.HasValue)
            {
                throw new InvalidOperationException("User ID is required");
            }

            var user = await _userService.GetUserByIdAsync(userId.Value) ?? throw new InvalidOperationException("User not found");

            // Verify current password before disabling 2FA
            var passwordHasher = new PasswordHasher<ApplicationUser>();
            var passwordVerificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (passwordVerificationResult == PasswordVerificationResult.Failed)
            {
                _logger.LogWarning("Password verification failed for user {UserId} during 2FA disable", userId.Value);
                throw new ArgumentException("Incorrect password. Please check your password and try again.");
            }

            _logger.LogInformation("Password verified successfully for user {UserId} - disabling 2FA", userId.Value);

            user.TwoFactorEnabled = false;
            user.AuthenticatorKey = null; // Clear the authenticator key

            await _userService.UpdateUserAsync(user);

            _logger.LogInformation("Two-factor authentication disabled for user {UserId}", userId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling two-factor authentication");
            throw;
        }
    }

    #endregion

    #region Security Policies

    public async Task<List<SecurityPolicyDto>> GetSecurityPoliciesAsync()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            var policies = await _unitOfWork.Repository<SecurityPolicy>()
                .FindAsync(sp => sp.TenantId == tenantId.Value);

            return policies.Select(MapToSecurityPolicyDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting security policies");
            throw;
        }
    }

    public async Task<SecurityPolicyDto> CreateSecurityPolicyAsync(CreateSecurityPolicyRequest request)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            var policy = new SecurityPolicy
            {
                TenantId = tenantId.Value,
                Name = request.Name,
                Description = request.Description,
                Type = Enum.Parse<Core.Enums.SecurityPolicyType>(request.PolicyType),
                PolicyRules = JsonSerializer.Serialize(request.Configuration ?? new Dictionary<string, object>()),
                IsActive = request.IsEnabled,
                Priority = 0, // Default priority since it's not in the request
                CreatedBy = _currentUserService.UserName
            };

            var createdPolicy = await _unitOfWork.Repository<SecurityPolicy>().AddAsync(policy);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created security policy: {PolicyName}", policy.Name);

            return MapToSecurityPolicyDto(createdPolicy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating security policy");
            throw;
        }
    }

    public async Task<SecurityPolicyDto> UpdateSecurityPolicyAsync(Guid policyId, UpdateSecurityPolicyRequest request)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant ID is required");
            }

            var policy = await _unitOfWork.Repository<SecurityPolicy>()
                .FirstOrDefaultAsync(sp => sp.Id == policyId && sp.TenantId == tenantId.Value) ?? throw new InvalidOperationException("Security policy not found");
            policy.Name = request.Name;
            policy.Description = request.Description;
            policy.PolicyRules = JsonSerializer.Serialize(request.Configuration ?? new Dictionary<string, object>());
            policy.IsActive = request.IsEnabled;
            policy.UpdatedBy = _currentUserService.UserName;

            await _unitOfWork.Repository<SecurityPolicy>().UpdateAsync(policy);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated security policy: {PolicyId}", policyId);

            return MapToSecurityPolicyDto(policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating security policy {PolicyId}", policyId);
            throw;
        }
    }

    private static SecurityPolicyDto MapToSecurityPolicyDto(SecurityPolicy policy)
    {
        return new SecurityPolicyDto
        {
            Id = policy.Id.ToString(),
            Name = policy.Name,
            Description = policy.Description ?? string.Empty,
            PolicyType = policy.Type.ToString(),
            IsEnabled = policy.IsActive,
            Configuration = !string.IsNullOrEmpty(policy.PolicyRules)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(policy.PolicyRules)
                : new Dictionary<string, object>(),
            LastModified = policy.UpdatedAt ?? policy.CreatedAt,
            ModifiedBy = policy.UpdatedBy ?? policy.CreatedBy ?? "System"
        };
    }

    #endregion

    #region Helper Methods

    private static async Task<int> CalculatePasswordComplianceAsync(List<ApplicationUser> users)
    {
        if (!users.Any())
        {
            return 100;
        }

        // Simple password compliance calculation
        // In a real implementation, this would check password age, complexity, etc.
        var compliantUsers = 0;
        var totalUsers = users.Count;

        foreach (var user in users)
        {
            // Assume users are compliant if they have 2FA enabled or were created recently
            if (user.TwoFactorEnabled || user.CreatedAt > DateTime.UtcNow.AddDays(-90))
            {
                compliantUsers++;
            }
        }

        return totalUsers > 0 ? (int)((double)compliantUsers / totalUsers * 100) : 100;
    }

    #endregion
}
